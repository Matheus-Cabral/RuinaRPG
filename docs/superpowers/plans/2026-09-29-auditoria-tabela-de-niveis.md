# Auditoria da Tabela de Níveis Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the regex-parsed free-text level table with a structured, Auditor-editable DB table (budget and cap columns, custom columns, add/remove levels) that every calculator reads, with server-enforced caps and a "Progressão do nível" panel on the sheets.

**Architecture:** Three tables (`NiveisProgressao`, `ColunasDeNivel`, `ValoresDeNivel`) seeded once from today's Markdown by a startup seeder that reuses today's regexes. A pure Domain class `ProgressaoDeNivel` holds the resolution rules (sum / inherit / exact) and exposes legacy-shaped adapters (`XpPorNivel`, `EapPorNivel`, `LevelBonus`) so `NivelCalculator`, `EapCalculator` and `LevelUpNoticeCalculator` barely change. A scoped `ITabelaDeNiveis` loads it per request and replaces `IRulesDataProvider.Niveis/XpPorNivel/EapPorNivel` in every gameplay consumer. The Compêndio keeps indexing the Markdown.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core + Npgsql, Blazor WASM + MudBlazor, xUnit + FluentAssertions, Testcontainers, bUnit.

**Spec:** `docs/superpowers/specs/2026-09-29-auditoria-tabela-de-niveis-design.md`

**Depends on:** plans `2026-09-29-evolucoes-de-arca.md` (uses `ArcaEvolucaoRules`) and `2026-09-29-auditoria-de-pericias.md` (skill `PUT` resolves perícias via `IPericiaCatalogo`, skill rows keyed by `PericiaId`) merged first.

## Global Constraints

