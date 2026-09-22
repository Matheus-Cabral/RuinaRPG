# Sistema de Históricos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GM-editable catalog of 26 Históricos (each granting +6 to one Perícia and +3 to another), a selectlist+popup field for it on the Ficha de Personagem/NPC Identidade section, direct propagation of that bonus into the Perícia Modificador (and everything derived from it), a dedicated Auditoria page to manage the catalog, and a live-updating "Históricos" tab in the Livro de Regras.

**Architecture:** Clone the existing `Trait`/`TraitsController`/`AuditoriaCaracteristicas.razor` pattern (seeded catalog, Auditor-gated CRUD, soft delete, in-use delete guard) for a new `Historico` entity. Unlike Traits (a multi-pick list joined via `CharacterTrait`), a Histórico is a single choice per sheet — a live FK (`HistoricoId`) directly on `CharacterSheet`/`NpcSheet`, same pattern already used for `ImageId`. `SkillFormulas.Modificador` gains a required `historicoBonus` parameter so the bonus flows through every existing consumer (Perícia table, Sub-Atributos "Bruto", Maestria Total) without introducing a parallel calculation path.

**Tech Stack:** ASP.NET Core 8 API, EF Core/Npgsql, Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions + Testcontainers (Postgres).

**Spec:** `docs/superpowers/specs/2026-09-22-historicos-design.md`

## Global Constraints

- TDD is mandatory (CLAUDE.md R0011): write the failing test first, then the minimal code to pass it.
- `dotnet build` must finish with 0 warnings, 0 errors after every task.
- Ficha de Criatura gets no Histórico field — the 3 Creature call sites of `SkillFormulas.Modificador` pass a literal `0` and are never touched again after Task 3.
- Every positional record change (`UpdateCharacterSheetRequest`, `UpdateNpcSheetRequest`, `CharacterSheetResponse`, `NpcSheetResponse`) appends the new field at the **end** of the parameter list — never insert in the middle, or every existing call site breaks silently instead of with a clear compiler error at the right place.
- `docs/Sistema RPG/Historico.md` has already been reformatted with real `#` headings (26 entries, `# N. Nome` + description paragraph + `+6 <Perícia>`/`+3 <Perícia>` lines, `---` separators between entries, unchanged from the original prose otherwise) — verify it looks like this before Task 2; if it doesn't (e.g. a fresh clone without this session's edit), recreate it in that shape first.

---

### Task 1: Docs — Requisitos updates

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`
- Modify: `Docs/Requisitos/Requisitos - Ficha de Criaturas.md`
- Modify: `Docs/Requisitos/Requisitos - Livro de Regras.md`
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md`
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`

**Interfaces:**
- Consumes: nothing (pure documentation, no code dependency).
- Produces: nothing consumed by later tasks — this task can run in parallel with any other, order doesn't matter.

- [ ] **Step 1: Add the Histórico field to Ficha de Personagem's Identidade (1.a)**

Open `Docs/Requisitos/Requisitos - Ficha de Personagem.md` and find this existing bullet (added for the Estrela field):

```
- *Estrela*: Um dropdown com as 10 Estrelas Alkerianas definidas em "[[As estrelas alkerianas]]", na ordem das Coroas — **Aeurer**, **Liora**, **Vaelen**, **Theron**, **Maetus**, **Almora**, **Elvani**, **Sareli**, **Gaelion** e **Sadir**. O valor padrão do dropdown deve ser o salvo no banco de dados e caso for NULL, exibe o placeholder *Escolha uma Estrela*. Um ícone ⓘ ao lado do dropdown abre um popup com a descrição da Estrela selecionada (tendência de comportamento, mesmo texto de "[[As estrelas alkerianas]]"), para não poluir a ficha com o texto completo.
```

Add a new bullet right after it:

```

- *Histórico*: Um dropdown com os Históricos cadastrados no catálogo (ver "[[Requisitos - Auditoria de Regras]]" — CRUD completo pelo Auditor de Regras), cada um concedendo +6 numa Perícia e +3 em outra (ver "[[Historico]]"). O valor padrão do dropdown deve ser o salvo no banco de dados e caso for NULL, exibe o placeholder *Escolha um Histórico*. Um ícone ⓘ ao lado do dropdown abre um popup com a descrição do Histórico selecionado e as duas Perícias bonificadas, para não poluir a ficha com o texto completo. O bônus entra direto no *Modificador* das duas Perícias bonificadas (2.d) — e, por extensão, em tudo que já deriva de Modificador (Sub-Atributos via "Bruto [Perícia]", Maestrias).
```

- [ ] **Step 2: Exclude Histórico from Ficha de Criaturas**

Open `Docs/Requisitos/Requisitos - Ficha de Criaturas.md` and find this line (already lists Estrela/Sina as excluded):

```
- *Linhagem*, *Variante*, *Vocação*, *Classe*, *Trabalho*, *Propriedade*, *Estrela* e *Sina* (ver "[[As estrelas alkerianas]]") não existem na Ficha de Criatura — uma criatura não nasce sob uma Coroa do calendário Alkeriano do mesmo jeito que um Personagem/NPC. Em vez das primeiras:
```

Replace it with:

```
- *Linhagem*, *Variante*, *Vocação*, *Classe*, *Trabalho*, *Propriedade*, *Estrela*, *Sina* (ver "[[As estrelas alkerianas]]") e *Histórico* (ver "[[Historico]]") não existem na Ficha de Criatura — uma criatura não nasce sob uma Coroa do calendário Alkeriano do mesmo jeito que um Personagem/NPC, e não tem um passado narrativo estruturado em Históricos. Em vez das primeiras:
```

- [ ] **Step 3: Add the 6th document + split-table row + real-time-catalog requirement to Livro de Regras**

Open `Docs/Requisitos/Requisitos - Livro de Regras.md`. Find R0001 (currently lists 5 documents ending with `[[As estrelas alkerianas]]`) and change its heading + list:

```
# **R0001** - O Livro de Regras deve exibir, em abas, o conteúdo completo de seis documentos do sistema.
```

Add a 6th bullet after `"[[As estrelas alkerianas]]"`:

```
- "[[Historico]]"
```

Find the R0004 split-level table (has a row for "As Estrelas Alkerianas" ending in `11 (Sina + uma por Estrela...)`). Add a new row right after it:

```
| Históricos | — | uma por Histórico (ver R0007 abaixo; vem do catálogo de Históricos — não do Markdown, mesmo tratamento de R0004 para Características) |
```

At the end of the file (after the existing R0006 about the calendar image), add a new requirement:

```

# **R0007** - A aba de Históricos reflete o catálogo de Históricos em tempo real.

**Descrição**: Ao contrário dos documentos com sobrescrita de Markdown (R0002/R0006), a aba de Históricos não tem uma sobrescrita de texto própria — ela é montada diretamente a partir do catálogo de Históricos (ver "[[Requisitos - Auditoria de Regras]]"). Uma edição salva ali aparece nessa aba imediatamente, sem precisar de nenhuma ação adicional do Auditor. O parágrafo introdutório de "[[Historico]]" (o texto explicando o que são Históricos e a regra do +6/+3) continua vindo do documento fonte, exibido no topo da aba antes dos cartões de cada Histórico.
```

- [ ] **Step 4: Add the Auditoria de Regras CRUD requirement for Históricos**

Open `Docs/Requisitos/Requisitos - Auditoria de Regras.md`, append at the end of the file (after R0007):

```

# **R0008** - O Auditor de Regras tem CRUD completo sobre o catálogo de Históricos.

**Descrição**: Uma página separada lista todos os Históricos (ver "[[Historico]]"), cada um com Nome, Descrição e as duas Perícias bonificadas (+6 e +3) editáveis, mais um botão de exclusão por linha e um formulário para adicionar um novo. As duas Perícias de uma mesma entrada não podem ser iguais — uma tentativa de salvar as duas iguais é rejeitada. Excluir um Histórico já usado em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Assim como o catálogo de Características (R0003), esse catálogo não é por GM — a mudança vale para o servidor inteiro. Editar ou excluir um Histórico originalmente vindo do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor.
```

- [ ] **Step 5: Add the Historicos table + HistoricoId columns to Modelo de Dados**

Open `Docs/Requisitos/Requisitos - Modelo de Dados.md`. Find the `Traits` table definition (has columns Id/Nome/Descricao/Custo/Polaridade/IsCustomized/IsDeleted/UpdatedByUserId). Add a new table right after it:

```

**Historicos** — `Historico.md` convertido em tabela (dado estático, seedado a partir do documento).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Nome | string |
| Descricao | text |
| PericiaMaisSeis | enum Pericia |
| PericiaMaisTres | enum Pericia |
| IsCustomized | bool — true depois de criada/editada pelo Auditor de Regras; protege a linha de ser sobrescrita pelo re-seed a partir de Historico.md |
| IsDeleted | bool — soft delete pelo Auditor de Regras; oculta a linha de toda leitura, mas ela continua existindo para o re-seed nunca recriá-la |
| UpdatedByUserId | FK → Users, nullable |
| UpdatedAt | DateTime, nullable |
```

Find the `CharacterSheets` table's `Propriedade` row (or the `Estrela`/`SinaAtual` rows added for the previous system) and add, right after them:

```
| HistoricoId | FK → Historicos, nullable | referência ao vivo (ver legenda) — nenhum campo é copiado para a ficha |
```

Do the exact same addition (same row) in the `NpcSheets` table.

- [ ] **Step 6: Commit**

```bash
git add "Docs/Requisitos/Requisitos - Ficha de Personagem.md" "Docs/Requisitos/Requisitos - Ficha de Criaturas.md" "Docs/Requisitos/Requisitos - Livro de Regras.md" "Docs/Requisitos/Requisitos - Auditoria de Regras.md" "Docs/Requisitos/Requisitos - Modelo de Dados.md"
git commit -m "docs: requisitos do sistema de Históricos"
```

---

### Task 2: Domain — HistoricoSeed record + HistoricoSeedParser

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/HistoricoSeed.cs`
- Create: `src/RuinaRPG.Domain/Rules/HistoricoSeedParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/HistoricoSeedParserTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.Rules.Markdown.MarkdownSectionParser.Parse(string markdown)` → `IReadOnlyList<MarkdownSection>` (each with `.Title`, `.Body`, `.Children`) — already exists, used unchanged.
- Produces: `HistoricoSeed(string Nome, string Descricao, Pericia PericiaMaisSeis, Pericia PericiaMaisTres)` and `HistoricoSeedParser.Parse(string markdown) -> IReadOnlyList<HistoricoSeed>`, both consumed by Task 4's `HistoricoSeeder`.

- [ ] **Step 1: Write the failing test**

Create `tests/RuinaRPG.Tests.Unit/Rules/HistoricoSeedParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class HistoricoSeedParserTests
{
    // Real shape from Docs/Sistema RPG/Historico.md: numbered "# N. Nome" headings, a description
    // paragraph, then a "+6 X"/"+3 Y" pair, separated by "---" lines (which the parser must ignore,
    // same as it ignores the intro paragraph before the first heading). One entry uses a
    // multi-word/accented Perícia label ("Empatia c/ Animais") to make sure the label lookup
    // isn't limited to single, unaccented words.
    private const string Markdown = """
        Texto introdutório que não pertence a nenhum Histórico.

        Cada historico concede +6 em uma perícia e +3 em outra.

        ---

        # 1. Estudo Acadêmico

        O personagem passou anos cercado por livros, mestres e estudos.

        +6 Arcano
        +3 Biblioteca

        ---

        # 19. Afinidade Animal

        O personagem sempre teve facilidade para se aproximar de animais.

        +6 Empatia c/ Animais
        +3 Sobrevivência
        """;

    [Fact]
    public void Parse_extracts_Nome_Descricao_and_the_two_bonified_Pericias()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        var estudoAcademico = result.Should().ContainSingle(h => h.Nome == "Estudo Acadêmico").Subject;
        estudoAcademico.Descricao.Should().Be("O personagem passou anos cercado por livros, mestres e estudos.");
        estudoAcademico.PericiaMaisSeis.Should().Be(Pericia.Arcano);
        estudoAcademico.PericiaMaisTres.Should().Be(Pericia.Biblioteca);
    }

    [Fact]
    public void Parse_strips_the_leading_number_from_the_heading_to_produce_Nome()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        result.Should().NotContain(h => h.Nome.StartsWith("1.") || h.Nome.StartsWith("19."));
    }

    [Fact]
    public void Parse_resolves_a_multi_word_accented_Pericia_label()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        var afinidadeAnimal = result.Should().ContainSingle(h => h.Nome == "Afinidade Animal").Subject;
        afinidadeAnimal.PericiaMaisSeis.Should().Be(Pericia.EmpatiaComAnimais);
        afinidadeAnimal.PericiaMaisTres.Should().Be(Pericia.Sobrevivencia);
    }

    [Fact]
    public void Parse_ignores_the_intro_prose_before_the_first_heading()
    {
        var result = HistoricoSeedParser.Parse(Markdown);

        result.Should().HaveCount(2);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~HistoricoSeedParserTests"`
