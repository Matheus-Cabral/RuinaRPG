# Efeitos no Livro de Regras Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the Livro de Regras' Graus & Círculos tab in sync with the Efeitos catalog: creating/editing/deleting an Efeito on the Auditoria writes/rewrites/removes its `## Nome` block in the tab's Markdown, and startup adds any catalog Efeito still missing.

**Architecture:** Pure Markdown operations + block generation in the Domain; an Infrastructure sync service applies them to the `graus-e-circulos` `RulebookDocumentOverride` (creating it from the embedded .md when absent) inside the same save as the Efeito change; Program.cs runs an add-missing pass at startup/`--migrate`.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8 + Npgsql, xUnit + FluentAssertions + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-29-efeitos-no-livro-de-regras-design.md`

## Global Constraints

- `dotnet build` 0 warnings, 0 errors. TDD mandatory.
- Slug is exactly `graus-e-circulos`; embedded resource name `GRAUS e CIRCULOS.md` (read with `RulesDataProvider.ReadResource`).
- Grau sections are the `#` headings in order: 1st `#` = Grau 1 … 9th = Grau 9. A missing section is created as `# {N}º GRAU / CÍRCULO {ROMAN}` (I, II, III, IV, V, VI, VII, VIII, IX) at the end of the document.
- Heading match: trimmed, case-insensitive, accent-insensitive; `Nome (X)` also matches `## Nome`.
- Shared blocks never rewritten/removed by sync: `Dano`, `Alcance`, `Duração` (→ `## Efeitos Básicos`), and any Nome of the form `Libra (...)` (→ `## Libra`). "Present" for them = shared heading exists.
- Generated block format is exactly the spec's (see Task 1 tests for literal expected strings).
- Startup/`--migrate` pass only **adds** missing blocks; never rewrites or removes.
- The Efeito save and the Livro save happen in one `SaveChangesAsync`.
- `RulebookDocumentOverride.UpdatedByUserId` becomes `Guid?` (null = system write).
- Integration tests: run with `--filter` chunks only (disk nearly full).

## Review Focus

- Creating an Efeito whose Nome already appears as a `##` heading (e.g. the Auditor hand-wrote it in the Livro first) must not duplicate it — Task 1 unit + Task 2 integration.
- Editing an original Efeito (e.g. "Cura") replaces only its own block; the neighbouring blocks and the Auditor's other hand edits stay byte-identical — Task 2.
- A rename to a Nome that collides with another existing heading: the old block is replaced in place, the other heading untouched — Task 1.
- The startup pass run twice changes nothing the second time (no `UpdatedAt` churn when nothing is missing) — Task 2.
- "Restaurar padrão" after adding a new Efeito still shows it — Task 2.

---