- TDD mandatory (Técnico R0011). `dotnet build` = 0 warnings, 0 errors after every task.
- UI text in Brazilian Portuguese. **No emojis** — MudBlazor icons: `Icons.Material.Filled.Functions` (Acumulativa), `Icons.Material.Filled.Straighten` (Por nível), `Icons.Material.Filled.Lock` (system column), each with a tooltip.
- Every new UI surface has an `InfoPopup` (ⓘ).
- **Regression guard:** after the switch, every budget/XP/EAP/level-up value for levels 1–50 must equal what today's regex calculators produce from the Markdown.
- Column types: `Acumulativa` = Σ levels 1..N (empty = 0); `PorNivel` = the level's own value, empty inherits the nearest lower non-empty value, none = no cap. **XP and EAP never inherit.**
- XP semantics unchanged: the row for level N holds the absolute XP needed to **reach N + 1**; empty on the last level = "nível máximo".
- Caps limit **`Gasto`** (points spent) of one attribute / one perícia; and the count of Passivas of each `CategoriaDePassiva` on the sheet. Caps apply to Personagem and NPC only. Rejected with 400; lowering an over-cap value is always allowed.
- System columns: renamable, not removable, type fixed. Custom columns: type fixed after creation.
- Auditor check: DB lookup of `IsRulesAuditor` (`RequireRulesAuditorAsync`).
- Integration tests: Docker; `--filter` batches of ≤ 8 classes; rerun a timeout in isolation before treating it as real; unique user names per test. Tests that edit the shared level table must restore what they changed (the table is shared by every test in the class's container) — prefer adding a custom column or editing a high level (e.g. 49) and restoring in a `finally`.
- Commits in Portuguese, conventional prefix, ending `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- Auditor adds level 51 without filling level 50's XP → Personagem XP ≥ level 49's threshold must still compute level 50 (not crash, not jump to 51); after filling level 50's XP, enough XP reaches 51.
- Auditor removes a custom column that has values → values cascade away; the sheet panel and Livro stop showing it; system columns 400.
- A cap is set on a low level only (e.g. Máx. de Perícia = 3 at level 1) → a level-20 sheet is capped at 3 too (inheritance), and a sheet already at 5 can lower to 4 but not raise to 6.
- Two Passivas of the same category when the cap is 1 → the second `POST` is 400 with a message naming the category; other categories unaffected.
- A sheet exactly at `UltimoNivel` when the Auditor tries "Remover último nível" → 400 naming how many sheets are at that level; NPC `PUT .../nivel` above `UltimoNivel` → 400.

---

## File Structure

| File | Responsibility |
|---|---|
| Create `src/RuinaRPG.Domain/Rules/Niveis/TipoDeColunaDeNivel.cs` | enum `Acumulativa`, `PorNivel` |
| Create `src/RuinaRPG.Domain/Rules/Niveis/ChavesDeNivel.cs` | the 13 system keys + their seed metadata |
| Create `src/RuinaRPG.Domain/Rules/Niveis/ProgressaoDeNivel.cs` | resolution rules + legacy adapters |
| Create `src/RuinaRPG.Domain/Rules/Niveis/NivelBonusExtractor.cs` | today's regexes, extracting numbers + leftover lines from a bonus cell |
| Create `src/RuinaRPG.Infrastructure/Rules/Niveis/NivelProgressao.cs`, `ColunaDeNivel.cs`, `ValorDeNivel.cs` | EF entities |
| Create `src/RuinaRPG.Infrastructure/Rules/Niveis/TabelaDeNiveisSeeder.cs` | seed from Markdown |
| Create `src/RuinaRPG.Infrastructure/Rules/Niveis/TabelaDeNiveis.cs` | `ITabelaDeNiveis` scoped loader |
| Create `src/RuinaRPG.Contracts/Rules/TabelaDeNiveisResponse.cs` + requests | DTOs |
| Create `src/RuinaRPG.Api/Controllers/TabelaDeNiveisController.cs` | read + Auditoria writes |
| Create `src/RuinaRPG.Api/Services/LimitesDeNivel.cs` | cap checks shared by controllers |
| Modify budget calculators, `*AttributesController`, `*SkillsController`, `*SpellAbilitiesController`, `*PossessionsController`, `*SheetsController`, `*SheetStats`, `CharacterAffinitiesController`, `RacialAbilitiesController`, `RulebookRenderer`, `RulebookDocumentsController` | consumers |
| Create `src/RuinaRPG.Client/Pages/AuditoriaTabelaDeNiveis.razor` | Auditoria page |
| Create `src/RuinaRPG.Client/Shared/ProgressaoDoNivelSection.razor` | sheet panel |
| Modify `src/RuinaRPG.Client/Pages/AuditoriaLivroDeRegras.razor`, `Layout/RulesAuditorNavLinks.razor`, `FichaDePersonagem.razor`, `FichaDeNpc.razor` | wiring |

---

### Task 1: Domain — `ProgressaoDeNivel` and the bonus extractor

**Files:**
- Create: the four Domain files above
- Test: `tests/RuinaRPG.Tests.Unit/Rules/ProgressaoDeNivelTests.cs`, `tests/RuinaRPG.Tests.Unit/Rules/NivelBonusExtractorTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace RuinaRPG.Domain.Rules.Niveis;
  public enum TipoDeColunaDeNivel { Acumulativa, PorNivel }
  public static class ChavesDeNivel
  {
      public const string PontosDeAtributo = "PontosDeAtributo", PontosDePericia = "PontosDePericia",
          EspacosDeCaracteristica = "EspacosDeCaracteristica", PontosDeIgnicao = "PontosDeIgnicao",
          EspacosDeMaestria = "EspacosDeMaestria", PontosDeMaestria = "PontosDeMaestria",
          MaxAtributo = "MaxAtributo", MaxPericia = "MaxPericia",
          MaxPassivasLivres = "MaxPassivasLivres", MaxPassivasVocacionais = "MaxPassivasVocacionais", MaxPassivasDeClasse = "MaxPassivasDeClasse",
          XpParaProximoNivel = "XpParaProximoNivel", EapBase = "EapBase";
      public sealed record Definicao(string Chave, string Nome, TipoDeColunaDeNivel Tipo, int Ordem);
      public static IReadOnlyList<Definicao> Sistema { get; }        // the 13, in display order
      public static bool SemHeranca(string? chave);                   // true for XP and EAP
      public static string MaxPassivas(CategoriaDePassiva categoria); // maps category → key
  }
  public sealed record ColunaDeNivelDef(Guid Id, string Nome, TipoDeColunaDeNivel Tipo, string? ChaveDeSistema, int Ordem);
  public sealed record LinhaDeNivel(int Nivel, string? OutrosBonus, IReadOnlyDictionary<Guid, int?> Valores);
  public sealed class ProgressaoDeNivel
  {
      public ProgressaoDeNivel(IReadOnlyList<ColunaDeNivelDef> colunas, IReadOnlyList<LinhaDeNivel> linhas);
      public IReadOnlyList<ColunaDeNivelDef> Colunas { get; }        // ordered by Ordem
      public IReadOnlyList<LinhaDeNivel> Linhas { get; }             // ordered by Nivel
      public int UltimoNivel { get; }
      public int Acumulado(string chave, int nivel);                 // Σ 1..nivel, empty = 0
      public int? Limite(string chave, int nivel);                   // PorNivel with inheritance (null = no cap)
      public int? ValorExato(string chave, int nivel);               // raw cell
      public int? Resolver(ColunaDeNivelDef coluna, int nivel);      // per type/key rules (for the panel)
      public IReadOnlyList<string> LinhasDeBonus(int nivel);         // "+N Nome" for non-zero Acumulativas (Ordem), then OutrosBonus split on newlines
      public IReadOnlyList<XpPorNivel> XpPorNivel();                 // XpAbsoluto = value or "Lvl. Max"
      public IReadOnlyList<EapPorNivel> EapPorNivel();               // ValorAbsoluto = value ?? 0
      public IReadOnlyList<LevelBonus> ComoLevelBonus();             // BonusText = string.Join("<br>", LinhasDeBonus(n))
  }
  public sealed record ExtracaoDeBonus(IReadOnlyDictionary<string, int> Valores, IReadOnlyList<string> Restante);
  public static class NivelBonusExtractor { public static ExtracaoDeBonus Extrair(string bonusText); }
  ```

- [ ] **Step 1: Failing tests**

`ProgressaoDeNivelTests.cs`:
```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.Niveis;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class ProgressaoDeNivelTests
{
    private static readonly ColunaDeNivelDef Atributo = new(Guid.NewGuid(), "Pontos de Atributo", TipoDeColunaDeNivel.Acumulativa, ChavesDeNivel.PontosDeAtributo, 0);
    private static readonly ColunaDeNivelDef MaxPericia = new(Guid.NewGuid(), "Máx. de Perícia", TipoDeColunaDeNivel.PorNivel, ChavesDeNivel.MaxPericia, 1);
    private static readonly ColunaDeNivelDef Xp = new(Guid.NewGuid(), "XP para o próximo nível", TipoDeColunaDeNivel.PorNivel, ChavesDeNivel.XpParaProximoNivel, 2);
    private static readonly ColunaDeNivelDef Fama = new(Guid.NewGuid(), "Fama", TipoDeColunaDeNivel.Acumulativa, null, 3);

    private static ProgressaoDeNivel Tabela() => new(
        new[] { Fama, Xp, MaxPericia, Atributo },
        new[]
        {
            new LinhaDeNivel(3, "Terceira linha", new Dictionary<Guid, int?> { [Atributo.Id] = 2, [MaxPericia.Id] = null, [Xp.Id] = null }),
            new LinhaDeNivel(1, "Status de Vida Aprimorado\nStatus de Foco Aprimorado", new Dictionary<Guid, int?> { [Atributo.Id] = 9, [MaxPericia.Id] = 4, [Xp.Id] = 50, [Fama.Id] = 1 }),
            new LinhaDeNivel(2, null, new Dictionary<Guid, int?> { [Atributo.Id] = null, [Xp.Id] = 150 }),
        });

    [Fact]
    public void Orders_columns_and_rows_and_knows_the_last_level()
    {
        var t = Tabela();
        t.Colunas.Select(c => c.Nome).Should().Equal("Pontos de Atributo", "Máx. de Perícia", "XP para o próximo nível", "Fama");
        t.Linhas.Select(l => l.Nivel).Should().Equal(1, 2, 3);
        t.UltimoNivel.Should().Be(3);
    }

    [Fact]
    public void Acumulado_sums_levels_up_to_N_treating_empty_as_zero()
    {
        var t = Tabela();
        t.Acumulado(ChavesDeNivel.PontosDeAtributo, 1).Should().Be(9);
        t.Acumulado(ChavesDeNivel.PontosDeAtributo, 2).Should().Be(9);
        t.Acumulado(ChavesDeNivel.PontosDeAtributo, 3).Should().Be(11);
        t.Acumulado(ChavesDeNivel.PontosDeIgnicao, 3).Should().Be(0); // column absent
    }

    [Fact]
    public void Limite_inherits_the_nearest_lower_value_and_is_null_when_none()
    {
        var t = Tabela();
        t.Limite(ChavesDeNivel.MaxPericia, 3).Should().Be(4);
        t.Limite(ChavesDeNivel.MaxAtributo, 3).Should().BeNull();
    }

    [Fact]
    public void Xp_never_inherits_and_the_empty_last_level_is_Lvl_Max()
    {
        var t = Tabela();
        t.ValorExato(ChavesDeNivel.XpParaProximoNivel, 3).Should().BeNull();
        t.Resolver(Xp, 3).Should().BeNull();
        t.XpPorNivel().Select(x => (x.Nivel, x.XpAbsoluto)).Should().Equal((1, "50"), (2, "150"), (3, "Lvl. Max"));
    }

    [Fact]
    public void Resolver_uses_the_column_type_for_custom_columns()
    {
        var t = Tabela();
        t.Resolver(Fama, 3).Should().Be(1);
        t.Resolver(MaxPericia, 2).Should().Be(4);
    }

    [Fact]
    public void LinhasDeBonus_lists_nonzero_acumulativas_then_the_free_text_lines()
    {
        var t = Tabela();
        t.LinhasDeBonus(1).Should().Equal("+9 Pontos de Atributo", "+1 Fama", "Status de Vida Aprimorado", "Status de Foco Aprimorado");
        t.LinhasDeBonus(2).Should().BeEmpty();
        t.ComoLevelBonus().Single(b => b.Nivel == 3).BonusText.Should().Be("+2 Pontos de Atributo<br>Terceira linha");
    }
}
```

`NivelBonusExtractorTests.cs` — the regression guard against the real Markdown (it's copied to the unit test output only if referenced; read it from the repo path instead):
```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Domain.Rules.ReferenceData;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class NivelBonusExtractorTests
{
    [Fact]
    public void Extracts_the_numbers_and_keeps_the_rest_of_level_1()
    {
        var e = NivelBonusExtractor.Extrair("+9 Pontos de Atributo  <br>+Status de Vida Aprimorado  <br>+Status de Foco Aprimorado  <br>+10 Pontos de Ignição  <br>+4 Pontos de Perícia  <br>+1 Espaço de Maestria  <br>+1 Ponto de Maestria");

        e.Valores.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            [ChavesDeNivel.PontosDeAtributo] = 9, [ChavesDeNivel.PontosDeIgnicao] = 10, [ChavesDeNivel.PontosDePericia] = 4,
            [ChavesDeNivel.EspacosDeMaestria] = 1, [ChavesDeNivel.PontosDeMaestria] = 1,
        });
        e.Restante.Should().Equal("+Status de Vida Aprimorado", "+Status de Foco Aprimorado");
    }

    [Fact]
    public void Summing_extracted_values_reproduces_todays_calculators_for_every_level()
    {
        var markdown = File.ReadAllText(Path.Combine(RepoRoot(), "Docs", "Sistema RPG", "Tabela de Níveis.md"));
        var niveis = NivelBonusParser.Parse(markdown);

        for (var nivel = 1; nivel <= 50; nivel++)
        {
            var ate = niveis.Where(n => n.Nivel <= nivel).Select(n => NivelBonusExtractor.Extrair(n.BonusText)).ToList();
            int Soma(string chave) => ate.Sum(e => e.Valores.GetValueOrDefault(chave));

            Soma(ChavesDeNivel.PontosDeAtributo).Should().Be(AttributePointBudgetCalculator.Compute(nivel, niveis), $"nível {nivel}");
            Soma(ChavesDeNivel.PontosDePericia).Should().Be(SkillPointBudgetCalculator.Compute(nivel, niveis), $"nível {nivel}");
            (5 + Soma(ChavesDeNivel.EspacosDeCaracteristica)).Should().Be(TraitPointBudgetCalculator.Compute(nivel, niveis), $"nível {nivel}");
            Soma(ChavesDeNivel.PontosDeIgnicao).Should().Be(PontosDeIgnicaoCalculator.ComputeTotal(nivel, 0, niveis), $"nível {nivel}");
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RuinaRPG.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
```
(Check the parser's actual name/signature in `Domain/Rules/ReferenceData/NivelBonusParser.cs` and adjust the call.)

- [ ] **Step 2: Run** `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~ProgressaoDeNivelTests|FullyQualifiedName~NivelBonusExtractorTests"` → build FAIL.

- [ ] **Step 3: Implement**

`ChavesDeNivel.Sistema` (Ordem 0–12, in this order): Pontos de Atributo, Pontos de Perícia, Espaços de Característica, Pontos de Ignição, Espaços de Maestria, Pontos de Maestria (all Acumulativa); Máx. de Atributo, Máx. de Perícia, Máx. Passivas Livres, Máx. Passivas Vocacionais, Máx. Passivas De Classe, XP para o próximo nível, EAP base (all PorNivel). `SemHeranca(chave) => chave is XpParaProximoNivel or EapBase`. `MaxPassivas(CategoriaDePassiva.Livre) => MaxPassivasLivres` etc. (`CategoriaDePassiva` is in `RuinaRPG.Domain.SpellsAndAbilities`).

`NivelBonusExtractor.Extrair`: split the cell on `<br>` (trim, drop empties); for each line try, in order, the six regexes (copy them **verbatim** from `AttributePointBudgetCalculator`, `SkillPointBudgetCalculator`, `TraitPointBudgetCalculator`, `PontosDeIgnicaoCalculator`, plus two new ones: `\+(\d+)\s+Espaços?\s+de\s+Maestria\b` and `\+(\d+)\s+Pontos?\s+de\s+Maestria\b`, all `IgnoreCase`); a matching line adds to that key (sum if repeated) and is consumed; a line matching none goes to `Restante`. Note "Pontos de Maestria" must not be confused with "Pontos de Atributo" — the regexes are anchored on the word after "de", so order doesn't matter, but assert with the level-1 test.

`ProgressaoDeNivel`:
```csharp
public sealed class ProgressaoDeNivel
{
    public ProgressaoDeNivel(IReadOnlyList<ColunaDeNivelDef> colunas, IReadOnlyList<LinhaDeNivel> linhas)
    {
        Colunas = colunas.OrderBy(c => c.Ordem).ToList();
        Linhas = linhas.OrderBy(l => l.Nivel).ToList();
    }

    public IReadOnlyList<ColunaDeNivelDef> Colunas { get; }
    public IReadOnlyList<LinhaDeNivel> Linhas { get; }
    public int UltimoNivel => Linhas.Count == 0 ? 0 : Linhas[^1].Nivel;

    private ColunaDeNivelDef? Coluna(string chave) => Colunas.FirstOrDefault(c => c.ChaveDeSistema == chave);

    public int Acumulado(string chave, int nivel) => Coluna(chave) is { } c ? Soma(c, nivel) : 0;
    public int? Limite(string chave, int nivel) => Coluna(chave) is { } c ? Herdado(c, nivel) : null;
    public int? ValorExato(string chave, int nivel) => Coluna(chave) is { } c ? Celula(c, nivel) : null;

    public int? Resolver(ColunaDeNivelDef coluna, int nivel) => coluna.Tipo switch
    {
        TipoDeColunaDeNivel.Acumulativa => Soma(coluna, nivel),
        _ when ChavesDeNivel.SemHeranca(coluna.ChaveDeSistema) => Celula(coluna, nivel),
        _ => Herdado(coluna, nivel),
    };

    public IReadOnlyList<string> LinhasDeBonus(int nivel)
    {
        var linhas = Colunas.Where(c => c.Tipo == TipoDeColunaDeNivel.Acumulativa)
            .Select(c => (c, v: Celula(c, nivel) ?? 0)).Where(x => x.v != 0)
            .Select(x => $"+{x.v} {x.c.Nome}").ToList();
        var outros = Linhas.FirstOrDefault(l => l.Nivel == nivel)?.OutrosBonus;
        if (!string.IsNullOrWhiteSpace(outros))
            linhas.AddRange(outros.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return linhas;
    }

    public IReadOnlyList<XpPorNivel> XpPorNivel() =>
        Linhas.Select(l => new XpPorNivel(l.Nivel, ValorExato(ChavesDeNivel.XpParaProximoNivel, l.Nivel)?.ToString() ?? "Lvl. Max", "")).ToList();

    public IReadOnlyList<EapPorNivel> EapPorNivel() =>
        Linhas.Select(l => new EapPorNivel(l.Nivel, ValorExato(ChavesDeNivel.EapBase, l.Nivel) ?? 0)).ToList();

    public IReadOnlyList<LevelBonus> ComoLevelBonus() =>
        Linhas.Select(l => new LevelBonus(l.Nivel, string.Join("<br>", LinhasDeBonus(l.Nivel)))).ToList();

    private int? Celula(ColunaDeNivelDef c, int nivel) =>
        Linhas.FirstOrDefault(l => l.Nivel == nivel)?.Valores.GetValueOrDefault(c.Id);

    private int Soma(ColunaDeNivelDef c, int nivel) =>
        Linhas.Where(l => l.Nivel <= nivel).Sum(l => l.Valores.GetValueOrDefault(c.Id) ?? 0);

    private int? Herdado(ColunaDeNivelDef c, int nivel) =>
        Linhas.Where(l => l.Nivel <= nivel).Reverse().Select(l => l.Valores.GetValueOrDefault(c.Id)).FirstOrDefault(v => v is not null);
}
```
(`XpPorNivel`, `EapPorNivel`, `LevelBonus` records live in `RuinaRPG.Domain.Rules.ReferenceData`; the methods share names with those types — qualify the types or rename the methods `ComoXpPorNivel()`/`ComoEapPorNivel()` if the compiler complains, and update the test accordingly.)

- [ ] **Step 4: Run** tests → PASS.
- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/Niveis tests/RuinaRPG.Tests.Unit/Rules
git commit -m "feat(domain): progressão de nível estruturada e extrator dos bônus da tabela"
```

---

### Task 2: Tables, seeder and `ITabelaDeNiveis`

**Files:**
- Create: the three entities, `TabelaDeNiveisSeeder.cs`, `TabelaDeNiveis.cs` (Infrastructure/Rules/Niveis)
- Modify: `RuinaRpgDbContext.cs`, `src/RuinaRPG.Api/Program.cs` (DI + seeder call in **both** the `--migrate` path (~line 230) and the startup path (~line 300), next to `DurabilidadePorRankSeeder`)
- Create: migration `AddTabelaDeNiveis`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/TabelaDeNiveisSeederTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  ```csharp
  public class NivelProgressao { int Nivel; string? OutrosBonus; }                              // PK Nivel
  public class ColunaDeNivel { Guid Id; string Nome; TipoDeColunaDeNivel Tipo; string? ChaveDeSistema; int Ordem; bool IsDeleted; }
  public class ValorDeNivel { int Nivel; Guid ColunaId; int? Valor; }                          // PK (Nivel, ColunaId), cascades
  // DbSets: NiveisProgressao, ColunasDeNivel, ValoresDeNivel
  public static class TabelaDeNiveisSeeder { public static Task<int> SeedAsync(RuinaRpgDbContext db, string niveisMarkdown, IReadOnlyList<XpPorNivel> xp, IReadOnlyList<EapPorNivel> eap); } // returns rows created
  public interface ITabelaDeNiveis { Task<ProgressaoDeNivel> ObterAsync(); }                  // scoped, cached per request, ignores IsDeleted columns
  ```

Seeder rules: (1) ensure every `ChavesDeNivel.Sistema` column exists (create missing ones with the default Nome/Tipo/Ordem) — idempotent; (2) only if `NiveisProgressao` is empty: for each `LevelBonus` from `NivelBonusParser.Parse(niveisMarkdown)` create the row with `OutrosBonus = string.Join("\n", extracao.Restante)` and a `ValorDeNivel` for each extracted key; XP: `int.TryParse(xpRow.XpAbsoluto)` → value, else null; EAP → value.

- [ ] **Step 1: Failing test** (`TabelaDeNiveisSeederTests`, `IClassFixture<PostgresFixture>`, builds a `RuinaRpgDbContext` like the Persistence migration tests, calls `MigrateAsync` then the seeder with the real Markdown via `RulesDataProvider.ReadResource("Tabela de Níveis.md")` and the real `RulesDataProvider` XP/EAP lists):
```csharp
    [Fact]
    public async Task Seeded_table_reproduces_todays_budgets_xp_and_eap()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        var rules = new RulesDataProvider();
        await TabelaDeNiveisSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Níveis.md"), rules.XpPorNivel, rules.EapPorNivel);

        var tabela = await new TabelaDeNiveis(db).ObterAsync();

        tabela.UltimoNivel.Should().Be(50);
        foreach (var nivel in new[] { 1, 10, 25, 50 })
        {
            tabela.Acumulado(ChavesDeNivel.PontosDeAtributo, nivel).Should().Be(AttributePointBudgetCalculator.Compute(nivel, rules.Niveis));
            tabela.Acumulado(ChavesDeNivel.PontosDePericia, nivel).Should().Be(SkillPointBudgetCalculator.Compute(nivel, rules.Niveis));
            tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivel).Should().Be(PontosDeIgnicaoCalculator.ComputeTotal(nivel, 0, rules.Niveis));
        }
        tabela.XpPorNivel().Select(x => x.XpAbsoluto).Should().Equal(rules.XpPorNivel.OrderBy(x => x.Nivel).Select(x => x.XpAbsoluto));
        tabela.EapPorNivel().Select(x => x.ValorAbsoluto).Should().Equal(rules.EapPorNivel.OrderBy(x => x.Nivel).Select(x => x.ValorAbsoluto));
        tabela.Limite(ChavesDeNivel.MaxPericia, 50).Should().BeNull();
    }

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        await using var db = NewDb();
        await db.Database.MigrateAsync();
        var rules = new RulesDataProvider();
        var md = RulesDataProvider.ReadResource("Tabela de Níveis.md");
        await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel);

        (await TabelaDeNiveisSeeder.SeedAsync(db, md, rules.XpPorNivel, rules.EapPorNivel)).Should().Be(0);
        (await db.ColunasDeNivel.CountAsync()).Should().Be(ChavesDeNivel.Sistema.Count);
    }