Expected: FAIL to compile — `HistoricoSeedParser`/`HistoricoSeed` do not exist yet.

- [ ] **Step 3: Create the HistoricoSeed record**

Create `src/RuinaRPG.Domain/Rules/HistoricoSeed.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.Rules;

public sealed record HistoricoSeed(string Nome, string Descricao, Pericia PericiaMaisSeis, Pericia PericiaMaisTres);
```

- [ ] **Step 4: Implement HistoricoSeedParser**

Create `src/RuinaRPG.Domain/Rules/HistoricoSeedParser.cs`:

```csharp
using System.Text.RegularExpressions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static partial class HistoricoSeedParser
{
    // Historico.md spells out each Perícia's proper Portuguese label (e.g. "Empatia c/ Animais"),
    // not the Pericia enum's unaccented member name — same label set as
    // RuinaRPG.Client.Shared.PericiaDisplay, inverted. Duplicated rather than shared because Domain
    // cannot reference the Client project.
    private static readonly Dictionary<string, Pericia> PericiaPorRotulo = new()
    {
        ["Acrobacia"] = Pericia.Acrobacia,
        ["Alquimia"] = Pericia.Alquimia,
        ["Arcano"] = Pericia.Arcano,
        ["Armadilhas"] = Pericia.Armadilhas,
        ["Armas Brancas"] = Pericia.ArmasBrancas,
        ["Artefatos Mágicos"] = Pericia.ArtefatosMagicos,
        ["Artístico"] = Pericia.Artistico,
        ["Atletismo"] = Pericia.Atletismo,
        ["Avaliação"] = Pericia.Avaliacao,
        ["Biblioteca"] = Pericia.Biblioteca,
        ["Brigar"] = Pericia.Brigar,
        ["Condução"] = Pericia.Conducao,
        ["Conhecimentos"] = Pericia.Conhecimentos,
        ["Crime"] = Pericia.Crime,
        ["Empatia c/ Animais"] = Pericia.EmpatiaComAnimais,
        ["Enganação"] = Pericia.Enganacao,
        ["Força de Vontade"] = Pericia.ForcaDeVontade,
        ["Fortitude"] = Pericia.Fortitude,
        ["Furtividade"] = Pericia.Furtividade,
        ["Herborismo"] = Pericia.Herborismo,
        ["Intimidação"] = Pericia.Intimidacao,
        ["Intuição"] = Pericia.Intuicao,
        ["Investigação"] = Pericia.Investigacao,
        ["Lábia"] = Pericia.Labia,
        ["Liderança"] = Pericia.Lideranca,
        ["Linguística"] = Pericia.Linguistica,
        ["Medicina"] = Pericia.Medicina,
        ["Navegação"] = Pericia.Navegacao,
        ["Ocultismo"] = Pericia.Ocultismo,
        ["Ofício"] = Pericia.Oficio,
        ["Percepção"] = Pericia.Percepcao,
        ["Pontaria"] = Pericia.Pontaria,
        ["Prontidão"] = Pericia.Prontidao,
        ["Reflexos"] = Pericia.Reflexos,
        ["Religião"] = Pericia.Religiao,
        ["Saquear"] = Pericia.Saquear,
        ["Sedução"] = Pericia.Seducao,
        ["Senso Comum"] = Pericia.SensoComum,
        ["Sobrevivência"] = Pericia.Sobrevivencia,
    };

    public static IReadOnlyList<HistoricoSeed> Parse(string markdown)
    {
        var sections = MarkdownSectionParser.Parse(markdown);
        var results = new List<HistoricoSeed>();

        foreach (var entry in sections)
        {
            var match = BonusLinesRegex().Match(entry.Body);
            if (!match.Success)
                continue; // no parseable "+6 X"/"+3 Y" pair — skip rather than seed a broken row

            if (!PericiaPorRotulo.TryGetValue(match.Groups[1].Value.Trim(), out var periciaMaisSeis))
                continue; // unrecognized Perícia label — skip rather than crash the whole seed
            if (!PericiaPorRotulo.TryGetValue(match.Groups[2].Value.Trim(), out var periciaMaisTres))
                continue;

            var nome = NumberPrefixRegex().Replace(entry.Title, "").Trim();
            var descricao = entry.Body[..match.Index].Trim();
            results.Add(new HistoricoSeed(nome, descricao, periciaMaisSeis, periciaMaisTres));
        }

        return results;
    }

    // "1. Estudo Acadêmico" -> "Estudo Acadêmico" — the leading "N. " is presentation order in the
    // source doc, not part of the catalog's Nome.
    [GeneratedRegex(@"^\d+\.\s*")]
    private static partial Regex NumberPrefixRegex();

    // Matches the "+6 <Perícia>" / "+3 <Perícia>" pair on two consecutive lines anywhere in the
    // entry's body (there is trailing "---" text after it in the raw body, which this pattern
    // simply never reaches).
    [GeneratedRegex(@"^\+6\s+(.+)$\r?\n^\+3\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex BonusLinesRegex();
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~HistoricoSeedParserTests"`
Expected: PASS (4/4).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/HistoricoSeed.cs src/RuinaRPG.Domain/Rules/HistoricoSeedParser.cs tests/RuinaRPG.Tests.Unit/Rules/HistoricoSeedParserTests.cs
git commit -m "feat: parser de Historico.md para o seed do catálogo de Históricos"
```

---

### Task 3: Domain — HistoricoBonusCalculator + SkillFormulas.Modificador signature change

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/HistoricoBonusCalculator.cs`
- Modify: `src/RuinaRPG.Domain/CharacterSheets/SkillFormulas.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs:48`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSkillsController.cs:40`
- Modify: `src/RuinaRPG.Api/Controllers/CreatureSkillsController.cs:41`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs:365` (`SubAttributes`' `BrutoOf`)
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs:324` (`SubAttributes`' `BrutoOf`)
- Modify: `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs:333` (`SubAttributes`' `BrutoOf`)
- Modify: `src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs:119` (`ComputeTotalAsync`)
- Modify: `src/RuinaRPG.Api/Controllers/NpcMasteriesController.cs:86` (`ComputeTotalAsync`)
- Modify: `src/RuinaRPG.Api/Controllers/CreatureMasteriesController.cs:92` (`ComputeTotalAsync`)
- Modify: `tests/RuinaRPG.Tests.Unit/CharacterSheets/SkillFormulasTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/HistoricoBonusCalculatorTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.CharacterSheets.Pericia` (existing enum).
- Produces: `HistoricoBonusCalculator.For(Pericia pericia, Pericia? periciaMaisSeis, Pericia? periciaMaisTres) -> int` (returns 6/3/0) and `SkillFormulas.Modificador(int gasto, int historicoBonus) -> int`, both consumed by Tasks 8-10. Every one of the 9 call sites listed above passes a literal `0` for now — Tasks 8-10 replace the `0` with a real computed value at the Character/Npc call sites only (6 of the 9); the 3 Creature call sites keep the literal `0` forever, since Ficha de Criatura has no Histórico field.

This task's job is a pure, behavior-preserving refactor: after this task, every existing test still passes because every call site's `historicoBonus` is `0`, identical to today's implicit behavior.

- [ ] **Step 1: Write the failing test for HistoricoBonusCalculator**

Create `tests/RuinaRPG.Tests.Unit/CharacterSheets/HistoricoBonusCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class HistoricoBonusCalculatorTests
{
    [Fact]
    public void For_returns_6_when_the_Pericia_matches_PericiaMaisSeis()
    {
        HistoricoBonusCalculator.For(Pericia.Arcano, Pericia.Arcano, Pericia.Biblioteca).Should().Be(6);
    }

    [Fact]
    public void For_returns_3_when_the_Pericia_matches_PericiaMaisTres()
    {
        HistoricoBonusCalculator.For(Pericia.Biblioteca, Pericia.Arcano, Pericia.Biblioteca).Should().Be(3);
    }

    [Fact]
    public void For_returns_0_when_the_Pericia_matches_neither()
    {
        HistoricoBonusCalculator.For(Pericia.Fortitude, Pericia.Arcano, Pericia.Biblioteca).Should().Be(0);
    }

    [Fact]
    public void For_returns_0_when_there_is_no_Historico_chosen()
    {
        HistoricoBonusCalculator.For(Pericia.Arcano, null, null).Should().Be(0);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~HistoricoBonusCalculatorTests"`
Expected: FAIL to compile — `HistoricoBonusCalculator` does not exist yet.

- [ ] **Step 3: Implement HistoricoBonusCalculator**

Create `src/RuinaRPG.Domain/CharacterSheets/HistoricoBonusCalculator.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Takes the two nullable Pericia fields directly (not the Historico entity itself, which lives in
/// RuinaRPG.Infrastructure — Domain cannot reference it) — both null means no Histórico is chosen.
/// </summary>
public static class HistoricoBonusCalculator
{
    public static int For(Pericia pericia, Pericia? periciaMaisSeis, Pericia? periciaMaisTres) =>
        pericia == periciaMaisSeis ? 6 : pericia == periciaMaisTres ? 3 : 0;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~HistoricoBonusCalculatorTests"`
Expected: PASS (4/4).

- [ ] **Step 5: Update the existing SkillFormulas test for the new signature, run it, watch it fail**

In `tests/RuinaRPG.Tests.Unit/CharacterSheets/SkillFormulasTests.cs`, replace the `Modificador_divides_gasto_by_3_rounded_down` test:

```csharp
    [Fact]
    public void Modificador_divides_gasto_by_3_rounded_down()
    {
        // "Modificador = Gasto ÷ 3 (arredondado para baixo)" — Sistema Básico §2.
        SkillFormulas.Modificador(gasto: 8, historicoBonus: 0).Should().Be(2);
        SkillFormulas.Modificador(gasto: 9, historicoBonus: 0).Should().Be(3);
        SkillFormulas.Modificador(gasto: 0, historicoBonus: 0).Should().Be(0);
    }

    [Fact]
    public void Modificador_adds_the_Historico_bonus_on_top_of_gasto_divided_by_3()
    {
        SkillFormulas.Modificador(gasto: 9, historicoBonus: 6).Should().Be(9); // 3 + 6
    }
```

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SkillFormulasTests"`
Expected: FAIL to compile — `Modificador` still takes one argument.

- [ ] **Step 6: Change SkillFormulas.Modificador's signature**

In `src/RuinaRPG.Domain/CharacterSheets/SkillFormulas.cs`, replace:

```csharp
    public static int Modificador(int gasto) => gasto / 3;
```

with:

```csharp
    public static int Modificador(int gasto, int historicoBonus) => gasto / 3 + historicoBonus;
```

- [ ] **Step 7: Run the Domain unit tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SkillFormulasTests"`
Expected: PASS (4/4).

- [ ] **Step 8: Fix every existing call site to pass a literal 0**

This is a mechanical, behavior-preserving change at exactly 9 locations — replace `SkillFormulas.Modificador(<expr>)` with `SkillFormulas.Modificador(<expr>, 0)` in each:

`src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs:48`:
```csharp
                var modificador = SkillFormulas.Modificador(s.Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/NpcSkillsController.cs:40`:
```csharp
                var modificador = SkillFormulas.Modificador(s.Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/CreatureSkillsController.cs:41`:
```csharp
                var modificador = SkillFormulas.Modificador(s.Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs:365`:
```csharp
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/NpcSheetsController.cs:324`:
```csharp
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs:333`:
```csharp
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs:119`:
```csharp
        var bruto = SkillFormulas.Modificador(skill.Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/NpcMasteriesController.cs:86`:
```csharp
        var bruto = SkillFormulas.Modificador(skill.Gasto, 0);
```

`src/RuinaRPG.Api/Controllers/CreatureMasteriesController.cs:92`:
```csharp
        var bruto = SkillFormulas.Modificador(skill.Gasto, 0);
```

- [ ] **Step 9: Build and run the full test suite to confirm nothing broke**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: all pass (same count as before this task, plus the 4 new `HistoricoBonusCalculatorTests` and the 1 new `SkillFormulasTests` case).

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SkillsControllerTests|FullyQualifiedName~MasteriesControllerTests|FullyQualifiedName~SubAttributes"`
Expected: all pass unchanged (this task must not change any observable behavior).

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/HistoricoBonusCalculator.cs src/RuinaRPG.Domain/CharacterSheets/SkillFormulas.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/HistoricoBonusCalculatorTests.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/SkillFormulasTests.cs src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs src/RuinaRPG.Api/Controllers/NpcSkillsController.cs src/RuinaRPG.Api/Controllers/CreatureSkillsController.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs src/RuinaRPG.Api/Controllers/NpcMasteriesController.cs src/RuinaRPG.Api/Controllers/CreatureMasteriesController.cs
git commit -m "refactor: SkillFormulas.Modificador ganha um parâmetro de bônus de Histórico (0 em todo lugar por enquanto)"
```

---

### Task 4: Infrastructure — Historico entity, migration, seeder, Program.cs wiring

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/Historico.cs`
- Create: `src/RuinaRPG.Infrastructure/Rules/HistoricoSeeder.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs`
- Modify: `src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs`
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj`
- Modify: `src/RuinaRPG.Api/Program.cs`
- Create (generated): a new EF Core migration under `src/RuinaRPG.Infrastructure/Persistence/Migrations/`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/HistoricoSeederTests.cs`

**Interfaces:**
- Consumes: `HistoricoSeedParser.Parse` and `HistoricoSeed` from Task 2.
- Produces: `RuinaRpgDbContext.Historicos` (`DbSet<Historico>`), `HistoricoSeeder.SeedAsync(RuinaRpgDbContext db, string historicoMarkdown) -> Task<(int Inserted, int Updated)>`, `CharacterSheet.HistoricoId`/`NpcSheet.HistoricoId` (`Guid?`) — all consumed by Tasks 5, 6, 7, 8, 9, 10, 11.

- [ ] **Step 1: Add the embedded resource for Historico.md**

In `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj`, find the line:

```xml
    <EmbeddedResource Include="../../Docs/Sistema RPG/As estrelas alkerianas.md" LogicalName="Estrelas Alkerianas.md" />
```

Add right after it:

```xml
    <EmbeddedResource Include="../../Docs/Sistema RPG/Historico.md" LogicalName="Historico.md" />
```

- [ ] **Step 2: Write the failing seeder test**

Create `tests/RuinaRPG.Tests.Unit/Rules/HistoricoSeederTests.cs`. This is a pure-Domain-facing test of `HistoricoSeedParser` composed with an in-memory list (no DB) to lock down the exact insertion shape `HistoricoSeeder` must produce from the real `Historico.md` — the actual DB-backed seeding behavior (skip `IsCustomized`, no resurrect, etc.) is covered by `HistoricosControllerTests` in Task 6, which has a real Postgres to assert against.

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class HistoricoSeederTests
{
    // The real, current Docs/Sistema RPG/Historico.md — read once here the same way
    // RulesDataProvider.ReadResource would at runtime, via the embedded resource. If this test
    // fails after editing Historico.md, the doc's heading/bonus-line shape broke the parser.
    private static string RealHistoricoMarkdown() =>
        System.IO.File.ReadAllText(FindRepoRoot() + "/Docs/Sistema RPG/Historico.md");

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(dir, "RuinaRPG.sln")))
            dir = System.IO.Directory.GetParent(dir)!.FullName;
        return dir;
    }

    [Fact]
    public void The_real_Historico_md_parses_into_exactly_26_entries()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        result.Should().HaveCount(26);
    }

    [Fact]
    public void The_real_Historico_md_has_no_duplicate_Nome()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        result.Select(h => h.Nome).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_real_Historico_md_never_bonifies_the_same_Pericia_twice_in_one_entry()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        result.Should().OnlyContain(h => h.PericiaMaisSeis != h.PericiaMaisTres);
    }

    [Fact]
    public void Estudo_Academico_parses_with_its_real_fields()
    {
        var result = HistoricoSeedParser.Parse(RealHistoricoMarkdown());

        var estudoAcademico = result.Should().ContainSingle(h => h.Nome == "Estudo Acadêmico").Subject;
        estudoAcademico.PericiaMaisSeis.Should().Be(Pericia.Arcano);
        estudoAcademico.PericiaMaisTres.Should().Be(Pericia.Biblioteca);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~HistoricoSeederTests"`
Expected: FAIL — either a file-not-found (if `Historico.md` isn't in the shape described in Global Constraints) or a count mismatch if the parser doesn't yet handle the real doc correctly. If it's a genuine parser bug against the real doc (not just "file missing"), fix `HistoricoSeedParser` from Task 2 before continuing — do not change `Historico.md`'s content to work around the parser.

- [ ] **Step 4: Make the test pass**

If Step 3 failed only because `Historico.md` doesn't yet have the reformatted heading shape described in Global Constraints, recreate it now with 26 `# N. Nome` sections (see Task 2's test fixture for the exact shape each entry must have — heading, description paragraph, `+6 X`/`+3 Y` lines, `---` separators). If it already has that shape and the test still fails, the failure is a real parser bug — fix `src/RuinaRPG.Domain/Rules/HistoricoSeedParser.cs` from Task 2.

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~HistoricoSeederTests"`
Expected: PASS (4/4).

- [ ] **Step 5: Create the Historico entity**

Create `src/RuinaRPG.Infrastructure/Rules/Historico.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.Rules;

public class Historico
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public Pericia PericiaMaisSeis { get; set; }
    public Pericia PericiaMaisTres { get; set; }

    // Set by HistoricosController's Create/Update — once true, HistoricoSeeder never touches this
    // row again, so a manual edit always wins over whatever Historico.md says. Mirrors Trait.
    public bool IsCustomized { get; set; }

    // Soft delete: hidden from every read path (an explicit "!IsDeleted" filter per query), but the
    // row itself stays so HistoricoSeeder still recognizes it as "already present" and never
    // resurrects it. Mirrors Trait.
    public bool IsDeleted { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

- [ ] **Step 6: Implement HistoricoSeeder**

Create `src/RuinaRPG.Infrastructure/Rules/HistoricoSeeder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public static class HistoricoSeeder
{
    /// <summary>
    /// Historico.md is the source of truth, re-read on every startup/migrate: a Histórico missing
    /// from the table (matched by Nome alone) is inserted. Unlike TraitSeeder, an existing
    /// non-customized row is never resynced field-by-field — there's no cheap metadata field on
    /// Historico worth resyncing the way TraitSeeder resyncs RequerEspecificacao, so presence is
    /// the only thing checked.
    ///
    /// Two exceptions, both mirroring TraitSeeder (Rules Audit System, see HistoricosController):
    /// - A row with IsCustomized = true is skipped entirely.
    /// - The match-key lookup includes soft-deleted rows, so a deliberately deleted Histórico is
    ///   recognized as "already present" and never reinserted.
    /// </summary>
    public static async Task<(int Inserted, int Updated)> SeedAsync(RuinaRpgDbContext db, string historicoMarkdown)
    {
        var parsed = HistoricoSeedParser.Parse(historicoMarkdown);
        var existingNomes = await db.Historicos.Select(h => h.Nome).ToListAsync();
        var existingSet = existingNomes.ToHashSet();

        var toInsert = new List<Historico>();
        foreach (var seed in parsed)
        {
            if (existingSet.Contains(seed.Nome))
                continue;

            toInsert.Add(new Historico
            {
                Id = Guid.NewGuid(),
                Nome = seed.Nome,
                Descricao = seed.Descricao,
                PericiaMaisSeis = seed.PericiaMaisSeis,
                PericiaMaisTres = seed.PericiaMaisTres,
            });
        }

        if (toInsert.Count == 0)
            return (0, 0);

        db.Historicos.AddRange(toInsert);
        await db.SaveChangesAsync();
        return (toInsert.Count, 0);
    }
}
```

- [ ] **Step 7: Add the DbSet and FK configuration to RuinaRpgDbContext**

In `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`, find:

```csharp
    public DbSet<Trait> Traits => Set<Trait>();
```

Add right after it:

```csharp
    public DbSet<Historico> Historicos => Set<Historico>();
```

Find the `builder.Entity<CharacterSheet>(entity => { ... });` block (has `entity.HasOne<Image>().WithMany().HasForeignKey(s => s.ImageId).OnDelete(DeleteBehavior.SetNull);` as its last line) and add a new line right before the block's closing `});`:

```csharp
            entity.HasOne<Historico>()
                .WithMany()
                .HasForeignKey(s => s.HistoricoId)
                .OnDelete(DeleteBehavior.SetNull);