### Task 1: Domain — block generation and Markdown operations

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/EfeitoParaLivro.cs`, `src/RuinaRPG.Domain/Rules/EfeitoMarkdownBlock.cs`, `src/RuinaRPG.Domain/Rules/GrausECirculosMarkdown.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/EfeitoMarkdownBlockTests.cs`, `tests/RuinaRPG.Tests.Unit/Rules/GrausECirculosMarkdownTests.cs`

**Interfaces (produced):**
- `sealed record EfeitoParaLivro(string Nome, int Grau, string Descricao, TipoDeCusto TipoDeCusto, int? CustoFixo, int? CustoPorUnidade, string? UnidadeLabel, string? QuantidadeDerivadaDeEfeito, int? MaxUnidades, bool MaxEscalaPorGrau, int? CustoAlternativo, int? CustoAlternativoAPartirDoGrau, IReadOnlyList<IReadOnlyList<string>> PreRequisitos)`
- `static class EfeitoMarkdownBlock { string Gerar(EfeitoParaLivro e); }`
- `static class GrausECirculosMarkdown { bool Contem(string md, string nome); string Inserir(string md, int grau, string bloco); string Substituir(string md, string nomeAntigo, int grau, string bloco); string Remover(string md, string nome); bool EhBlocoCompartilhado(string nome); }`

- [ ] **Step 1: Failing tests.**

```csharp
// EfeitoMarkdownBlockTests.cs
using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class EfeitoMarkdownBlockTests
{
    private static EfeitoParaLivro Efeito(TipoDeCusto tipo, int? fixo = null, int? porUnidade = null, string? unidade = null,
        string? derivado = null, int? max = null, bool maxPorGrau = false, int? alt = null, int? altGrau = null,
        IReadOnlyList<IReadOnlyList<string>>? pre = null) =>
        new("Chama Viva", 2, "Envolve o alvo em chamas.", tipo, fixo, porUnidade, unidade, derivado, max, maxPorGrau, alt, altGrau, pre ?? []);

    [Fact]
    public void Fixo() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.Fixo, fixo: 3)).Should().Be(
            "## Chama Viva\n\n**Gasto:** 3 PI.\n\nEnvolve o alvo em chamas.\n");

    [Fact]
    public void Por_unidade_with_max_per_grau_and_prerequisites()
    {
        var e = Efeito(TipoDeCusto.PorUnidade, porUnidade: 2, unidade: "Dado", max: 3, maxPorGrau: true,
            pre: [["Duração"], ["Dano", "Alcance"]]);
        EfeitoMarkdownBlock.Gerar(e).Should().Be(
            "## Chama Viva\n\n**Gasto:** 2 PI por Dado.\n\nMax. 3 Dado por Grau/Círculo.\n\nEnvolve o alvo em chamas.\n\n" +
            "Obrigatória a compra de Duração.\n\nObrigatória a compra de Dano ou Alcance.\n");
    }

    [Fact]
    public void Max_not_per_grau() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.PorUnidade, porUnidade: 1, unidade: "Alvo", max: 2)).Should().Contain("\n\nMax. 2 Alvo.\n\n");

    [Theory]
    [InlineData(TipoDeCusto.Manual, null, "**Gasto:** X PI.")]
    [InlineData(TipoDeCusto.ManualPorUnidade, "Dado", "**Gasto:** X PI por Dado.")]
    public void Manual_costs(TipoDeCusto tipo, string? unidade, string esperado) =>
        EfeitoMarkdownBlock.Gerar(Efeito(tipo, unidade: unidade)).Should().Contain(esperado);

    [Fact]
    public void Derived_cost() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.DerivadoDeOutroEfeito, derivado: "Dano"))
            .Should().Contain("**Gasto:** igual à Quantidade de Dano.");

    [Fact]
    public void Alternative_cost() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.Fixo, fixo: 3, alt: 5, altGrau: 4))
            .Should().Contain("**Gasto:** 3 PI (5 PI a partir do 4º Grau).");
}
```

```csharp
// GrausECirculosMarkdownTests.cs
using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class GrausECirculosMarkdownTests
{
    private const string Md =
        "Intro.\n\n# 1º GRAU / CÍRCULO I\n\n## Efeitos Básicos\n\nDano...\n\n## Cura\n\nTexto da Cura.\n\n" +
        "# 2º GRAU / CÍRCULO II\n\n## Libra\n\nTexto da Libra.\n\n## Aceleração\n\nTexto.\n";

    private const string Bloco = "## Chama Viva\n\n**Gasto:** 3 PI.\n\nEnvolve.\n";

    [Theory]
    [InlineData("Cura", true)]
    [InlineData("cura", true)]
    [InlineData("Aceleracao", true)]       // accent-insensitive
    [InlineData("Libra (Arcana)", true)]   // shares ## Libra
    [InlineData("Dano", true)]             // shares ## Efeitos Básicos
    [InlineData("Chama Viva", false)]
    public void Contem(string nome, bool esperado) => GrausECirculosMarkdown.Contem(Md, nome).Should().Be(esperado);

    [Fact]
    public void Inserir_appends_at_the_end_of_the_grau_section()
    {
        var md = GrausECirculosMarkdown.Inserir(Md, 1, Bloco);
        md.Should().Contain("Texto da Cura.\n\n## Chama Viva\n\n**Gasto:** 3 PI.\n\nEnvolve.\n\n# 2º GRAU / CÍRCULO II");
    }

    [Fact]
    public void Inserir_in_the_last_section_appends_at_the_end() =>
        GrausECirculosMarkdown.Inserir(Md, 2, Bloco).Should().EndWith("Texto.\n\n" + Bloco);

    [Fact]
    public void Inserir_creates_a_missing_grau_section() =>
        GrausECirculosMarkdown.Inserir(Md, 4, Bloco).Should().EndWith("\n\n# 4º GRAU / CÍRCULO IV\n\n" + Bloco);

    [Fact]
    public void Inserir_does_nothing_when_already_present() =>
        GrausECirculosMarkdown.Inserir(Md, 1, "## Cura\n\nOutro.\n").Should().Be(Md);

    [Fact]
    public void Substituir_same_grau_replaces_only_that_block()
    {
        var md = GrausECirculosMarkdown.Substituir(Md, "Cura", 1, "## Cura\n\nNovo texto.\n");
        md.Should().Contain("## Cura\n\nNovo texto.\n\n# 2º GRAU");
        md.Should().NotContain("Texto da Cura.");
        md.Should().Contain("## Efeitos Básicos\n\nDano...");
    }

    [Fact]
    public void Substituir_moves_to_another_grau_and_renames()
    {
        var md = GrausECirculosMarkdown.Substituir(Md, "Cura", 2, "## Cura Maior\n\nNovo.\n");
        md.Should().NotContain("## Cura\n");
        md.Should().EndWith("Texto.\n\n## Cura Maior\n\nNovo.\n");
    }

    [Fact]
    public void Substituir_when_missing_inserts() =>
        GrausECirculosMarkdown.Substituir(Md, "Chama Viva", 1, Bloco).Should().Contain(Bloco);

    [Fact]
    public void Remover_removes_only_the_block()
    {
        var md = GrausECirculosMarkdown.Remover(Md, "Cura");
        md.Should().NotContain("## Cura");
        md.Should().Contain("Dano...\n\n# 2º GRAU");
    }

    [Theory]
    [InlineData("Dano")]
    [InlineData("Libra (Vitalidade)")]
    public void Shared_blocks_are_never_replaced_or_removed(string nome)
    {
        GrausECirculosMarkdown.EhBlocoCompartilhado(nome).Should().BeTrue();
        GrausECirculosMarkdown.Remover(Md, nome).Should().Be(Md);
        GrausECirculosMarkdown.Substituir(Md, nome, 1, "## X\n\nY.\n").Should().Be(Md);
    }
}
```

- [ ] **Step 2: Run → FAIL** (`dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EfeitoMarkdownBlock|FullyQualifiedName~GrausECirculosMarkdown"`).