```
(Check `RulesDataProvider`'s constructor and `ReadResource` visibility and adapt. `NewDb()` = `new RuinaRpgDbContext(new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options)`.)

- [ ] **Step 2: Run** `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~TabelaDeNiveisSeederTests` → build FAIL.

- [ ] **Step 3: Implement** entities, DbContext config:
```csharp
        builder.Entity<NivelProgressao>(e => { e.ToTable("NiveisProgressao"); e.HasKey(n => n.Nivel); e.Property(n => n.Nivel).ValueGeneratedNever(); });
        builder.Entity<ColunaDeNivel>(e => { e.ToTable("ColunasDeNivel"); e.HasIndex(c => c.ChaveDeSistema).IsUnique().HasFilter("\"ChaveDeSistema\" IS NOT NULL"); });
        builder.Entity<ValorDeNivel>(e =>
        {
            e.ToTable("ValoresDeNivel");
            e.HasKey(v => new { v.Nivel, v.ColunaId });
            e.HasOne<NivelProgressao>().WithMany().HasForeignKey(v => v.Nivel).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<ColunaDeNivel>().WithMany().HasForeignKey(v => v.ColunaId).OnDelete(DeleteBehavior.Cascade);
        });
```
Migration: `dotnet ef migrations add AddTabelaDeNiveis --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations` (creates the 3 tables only).
`TabelaDeNiveis` (scoped): loads non-deleted columns, all rows and values once, builds `ProgressaoDeNivel`; cache in a field. Register `builder.Services.AddScoped<ITabelaDeNiveis, TabelaDeNiveis>();`. Seeder call in `Program.cs` in both paths, mirroring the Durabilidade seeder lines (read the Markdown with `RulesDataProvider.ReadResource("Tabela de Níveis.md")`, pass the singleton `IRulesDataProvider`'s `XpPorNivel`/`EapPorNivel`, log the count like the neighbours do).

**Also** check `tests/RuinaRPG.Tests.Integration/ApiFactory.cs`: if it runs the startup seeders through `Program`, nothing to do; if it seeds manually, add this seeder there.

- [ ] **Step 4: Run** the test → PASS; `dotnet build` → 0/0.
- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(infra): Tabela de Níveis estruturada no banco, semeada a partir do Markdown"
```