```

Find the `builder.Entity<NpcSheet>(entity => { ... });` block (has `entity.HasOne<Image>().WithMany().HasForeignKey(s => s.ImageId).OnDelete(DeleteBehavior.SetNull);` as its last line) and add, in the same one-line-per-relationship style already used there:

```csharp
            entity.HasOne<Historico>().WithMany().HasForeignKey(s => s.HistoricoId).OnDelete(DeleteBehavior.SetNull);
```

- [ ] **Step 8: Add HistoricoId to the CharacterSheet and NpcSheet entities**

In `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs`, find:

```csharp
    public string? Propriedade { get; set; }
    public Estrela? Estrela { get; set; }
```

Replace with:

```csharp
    public string? Propriedade { get; set; }
    public Estrela? Estrela { get; set; }
    public Guid? HistoricoId { get; set; }
```

Do the exact same replacement in `src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs`.

- [ ] **Step 9: Build (to catch typos before generating the migration)**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 10: Generate the EF Core migration**

Run:
```bash
dotnet ef migrations add AddHistoricosAndSheetHistoricoId --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Open the generated `..._AddHistoricosAndSheetHistoricoId.cs` and confirm it creates the `Historicos` table (Id, Nome, Descricao, PericiaMaisSeis int, PericiaMaisTres int, IsCustomized bool, IsDeleted bool, UpdatedByUserId nullable uuid, UpdatedAt nullable timestamp) and adds nullable `HistoricoId` uuid columns + FK constraints + indexes on both `CharacterSheets` and `NpcSheets` — if anything is missing or wrong, fix Steps 5-8 and regenerate (`dotnet ef migrations remove --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api` first).

- [ ] **Step 11: Wire HistoricoSeeder into Program.cs, at both existing TraitSeeder.SeedAsync call sites**

In `src/RuinaRPG.Api/Program.cs`, find (in the `--migrate` CLI path):

```csharp
    var migrateCaracteristicasMarkdown = RulesDataProvider.ReadResource("Caracteristicas.md");
    var migrateSeedResult = await TraitSeeder.SeedAsync(migrateDb, migrateCaracteristicasMarkdown);
    app.Logger.LogInformation("Trait seed: {InsertedCount} new row(s) inserted, {UpdatedCount} existing row(s) updated", migrateSeedResult.Inserted, migrateSeedResult.Updated);
```

Add right after it:

```csharp

    var migrateHistoricoMarkdown = RulesDataProvider.ReadResource("Historico.md");
    var migrateHistoricoSeedResult = await HistoricoSeeder.SeedAsync(migrateDb, migrateHistoricoMarkdown);
    app.Logger.LogInformation("Historico seed: {InsertedCount} new row(s) inserted", migrateHistoricoSeedResult.Inserted);
```

Find the second, normal-startup call site:

```csharp
    var caracteristicasMarkdown = RulesDataProvider.ReadResource("Caracteristicas.md");
    var seedResult = await TraitSeeder.SeedAsync(db, caracteristicasMarkdown);
    app.Logger.LogInformation("Trait seed: {InsertedCount} new row(s) inserted, {UpdatedCount} existing row(s) updated", seedResult.Inserted, seedResult.Updated);
```

Add right after it:

```csharp

    var historicoMarkdown = RulesDataProvider.ReadResource("Historico.md");
    var historicoSeedResult = await HistoricoSeeder.SeedAsync(db, historicoMarkdown);
    app.Logger.LogInformation("Historico seed: {InsertedCount} new row(s) inserted", historicoSeedResult.Inserted);
```

- [ ] **Step 12: Build and run the full unit test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: all pass, including the new `HistoricoSeederTests` (4/4).

- [ ] **Step 13: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/Historico.cs src/RuinaRPG.Infrastructure/Rules/HistoricoSeeder.cs src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj src/RuinaRPG.Api/Program.cs src/RuinaRPG.Infrastructure/Persistence/Migrations/ tests/RuinaRPG.Tests.Unit/Rules/HistoricoSeederTests.cs
git commit -m "feat: catálogo de Históricos (entidade, migration, seed) e HistoricoId nas fichas"
```

---

### Task 5: Contracts — Historico DTOs + HistoricoId on the sheet response/request records

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/HistoricoResponse.cs`
- Create: `src/RuinaRPG.Contracts/Rules/CreateHistoricoRequest.cs`
- Create: `src/RuinaRPG.Contracts/Rules/UpdateHistoricoRequest.cs`
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs`
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs`
- Modify: `src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs`
- Modify: `src/RuinaRPG.Contracts/NpcSheets/UpdateNpcSheetRequest.cs`
- Modify: every existing call site that positionally constructs `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest`/`CharacterSheetResponse`/`NpcSheetResponse` (found via grep in Step 1 below — do not skip this, new call sites may have appeared since this plan was written)