- [ ] **Step 3: Implement.** Normalize line endings to `\n` on input. Parse the document into: preamble (before the first `#`), then `#` sections, each with its heading line and its `##` blocks (a block = its `##` heading line through the line before the next `##`/`#`). Blocks are joined with a single blank line between them; `Inserir` trims trailing blank lines of the section before appending `"\n" + bloco` so the output matches the tests exactly. Heading normalization: `string.Normalize(NormalizationForm.FormD)` stripping `UnicodeCategory.NonSpacingMark`, then `ToLowerInvariant().Trim()`. `Contem(nome)`: basics → `Efeitos Básicos` heading exists; `Libra (…)`-style (`^(.+?)\s*\(.*\)$`) → heading equals the name or the stripped base; otherwise heading equals the name. Roman numerals I–IX for new sections. `Substituir` on a shared name returns `md` unchanged; otherwise removes the old block (if present) and inserts the new one — in place when the Grau is unchanged (same position), at the end of the target Grau section otherwise.

- [ ] **Step 4: Run → PASS**; build 0 warnings.

- [ ] **Step 5: Commit** — `git commit -m "feat(domain): blocos de Efeito no Markdown de Graus & Círculos"`

---

### Task 2: Sync service, wiring and docs

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/LivroDeRegrasEfeitosSync.cs`
- Modify: `src/RuinaRPG.Infrastructure/Rules/RulebookDocumentOverride.cs` (`Guid? UpdatedByUserId`), `Persistence/RuinaRpgDbContext.cs` if needed, migration `RulebookOverrideAutorOpcional`
- Modify: `src/RuinaRPG.Api/Controllers/EfeitosController.cs` (create/update/delete), `RulebookDocumentsController.cs` (delete of `graus-e-circulos` → add-missing pass; also map a null `UpdatedByUserId` in any response), `Program.cs` (register Scoped; run `SincronizarFaltantesAsync()` after `EfeitoSeeder` in both seed blocks, log inserted count)
- Modify: `tests/RuinaRPG.Tests.Integration/ApiFactory.cs` only if tests need the pass; `Docs/Requisitos/Requisitos - Auditoria de Regras.md` (R0006 addition), `Requisitos - Modelo de Dados.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EfeitosNoLivroDeRegrasTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: `class LivroDeRegrasEfeitosSync(RuinaRpgDbContext db) { Task AoCriarAsync(Efeito e, Guid autor); Task AoEditarAsync(string nomeAntigo, Efeito e, Guid autor); Task AoExcluirAsync(Efeito e, Guid autor); Task<int> SincronizarFaltantesAsync(Guid? autor = null); }` — each loads (or creates from `RulesDataProvider.ReadResource("GRAUS e CIRCULOS.md")`) the `graus-e-circulos` override, applies the Markdown operation, sets `MarkdownText`/`UpdatedAt`/`UpdatedByUserId` **only if the text changed**, and does **not** call `SaveChangesAsync` (the caller saves once). `SincronizarFaltantesAsync` saves itself and returns how many blocks it added. A static helper maps `Efeito` → `EfeitoParaLivro` (PreRequisitos from `PreRequisitosJson`, same deserialization EfeitosController already uses).