---

### Task 3: Every gameplay consumer reads `ITabelaDeNiveis`

A refactor with the regression guard: the whole existing suite must stay green.

**Files:**
- Modify Domain calculators: `AttributePointBudgetCalculator`, `SkillPointBudgetCalculator`, `TraitPointBudgetCalculator`, `PontosDeIgnicaoCalculator`, `CreatureSheets/CreatureAttributePointBudgetCalculator` — take `ProgressaoDeNivel` instead of `IReadOnlyList<LevelBonus>`; delete their regexes (they now live only in `NivelBonusExtractor`).
- Modify: `ArcaEvolucaoRules` (`NivelValido(int nivel, int ultimoNivel)`, drop `NivelMaximo`) + `RacialAbilitiesController` + `ArcaEvolucoesEditor.razor` (`Max` from a new parameter or drop the `Max` attribute) + their tests.
- Modify Api: every file from the consumer grep — `CharacterSheetsController` (XP → level, level-up notice, EAP, ignição), `NpcSheetsController`, `CreatureSheetsController`, `CharacterAffinitiesController`, the four `*PossessionsController` budget calls, `CharacterAttributesController`, `NpcAttributesController`, `CreatureAttributesController`, `CharacterSkillsController`, `Services/CharacterSheetStats.cs`.
- Tests: update unit tests of the five calculators to build a small `ProgressaoDeNivel` instead of `LevelBonus` lists (keep the same expected numbers where the inputs map 1:1); `NivelBonusExtractorTests.Summing_extracted_values_reproduces_todays_calculators_for_every_level` must now compute the expected values without the deleted regex calculators — replace it with a snapshot of the expected per-level totals captured **before** deleting them (run the old test once, print the four arrays of 50 values, paste them as literals, then delete the calls).
- Add: `tests/RuinaRPG.Tests.Integration/Controllers/NivelLimitsTests.cs` (NPC level validation, below).