**Interfaces:**
- Consumes: nothing new.
- Produces: `HistoricoResponse(string Id, string Nome, string Descricao, string PericiaMaisSeis, string PericiaMaisTres, bool IsCustomized)`, `CreateHistoricoRequest(string Nome, string Descricao, string PericiaMaisSeis, string PericiaMaisTres)`, `UpdateHistoricoRequest(string Nome, string Descricao, string PericiaMaisSeis, string PericiaMaisTres)` — consumed by Task 6. `CharacterSheetResponse`/`NpcSheetResponse` gain a trailing `string? HistoricoId`; `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest` gain a trailing `string? HistoricoId` — consumed by Task 7 (and the client, Task 12).

This task has no independent test of its own — it is pure data-shape plumbing whose correctness is proven by Task 6/7's tests and by the build passing. Follow it exactly; do not skip the grep in Step 1.

- [ ] **Step 1: Find every positional construction site of the 4 sheet records**

Run:
```bash
cd /home/pro/RuinaRPG
grep -rn "UpdateCharacterSheetRequest(\|UpdateNpcSheetRequest(\|CharacterSheetResponse(\|NpcSheetResponse(" src tests --include="*.cs" --include="*.razor"
```

Note every file this prints (both `new TypeName(` and target-typed `=> new(` forms — check the line above a bare `new(` to see which record it's constructing). Every one of them needs its final positional argument list updated in Step 6 below. As of this plan's writing, that list is: `CharacterSheetsController.cs` (ToResponseAsync), `NpcSheetsController.cs` (ToResponseAsync), `FichaDePersonagem.razor`, `FichaDeNpc.razor`, `CharacterSheetsControllerTests.cs` (`ValidUpdate`), `NpcSheetsControllerTests.cs` (`ValidUpdate`), `VarianteSolarAloraControllerTests.cs` (`CharacterUpdate`/`NpcUpdate`), `EncounterHubTests.cs` (`ValidNpcUpdate`/`ValidCharacterUpdate`), `EncounterParticipantsControllerTests.cs` (`ValidNpcUpdate`/`ValidCharacterUpdate`), `CampaignPlayerViewControllerTests.cs` (inline `UpdateCharacterSheetRequest` + `NpcUpdateWithNome`), `CampaignGrantsControllerTests.cs` (inline `UpdateNpcSheetRequest`), `CharacterAffinitiesControllerTests.cs` (`setVocacao`/`updateSheet`), `NpcAffinitiesControllerTests.cs` (`setVocacao`/`updateSheet`), `CampaignAttachmentsControllerTests.cs` (`NpcUpdateWithImage`), `CharacterRacialTraitsControllerTests.cs` (`ValidUpdate`), `NpcRacialTraitsControllerTests.cs` (`ValidUpdate`). If the grep finds any file not in this list, it's new since this plan was written — treat it the same way in Step 6.

- [ ] **Step 2: Create the Historico contracts**

Create `src/RuinaRPG.Contracts/Rules/HistoricoResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record HistoricoResponse(string Id, string Nome, string Descricao, string PericiaMaisSeis, string PericiaMaisTres, bool IsCustomized);
```

Create `src/RuinaRPG.Contracts/Rules/CreateHistoricoRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record CreateHistoricoRequest(string Nome, string Descricao, string PericiaMaisSeis, string PericiaMaisTres);
```

Create `src/RuinaRPG.Contracts/Rules/UpdateHistoricoRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record UpdateHistoricoRequest(string Nome, string Descricao, string PericiaMaisSeis, string PericiaMaisTres);
```

- [ ] **Step 3: Append HistoricoId to CharacterSheetResponse**

In `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs`, replace the final line:

```csharp
    string? Estrela,
    int SinaAtual);
```

with:

```csharp
    string? Estrela,
    int SinaAtual,
    string? HistoricoId);
```

- [ ] **Step 4: Append HistoricoId to UpdateCharacterSheetRequest**

In `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs`, replace the final line:

```csharp
    string? Estrela,
    int SinaAtual);
```

with:

```csharp
    string? Estrela,
    int SinaAtual,
    string? HistoricoId);
```

- [ ] **Step 5: Append HistoricoId to NpcSheetResponse and UpdateNpcSheetRequest**

In `src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs`, replace the final line:

```csharp
    string? Estrela,
    int SinaAtual);
```

with:

```csharp
    string? Estrela,
    int SinaAtual,
    string? HistoricoId);
```

In `src/RuinaRPG.Contracts/NpcSheets/UpdateNpcSheetRequest.cs`, replace the final line:

```csharp
    string? Estrela,
    int SinaAtual);
```

with:

```csharp
    string? Estrela,
    int SinaAtual,
    string? HistoricoId);
```

- [ ] **Step 6: Fix every call site found in Step 1**

Build first to get the compiler's exact list of errors — this is faster and more reliable than eyeballing the grep output for every call site:

Run: `dotnet build`

For every `CS7036`/`CS1729` ("no argument given that corresponds to...") error, open the file at the reported line and append one more trailing argument to the constructor call:
- For every `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest`/`CharacterSheetResponse`/`NpcSheetResponse` construction in **test** files: append `null` (no Histórico set, matching how these test helpers already default `Estrela` to `null`).
- For `CharacterSheetsController.cs`'s `ToResponseAsync`, append `s.HistoricoId?.ToString()` after `s.SinaAtual`.
- For `NpcSheetsController.cs`'s `ToResponseAsync`, append `s.HistoricoId?.ToString()` after `s.SinaAtual`.
- For `FichaDePersonagem.razor`'s `UpdateCharacterSheetRequest` construction, append `_form.HistoricoId` (Task 12 adds this field to `SheetFormModel` — for now, since Task 12 hasn't run yet, append `null` instead, and Task 12 will change it to `_form.HistoricoId` when it adds the field).
- For `FichaDeNpc.razor`'s `UpdateNpcSheetRequest` construction, same: append `null` for now.

Repeat `dotnet build` until it succeeds with 0 errors.

- [ ] **Step 7: Build and run the full test suite to confirm nothing broke**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: all pass, unchanged count.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Contracts/Rules/HistoricoResponse.cs src/RuinaRPG.Contracts/Rules/CreateHistoricoRequest.cs src/RuinaRPG.Contracts/Rules/UpdateHistoricoRequest.cs src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs src/RuinaRPG.Contracts/NpcSheets/UpdateNpcSheetRequest.cs
git add -u
git commit -m "feat: contratos de Histórico e HistoricoId nos contratos de ficha"
```

(`git add -u` picks up every already-tracked file this task modified across the codebase — the exact list from Step 1/6 — without needing to enumerate them all by hand here.)

---

### Task 6: API — HistoricosController CRUD

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/HistoricosController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/HistoricosControllerTests.cs`

**Interfaces:**
- Consumes: `Historico` entity, `RuinaRpgDbContext.Historicos` (Task 4); `HistoricoResponse`/`CreateHistoricoRequest`/`UpdateHistoricoRequest` (Task 5).
- Produces: `GET/POST /api/historicos`, `PUT/DELETE /api/historicos/{id}` — consumed by Task 12 (client dropdown) and Task 13 (audit page).

- [ ] **Step 1: Write the failing tests**

Create `tests/RuinaRPG.Tests.Integration/Controllers/HistoricosControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class HistoricosControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public HistoricosControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/historicos");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_with_no_filter_returns_a_real_Id_for_a_seeded_Historico()
    {
        var token = await RegisterGmAndGetTokenAsync("HistGm1", "hist1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        var estudoAcademico = body!.Should().ContainSingle(h => h.Nome == "Estudo Acadêmico").Subject;
        Guid.TryParse(estudoAcademico.Id, out _).Should().BeTrue();
        estudoAcademico.PericiaMaisSeis.Should().Be("Arcano");
        estudoAcademico.PericiaMaisTres.Should().Be("Biblioteca");
    }

    [Fact]
    public async Task CreateHistorico_by_a_Rules_Auditor_persists_it_as_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm1", "histcrudgm1@teste.com");
        await GrantRulesAuditorAsync("histcrudgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Testado a Ferro", "Descrição de teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<HistoricoResponse>();
        body!.Nome.Should().Be("Testado a Ferro");
        body.IsCustomized.Should().BeTrue();

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Should().Contain(h => h.Nome == "Testado a Ferro");
    }

    [Fact]
    public async Task CreateHistorico_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm2", "histcrudgm2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Não Deveria Salvar", "Teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateHistorico_with_the_same_Pericia_twice_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm3", "histcrudgm3@teste.com");
        await GrantRulesAuditorAsync("histcrudgm3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Perícia Repetida", "Teste.", "Atletismo", "Atletismo")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateHistorico_with_an_invalid_Pericia_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm4", "histcrudgm4@teste.com");
        await GrantRulesAuditorAsync("histcrudgm4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Perícia Inválida", "Teste.", "NaoExiste", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateHistorico_by_a_Rules_Auditor_persists_the_change_and_marks_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm5", "histcrudgm5@teste.com");
        await GrantRulesAuditorAsync("histcrudgm5@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Editar", "Antes.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created!.Id}", gmToken,
            new UpdateHistoricoRequest("Para Editar", "Depois.", "Atletismo", "Acrobacia")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Single(h => h.Id == created.Id).Descricao.Should().Be("Depois.");
    }

    [Fact]
    public async Task UpdateHistorico_with_the_same_Pericia_twice_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm6", "histcrudgm6@teste.com");
        await GrantRulesAuditorAsync("histcrudgm6@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Editar Errado", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created!.Id}", gmToken,
            new UpdateHistoricoRequest("Para Editar Errado", "Teste.", "Atletismo", "Atletismo")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteHistorico_soft_deletes_and_it_no_longer_appears_in_List()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm7", "histcrudgm7@teste.com");
        await GrantRulesAuditorAsync("histcrudgm7@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Excluir", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Should().NotContain(h => h.Id == created.Id);
    }

    [Fact]
    public async Task DeleteHistorico_that_is_already_in_use_on_a_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm8", "histcrudgm8@teste.com");
        await GrantRulesAuditorAsync("histcrudgm8@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Em Uso", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "HistCrudPlayer8", "histcrudplayer8@teste.com");
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha Histórico", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        // Use the raw DB context to set HistoricoId directly — CharacterSheetsController.Update
        // doesn't accept/validate HistoricoId until Task 7 lands, and this test only needs the
        // reference to exist, not to exercise Update itself.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var sheet = await db.CharacterSheets.SingleAsync(s => s.Id == Guid.Parse(sheetId));
        sheet.HistoricoId = Guid.Parse(created!.Id);
        await db.SaveChangesAsync();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~HistoricosControllerTests"`
Expected: FAIL to compile — `HistoricosController` doesn't exist, `/api/historicos` has no route yet.

- [ ] **Step 3: Implement HistoricosController**

Create `src/RuinaRPG.Api/Controllers/HistoricosController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Historico.md's static, GM-independent catalog (seeded once, shared by every GM/Jogador) — no
/// owning GmId, same treatment as TraitsController. List (GET) stays open to any authenticated
/// caller. Create/Update/Delete are gated to the Rules Auditor (see Requisitos - Auditoria de
/// Regras), checked directly against the DB (ApplicationUser.IsRulesAuditor), not a JWT claim.
/// </summary>
[ApiController]
[Route("api/historicos")]
[Authorize]
public class HistoricosController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HistoricoResponse>>> List()
    {
        var historicos = await db.Historicos.Where(h => !h.IsDeleted).OrderBy(h => h.Nome).ToListAsync();
        return historicos.Select(ToResponse).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<HistoricoResponse>> Create(CreateHistoricoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<Pericia>(request.PericiaMaisSeis, out var periciaMaisSeis))
            return BadRequest("PericiaMaisSeis inválida.");
        if (!Enum.TryParse<Pericia>(request.PericiaMaisTres, out var periciaMaisTres))
            return BadRequest("PericiaMaisTres inválida.");
        if (periciaMaisSeis == periciaMaisTres)
            return BadRequest("PericiaMaisSeis e PericiaMaisTres não podem ser a mesma Perícia.");

        var historico = new Historico
        {
            Id = Guid.NewGuid(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            PericiaMaisSeis = periciaMaisSeis,
            PericiaMaisTres = periciaMaisTres,
            IsCustomized = true,
            UpdatedByUserId = CurrentUserId(),
            UpdatedAt = DateTime.UtcNow,
        };
        db.Historicos.Add(historico);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(historico));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateHistoricoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var historico = await db.Historicos.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (historico is null)
            return NotFound();

        if (!Enum.TryParse<Pericia>(request.PericiaMaisSeis, out var periciaMaisSeis))
            return BadRequest("PericiaMaisSeis inválida.");
        if (!Enum.TryParse<Pericia>(request.PericiaMaisTres, out var periciaMaisTres))
            return BadRequest("PericiaMaisTres inválida.");
        if (periciaMaisSeis == periciaMaisTres)
            return BadRequest("PericiaMaisSeis e PericiaMaisTres não podem ser a mesma Perícia.");

        historico.Nome = request.Nome;
        historico.Descricao = request.Descricao;
        historico.PericiaMaisSeis = periciaMaisSeis;
        historico.PericiaMaisTres = periciaMaisTres;
        historico.IsCustomized = true;
        historico.UpdatedByUserId = CurrentUserId();
        historico.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var historico = await db.Historicos.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (historico is null)
            return NotFound();

        var inUse = await db.CharacterSheets.AnyAsync(s => s.HistoricoId == id)
            || await db.NpcSheets.AnyAsync(s => s.HistoricoId == id);
        if (inUse)
            return Conflict("Este Histórico está em uso em pelo menos uma ficha e não pode ser excluído.");

        historico.IsDeleted = true;
        historico.UpdatedByUserId = CurrentUserId();
        historico.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static HistoricoResponse ToResponse(Historico h) =>
        new(h.Id.ToString(), h.Nome, h.Descricao, h.PericiaMaisSeis.ToString(), h.PericiaMaisTres.ToString(), h.IsCustomized);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~HistoricosControllerTests"`