- [ ] **Step 1: Failing tests** (copy Auditor + helpers from `EfeitosControllerTests` / `RulebookDocumentsControllerTests`; read the Livro via `GET /api/rulebook/graus-e-circulos` and find the Grau section in `Sections`):
  - Create `Chama Viva` Grau 2 Fixo 3 → the Grau 2 section's Html contains "Chama Viva" and "3 PI"; the Grau 1 section doesn't.
  - Edit it to Nome `Chama Eterna`, Grau 3 → no longer in Grau 2; present in Grau 3 as "Chama Eterna".
  - Delete it → absent everywhere.
  - Edit original `Cura`'s Descrição → the Livro's Cura block shows the new Descrição; the "Aceleração" block text is unchanged (compare the override Markdown before/after outside the Cura block).
  - Edit `Dano` → Livro Markdown unchanged.
  - Auditor saves a hand-edited override (PUT rulebook-documents) with an extra paragraph, then creates an Efeito → the extra paragraph survives.
  - Hand-written heading first: PUT an override containing `## Chama Viva` in Grau 2, then create Efeito `Chama Viva` → exactly one `## Chama Viva` in the Markdown.
  - Startup pass: insert an Efeito row directly in the DB (bypassing the API), call `SincronizarFaltantesAsync()` via a DI scope → returns 1 and the block appears; call again → returns 0 and `UpdatedAt` unchanged.
  - Restore default (`DELETE /api/rulebook-documents/graus-e-circulos`) after creating an Efeito → the Livro still shows it.

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** as specified. In `EfeitosController.Update`, capture `nomeAntigo = efeito.Nome` before mutating. Make `UpdatedByUserId` nullable (FK stays, `OnDelete(Restrict)`); generate the migration (AlterColumn only). Docs: Auditoria R0006 gets a paragraph (creating/editing/excluding an Efeito updates its block in [[GRAUS & CÍRCULOS]] of the Livro de Regras — shared blocks Efeitos Básicos/Libra excepted; the Livro always contains every catalog Efeito: checked at startup and after "Restaurar padrão"); Modelo de Dados: `UpdatedByUserId` nullable (null = escrita do sistema).

- [ ] **Step 4: Run** new tests + `--filter "FullyQualifiedName~Efeito|FullyQualifiedName~Rulebook"` + Unit; build 0 warnings.

- [ ] **Step 5: Commit** — `git commit -m "feat: Efeitos da Auditoria entram no Livro de Regras (Graus & Círculos)"`

---

### Task 3: Full verification

- [ ] Build 0/0; Unit + Client; Integration in `--filter` chunks, all green (rerun single failures in isolation — known flake).
- [ ] Manual sanity of the generated Markdown for one Efeito of each TipoDeCusto against an existing hand-written block of the same kind in `Docs/Sistema RPG/GRAUS & CÍRCULOS.md` (same visual shape).