**Interfaces:**
- Consumes: `ITabelaDeNiveis.ObterAsync()`, `ProgressaoDeNivel` (Tasks 1–2).
- Produces (new calculator signatures):
  ```csharp
  AttributePointBudgetCalculator.Compute(int nivel, ProgressaoDeNivel tabela)          // = tabela.Acumulado(PontosDeAtributo, nivel)
  SkillPointBudgetCalculator.Compute(int nivel, ProgressaoDeNivel tabela)
  TraitPointBudgetCalculator.Compute(int nivel, ProgressaoDeNivel tabela)              // 5 + Acumulado(EspacosDeCaracteristica)
  PontosDeIgnicaoCalculator.ComputeTotal(int nivel, int bonusManual, ProgressaoDeNivel tabela)
  CreatureAttributePointBudgetCalculator.Compute(Rank? rank, int nivel, ProgressaoDeNivel tabela)
  ArcaEvolucaoRules.NivelValido(int nivel, int ultimoNivel)
  ```

Mechanical rules:
1. Inject `ITabelaDeNiveis tabelaDeNiveis` into each controller/service primary constructor; at the top of the action `var tabela = await tabelaDeNiveis.ObterAsync();`.
2. `rules.Niveis` passed to a budget calculator → `tabela`.
3. `rules.XpPorNivel` → `tabela.XpPorNivel()`; `rules.EapPorNivel` → `tabela.EapPorNivel()`; `LevelUpNoticeCalculator.PendingBonuses(..., rules.Niveis)` → `(..., tabela.ComoLevelBonus())`.
4. `IRulesDataProvider` stays injected where it's still used for other tables (Vocações, Classes, CirculoGrauPorEap…). `IRulesDataProvider.Niveis/XpPorNivel/EapPorNivel` remain only for `CompendioSearchService` (and the seeder).
5. NPC `PUT {id}/nivel` and Creature equivalent: reject `nivel < 1 || nivel > tabela.UltimoNivel` with 400 `$"O nível deve estar entre 1 e {tabela.UltimoNivel}."`.
6. Level-up notice lines keep the same content but now list the numeric bonuses first (column order), then the free-text lines. If an existing level-up-notice test asserts the exact order, update its expected order — never its content.
7. `RacialAbilitiesController.ValidateEvolucao` becomes async and uses `tabela.UltimoNivel`; message `$"O nível da evolução deve estar entre 1 e {ultimo}."`.

- [ ] **Step 1: Failing test** `NivelLimitsTests`:
```csharp
    [Fact]
    public async Task Npc_level_above_the_last_level_of_the_table_is_rejected()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NivelLimGm1", "nivellim1@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmToken);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 51))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 50))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
```
(Copy `CreateNpcSheetAsync`/helpers from `NpcSheetsControllerTests`.) Run → FAIL (51 accepted today).

- [ ] **Step 2: Capture the regression snapshot** (see Tests bullet above) and commit nothing yet.
- [ ] **Step 3: Apply the refactor** (calculators, then controllers, following the rules).
- [ ] **Step 4: Verify**: `dotnet build` 0/0; unit suite PASS; client suite PASS; integration batches covering `*Attributes*`, `*Skills*`, `*Possessions*`, `*Sheets*`, `*Affinit*`, `SheetStats`, `RacialAbilities`, `LevelUp`, `NivelLimitsTests` → PASS.
- [ ] **Step 5: Commit**

```bash
git add -A src tests
git commit -m "refactor: saldos, XP, EAP e aviso de nível vêm da Tabela de Níveis do banco"
```

---

### Task 4: Caps enforcement (attribute, perícia, Passivas)

**Files:**
- Create: `src/RuinaRPG.Api/Services/LimitesDeNivel.cs`
- Modify: `CharacterAttributesController.Update`, `NpcAttributesController.Update`, `CharacterSkillsController.Update`, `NpcSkillsController.Update`, `CharacterSpellAbilitiesController.Add`, `NpcSpellAbilitiesController.Add`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/LimitesDeNivelTests.cs`

**Interfaces:**
- Consumes: `ProgressaoDeNivel.Limite`, `ChavesDeNivel.MaxPassivas`, `PUT api/tabela-de-niveis/{nivel}/valores/{colunaId}` from Task 5 is **not** available yet — tests set caps directly through the DbContext (`_factory.Services.CreateScope()` → `RuinaRpgDbContext`), writing a `ValorDeNivel` for the column found by `ChaveDeSistema`, and delete it in `finally`.
- Produces:
  ```csharp
  public static class LimitesDeNivel
  {
      // null = allowed; otherwise the 400 message
      public static string? Gasto(string rotulo, int gastoAtual, int gastoNovo, int? limite, int nivel);
      public static string? Passivas(CategoriaDePassiva categoria, int jaNaFicha, int? limite, int nivel);
  }
  ```
  `Gasto`: allowed when `limite is null || gastoNovo <= limite || gastoNovo <= gastoAtual`; message `$"{rotulo} não pode passar de {limite} pontos no nível {nivel}."`. `Passivas`: allowed when `limite is null || jaNaFicha < limite`; message `$"O nível {nivel} permite no máximo {limite} Passiva(s) {Rotulo(categoria)}."` with rótulos "Livre(s)", "Vocacional(is)", "De Classe".

- [ ] **Step 1: Failing tests** — unit tests for `LimitesDeNivel` in `tests/RuinaRPG.Tests.Unit/...`? It's in Api; put them in the integration project as plain `[Fact]`s without the fixture, or move `LimitesDeNivel` to Domain (`RuinaRPG.Domain/Rules/Niveis/LimitesDeNivel.cs`) and unit-test it there — **prefer Domain**:
```csharp
public class LimitesDeNivelTests
{
    [Theory]
    [InlineData(2, 3, null, true)]
    [InlineData(2, 3, 3, true)]
    [InlineData(2, 4, 3, false)]
    [InlineData(5, 4, 3, true)]   // lowering an over-cap value is allowed
    [InlineData(5, 5, 3, true)]   // unchanged over-cap value is allowed
    [InlineData(5, 6, 3, false)]
    public void Gasto(int atual, int novo, int? limite, bool permitido) =>
        (LimitesDeNivel.Gasto("Força", atual, novo, limite, 7) is null).Should().Be(permitido);