Expected: PASS (10/10).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/HistoricosController.cs tests/RuinaRPG.Tests.Integration/Controllers/HistoricosControllerTests.cs
git commit -m "feat: CRUD de Históricos (api/historicos), gated ao Auditor de Regras"
```

---

### Task 7: API — HistoricoId on CharacterSheetsController/NpcSheetsController

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `HistoricosController` catalog (Task 6, via direct DB access `db.Historicos`), `CharacterSheet.HistoricoId`/`NpcSheet.HistoricoId` (Task 4), `UpdateCharacterSheetRequest.HistoricoId`/`CharacterSheetResponse.HistoricoId` (Task 5).
- Produces: `PUT /api/character-sheets/{id}` and `PUT /api/npc-sheets/{id}` now validate+persist `HistoricoId`; `GET` on both now returns it — consumed by Task 8/9/10 (which load `sheet.HistoricoId` to compute the bonus) and Task 12 (client).

- [ ] **Step 1: Write the failing tests for CharacterSheetsController**

In `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, add these 3 tests right after `Update_persists_Estrela_and_SinaAtual` (added by the previous Estrelas Alkerianas work):

```csharp
    [Fact]
    public async Task Update_with_a_garbage_HistoricoId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdHist1", "sheetupdhist1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdHist1", "sheetplayerupdhist1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Historico Invalido");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { HistoricoId = "not-a-guid" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_HistoricoId_that_does_not_exist_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdHist2", "sheetupdhist2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdHist2", "sheetplayerupdhist2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Historico Inexistente");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var invalid = ValidUpdate() with { HistoricoId = Guid.NewGuid().ToString() };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_persists_a_valid_HistoricoId()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdHist3", "sheetupdhist3@teste.com");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var historico = await db.Historicos.FirstAsync();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdHist3", "sheetplayerupdhist3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Historico Valido");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var update = ValidUpdate() with { HistoricoId = historico.Id.ToString() };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.HistoricoId.Should().Be(historico.Id.ToString());
    }
```

Also update `ValidUpdate()` itself to append a trailing `null`:

```csharp
    private static UpdateCharacterSheetRequest ValidUpdate() => new(
        null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, "Marcado pela Ruína",
        // 749 XP is one below Nível 6's threshold (750) — Nível is derived now, and reaching a
        // threshold exactly already counts as that Nível, so 749 keeps this at Nível 5.
        true, 749, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null, null, 0, null);
```