    [Fact]
    public void Gasto_message_names_the_target_cap_and_level() =>
        LimitesDeNivel.Gasto("Atletismo", 0, 9, 8, 5).Should().Be("Atletismo não pode passar de 8 pontos no nível 5.");

    [Theory]
    [InlineData(0, null, true)]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(0, 0, false)]
    public void Passivas(int jaNaFicha, int? limite, bool permitido) =>
        (LimitesDeNivel.Passivas(CategoriaDePassiva.Livre, jaNaFicha, limite, 10) is null).Should().Be(permitido);
}
```
Integration (`LimitesDeNivelTests` in Controllers) — each sets a cap through the DbContext at **level 1** (inherited by every level), runs, restores in `finally`. Run these tests sequentially in one class (the cap is global):
  1. `Character_attribute_gasto_above_MaxAtributo_is_rejected_but_lowering_is_allowed` — cap 3; PUT Força Gasto 4 → 400 with "não pode passar de 3"; Gasto 3 → 204; remove cap, set Gasto 5, set cap 3, PUT Gasto 4 → 204, Gasto 6 → 400.
  2. `Npc_attribute_gasto_above_MaxAtributo_is_rejected`.
  3. `Character_skill_gasto_above_MaxPericia_is_rejected` — `PUT .../skills/Atletismo` with Gasto 4 when cap 3 → 400.
  4. `Npc_skill_gasto_above_MaxPericia_is_rejected`.
  5. `Adding_a_passiva_beyond_its_category_cap_is_rejected_other_categories_unaffected` — create two Livre and one Vocacional Passivas with no requisites in the GM's bank (copy the bank-entry creation from `CharacterSpellAbilitiesControllerTests`' Passiva tests), cap MaxPassivasLivres = 1: first Livre → 201, second Livre → 400 mentioning "Livre", Vocacional → 201.
  6. `Npc_passiva_cap_is_enforced_too`.

- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** `LimitesDeNivel` (Domain) and call it in each controller before saving, e.g. in `CharacterAttributesController.Update` right after the budget check:
```csharp
        var limite = tabela.Limite(ChavesDeNivel.MaxAtributo, sheet.Nivel);
        if (LimitesDeNivel.Gasto(AtributoDisplayName(atributo), attribute.Gasto, request.Gasto, limite, sheet.Nivel) is { } erro)
            return BadRequest(erro);
```
(Use the atributo's Portuguese label — check for an existing label helper in Domain, e.g. `AtributoLabels`; otherwise `atributo.ToString()`.) Skills use the perícia `Nome` from `IPericiaCatalogo` and the existing row's Gasto (0 when the row doesn't exist yet). Passivas: in `Add`, when `bankEntry.Tipo == SpellAbilityTipo.Passiva && bankEntry.Categoria is { } cat`, count `db.CharacterSpellAbilities.CountAsync(e => e.CharacterSheetId == sheetId && e.Tipo == SpellAbilityTipo.Passiva && e.Categoria == cat)` and check `LimitesDeNivel.Passivas(cat, count, tabela.Limite(ChavesDeNivel.MaxPassivas(cat), sheet.Nivel), sheet.Nivel)`.
- [ ] **Step 4: Run** unit + the integration class → PASS; build 0/0.
- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(api): limites por nível para atributos, perícias e Passivas"
```

---

### Task 5: Tabela de Níveis API (read + Auditoria writes)

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/TabelaDeNiveisResponse.cs` (+ request records in the same folder)
- Create: `src/RuinaRPG.Api/Controllers/TabelaDeNiveisController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/TabelaDeNiveisControllerTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public record ColunaDeNivelResponse(Guid Id, string Nome, string Tipo, string? ChaveDeSistema, bool DoSistema, int Ordem);
  public record LinhaDeNivelResponse(int Nivel, string? OutrosBonus, Dictionary<Guid, int?> Valores);
  public record TabelaDeNiveisResponse(List<ColunaDeNivelResponse> Colunas, List<LinhaDeNivelResponse> Linhas);
  public record AtualizarValorDeNivelRequest(int? Valor);
  public record AtualizarOutrosBonusRequest(string? OutrosBonus);
  public record CriarColunaDeNivelRequest(string Nome, string Tipo);
  public record RenomearColunaDeNivelRequest(string Nome);
  public record ReordenarColunasDeNivelRequest(List<Guid> Ids);
  // GET    api/tabela-de-niveis                              any authenticated
  // PUT    api/tabela-de-niveis/{nivel}/valores/{colunaId}   auditor → 204; Valor < 0 → 400; unknown level/column → 404
  // PUT    api/tabela-de-niveis/{nivel}/outros-bonus         auditor → 204
  // POST   api/tabela-de-niveis/niveis                       auditor → 201 LinhaDeNivelResponse (UltimoNivel + 1, empty)
  // DELETE api/tabela-de-niveis/niveis/ultimo                auditor → 204; only 1 level left → 400; Character/Npc/Creature sheet at that level → 400 "N ficha(s) estão no nível X."
  // POST   api/tabela-de-niveis/colunas                      auditor → 201 ColunaDeNivelResponse (Ordem = max + 1); blank name / bad Tipo → 400
  // PUT    api/tabela-de-niveis/colunas/{id}                 auditor → 204 rename; blank → 400
  // DELETE api/tabela-de-niveis/colunas/{id}                 auditor → 204 (hard delete, values cascade); system column → 400
  // PUT    api/tabela-de-niveis/colunas/ordem                auditor → 204; Ids must be exactly the set of columns → else 400
  ```

- [ ] **Step 1: Failing integration tests** (auditor helper from the Perícias plan):
  1. `Get_returns_the_13_system_columns_and_50_levels_for_any_user` (jogador token) — `Colunas.Count(c => c.DoSistema) == 13`, `Linhas.Count == 50`, level 1 `Valores[col PontosDeAtributo] == 9`.
  2. `Writes_return_403_for_a_non_auditor` — each write route.
  3. `Editing_a_value_changes_the_budget_seen_by_a_sheet` — set level 49 Pontos de Atributo to 99 → `GET` shows 99; NPC at level 49's attributes budget includes it; restore original value in `finally`.
  4. `Negative_value_returns_400`.
  5. `Editing_outros_bonus_shows_up_in_the_level_up_notice` — level 2 outros-bonus "Bônus de teste" → a Personagem whose XP crosses to level 2 sees "Bônus de teste" in `GET .../level-up-notice`; restore.
  6. `Add_level_then_remove_last_level` — POST → 201 Nivel 51; DELETE último → 204; GET shows 50 levels.
  7. `Remove_last_level_in_use_returns_400` — add level 51, set level 50 XP to 99999 and give an NPC `PUT nivel 51` → DELETE último → 400 containing "1 ficha"; set NPC back to 1, DELETE → 204, restore level 50 XP to null.
  8. `Custom_column_crud_and_reorder` — create "Pontos de Fama" Acumulativa → appears last; rename; reorder (move it first) → GET order reflects; delete → gone.
  9. `System_column_cannot_be_deleted_and_bad_type_is_rejected`.
  10. `Reorder_with_a_missing_id_returns_400`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** the controller (pattern: `DurabilidadesPorRankController` + `RequireRulesAuditorAsync`). Upsert `ValorDeNivel` on PUT (create if missing; `Valor = null` keeps the row with null). "In use" count: `CharacterSheets.CountAsync(s => s.Nivel == ultimo) + NpcSheets… + CreatureSheets…`. Deleting the last level deletes its `NivelProgressao` row (values cascade). Tipo parse: `Enum.TryParse<TipoDeColunaDeNivel>(request.Tipo, out var tipo) && Enum.IsDefined(tipo)`.
- [ ] **Step 4: Run** → PASS; build 0/0.
- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(api): Auditoria da Tabela de Níveis (valores, níveis e colunas)"
```

---

### Task 6: Client — `/auditoria/tabela-de-niveis`

**Files:**
- Create: `src/RuinaRPG.Client/Pages/AuditoriaTabelaDeNiveis.razor`
- Modify: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor` (link `Icons.Material.Filled.TableChart`, "Tabela de Níveis")
- Test: `tests/RuinaRPG.Tests.Client/Pages/AuditoriaTabelaDeNiveisTests.cs` (+ nav links test if it pins the list)

Layout:
- Breadcrumbs, `DismissibleAlert`, `Section Title="Tabela de Níveis"` with `TitleInfo` InfoPopup "Como funciona a Tabela de Níveis":
  > Cada linha é um nível e cada coluna, um recurso. Colunas Acumulativas (ícone de somatório) somam do nível 1 até o nível do personagem — ex.: Pontos de Atributo. Colunas Por nível (ícone de régua) valem o número da linha do nível atual — ex.: Máx. de Perícia; uma célula vazia repete o valor do nível anterior mais próximo, e se nenhum nível tiver valor não há limite (XP e EAP não repetem: XP vazio no último nível significa nível máximo). Os limites (Máx. de Atributo e Máx. de Perícia — pontos gastos num atributo ou perícia — e Máx. de Passivas por categoria) bloqueiam o salvamento da ficha; uma ficha que já passou do limite continua válida, mas não pode subir mais. Colunas com o ícone de cadeado são do sistema: podem ser renomeadas, não removidas. Colunas criadas por você aparecem na ficha só como informação. "Remover último nível" é recusado se alguma ficha estiver nesse nível.
- Toolbar: "+ Coluna" (`Icons.Material.Filled.ViewColumn`) opens a `MudDialog` with Nome, Tipo (`MudRadioGroup` Acumulativa / Por nível) and its own InfoPopup ("Acumulativa soma os níveis até o atual; Por nível vale o número do nível atual, herdando o anterior quando vazio. O tipo não pode ser mudado depois."); "+ Nível" (`Icons.Material.Filled.Add`); "Remover último nível" (`Icons.Material.Filled.RemoveCircleOutline`, `Color.Error`, confirmation dialog).
- Grid inside `<div class="table-responsive">` → `MudSimpleTable Dense`: first column "Nível" (sticky if simple via CSS `position: sticky; left: 0`), then one column per `ColunaDeNivelResponse`: header = type icon (`Functions`/`Straighten` in a `MudTooltip` "Acumulativa"/"Por nível") + name `MudTextField` (rename on change) + lock icon (system) or delete `MudIconButton` (custom, `aria-label="Remover coluna {Nome}"`) + left/right arrow `MudIconButton`s (`ChevronLeft`/`ChevronRight`, send the full reordered id list); cells = `MudNumericField<int?>` (`Min="0"`, `Clearable`), save on change; last column "Outros bônus" = multiline `MudTextField` saved on change.
- After any failed write: show the API message and reload.

- [ ] **Step 1: Failing bUnit tests** (stateful fake like `AuditoriaDurabilidadePorRankTests`, 2 system columns + 1 custom, 3 levels):
  1. `Renders_one_row_per_level_and_one_column_per_coluna_plus_outros_bonus`.
  2. `Editing_a_cell_puts_the_value` → PUT `tabela-de-niveis/2/valores/{id}` body `Valor = 7`; clearing sends `null`.
  3. `System_columns_show_a_lock_and_no_delete_button_custom_ones_can_be_removed` → one `button[aria-label='Remover coluna Fama']`, none for system; clicking it → DELETE `colunas/{id}`.
  4. `Moving_a_column_right_puts_the_new_order`.
  5. `Adding_a_column_posts_name_and_type`.
  6. `Adicionar_nivel_posts_and_the_new_row_appears`.
  7. `Remover_ultimo_nivel_confirms_then_deletes_and_shows_the_400_message` → fake returns 400 "1 ficha(s) estão no nível 3." → alert shows it.
  8. `Info_popup_explains_the_column_types` → click first InfoPopup → markup contains "célula vazia repete o valor".
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement.** **Step 4: Run** client suite → PASS; build 0/0.
- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): página de Auditoria da Tabela de Níveis"
```

---

### Task 7: Sheet panel "Progressão do nível"

**Files:**
- Create: `src/RuinaRPG.Client/Shared/ProgressaoDoNivelSection.razor`
- Modify: `FichaDePersonagem.razor`, `FichaDeNpc.razor` (tab Atributos & Perícias, top — before the attributes table)
- Test: `tests/RuinaRPG.Tests.Client/Shared/ProgressaoDoNivelSectionTests.cs`

**Interfaces:**
- Consumes: `GET api/tabela-de-niveis` (Task 5).
- Produces: `<ProgressaoDoNivelSection Nivel="int" />` — loads the table itself (`@inject HttpClient Http`) and resolves values client-side with the **same rules as `ProgressaoDeNivel`** — reuse it: the Client references Domain, so build a `ProgressaoDeNivel` from the response (map `ColunaDeNivelResponse` → `ColunaDeNivelDef`, `LinhaDeNivelResponse` → `LinhaDeNivel`) and call `Resolver(coluna, Nivel)`. No duplicated logic.

Rendering: `Section Title="Progressão do nível"` with InfoPopup:
> Saldos e limites do nível atual, definidos na Tabela de Níveis. Totais somam todos os níveis até o atual; limites (máx.) valem para o nível atual e o app não deixa ultrapassá-los.
Then a compact `MudSimpleTable` (or chips) with one line per column except XP and EAP (already shown elsewhere on the sheet): Acumulativa → "Nome: N"; PorNivel → "Nome: máx. N" or "Nome: sem limite" when null; custom columns the same way. Reload when `Nivel` changes (`OnParametersSetAsync` comparing to the last loaded level; fetch the table once).