(This is the same line Task 5 Step 6 already changed from ending `..., null, 0);` to `..., null, 0, null);` — if Task 5 already appended a trailing `null` here, this step is a no-op; just confirm it reads exactly that way before moving on.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests.Update_with_a_garbage_HistoricoId_returns_400|FullyQualifiedName~CharacterSheetsControllerTests.Update_with_a_HistoricoId_that_does_not_exist_returns_400|FullyQualifiedName~CharacterSheetsControllerTests.Update_persists_a_valid_HistoricoId"`
Expected: FAIL — either a compile error (`HistoricoId` init-only member not found on the `with` expression, if Task 5 wasn't applied) or, once it compiles, the garbage/nonexistent-id tests return 204 instead of 400 (nothing validates `HistoricoId` yet), and the valid-id test's `GET` returns `HistoricoId: null` instead of the real id.

- [ ] **Step 3: Wire HistoricoId into CharacterSheetsController.Update**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, find:

```csharp
        if (!TryParseEnum<Estrela>(request.Estrela, out var estrela))
            return BadRequest("Estrela inválida.");
        if (request.SinaAtual is < 0 or > 3)
            return BadRequest("SinaAtual deve estar entre 0 e 3.");
```

Add right after it:

```csharp
        Guid? historicoId = null;
        if (!string.IsNullOrWhiteSpace(request.HistoricoId))
        {
            if (!Guid.TryParse(request.HistoricoId, out var parsedHistoricoId))
                return BadRequest("HistoricoId inválido.");
            if (!await db.Historicos.AnyAsync(h => h.Id == parsedHistoricoId && !h.IsDeleted))
                return BadRequest("Histórico não encontrado.");
            historicoId = parsedHistoricoId;
        }
```

Find:

```csharp
        sheet.Estrela = estrela;
        // Nível is a pure function of Experiência Atual now (1.b, "Para o próximo") — no more
```

Replace with:

```csharp
        sheet.Estrela = estrela;
        sheet.HistoricoId = historicoId;
        // Nível is a pure function of Experiência Atual now (1.b, "Para o próximo") — no more
```

Find, in `ToResponseAsync`:

```csharp
            s.PontosDeIgnicaoBonusManual, s.PontosDePericiaBonusCritico, s.ImageId?.ToString(), s.ArcaRolada,
            s.Estrela?.ToString(), s.SinaAtual);
```

Replace with:

```csharp
            s.PontosDeIgnicaoBonusManual, s.PontosDePericiaBonusCritico, s.ImageId?.ToString(), s.ArcaRolada,
            s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString());
```

- [ ] **Step 4: Run the CharacterSheetsController tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests"`
Expected: PASS, including the 3 new tests.

- [ ] **Step 5: Repeat for NpcSheetsController — write the failing tests**

In `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`, add right after `Update_persists_Estrela_and_SinaAtual`:

```csharp
    [Fact]
    public async Task Update_with_a_garbage_HistoricoId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmUpdHist1", "npcupdhist1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var invalid = ValidUpdate() with { HistoricoId = "not-a-guid" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_HistoricoId_that_does_not_exist_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmUpdHist2", "npcupdhist2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var invalid = ValidUpdate() with { HistoricoId = Guid.NewGuid().ToString() };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_persists_a_valid_HistoricoId()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmUpdHist3", "npcupdhist3@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var historico = await db.Historicos.FirstAsync();

        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { HistoricoId = historico.Id.ToString() };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.HistoricoId.Should().Be(historico.Id.ToString());
    }
```

Confirm imports at the top of the file include `using Microsoft.EntityFrameworkCore;` and `using Microsoft.Extensions.DependencyInjection;` — add them if missing (needed for `.FirstAsync()`/`.CreateAsyncScope()`).

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSheetsControllerTests.Update_with_a_garbage_HistoricoId_returns_400|FullyQualifiedName~NpcSheetsControllerTests.Update_with_a_HistoricoId_that_does_not_exist_returns_400|FullyQualifiedName~NpcSheetsControllerTests.Update_persists_a_valid_HistoricoId"`
Expected: FAIL, same reasons as Step 2.

- [ ] **Step 7: Wire HistoricoId into NpcSheetsController.Update**

In `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`, find:

```csharp
        if (!TryParseEnum<Estrela>(request.Estrela, out var estrela))
            return BadRequest("Estrela inválida.");
        if (request.SinaAtual is < 0 or > 3)
            return BadRequest("SinaAtual deve estar entre 0 e 3.");
```

Add right after it:

```csharp
        Guid? historicoId = null;
        if (!string.IsNullOrWhiteSpace(request.HistoricoId))
        {
            if (!Guid.TryParse(request.HistoricoId, out var parsedHistoricoId))
                return BadRequest("HistoricoId inválido.");
            if (!await db.Historicos.AnyAsync(h => h.Id == parsedHistoricoId && !h.IsDeleted))
                return BadRequest("Histórico não encontrado.");
            historicoId = parsedHistoricoId;
        }
```

Find:

```csharp
        sheet.Estrela = estrela;
        sheet.Nivel = request.Nivel;
```

Replace with:

```csharp
        sheet.Estrela = estrela;
        sheet.HistoricoId = historicoId;
        sheet.Nivel = request.Nivel;
```

Find, in `ToResponseAsync`:

```csharp
            campaignId?.ToString(), s.ImageId?.ToString(), s.ArcaRolada,
            s.Estrela?.ToString(), s.SinaAtual);
```

Replace with:

```csharp
            campaignId?.ToString(), s.ImageId?.ToString(), s.ArcaRolada,
            s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString());
```

Confirm the top of the file has `using RuinaRPG.Infrastructure.Rules;` — it already does (needed for `Historico`/`db.Historicos`, though `db.Historicos` alone doesn't strictly require the using since it's a property access, not a type name — no change needed if the build passes without it).

- [ ] **Step 8: Run the NpcSheetsController tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSheetsControllerTests"`
Expected: PASS, including the 3 new tests.

- [ ] **Step 9: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs
git commit -m "feat: HistoricoId validado e persistido em Ficha de Personagem/NPC"
```

---

### Task 8: API — Historico bonus in the Perícia list (CharacterSkillsController/NpcSkillsController)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSkillsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSkillsControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSkillsControllerTests.cs`

**Interfaces:**
- Consumes: `HistoricoBonusCalculator.For` (Task 3), `sheet.HistoricoId` (Task 4/7), `HistoricosController`'s `POST /api/historicos` (Task 6) to set up test fixtures.
- Produces: `GET .../skills`'s `Modificador` field now reflects the sheet's Histórico — consumed by nothing else in this plan (this is a leaf endpoint), but is the primary player-facing proof the feature works.

- [ ] **Step 1: Write the failing test for CharacterSkillsController**

In `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSkillsControllerTests.cs`, add near the top of the class (after the existing private helpers, before the first `[Fact]`):

```csharp
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    private static UpdateCharacterSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, historicoId);
```

Add `using Microsoft.EntityFrameworkCore;` and `using Microsoft.Extensions.DependencyInjection;` and `using RuinaRPG.Contracts.Rules;` to the top of the file if not already present.

Add this test after `Update_a_skill_sets_Gasto_and_the_response_computes_Modificador`:

```csharp
    [Fact]
    public async Task List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillHistGm1", "skillhistgm1@teste.com");
        await GrantRulesAuditorAsync("skillhistgm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillHistPlayer1", "skillhistplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var historicoResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus na Perícia", "Descrição de teste.", "Arcano", "Biblioteca")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            UpdateWithHistorico(historico!.Id)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Arcano", playerToken,
            new UpdateCharacterSkillRequest(3, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Biblioteca", playerToken,
            new UpdateCharacterSkillRequest(3, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();
        body!.Single(s => s.Pericia == "Arcano").Modificador.Should().Be(7); // 3/3=1 + 6
        body.Single(s => s.Pericia == "Biblioteca").Modificador.Should().Be(4); // 3/3=1 + 3
        body.Single(s => s.Pericia == "Fortitude").Modificador.Should().Be(0); // untouched, no bonus
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSkillsControllerTests.List_adds_the_sheets_Historico_bonus"`
Expected: FAIL — `Arcano`'s Modificador is `1`, not `7` (no bonus applied yet).

- [ ] **Step 3: Wire the bonus into CharacterSkillsController.List**

In `src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs`, find:

```csharp
        var attributeTotals = await db.CharacterAttributes
            .Where(a => a.CharacterSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto, 0);
```

Replace with:

```csharp
        var attributeTotals = await db.CharacterAttributes
            .Where(a => a.CharacterSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        return skills
            .Select(s =>
            {
                var historicoBonus = HistoricoBonusCalculator.For(s.Pericia, historico?.PericiaMaisSeis, historico?.PericiaMaisTres);
                var modificador = SkillFormulas.Modificador(s.Gasto, historicoBonus);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSkillsControllerTests"`
Expected: PASS, including the new test.

- [ ] **Step 5: Repeat for NpcSkillsController — write the failing test**

In `tests/RuinaRPG.Tests.Integration/Controllers/NpcSkillsControllerTests.cs`, add near the top of the class:

```csharp
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    private static UpdateNpcSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        1, true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, historicoId);
```

Add `using Microsoft.EntityFrameworkCore;`, `using Microsoft.Extensions.DependencyInjection;`, `using RuinaRPG.Contracts.Rules;` to the top of the file if not already present.

Add this test after the corresponding Update/List test in the file:

```csharp
    [Fact]
    public async Task List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillHistGm1", "npcskillhistgm1@teste.com");
        await GrantRulesAuditorAsync("npcskillhistgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var historicoResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus NPC", "Descrição de teste.", "Arcano", "Biblioteca")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            UpdateWithHistorico(historico!.Id)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Arcano", gmToken,
            new UpdateNpcSkillRequest(3, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();
        body!.Single(s => s.Pericia == "Arcano").Modificador.Should().Be(7); // 3/3=1 + 6
    }
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSkillsControllerTests.List_adds_the_sheets_Historico_bonus"`
Expected: FAIL — Arcano's Modificador is `1`, not `7`.

- [ ] **Step 7: Wire the bonus into NpcSkillsController.List**

In `src/RuinaRPG.Api/Controllers/NpcSkillsController.cs`, find:

```csharp
        var attributeTotals = await db.NpcAttributes
            .Where(a => a.NpcSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto, 0);
```

Replace with:

```csharp
        var attributeTotals = await db.NpcAttributes
            .Where(a => a.NpcSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        return skills
            .Select(s =>
            {
                var historicoBonus = HistoricoBonusCalculator.For(s.Pericia, historico?.PericiaMaisSeis, historico?.PericiaMaisTres);
                var modificador = SkillFormulas.Modificador(s.Gasto, historicoBonus);
```

Add `using RuinaRPG.Infrastructure.Rules;` to the top of `NpcSkillsController.cs` if it isn't already there (needed for `db.Historicos`' element type to resolve — check by building first; add only if the build actually complains).

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSkillsControllerTests"`
Expected: PASS, including the new test.

- [ ] **Step 9: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs src/RuinaRPG.Api/Controllers/NpcSkillsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSkillsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSkillsControllerTests.cs
git commit -m "feat: bônus de Histórico entra no Modificador da Perícia (2.d)"
```

---

### Task 9: API — Historico bonus in Sub-Atributos (CharacterSheetsController/NpcSheetsController)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `HistoricoBonusCalculator.For` (Task 3), `sheet.HistoricoId` (Task 4/7).
- Produces: `GET .../sub-attributes`'s Iniciativa/Esquiva Natural/Defesa Natural now reflect a Histórico bonus on Prontidão/Reflexos/Fortitude — a leaf endpoint, nothing else in this plan consumes it.

- [ ] **Step 1: Write the failing test for CharacterSheetsController**

In `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, add right after `SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador`:

```csharp
    [Fact]
    public async Task SubAttributes_includes_the_Historico_bonus_on_Prontidao_and_Reflexos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSubHist1", "sheetsubhist1@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "SHEETSUBHIST1@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var historicoResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus Sub-Atributo", "Descrição de teste.", "Prontidao", "Reflexos")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSubHist1", "sheetplayersubhist1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Historico");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { HistoricoId = historico!.Id }));

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));

        // Prontidao Gasto 9 -> Modificador 3 + 6 (Histórico) = 9. Reflexos Gasto 6 -> Modificador 2 + 3 = 5.
        // Fortitude Gasto 3 -> Modificador 1, untouched by this Histórico.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Prontidao", playerToken,
            new UpdateCharacterSkillRequest(9, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Reflexos", playerToken,
            new UpdateCharacterSkillRequest(6, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Fortitude", playerToken,
            new UpdateCharacterSkillRequest(3, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(13); // agilidade 4 + brutoProntidao (3+6=9) + 0 artefato
        body.EsquivaNatural.Should().Be(9); // agilidade 4 + brutoReflexos (2+3=5) + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 (no bonus) + 0 escudo + 0 artefatos + 0 cobertura
    }
```

Add `using RuinaRPG.Contracts.Rules;` to the top of the file if not already present (needed for `CreateHistoricoRequest`/`HistoricoResponse`).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests.SubAttributes_includes_the_Historico_bonus"`
Expected: FAIL — `Iniciativa` is `7`, not `13` (matches the pre-existing baseline test's numbers, since the bonus isn't wired in yet).

- [ ] **Step 3: Wire the bonus into CharacterSheetsController.SubAttributes**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, find (inside `SubAttributes`):

```csharp
        var brutoSkills = await db.CharacterSkills
            .Where(s => s.CharacterSheetId == id && (s.Pericia == Pericia.Prontidao || s.Pericia == Pericia.Reflexos || s.Pericia == Pericia.Fortitude))
            .ToListAsync();
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto, 0);
```

Replace with:

```csharp
        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        var brutoSkills = await db.CharacterSkills
            .Where(s => s.CharacterSheetId == id && (s.Pericia == Pericia.Prontidao || s.Pericia == Pericia.Reflexos || s.Pericia == Pericia.Fortitude))
            .ToListAsync();
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(
            brutoSkills.Single(s => s.Pericia == pericia).Gasto,
            HistoricoBonusCalculator.For(pericia, historico?.PericiaMaisSeis, historico?.PericiaMaisTres));
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests"`
Expected: PASS, including the new test and the existing `SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador` (still passes: no Histórico on that sheet, bonus stays 0).

- [ ] **Step 5: Repeat for NpcSheetsController — write the failing test**

`NpcSheetsControllerTests.cs`'s existing baseline test (`SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador`) uses the exact same Agilidade/Vigor/Prontidao/Reflexos/Fortitude values as the Character version (4/4/9/6/3), so the expected numbers below are the same. Add this test right after it:

```csharp
    [Fact]
    public async Task SubAttributes_includes_the_Historico_bonus_on_Prontidao_and_Reflexos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSubHist1", "npcsubhist1@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "NPCSUBHIST1@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var historicoResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus Sub-Atributo NPC", "Descrição de teste.", "Prontidao", "Reflexos")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var sheetId = await CreateSheetAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { HistoricoId = historico!.Id }));

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Agilidade", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));

        // Prontidao Gasto 9 -> Modificador 3 + 6 (Histórico) = 9. Reflexos Gasto 6 -> Modificador 2 + 3 = 5.
        // Fortitude Gasto 3 -> Modificador 1, untouched by this Histórico.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Prontidao", gmToken,
            new UpdateNpcSkillRequest(9, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Reflexos", gmToken,
            new UpdateNpcSkillRequest(6, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Fortitude", gmToken,
            new UpdateNpcSkillRequest(3, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(13); // agilidade 4 + brutoProntidao (3+6=9) + 0 artefato
        body.EsquivaNatural.Should().Be(9); // agilidade 4 + brutoReflexos (2+3=5) + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 (no bonus) + 0 escudo + 0 artefatos + 0 cobertura
    }
```

Add `using RuinaRPG.Contracts.Rules;` to the top of the file if not already present (needed for `CreateHistoricoRequest`/`HistoricoResponse`).

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSheetsControllerTests.SubAttributes_includes_the_Historico_bonus"`
Expected: FAIL, same reason as Step 2.

- [ ] **Step 7: Wire the bonus into NpcSheetsController.SubAttributes**

In `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`, find (inside `SubAttributes`):

```csharp
        var brutoSkills = await db.NpcSkills
            .Where(s => s.NpcSheetId == id && (s.Pericia == Pericia.Prontidao || s.Pericia == Pericia.Reflexos || s.Pericia == Pericia.Fortitude))
            .ToListAsync();
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(brutoSkills.Single(s => s.Pericia == pericia).Gasto, 0);
```

Replace with:

```csharp
        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        var brutoSkills = await db.NpcSkills
            .Where(s => s.NpcSheetId == id && (s.Pericia == Pericia.Prontidao || s.Pericia == Pericia.Reflexos || s.Pericia == Pericia.Fortitude))
            .ToListAsync();
        int BrutoOf(Pericia pericia) => SkillFormulas.Modificador(
            brutoSkills.Single(s => s.Pericia == pericia).Gasto,
            HistoricoBonusCalculator.For(pericia, historico?.PericiaMaisSeis, historico?.PericiaMaisTres));
```

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSheetsControllerTests"`
Expected: PASS, including the new test.

- [ ] **Step 9: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs
git commit -m "feat: bônus de Histórico propaga para Sub-Atributos (Iniciativa/Esquiva/Defesa Natural)"
```

---

### Task 10: API — Historico bonus in Maestrias (CharacterMasteriesController/NpcMasteriesController)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcMasteriesController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterMasteriesControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/NpcMasteriesControllerTests.cs`

**Interfaces:**
- Consumes: `HistoricoBonusCalculator.For` (Task 3), `sheet.HistoricoId` (Task 4/7).
- Produces: a Maestria's `Total` now reflects a Histórico bonus on its own Perícia — a leaf endpoint.

- [ ] **Step 1: Write the failing test for CharacterMasteriesController**

`CharacterMasteriesControllerTests.cs` has neither a `GrantRulesAuditorAsync` nor an `UpdateWithHistorico`-style helper yet — add both, mirroring the ones Task 8 added to `CharacterSkillsControllerTests.cs`. Near the top of the class, after the existing private helpers (including `SetUpSheetAsync`):

```csharp
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    private static UpdateCharacterSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, historicoId);
```

Add `using Microsoft.EntityFrameworkCore;`, `using Microsoft.Extensions.DependencyInjection;`, `using RuinaRPG.Contracts.Rules;` to the top of the file if not already present.

Add this test after `Add_a_valid_mastery_returns_201`:

```csharp
    [Fact]
    public async Task Add_a_mastery_includes_the_Historico_bonus_in_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MasteryHistGm1", "masteryhistgm1@teste.com");
        await GrantRulesAuditorAsync("masteryhistgm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryHistPlayer1", "masteryhistplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var historicoResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus Maestria", "Descrição de teste.", "Pontaria", "Furtividade")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            UpdateWithHistorico(historico!.Id)));
        // Pontaria Gasto 9 -> Modificador 3 + 6 (Histórico) = 9.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Pontaria", playerToken,
            new UpdateCharacterSkillRequest(9, null)));
        // Destreza Gasto 4, no bônus/maestria/artefato -> Total 4.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Destreza", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", playerToken,
            new AddCharacterMasteryRequest("Teste Maestria Bônus", "Pontaria", "Destreza", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterMasteryResponse>();
        body!.Total.Should().Be(15); // gastoMaestria 2 + bruto (9/3=3 + 6 Histórico = 9) + atributoTotal 4
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterMasteriesControllerTests.Add_a_mastery_includes_the_Historico_bonus_in_Total"`
Expected: FAIL — `Total` is 6 lower than expected (no bonus applied yet).

- [ ] **Step 3: Wire the bonus into CharacterMasteriesController.ComputeTotalAsync**

In `src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs`, find:

```csharp
    private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, Atributo atributo, int gastoMaestria)
    {
        var skill = await db.CharacterSkills.SingleAsync(s => s.CharacterSheetId == sheetId && s.Pericia == pericia);
        var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);
        var bruto = SkillFormulas.Modificador(skill.Gasto, 0);
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }
```

Replace with:

```csharp
    private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, Atributo atributo, int gastoMaestria)
    {
        var skill = await db.CharacterSkills.SingleAsync(s => s.CharacterSheetId == sheetId && s.Pericia == pericia);
        var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);
        var historicoId = await db.CharacterSheets.Where(s => s.Id == sheetId).Select(s => s.HistoricoId).SingleAsync();
        var historico = historicoId is null ? null : await db.Historicos.FindAsync(historicoId.Value);
        var bruto = SkillFormulas.Modificador(skill.Gasto, HistoricoBonusCalculator.For(pericia, historico?.PericiaMaisSeis, historico?.PericiaMaisTres));
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }
```

Add `using RuinaRPG.Infrastructure.Rules;` to the top of the file if the build complains it's missing.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterMasteriesControllerTests"`
Expected: PASS, including the new test and every pre-existing test in the file (all pass a sheet with no `HistoricoId`, so `bruto` is unchanged from before).

- [ ] **Step 5: Write the failing test for NpcMasteriesController**

`NpcMasteriesControllerTests.cs`'s existing `Add_a_valid_mastery_returns_201` test uses `AddNpcMasteryRequest(string Nome, string Pericia, string Atributo, int GastoMaestria)` against `CreateSheetAsync(gmToken)` (no campaign/player needed for an NPC sheet) — same shape as the Character version. Add the same two helpers used in Step 1, near the top of the class:

```csharp
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    private static UpdateNpcSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        1, true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, historicoId);
```

Add `using Microsoft.EntityFrameworkCore;`, `using Microsoft.Extensions.DependencyInjection;`, `using RuinaRPG.Contracts.Rules;` to the top of the file if not already present.

Add this test after `Add_a_valid_mastery_returns_201`:

```csharp
    [Fact]
    public async Task Add_a_mastery_includes_the_Historico_bonus_in_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMasteryHistGm1", "npcmasteryhistgm1@teste.com");
        await GrantRulesAuditorAsync("npcmasteryhistgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var historicoResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus Maestria NPC", "Descrição de teste.", "Pontaria", "Furtividade")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            UpdateWithHistorico(historico!.Id)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Pontaria", gmToken,
            new UpdateNpcSkillRequest(9, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Destreza", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/masteries", gmToken,
            new AddNpcMasteryRequest("Teste Maestria Bônus NPC", "Pontaria", "Destreza", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<NpcMasteryResponse>();
        body!.Total.Should().Be(15); // gastoMaestria 2 + bruto (9/3=3 + 6 Histórico = 9) + atributoTotal 4
    }
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcMasteriesControllerTests.Add_a_mastery_includes_the_Historico_bonus_in_Total"`
Expected: FAIL — `Total` is 6 lower than expected.

- [ ] **Step 7: Wire the bonus into NpcMasteriesController.ComputeTotalAsync**

In `src/RuinaRPG.Api/Controllers/NpcMasteriesController.cs`, find:

```csharp
    private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, Atributo atributo, int gastoMaestria)
    {
        var skill = await db.NpcSkills.SingleAsync(s => s.NpcSheetId == sheetId && s.Pericia == pericia);
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        var bruto = SkillFormulas.Modificador(skill.Gasto, 0);
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }
```

Replace with:

```csharp
    private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, Atributo atributo, int gastoMaestria)
    {
        var skill = await db.NpcSkills.SingleAsync(s => s.NpcSheetId == sheetId && s.Pericia == pericia);
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        var historicoId = await db.NpcSheets.Where(s => s.Id == sheetId).Select(s => s.HistoricoId).SingleAsync();
        var historico = historicoId is null ? null : await db.Historicos.FindAsync(historicoId.Value);
        var bruto = SkillFormulas.Modificador(skill.Gasto, HistoricoBonusCalculator.For(pericia, historico?.PericiaMaisSeis, historico?.PericiaMaisTres));
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }
```

Add `using RuinaRPG.Infrastructure.Rules;` to the top of the file if the build complains it's missing.

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcMasteriesControllerTests"`
Expected: PASS, including the new test.

- [ ] **Step 9: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs src/RuinaRPG.Api/Controllers/NpcMasteriesController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterMasteriesControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcMasteriesControllerTests.cs
git commit -m "feat: bônus de Histórico propaga para o Total de Maestria"
```

---

### Task 11: Infrastructure — "Históricos" tab in the Livro de Regras

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs`

**Interfaces:**
- Consumes: `RuinaRpgDbContext.Historicos` (Task 4), `RulebookRenderer.SplitIntoSections` (existing, unchanged), `HistoricosController` (Task 6, to edit the catalog from the test).
- Produces: a 6th `RulebookDocument` with slug `"historicos"` in `IRulebookRenderer.GetDocuments()` — consumed by the client's existing generic `LivroDeRegras.razor` rendering (no client change needed, same as how the Estrelas Alkerianas tab required none).

- [ ] **Step 1: Write the failing tests**

In `tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs`, change the existing document-count test:

```csharp
    [Fact]
    public async Task Get_returns_the_five_documents_split_into_sections()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm1", "rulebook1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        body!.Select(d => d.Slug).Should().Equal(
            "caracteristicas", "sistema-basico", "graus-e-circulos", "tabela-de-niveis", "estrelas-alkerianas");
```

to:

```csharp
    [Fact]
    public async Task Get_returns_the_six_documents_split_into_sections()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm1", "rulebook1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        body!.Select(d => d.Slug).Should().Equal(
            "caracteristicas", "sistema-basico", "graus-e-circulos", "tabela-de-niveis", "estrelas-alkerianas", "historicos");
```

(keep the rest of that test's body unchanged).

Add two new tests after `EstrelasAlkerianas_IntroHtml_includes_the_calendar_image_and_has_11_sections`:

```csharp
    [Fact]
    public async Task Historicos_has_26_sections_and_the_intro_paragraph()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm5", "rulebook5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var historicos = body!.Single(d => d.Slug == "historicos");
        (historicos.IntroHtml ?? "").Should().Contain("marcaram a vida do personagem");
        historicos.Sections.Should().HaveCount(26);
        historicos.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Html));
        historicos.Sections.Select(s => s.Titulo).Should().Contain("Estudo Acadêmico");
    }

    [Fact]
    public async Task Historicos_reflects_a_catalog_edit_immediately()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookGm6", "rulebook6@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOK6@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new RuinaRPG.Contracts.Rules.CreateHistoricoRequest("Recém Cadastrado", "Aparece na aba na hora.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Rules.HistoricoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", gmToken));
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var historicos = body!.Single(d => d.Slug == "historicos");
        historicos.Sections.Should().Contain(s => s.Titulo == "Recém Cadastrado");

        // Cleanup so this created row can't affect other tests' section counts in this class.
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created!.Id}", gmToken));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookControllerTests"`
Expected: `Get_returns_the_six_documents_split_into_sections`, `Historicos_has_26_sections_and_the_intro_paragraph`, `Historicos_reflects_a_catalog_edit_immediately` all FAIL (no `"historicos"` slug exists yet).

- [ ] **Step 3: Implement BuildHistoricosAsync**

In `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs`, find:

```csharp
    public async Task<IReadOnlyList<RulebookDocument>> GetDocuments() =>
    [
        await BuildCaracteristicasAsync(),
        await BuildSistemaBasicoAsync(),
        await BuildGrausECirculosAsync(),
        await BuildTabelaDeNiveisAsync(),
        await BuildEstrelasAlkerianasAsync(),
    ];
```

Replace with:

```csharp
    public async Task<IReadOnlyList<RulebookDocument>> GetDocuments() =>
    [
        await BuildCaracteristicasAsync(),
        await BuildSistemaBasicoAsync(),
        await BuildGrausECirculosAsync(),
        await BuildTabelaDeNiveisAsync(),
        await BuildEstrelasAlkerianasAsync(),
        await BuildHistoricosAsync(),
    ];
```

Find:

```csharp
    private const string CalendarImageHtml = """
        <figure style="margin:0 0 16px">
            <img src="/rulebook/Calendario alkeriano.jpeg" alt="Calendário Alkeriano" style="max-width:100%" />
            <figcaption>Calendário Alkeriano</figcaption>
        </figure>
        """;
```

Add right after it:

```csharp

    /// <summary>
    /// Unlike every other document, this one has two different sources: IntroHtml comes from
    /// Historico.md's own lead-in paragraph (reused the same way BuildGrausECirculosAsync/
    /// BuildEstrelasAlkerianasAsync extract IntroHtml — via SplitIntoSections, discarding its
    /// Sections), but the per-entry Sections come from the live Historicos table instead of the
    /// Markdown — same "catalog is the source of truth" treatment BuildCaracteristicasAsync already
    /// gives Traits. Editing a Histórico via HistoricosController is what changes those.
    /// </summary>
    private async Task<RulebookDocument> BuildHistoricosAsync()
    {
        var (intro, _) = SplitIntoSections(RulesDataProvider.ReadResource("Historico.md"), splitLevel: 1);

        var historicos = await db.Historicos
            .Where(h => !h.IsDeleted)
            .OrderBy(h => h.Nome)
            .ToListAsync();

        var sections = historicos.Select(h => new RulebookSection(
            Id: Slugify(h.Nome),
            Titulo: h.Nome,
            Html: WebUtility.HtmlEncode(h.Descricao).Replace("\n", "<br />")
                + $"<p><em>+6 {WebUtility.HtmlEncode(h.PericiaMaisSeis.ToString())} / +3 {WebUtility.HtmlEncode(h.PericiaMaisTres.ToString())}</em></p>"
        )).ToList();

        return new RulebookDocument("historicos", "Históricos", intro, sections);
    }
```

Note: this document has no `RulebookDocumentOverride` support — it is intentionally absent from `RulebookDocumentsController.ValidSlugs`, so `ReadMarkdownAsync`/`ReadEmbeddedMarkdown` are not used here; `RulesDataProvider.ReadResource("Historico.md")` is called directly instead, since the Markdown is only ever the embedded default for the intro paragraph, never a saved override.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookControllerTests"`
Expected: PASS, all tests in the file.

- [ ] **Step 5: Build and run the full test suite**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs
git commit -m "feat: aba de Históricos no Livro de Regras, montada ao vivo a partir do catálogo"
```

---

### Task 12: Client — HistoricoSelect.razor + wiring into both Fichas

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Fields/HistoricoSelect.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`

**Interfaces:**
- Consumes: `GET /api/historicos` (Task 6) → `HistoricoResponse[]`; `CharacterSheetResponse.HistoricoId`/`NpcSheetResponse.HistoricoId`, `UpdateCharacterSheetRequest.HistoricoId`/`UpdateNpcSheetRequest.HistoricoId` (Task 5/7).
- Produces: `HistoricoSelect` Blazor component with `[Parameter] string? Value` / `[Parameter] EventCallback<string?> ValueChanged` — a self-contained field, nothing else in this plan consumes it directly.

This task has no automated test (Blazor component + page wiring, same as the Estrelas Alkerianas client work) — verify manually per Step 4 below, per CLAUDE.md's guidance to test UI changes in a running browser when automated coverage isn't practical.

- [ ] **Step 1: Create HistoricoSelect.razor**

Create `src/RuinaRPG.Client/Shared/Fields/HistoricoSelect.razor`:

```razor
@using MudBlazor
@using RuinaRPG.Contracts.Rules
@inject HttpClient Http

@* R0001 1.a — dropdown dos Históricos do catálogo + ícone de descrição, mesma ideia de popup que
   EstrelaSelect.razor usa — mas as opções vêm do catálogo ao vivo (GET api/historicos) em vez de
   uma lista estática, porque um Auditor de Regras pode adicionar/editar/remover Históricos a
   qualquer momento. *@
<div class="d-flex align-center" style="gap:4px">
    <MudSelect T="string" Label="Histórico" Value="Value" ValueChanged="OnValueChangedAsync" Placeholder="Escolha um Histórico">
        @foreach (var historico in _historicos)
        {
            <MudSelectItem Value="@historico.Id">@historico.Nome</MudSelectItem>
        }
    </MudSelect>
    @if (Value is not null)
    {
        <MudIconButton Icon="@Icons.Material.Filled.Info" Size="Size.Small" OnClick="@(() => _open = true)" title="Descrição do Histórico" />
    }
</div>

<MudDialog @bind-Visible="_open">
    <TitleContent>@Selecionado?.Nome</TitleContent>
    <DialogContent>
        <MudText>@Selecionado?.Descricao</MudText>
        @if (Selecionado is not null)
        {
            <MudText Typo="Typo.body2" Class="mt-2">
                <em>+6 @PericiaDisplay.Label(Selecionado.PericiaMaisSeis) / +3 @PericiaDisplay.Label(Selecionado.PericiaMaisTres)</em>
            </MudText>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _open = false)">Fechar</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    private bool _open;
    private List<HistoricoResponse> _historicos = new();

    private HistoricoResponse? Selecionado => _historicos.FirstOrDefault(h => h.Id == Value);

    protected override async Task OnInitializedAsync()
    {
        var response = await Http.GetAsync("historicos");
        if (response.IsSuccessStatusCode)
            _historicos = await response.Content.ReadFromJsonAsync<List<HistoricoResponse>>() ?? new();
    }

    private async Task OnValueChangedAsync(string? novoValor)
    {
        Value = novoValor;
        await ValueChanged.InvokeAsync(Value);
    }
}
```

This references `PericiaDisplay.Label` (`RuinaRPG.Client.Shared.PericiaDisplay`, an existing static class) — since `HistoricoSelect.razor` lives under `RuinaRPG.Client.Shared.Fields` and `PericiaDisplay` is in `RuinaRPG.Client.Shared`, add `@using RuinaRPG.Client.Shared` to the top of the file if the Razor compiler doesn't resolve it implicitly (check the build in Step 3 — `AfinidadeSelect.razor` in the same folder doesn't need this using because it doesn't reference `PericiaDisplay`, so this file may be the first one in `Shared/Fields/` that does).

- [ ] **Step 2: Mount HistoricoSelect in FichaDePersonagem.razor**

In `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, find:

```razor
                <EstrelaSelect @bind-Value="_form.Estrela" @bind-Value:after="NotifySavedAsync" />
            </Section>
```

Replace with:

```razor
                <EstrelaSelect @bind-Value="_form.Estrela" @bind-Value:after="NotifySavedAsync" />
                <HistoricoSelect @bind-Value="_form.HistoricoId" @bind-Value:after="NotifySavedAsync" />
            </Section>
```

Find, in the `_form.Estrela = sheet.Estrela;` load block:

```csharp
        _form.Estrela = sheet.Estrela;
        _form.SinaAtual = sheet.SinaAtual;
```

Replace with:

```csharp
        _form.Estrela = sheet.Estrela;
        _form.SinaAtual = sheet.SinaAtual;
        _form.HistoricoId = sheet.HistoricoId;
```

Find, in the save request construction (Task 5 Step 6 left a trailing literal `null` here as a placeholder — replace that `null`, not add a new argument):

```csharp
            _form.Cobertura, _form.Ciclos, _form.PontosDePericiaBonusCritico, _form.ArcaRolada, _form.Estrela, _form.SinaAtual, null);
```

Replace with:

```csharp
            _form.Cobertura, _form.Ciclos, _form.PontosDePericiaBonusCritico, _form.ArcaRolada, _form.Estrela, _form.SinaAtual, _form.HistoricoId);