- [ ] **Step 1: Failing bUnit tests**:
  1. `Shows_accumulated_totals_up_to_the_level` — table: Pontos de Atributo 9 at L1, 1 at L2; `Nivel=2` → "Pontos de Atributo: 10".
  2. `Shows_caps_with_inheritance_and_sem_limite_when_empty` — Máx. de Perícia 4 at L1 only; `Nivel=3` → "máx. 4"; Máx. de Atributo empty → "sem limite".
  3. `Hides_xp_and_eap_rows`.
  4. `Shows_custom_columns`.
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement** and place `<ProgressaoDoNivelSection Nivel="@_form.Nivel" />` (use each page's actual level field). **Step 4: Run** client suite → PASS; build 0/0.
- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): painel Progressão do nível nas fichas de Personagem e NPC"
```

---

### Task 8: Livro de Regras generated from the table; override section removed

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs` (`BuildTabelaDeNiveisAsync`, `ReadEmbeddedMarkdown`, class doc comment)
- Modify: `src/RuinaRPG.Api/Controllers/RulebookDocumentsController.cs` (`ValidSlugs` drops `"tabela-de-niveis"`, doc comment)
- Modify: `src/RuinaRPG.Client/Pages/AuditoriaLivroDeRegras.razor` (drop the `"tabela-de-niveis"` label; adjust the intro text that lists editable documents if it names Tabela de Níveis)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs`, `RulebookDocumentsControllerTests.cs` (whatever exists — grep), `tests/RuinaRPG.Tests.Client/Pages/AuditoriaLivroDeRegrasTests.cs`

**Interfaces:**
- Consumes: `ITabelaDeNiveis` (inject into `RulebookRenderer`; check how it's registered — scoped is required since it now depends on a scoped service).

- [ ] **Step 1: Failing tests**:
  1. Rulebook: `GET` the `tabela-de-niveis` document → its HTML contains a `<table>` whose header has "Nível", "Pontos de Atributo" and "Outros bônus"; after adding a custom column "Pontos de Fama" (auditor) it also contains "Pontos de Fama" (delete it in `finally`).
  2. Rulebook documents: `GET rulebook-documents` (auditor) no longer lists `tabela-de-niveis`; `PUT rulebook-documents/tabela-de-niveis` → 404 (or whatever the controller returns for an invalid slug today — assert that same status).
  3. Client `AuditoriaLivroDeRegrasTests`: with a fake response lacking the slug, no "Tabela de Níveis" section renders (update any existing test that expected it).
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** `BuildTabelaDeNiveisAsync`:
```csharp
    private async Task<RulebookDocument> BuildTabelaDeNiveisAsync()
    {
        var tabela = await tabelaDeNiveis.ObterAsync();
        var colunas = tabela.Colunas;
        var html = new StringBuilder("<table><thead><tr><th>Nível</th>");
        foreach (var c in colunas)
            html.Append("<th>").Append(WebUtility.HtmlEncode(c.Nome)).Append("</th>");
        html.Append("<th>Outros bônus</th></tr></thead><tbody>");
        foreach (var linha in tabela.Linhas)
        {
            html.Append("<tr><td>").Append(linha.Nivel).Append("</td>");
            foreach (var c in colunas)
                html.Append("<td>").Append(linha.Valores.GetValueOrDefault(c.Id)?.ToString() ?? "").Append("</td>");
            var outros = WebUtility.HtmlEncode(linha.OutrosBonus ?? "").Replace("\n", "<br />");
            html.Append("<td>").Append(outros).Append("</td></tr>");
        }
        html.Append("</tbody></table>");
        return new RulebookDocument("tabela-de-niveis", "Tabela de Níveis", html.ToString(), []);
    }
```
Remove the `"tabela-de-niveis"` arms from `ReadMarkdownAsync`/`ReadEmbeddedMarkdown`. An existing `RulebookDocumentOverride` row with that slug is simply ignored (no migration needed).
- [ ] **Step 4: Run** the rulebook integration classes + client suite → PASS; build 0/0.
- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: Livro de Regras monta a Tabela de Níveis a partir da Auditoria; seção sai do editor do Livro"
```

---

### Task 9: Requirements docs and CLAUDE.md

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md` (append R0013; R0002's list of editable Livro documents drops Tabela de Níveis)
- Modify: `Docs/Requisitos/Requisitos - Livro de Regras.md` (Tabela de Níveis tab is generated from the Auditoria table)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (painel Progressão do nível; limits on 2.a attributes, 2.d perícias, 4.f Passivas; "Nível … de 1 a 50" → "de 1 ao último nível da Tabela de Níveis")
- Modify: `Docs/Requisitos/Requisitos - Habilidades Raciais.md` R0006 ("1 a 50" → "1 ao último nível da Tabela de Níveis")
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md` (§11: `NiveisProgressao`, `ColunasDeNivel`, `ValoresDeNivel`; `ArcaEvolucoes.Nivel` range)
- Modify: `CLAUDE.md` (the embedded-resources bullet: `Tabela de Níveis.md` now only seeds the DB table once and feeds the Compêndio; editing its structure no longer changes budgets after the first seed)

- [ ] **Step 1: R0013**:

```markdown

# **R0013** - O Auditor edita a Tabela de Níveis.

**Descrição**: A página **Auditoria → Tabela de Níveis** mostra uma linha por nível e uma coluna por recurso, mais um campo de texto livre **Outros bônus** por nível (ex.: "Status de Vida Aprimorado", "Primeira Passiva"). Cada coluna é de um de dois tipos:

- **Acumulativa**: o valor da ficha é a soma dos níveis 1 até o nível atual (célula vazia vale 0). Ex.: Pontos de Atributo, Pontos de Perícia, Espaços de Característica, Pontos de Ignição, Espaços e Pontos de Maestria.
- **Por nível**: vale o número do nível atual; uma célula vazia repete o valor do nível anterior mais próximo, e sem nenhum valor não há limite. Ex.: Máx. de Atributo, Máx. de Perícia, Máx. de Passivas Livres/Vocacionais/De Classe. **XP para o próximo nível** e **EAP base** também são Por nível, mas nunca repetem valor: XP vazio no último nível significa nível máximo.

As colunas acima são do sistema — podem ser renomeadas, não removidas, e alimentam as fichas. O Auditor pode criar colunas próprias (nome + tipo, o tipo não muda depois), que aparecem na ficha só como informação, e removê-las. Pode também adicionar um nível ao fim da tabela e remover o último nível, desde que nenhuma ficha esteja nele.

**Limites**: Máx. de Atributo e Máx. de Perícia limitam os pontos gastos (Gasto) num atributo ou numa perícia; Máx. de Passivas limita quantas Passivas de cada categoria a ficha pode ter. Valem para Personagem e NPC: o servidor recusa salvar acima do limite, mas uma ficha que já está acima continua válida e pode baixar o valor.

O Livro de Regras monta a aba Tabela de Níveis a partir desta tabela; ela não é mais editada em Auditoria → Livro de Regras.
```

- [ ] **Step 2:** apply the other doc edits listed under **Files** (short, one paragraph each, using `[[wikilinks]]`, no duplicated numbers).
- [ ] **Step 3: Commit**

```bash
git add Docs/Requisitos CLAUDE.md
git commit -m "docs: requisitos da Auditoria da Tabela de Níveis (R0013)"
```