```

Find, in the `SheetFormModel` class:

```csharp
        public string? Estrela { get; set; }
        public int SinaAtual { get; set; }
```

Replace with:

```csharp
        public string? Estrela { get; set; }
        public int SinaAtual { get; set; }
        public string? HistoricoId { get; set; }
```

- [ ] **Step 3: Mount HistoricoSelect in FichaDeNpc.razor**

In `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`, find:

```razor
                <EstrelaSelect @bind-Value="_form.Estrela" @bind-Value:after="NotifySavedAsync" />
            </Section>
```

Replace with:

```razor
                <EstrelaSelect @bind-Value="_form.Estrela" @bind-Value:after="NotifySavedAsync" />
                <HistoricoSelect @bind-Value="_form.HistoricoId" @bind-Value:after="NotifySavedAsync" />
            </Section>
```

Find, in the load block:

```csharp
        _form.Estrela = sheet.Estrela;
        _form.SinaAtual = sheet.SinaAtual;
```

Replace with:

```csharp
        _form.Estrela = sheet.Estrela;
        _form.SinaAtual = sheet.SinaAtual;
        _form.HistoricoId = sheet.HistoricoId;
```

Find, in the save request construction (Task 5 Step 6 left a trailing literal `null` here as a placeholder — replace that `null`, not add a new argument):

```csharp
            _form.Cobertura, _form.Ciclos, _form.ArcaRolada, _form.Estrela, _form.SinaAtual, null);
```

Replace with:

```csharp
            _form.Cobertura, _form.Ciclos, _form.ArcaRolada, _form.Estrela, _form.SinaAtual, _form.HistoricoId);
```

Find, in the `SheetFormModel` class:

```csharp
        public string? Estrela { get; set; }
        public int SinaAtual { get; set; }
```

Replace with:

```csharp
        public string? Estrela { get; set; }
        public int SinaAtual { get; set; }
        public string? HistoricoId { get; set; }
```

- [ ] **Step 4: Build and manually verify in the browser**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

Run `make deploy` (or the project's existing dev-run flow), log in as a GM, open a character sheet's Informações Básicas tab, and confirm: the "Histórico" dropdown appears next to Estrela and lists real catalog entries; picking one shows the ⓘ icon; clicking it opens a popup with the Histórico's description and its two bonified Perícias; the choice persists across a page reload. Repeat on a Ficha de NPC. Then open the Atributos & Perícias tab and confirm the two bonified Perícias' Modificador is 6/3 higher than an untouched Perícia with the same Gasto.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Shared/Fields/HistoricoSelect.razor src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: campo Histórico (dropdown + popup) na Identidade das fichas"
```

---

### Task 13: Client — AuditoriaHistoricos.razor + nav link

**Files:**
- Create: `src/RuinaRPG.Client/Pages/AuditoriaHistoricos.razor`
- Modify: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`

**Interfaces:**
- Consumes: `GET/POST/PUT/DELETE /api/historicos` (Task 6) → `HistoricoResponse`/`CreateHistoricoRequest`/`UpdateHistoricoRequest`.
- Produces: the `/auditoria/historicos` page — a leaf page, nothing else in this plan consumes it.

This task has no automated test (Blazor page), verified manually per Step 3.

- [ ] **Step 1: Create AuditoriaHistoricos.razor**

Create `src/RuinaRPG.Client/Pages/AuditoriaHistoricos.razor`:

```razor
@page "/auditoria/historicos"
@inject HttpClient Http
@using RuinaRPG.Contracts.Rules
@using MudBlazor

<Breadcrumbs Items="@Crumbs" />
<MudText Typo="Typo.h3">Auditoria: Históricos</MudText>

<DismissibleAlert @bind-Message="_errorMessage" Class="mt-3" />

@if (_forbidden)
{
    <MudAlert Severity="Severity.Warning" Variant="Variant.Filled" Class="mt-3">Você não é o Auditor de Regras designado — sem permissão para editar.</MudAlert>
}
else
{
    <Section Title="Adicionar Histórico">
        <MudTextField T="string" @bind-Value="_newForm.Nome" Label="Nome" />
        <MudTextField T="string" @bind-Value="_newForm.Descricao" Label="Descrição" Lines="3" />
        <MudSelect T="string" @bind-Value="_newForm.PericiaMaisSeis" Label="Perícia +6">
            @foreach (var pericia in Pericias)
            {
                <MudSelectItem Value="@pericia">@RuinaRPG.Client.Shared.PericiaDisplay.Label(pericia)</MudSelectItem>
            }
        </MudSelect>
        <MudSelect T="string" @bind-Value="_newForm.PericiaMaisTres" Label="Perícia +3">
            @foreach (var pericia in Pericias)
            {
                <MudSelectItem Value="@pericia">@RuinaRPG.Client.Shared.PericiaDisplay.Label(pericia)</MudSelectItem>
            }
        </MudSelect>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-2" OnClick="AddAsync">Adicionar</MudButton>
    </Section>

    <Section Title="Históricos">
        <MudSimpleTable Dense="true" Hover="true">
            <thead>
                <tr><th>Nome</th><th>Descrição</th><th>Perícia +6</th><th>Perícia +3</th><th></th></tr>
            </thead>
            <tbody>
                @foreach (var historico in _historicos)
                {
                    <tr>
                        <td><MudTextField T="string" Value="@historico.Nome" ValueChanged="@(v => UpdateAsync(historico, nome: v))" /></td>
                        <td><MudTextField T="string" Value="@historico.Descricao" ValueChanged="@(v => UpdateAsync(historico, descricao: v))" /></td>
                        <td>
                            <MudSelect T="string" Value="@historico.PericiaMaisSeis" ValueChanged="@(v => UpdateAsync(historico, periciaMaisSeis: v))">
                                @foreach (var pericia in Pericias)
                                {
                                    <MudSelectItem Value="@pericia">@RuinaRPG.Client.Shared.PericiaDisplay.Label(pericia)</MudSelectItem>
                                }
                            </MudSelect>
                        </td>
                        <td>
                            <MudSelect T="string" Value="@historico.PericiaMaisTres" ValueChanged="@(v => UpdateAsync(historico, periciaMaisTres: v))">
                                @foreach (var pericia in Pericias)
                                {
                                    <MudSelectItem Value="@pericia">@RuinaRPG.Client.Shared.PericiaDisplay.Label(pericia)</MudSelectItem>
                                }
                            </MudSelect>
                        </td>
                        <td><MudIconButton Icon="@Icons.Material.Filled.Delete" OnClick="@(() => DeleteAsync(historico.Id))" /></td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
    </Section>
}

@code {
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Auditoria: Históricos"),
    };

    // Same 39-value canonical list as the Pericia enum (Requisitos - Ficha de Personagem.md §2.d) —
    // wire-format identifiers, displayed via PericiaDisplay.Label.
    private static readonly string[] Pericias =
    [
        "Acrobacia", "Alquimia", "Arcano", "Armadilhas", "ArmasBrancas", "ArtefatosMagicos", "Artistico",
        "Atletismo", "Avaliacao", "Biblioteca", "Brigar", "Conducao", "Conhecimentos", "Crime",
        "EmpatiaComAnimais", "Enganacao", "ForcaDeVontade", "Fortitude", "Furtividade", "Herborismo",
        "Intimidacao", "Intuicao", "Investigacao", "Labia", "Lideranca", "Linguistica", "Medicina",
        "Navegacao", "Ocultismo", "Oficio", "Percepcao", "Pontaria", "Prontidao", "Reflexos", "Religiao",
        "Saquear", "Seducao", "SensoComum", "Sobrevivencia",
    ];

    private List<HistoricoResponse> _historicos = new();
    private string? _errorMessage;
    private bool _forbidden;
    private readonly NewHistoricoFormModel _newForm = new();

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var response = await Http.GetAsync("historicos");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar os Históricos.";
            return;
        }

        _historicos = await response.Content.ReadFromJsonAsync<List<HistoricoResponse>>() ?? new();
    }

    private async Task AddAsync()
    {
        var response = await Http.PostAsJsonAsync("historicos",
            new CreateHistoricoRequest(_newForm.Nome, _newForm.Descricao, _newForm.PericiaMaisSeis, _newForm.PericiaMaisTres));
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar o Histórico — as duas Perícias não podem ser iguais.";
            return;
        }

        _newForm.Nome = "";
        _newForm.Descricao = "";
        await LoadAsync();
    }

    private async Task UpdateAsync(HistoricoResponse current, string? nome = null, string? descricao = null, string? periciaMaisSeis = null, string? periciaMaisTres = null)
    {
        var response = await Http.PutAsJsonAsync($"historicos/{current.Id}", new UpdateHistoricoRequest(
            nome ?? current.Nome,
            descricao ?? current.Descricao,
            periciaMaisSeis ?? current.PericiaMaisSeis,
            periciaMaisTres ?? current.PericiaMaisTres));
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a alteração.";
            return;
        }

        await LoadAsync();
    }

    private async Task DeleteAsync(string id)
    {
        var response = await Http.DeleteAsync($"historicos/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível excluir — este Histórico pode estar em uso em alguma ficha.";
            return;
        }

        await LoadAsync();
    }

    private class NewHistoricoFormModel
    {
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string PericiaMaisSeis { get; set; } = "Acrobacia";
        public string PericiaMaisTres { get; set; } = "Alquimia";
    }
}
```

- [ ] **Step 2: Add the nav link**

In `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`, find:

```razor
    <MudNavLink Href="auditoria/efeitos">Auditoria: Efeitos</MudNavLink>
```

Add right after it:

```razor
    <MudNavLink Href="auditoria/historicos">Auditoria: Históricos</MudNavLink>
```

- [ ] **Step 3: Build and manually verify in the browser**

Run: `dotnet build`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

Run `make deploy` (or the project's existing dev-run flow), grant a GM account the Rules Auditor role (`make grant-rules-auditor EMAIL=...`), log in as that account, open the side nav, confirm "Auditoria: Históricos" appears, open it, add a new Histórico, edit its Nome/Descrição/Perícias inline, confirm it shows up immediately, delete it, confirm it disappears. Then open the Livro de Regras' "Históricos" tab and confirm an edit made here shows up there without a page reload of the renderer (a fresh navigation to the tab is enough — this isn't a live-socket update, just "the next GET reflects it").

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/AuditoriaHistoricos.razor src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor
git commit -m "feat: página de Auditoria para gerenciar o catálogo de Históricos"
```

---

## Self-Review Notes

- **Spec coverage**: catalog+seed (Tasks 2, 4), Auditor CRUD (Task 6), sheet field + popup (Task 12), bonus in Modificador/Bruto/Maestria (Tasks 3, 8, 9, 10), Livro de Regras tab reflecting the catalog live (Task 11), audit page (Task 13), Criatura exclusion (Task 3's literal `0` at the 3 Creature call sites, Task 1 Step 2's docs update), all 5 Requisitos docs (Task 1). Every section of `docs/superpowers/specs/2026-09-22-historicos-design.md` has a task.
- **Type consistency verified**: `HistoricoBonusCalculator.For(Pericia, Pericia?, Pericia?)` (Task 3) is called with the exact same 3-argument shape in Tasks 8, 9, 10. `SkillFormulas.Modificador(int, int)` (Task 3) is called with 2 arguments everywhere from Task 3 onward. `HistoricoResponse`/`CreateHistoricoRequest`/`UpdateHistoricoRequest` (Task 5) field order and names match exactly between `HistoricosController` (Task 6), the integration tests (Task 6, 8, 9, 11), and the client (Tasks 12, 13). `CharacterSheetResponse.HistoricoId`/`UpdateCharacterSheetRequest.HistoricoId` (Task 5) are always the last positional parameter, consistent in Tasks 7, 9, 12.
- **No placeholders**: every step above contains complete, runnable code — no "add validation", no "similar to Task N" without the actual code repeated in place.
