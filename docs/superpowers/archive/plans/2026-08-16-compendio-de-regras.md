# Compêndio de Regras Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A read-only, searchable index over `Docs/Sistema RPG/` — Regras (Sistema Básico), Efeitos de Graus & Círculos, Tabelas de progressão, and Características — reachable by both GM and Jogador, with zero editing surface. Along the way this plan stands up the **shared game-rules reference-data layer** (level bonuses, Vocação/Arquétipo/Classes progression, Círculo-Grau-por-EAP, XP thresholds) that the later Ficha de Personagem and Ficha de Criaturas plans will consume directly for Vida/Arcana/XP-threshold formulas — building it once here avoids two separate parses of the same source tables.

**Architecture:** `Docs/Sistema RPG/*.md` files are embedded as MSBuild `EmbeddedResource` items in `RuinaRPG.Infrastructure` — content updates take effect on the next image rebuild (matching Técnico R0007's rebuild-per-deploy model), with zero dependency on the container's runtime filesystem layout. Two pure, zero-I/O parser primitives live in `RuinaRPG.Domain` — a generic pipe-table parser and a generic heading-section parser — unit-tested against literal fixture strings. Typed parsers (Domain) sit on top of those primitives for each concrete source shape. `IRulesDataProvider`'s **interface** lives in `RuinaRPG.Domain.Rules` (it's a pure data-shape contract — Técnico R0002 keeps Domain free of infrastructure dependencies, and `CompendioSearchService`, itself Domain, needs to consume this shape); its **implementation**, `RulesDataProvider`, lives in `RuinaRPG.Infrastructure.Rules` and is the only piece that touches I/O — it reads the embedded resources through the typed parsers once and caches the result (singleton, computed on first access — the source text never changes at runtime). This is the one thing later plans (Personagem, Criaturas) will inject for Vida/Arcana/XP lookups. Características/Traits is the one exception to "parse on demand": Modelo de Dados requires it to be a real, FK-able DB table (future `CharacterTraits.TraitId`), so it gets an EF Core entity plus an **additive-only** startup seeder — parses `Características.md` once at boot and inserts any (Nome, Custo, Polaridade) tuple not already present; it never updates or deletes an existing row, protecting any `CharacterTraits` reference created later. `CompendioSearchService` (Domain) is the single search surface, combining the DB-backed Traits query with the in-memory `IRulesDataProvider` content, filterable by category (R0003).

**Tech Stack:** Same as established (ASP.NET Core 8, EF Core 8/Npgsql, Blazor WebAssembly 8), plus MSBuild `EmbeddedResource` (new to this plan) and a startup `IHostedService` for the additive Traits seed.

**Spec:** `Docs/Requisitos/Requisitos - Compêndio de Regras.md` (all 4 requirements), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §6.1 (Traits table) and §9 (Compêndio legend).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Read-only: no create/edit/delete endpoint for any Compêndio content anywhere in this plan (R0004) — the interface only ever reads.
- Both **GM** and **Jogador** have full read access to the search endpoint (Compêndio preamble) — no `[Authorize(Roles=...)]` restriction, only `[Authorize]` (any authenticated user).
- Content reflects `Docs/Sistema RPG/*.md` **as embedded at build time** — a doc edit takes effect on the next `make deploy` image rebuild, not by editing a running container's filesystem.
- The Traits seeder is additive-only — never update or delete an existing row, even if the source text for that trait changes (protects future `CharacterTraits.TraitId` FK integrity). A changed description produces a new row alongside the old one; reconciling stale rows is an explicit, separate, future operation, never automatic.
- No secret ever hardcoded — unaffected by this plan (no new secrets).

---

### Task 1: Domain — generic pipe-table parser

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/Markdown/MarkdownTable.cs`
- Create: `src/RuinaRPG.Domain/Rules/Markdown/MarkdownTableParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/Markdown/MarkdownTableParserTests.cs`

**Interfaces:**
- Produces: `MarkdownTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows)`; `MarkdownTableParser.ParseTables(string markdown) : IReadOnlyList<MarkdownTable>`. Every later table-typed parser in this plan (Tasks 2) calls this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Rules/Markdown/MarkdownTableParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Tests.Unit.Rules.Markdown;

public class MarkdownTableParserTests
{
    [Fact]
    public void ParseTables_parses_a_single_simple_table()
    {
        const string markdown = """
            | Nível | Bônus |
            | :---: | ----- |
            |   1   | +9 Pontos de Atributo |
            |   2   | +4 Pontos de Ignição |
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables.Should().HaveCount(1);
        tables[0].Headers.Should().Equal("Nível", "Bônus");
        tables[0].Rows.Should().HaveCount(2);
        tables[0].Rows[0].Should().Equal("1", "+9 Pontos de Atributo");
        tables[0].Rows[1].Should().Equal("2", "+4 Pontos de Ignição");
    }

    [Fact]
    public void ParseTables_parses_multiple_tables_separated_by_blank_lines()
    {
        const string markdown = """
            | A | B |
            | - | - |
            | 1 | 2 |

            | C | D |
            | - | - |
            | 3 | 4 |
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables.Should().HaveCount(2);
        tables[0].Headers.Should().Equal("A", "B");
        tables[1].Headers.Should().Equal("C", "D");
    }

    [Fact]
    public void ParseTables_ignores_prose_outside_tables()
    {
        const string markdown = """
            Some introductory prose that is not a table.

            | X | Y |
            | - | - |
            | 1 | 2 |

            More prose after the table.
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables.Should().HaveCount(1);
        tables[0].Headers.Should().Equal("X", "Y");
    }

    [Fact]
    public void ParseTables_handles_a_row_with_an_empty_first_cell()
    {
        // Mirrors the real shape of Tabela de Vocação's sub-header row: "|       |  Vida   | Arcana  | ..."
        const string markdown = """
            | Nível | Campeão | Campeão |
            | :---: | :-----: | :-----: |
            |       |  Vida   | Arcana  |
            |   1   |    8    |    4    |
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables[0].Rows[0].Should().Equal("", "Vida", "Arcana");
        tables[0].Rows[1].Should().Equal("1", "8", "4");
    }

    [Fact]
    public void ParseTables_returns_empty_when_there_are_no_tables()
    {
        var tables = MarkdownTableParser.ParseTables("Just some prose, no pipe tables here.");

        tables.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter MarkdownTableParserTests`
Expected: FAIL to compile — `MarkdownTable`/`MarkdownTableParser` don't exist yet.

- [ ] **Step 3: Write `MarkdownTable`**

`src/RuinaRPG.Domain/Rules/Markdown/MarkdownTable.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.Markdown;

public sealed record MarkdownTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);
```

- [ ] **Step 4: Write `MarkdownTableParser`**

`src/RuinaRPG.Domain/Rules/Markdown/MarkdownTableParser.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.Markdown;

public static class MarkdownTableParser
{
    public static IReadOnlyList<MarkdownTable> ParseTables(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var tables = new List<MarkdownTable>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (!IsTableRow(lines[i]) || i + 1 >= lines.Length || !IsSeparatorRow(lines[i + 1]))
                continue;

            var headers = SplitRow(lines[i]);
            var rows = new List<IReadOnlyList<string>>();
            var j = i + 2;
            while (j < lines.Length && IsTableRow(lines[j]))
            {
                rows.Add(SplitRow(lines[j]));
                j++;
            }

            tables.Add(new MarkdownTable(headers, rows));
            i = j - 1;
        }

        return tables;
    }

    private static bool IsTableRow(string line) => line.TrimStart().StartsWith('|');

    private static bool IsSeparatorRow(string line) =>
        IsTableRow(line) && line.Replace("|", "").Trim().All(c => c is '-' or ':' or ' ');

    private static IReadOnlyList<string> SplitRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter MarkdownTableParserTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/Markdown tests/RuinaRPG.Tests.Unit/Rules/Markdown/MarkdownTableParserTests.cs
git commit -m "feat: add a generic markdown pipe-table parser"
```

---

### Task 2: Domain — reference-data typed parsers (Níveis, Vocação, Arquétipos, Círculo-Grau-por-EAP, XP)

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/LevelBonus.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/NivelBonusParser.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/VocacaoProgressao.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/VocacaoProgressaoParser.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/ArquetipoProgressao.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/ArquetipoProgressaoParser.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/CirculoGrauPorEap.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/CirculoGrauPorEapParser.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/XpPorNivel.cs`
- Create: `src/RuinaRPG.Domain/Rules/ReferenceData/XpPorNivelParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/NivelBonusParserTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/VocacaoProgressaoParserTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/ArquetipoProgressaoParserTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/CirculoGrauPorEapParserTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/XpPorNivelParserTests.cs`

**Interfaces:**
- Consumes: `MarkdownTableParser.ParseTables` (Task 1).
- Produces: `LevelBonus(int Nivel, string BonusText)`, `NivelBonusParser.Parse(string markdown) : IReadOnlyList<LevelBonus>`; `VocacaoProgressao(string Vocacao, int Nivel, int Vida, int Arcana)`, `VocacaoProgressaoParser.Parse(string markdown) : IReadOnlyList<VocacaoProgressao>`; `ArquetipoProgressao(string Arquetipo, int Nivel, int Vida, int Arcana)`, `ArquetipoProgressaoParser.Parse(string markdown) : IReadOnlyList<ArquetipoProgressao>`; `CirculoGrauPorEap(int CirculoOuGrau, string? EapAbsoluto, string? EapRelativo, int AfinidadeAbsoluta, int AfinidadeGanhoPorNivel)`, `CirculoGrauPorEapParser.Parse(string markdown) : IReadOnlyList<CirculoGrauPorEap>`; `XpPorNivel(int Nivel, string XpAbsoluto, string XpRelativo)`, `XpPorNivelParser.Parse(string markdown) : IReadOnlyList<XpPorNivel>`. Task 6 (`IRulesDataProvider`) and the later Ficha de Personagem/Criaturas plans consume all five records and parsers by these exact names.

`EapAbsoluto`/`EapRelativo` on `CirculoGrauPorEap` are `string?` (not `int?`) because the real table's Grau 9 row reads `Max.` in both EAP columns, not a number — parse it as text (`"Max."` or the numeric string), never throw on non-numeric cells.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/NivelBonusParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class NivelBonusParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Níveis.md (levels 1, 2, 50).
    private const string Markdown = """
        | NÍVEL | BÔNUS                                                                                                                                                                                           |
        | :---: | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
        |   1   | +9 Pontos de Atributo  <br>+Status de Vida Aprimorado  <br>+Status de Foco Aprimorado  <br>+10 Pontos de Ignição  <br>+4 Pontos de Perícia  <br>+1 Espaço de Maestria  <br>+1 Ponto de Maestria |
        |   2   | +4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+1 Ponto de Atributo                                                                                                             |
        |  50   | +30 Pontos de Ignição  <br>+Status Aprimorado de Vida/Foco (Total)  <br>+4 Pontos de Perícia  <br>+ Ultima Passiva  <br>+8 Pontos de atributo  <br>+1 Espaço de Maestria                       |
        """;

    [Fact]
    public void Parse_extracts_one_entry_per_level_row()
    {
        var result = NivelBonusParser.Parse(Markdown);

        result.Should().HaveCount(3);
        result[0].Nivel.Should().Be(1);
        result[0].BonusText.Should().Contain("+9 Pontos de Atributo");
        result[2].Nivel.Should().Be(50);
        result[2].BonusText.Should().Contain("Ultima Passiva");
    }
}
```

`tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/VocacaoProgressaoParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class VocacaoProgressaoParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Vocação.md (level 1 row, all 5 vocações).
    private const string Markdown = """
        | Nível | Campeão | Campeão | Caçador | Caçador | Feiticeiro | Feiticeiro | Adepto | Adepto | Bruxo | Bruxo  |
        | :---: | :-----: | :-----: | :-----: | :-----: | :--------: | :--------: | :----: | :----: | :---: | :----: |
        |       |  Vida   | Arcana  |  Vida   | Arcana  |    Vida    |   Arcana   |  Vida  | Arcana | Vida  | Arcana |
        |   1   |    8    |    4    |    6    |    6    |     4      |     8      |   4    |   8    |   4   |   8    |
        """;

    [Fact]
    public void Parse_extracts_one_entry_per_vocacao_per_level()
    {
        var result = VocacaoProgressaoParser.Parse(Markdown);

        result.Should().HaveCount(5);
        result.Should().ContainSingle(r => r.Vocacao == "Campeão" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 8, Arcana = 4 });
        result.Should().ContainSingle(r => r.Vocacao == "Feiticeiro" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 4, Arcana = 8 });
    }
}
```

`tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/ArquetipoProgressaoParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class ArquetipoProgressaoParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Arquetipos.md (level 1 row).
    private const string Markdown = """
        |       | Fisico | Fisico | Arcano | Arcano |
        | :---: | :----: | :----: | :----: | :----: |
        | Nível |  Vida  | Arcana |  Vida  | Arcana |
        |   1   |   8    |   4    |   4    |   8    |
        """;

    [Fact]
    public void Parse_extracts_one_entry_per_arquetipo_per_level()
    {
        var result = ArquetipoProgressaoParser.Parse(Markdown);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(r => r.Arquetipo == "Fisico" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 8, Arcana = 4 });
        result.Should().ContainSingle(r => r.Arquetipo == "Arcano" && r.Nivel == 1)
            .Which.Should().BeEquivalentTo(new { Vida = 4, Arcana = 8 });
    }
}
```

`tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/CirculoGrauPorEapParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class CirculoGrauPorEapParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabela de Circulo e Grau por EAP.md (rows 0, 1, 9).
    private const string Markdown = """
        |              | EAP máximo por círculo/grau | EAP para Próximo  |     Afinidade     |    Afinidade    |
        | :----------: | :-------------------------: | :---------------: | :---------------: | :-------------: |
        | Círculo/Grau |      Valores absolutos      | Valores relativos | Valores Absolutos | Ganho por Nível |
        |      0       |              0              |         0         |         3         |        0        |
        |      1       |             100             |        100        |         5         |        2        |
        |      9       |            Max.             |       Max.        |        21         |        2        |
        """;

    [Fact]
    public void Parse_handles_numeric_and_non_numeric_EAP_cells()
    {
        var result = CirculoGrauPorEapParser.Parse(Markdown);

        result.Should().HaveCount(3);
        result[0].Should().BeEquivalentTo(new { CirculoOuGrau = 0, EapAbsoluto = "0", AfinidadeAbsoluta = 3, AfinidadeGanhoPorNivel = 0 });
        result[1].Should().BeEquivalentTo(new { CirculoOuGrau = 1, EapAbsoluto = "100", AfinidadeAbsoluta = 5 });
        result[2].Should().BeEquivalentTo(new { CirculoOuGrau = 9, EapAbsoluto = "Max.", EapRelativo = "Max." });
    }
}
```

`tests/RuinaRPG.Tests.Unit/Rules/ReferenceData/XpPorNivelParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.Rules.ReferenceData;

public class XpPorNivelParserTests
{
    // Real excerpt from Docs/Sistema RPG/Tabelas de XP, Atributos, Características e EAP.md — the FIRST
    // of the four tables in that file (XP máximo por nível), plus a second small table to prove multi-table handling.
    private const string Markdown = """
        |       | XP máximo por nível | XP máximo por nível |
        | :---: | :-----------------: | :-----------------: |
        | Nível |  Valores absolutos  |  Valores relativos  |
        |   1   |         50          |         50          |
        |  50   |      Lvl. Max       |      Lvl. Max       |

        |       |     Atributos     |    Atributos    |
        | :---: | :---------------: | :-------------: |
        | Nível | Valores Absolutos | Ganho por Nível |
        |   1   |         9         |        0        |
        """;

    [Fact]
    public void Parse_reads_only_the_first_XP_table_and_tolerates_non_numeric_cells()
    {
        var result = XpPorNivelParser.Parse(Markdown);

        result.Should().HaveCount(2);
        result[0].Should().BeEquivalentTo(new { Nivel = 1, XpAbsoluto = "50", XpRelativo = "50" });
        result[1].Should().BeEquivalentTo(new { Nivel = 50, XpAbsoluto = "Lvl. Max", XpRelativo = "Lvl. Max" });
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~ReferenceData"`
Expected: FAIL to compile — none of the five record/parser types exist yet.

- [ ] **Step 3: Write the records and parsers**

`src/RuinaRPG.Domain/Rules/ReferenceData/LevelBonus.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.ReferenceData;

public sealed record LevelBonus(int Nivel, string BonusText);
```

`src/RuinaRPG.Domain/Rules/ReferenceData/NivelBonusParser.cs`:

```csharp
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class NivelBonusParser
{
    public static IReadOnlyList<LevelBonus> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        return table.Rows
            .Select(row => new LevelBonus(int.Parse(row[0]), row[1]))
            .ToList();
    }
}
```

`src/RuinaRPG.Domain/Rules/ReferenceData/VocacaoProgressao.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.ReferenceData;

public sealed record VocacaoProgressao(string Vocacao, int Nivel, int Vida, int Arcana);
```

`src/RuinaRPG.Domain/Rules/ReferenceData/VocacaoProgressaoParser.cs`:

```csharp
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class VocacaoProgressaoParser
{
    // Column 0 is "Nível"; columns 1.. come in (Vida, Arcana) pairs, one pair per Vocação,
    // named by Headers[i] (the Vocação repeats across its pair) and disambiguated by row 0
    // (the sub-header: "Vida"/"Arcana"), same shape as ArquetipoProgressaoParser.
    public static IReadOnlyList<VocacaoProgressao> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        var subHeader = table.Rows[0];
        var results = new List<VocacaoProgressao>();

        for (var col = 1; col < table.Headers.Count; col += 2)
        {
            var vocacao = table.Headers[col];
            foreach (var row in table.Rows.Skip(1))
            {
                results.Add(new VocacaoProgressao(vocacao, int.Parse(row[0]), int.Parse(row[col]), int.Parse(row[col + 1])));
            }
        }

        return results;
    }
}
```

`src/RuinaRPG.Domain/Rules/ReferenceData/ArquetipoProgressao.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.ReferenceData;

public sealed record ArquetipoProgressao(string Arquetipo, int Nivel, int Vida, int Arcana);
```

`src/RuinaRPG.Domain/Rules/ReferenceData/ArquetipoProgressaoParser.cs`:

```csharp
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class ArquetipoProgressaoParser
{
    // Same (Vida, Arcana)-pair shape as VocacaoProgressaoParser, but here row 0 ("Nível") is the
    // real column-0 label and the Vocação/Arquétipo names live in Headers, so Nível comes from
    // table.Rows[1..][0] while the header pairs still start at column 1.
    public static IReadOnlyList<ArquetipoProgressao> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        var results = new List<ArquetipoProgressao>();

        for (var col = 1; col < table.Headers.Count; col += 2)
        {
            var arquetipo = table.Headers[col];
            foreach (var row in table.Rows.Skip(1))
            {
                results.Add(new ArquetipoProgressao(arquetipo, int.Parse(row[0]), int.Parse(row[col]), int.Parse(row[col + 1])));
            }
        }

        return results;
    }
}
```

`src/RuinaRPG.Domain/Rules/ReferenceData/CirculoGrauPorEap.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.ReferenceData;

public sealed record CirculoGrauPorEap(int CirculoOuGrau, string EapAbsoluto, string EapRelativo, int AfinidadeAbsoluta, int AfinidadeGanhoPorNivel);
```

`src/RuinaRPG.Domain/Rules/ReferenceData/CirculoGrauPorEapParser.cs`:

```csharp
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class CirculoGrauPorEapParser
{
    public static IReadOnlyList<CirculoGrauPorEap> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).Single();
        return table.Rows
            .Skip(1) // row 0 is the sub-header ("Círculo/Grau", "Valores absolutos", ...)
            .Select(row => new CirculoGrauPorEap(
                int.Parse(row[0]),
                row[1],
                row[2],
                int.Parse(row[3]),
                int.Parse(row[4])))
            .ToList();
    }
}
```

`src/RuinaRPG.Domain/Rules/ReferenceData/XpPorNivel.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.ReferenceData;

public sealed record XpPorNivel(int Nivel, string XpAbsoluto, string XpRelativo);
```

`src/RuinaRPG.Domain/Rules/ReferenceData/XpPorNivelParser.cs`:

```csharp
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules.ReferenceData;

public static class XpPorNivelParser
{
    // The source file has 4 tables (XP, Atributos, Características, EAP); this parser only
    // reads the first — the XP thresholds — which is all Personagem's "Para o próximo" (1.b) needs.
    public static IReadOnlyList<XpPorNivel> Parse(string markdown)
    {
        var table = MarkdownTableParser.ParseTables(markdown).First();
        return table.Rows
            .Skip(1) // row 0 is the sub-header ("Nível", "Valores absolutos", "Valores relativos")
            .Select(row => new XpPorNivel(int.Parse(row[0]), row[1], row[2]))
            .ToList();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~ReferenceData"`
Expected: PASS (5/5 test classes, one test each).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/ReferenceData tests/RuinaRPG.Tests.Unit/Rules/ReferenceData
git commit -m "feat: add typed parsers for level, vocacao, arquetipo, eap and xp reference tables"
```

---

### Task 3: Domain — generic heading-section parser

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/Markdown/MarkdownSection.cs`
- Create: `src/RuinaRPG.Domain/Rules/Markdown/MarkdownSectionParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/Markdown/MarkdownSectionParserTests.cs`

**Interfaces:**
- Produces: `MarkdownSection(int Level, string Title, string Body, IReadOnlyList<MarkdownSection> Children)`; `MarkdownSectionParser.Parse(string markdown) : IReadOnlyList<MarkdownSection>` — parses top-level (`#`/H1) sections, each with its own nested (`##`/H2, `###`/H3, ...) children, recursively, `Body` being the section's own text up to (not including) its first child heading. Tasks 4 (Efeitos), 5 (Regras) and 7 (Traits) all call this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Rules/Markdown/MarkdownSectionParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Tests.Unit.Rules.Markdown;

public class MarkdownSectionParserTests
{
    [Fact]
    public void Parse_extracts_top_level_sections_with_their_body_text()
    {
        const string markdown = """
            # Primeira Seção

            Corpo da primeira seção.

            # Segunda Seção

            Corpo da segunda seção.
            """;

        var sections = MarkdownSectionParser.Parse(markdown);

        sections.Should().HaveCount(2);
        sections[0].Title.Should().Be("Primeira Seção");
        sections[0].Body.Should().Contain("Corpo da primeira seção.");
        sections[1].Title.Should().Be("Segunda Seção");
    }

    [Fact]
    public void Parse_nests_child_sections_under_their_parent()
    {
        const string markdown = """
            # 1º GRAU / CÍRCULO I

            ## Aumentar Armadura

            Gasto: 2 PI por Ponto de Redução.

            ## Cura

            Gasto: 2 PI

            # 2º GRAU / CÍRCULO II

            ## Aceleração

            Gasto: 3 PI
            """;

        var sections = MarkdownSectionParser.Parse(markdown);

        sections.Should().HaveCount(2);
        sections[0].Title.Should().Be("1º GRAU / CÍRCULO I");
        sections[0].Children.Should().HaveCount(2);
        sections[0].Children[0].Title.Should().Be("Aumentar Armadura");
        sections[0].Children[0].Body.Should().Contain("Gasto: 2 PI por Ponto de Redução.");
        sections[0].Children[1].Title.Should().Be("Cura");
        sections[1].Children.Should().ContainSingle(c => c.Title == "Aceleração");
    }

    [Fact]
    public void Parse_returns_empty_for_markdown_with_no_headings()
    {
        var sections = MarkdownSectionParser.Parse("Just prose, no headings.");

        sections.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter MarkdownSectionParserTests`
Expected: FAIL to compile — `MarkdownSection`/`MarkdownSectionParser` don't exist yet.

- [ ] **Step 3: Write `MarkdownSection`**

`src/RuinaRPG.Domain/Rules/Markdown/MarkdownSection.cs`:

```csharp
namespace RuinaRPG.Domain.Rules.Markdown;

public sealed record MarkdownSection(int Level, string Title, string Body, IReadOnlyList<MarkdownSection> Children);
```

- [ ] **Step 4: Write `MarkdownSectionParser`**

`src/RuinaRPG.Domain/Rules/Markdown/MarkdownSectionParser.cs`:

```csharp
using System.Text;

namespace RuinaRPG.Domain.Rules.Markdown;

public static class MarkdownSectionParser
{
    public static IReadOnlyList<MarkdownSection> Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var (sections, _) = ParseAtLevel(lines, 0, 1);
        return sections;
    }

    private static (List<MarkdownSection> Sections, int NextIndex) ParseAtLevel(string[] lines, int startIndex, int level)
    {
        var sections = new List<MarkdownSection>();
        var i = startIndex;

        while (i < lines.Length)
        {
            var headingLevel = HeadingLevelOf(lines[i]);
            if (headingLevel == 0)
            {
                i++;
                continue;
            }
            if (headingLevel < level)
                break; // belongs to an ancestor, let the caller handle it

            var title = lines[i].TrimStart('#').Trim();
            var bodyBuilder = new StringBuilder();
            var j = i + 1;
            while (j < lines.Length && (HeadingLevelOf(lines[j]) == 0 || HeadingLevelOf(lines[j]) < headingLevel && HeadingLevelOf(lines[j]) != 0 && false))
            {
                // Body text ends at the first heading of ANY level >= headingLevel + 0 (i.e. any next heading).
                if (HeadingLevelOf(lines[j]) != 0)
                    break;
                bodyBuilder.AppendLine(lines[j]);
                j++;
            }

            var (children, nextIndex) = j < lines.Length && HeadingLevelOf(lines[j]) > headingLevel
                ? ParseAtLevel(lines, j, headingLevel + 1)
                : (new List<MarkdownSection>(), j);

            sections.Add(new MarkdownSection(headingLevel, title, bodyBuilder.ToString().Trim(), children));
            i = nextIndex;
        }

        return (sections, i);
    }

    private static int HeadingLevelOf(string line)
    {
        var trimmed = line.TrimStart();
        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
            level++;
        return level > 0 && level < trimmed.Length && trimmed[level] == ' ' ? level : 0;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter MarkdownSectionParserTests`
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/Markdown/MarkdownSection.cs src/RuinaRPG.Domain/Rules/Markdown/MarkdownSectionParser.cs tests/RuinaRPG.Tests.Unit/Rules/Markdown/MarkdownSectionParserTests.cs
git commit -m "feat: add a generic markdown heading-section parser"
```

---

### Task 4: Domain — Efeitos de Graus & Círculos parser

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/GraduacaoEfeito.cs`
- Create: `src/RuinaRPG.Domain/Rules/GraduacaoEfeitoParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/GraduacaoEfeitoParserTests.cs`

**Interfaces:**
- Consumes: `MarkdownSectionParser.Parse` (Task 3).
- Produces: `GraduacaoEfeito(string Nome, int Grau, string Descricao, string Gasto, bool TemPreRequisito, string? PreRequisitoDescricao)`; `GraduacaoEfeitoParser.Parse(string markdown) : IReadOnlyList<GraduacaoEfeito>`. Task 6 (`IRulesDataProvider`) consumes this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Rules/GraduacaoEfeitoParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class GraduacaoEfeitoParserTests
{
    // Real excerpt from Docs/Sistema RPG/GRAUS & CÍRCULOS.md — top intro table skipped
    // (that's CirculoGrauPorEap's scaling table, not a named effect), then 2 effects from
    // 1º Grau (one with a prerequisite) and 1 from 2º Grau, to prove multi-grade extraction.
    private const string Markdown = """
        Custo: 1,25 de arcana por PI (arredondado para cima)

        # 1º GRAU / CÍRCULO I

        ## Efeitos Básicos

        Dano: 2 Pontos por Dado.

        ## Aumentar Armadura

        Gasto: 2 PI por Ponto de Redução.

        Max. 5 de Redução por Grau/Círculo.

        Concede ao personagem Redução Física.

        Obrigatória a compra de Duração.

        ## Contrato Mágico

        Gasto: 3 PI

        Essencial para os bruxos que expressam sua magia como Invocação.

        # 2º GRAU / CÍRCULO II

        ## Aceleração

        Gasto: 3 PI

        O Alvo acometido por Aceleração recebe metade de sua movimentação como movimentação adicional.
        """;

    [Fact]
    public void Parse_extracts_named_effects_with_their_introducing_grade()
    {
        var result = GraduacaoEfeitoParser.Parse(Markdown);

        result.Should().Contain(e => e.Nome == "Aumentar Armadura" && e.Grau == 1);
        result.Should().Contain(e => e.Nome == "Contrato Mágico" && e.Grau == 1);
        result.Should().Contain(e => e.Nome == "Aceleração" && e.Grau == 2);
    }

    [Fact]
    public void Parse_excludes_the_Efeitos_Basicos_heading_itself()
    {
        // "Efeitos Básicos" is a sub-section wrapper (Dano/Alcance/Duração scaling), not a named,
        // purchasable Special Effect — it must not appear as its own GraduacaoEfeito entry.
        var result = GraduacaoEfeitoParser.Parse(Markdown);

        result.Should().NotContain(e => e.Nome == "Efeitos Básicos");
    }

    [Fact]
    public void Parse_extracts_Gasto_text_and_detects_a_prerequisite()
    {
        var result = GraduacaoEfeitoParser.Parse(Markdown);

        var aumentarArmadura = result.Single(e => e.Nome == "Aumentar Armadura");
        aumentarArmadura.Gasto.Should().Be("2 PI por Ponto de Redução.");
        aumentarArmadura.TemPreRequisito.Should().BeTrue();
        aumentarArmadura.PreRequisitoDescricao.Should().Contain("Duração");

        var contratoMagico = result.Single(e => e.Nome == "Contrato Mágico");
        contratoMagico.Gasto.Should().Be("3 PI");
        contratoMagico.TemPreRequisito.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter GraduacaoEfeitoParserTests`
Expected: FAIL to compile — `GraduacaoEfeito`/`GraduacaoEfeitoParser` don't exist yet.

- [ ] **Step 3: Write `GraduacaoEfeito`**

`src/RuinaRPG.Domain/Rules/GraduacaoEfeito.cs`:

```csharp
namespace RuinaRPG.Domain.Rules;

public sealed record GraduacaoEfeito(string Nome, int Grau, string Descricao, string Gasto, bool TemPreRequisito, string? PreRequisitoDescricao);
```

- [ ] **Step 4: Write `GraduacaoEfeitoParser`**

`src/RuinaRPG.Domain/Rules/GraduacaoEfeitoParser.cs`:

```csharp
using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static partial class GraduacaoEfeitoParser
{
    public static IReadOnlyList<GraduacaoEfeito> Parse(string markdown)
    {
        var sections = MarkdownSectionParser.Parse(markdown);
        var results = new List<GraduacaoEfeito>();

        foreach (var gradeSection in sections)
        {
            var grau = ExtractGrauNumber(gradeSection.Title);
            if (grau is null)
                continue;

            foreach (var effect in gradeSection.Children.Where(c => c.Title != "Efeitos Básicos"))
            {
                var gasto = GastoRegex().Match(effect.Body) is { Success: true } gastoMatch
                    ? gastoMatch.Groups[1].Value.Trim()
                    : "";
                var preRequisitoMatch = PreRequisitoRegex().Match(effect.Body);

                results.Add(new GraduacaoEfeito(
                    effect.Title,
                    grau.Value,
                    effect.Body,
                    gasto,
                    preRequisitoMatch.Success,
                    preRequisitoMatch.Success ? preRequisitoMatch.Value.Trim() : null));
            }
        }

        return results;
    }

    private static int? ExtractGrauNumber(string sectionTitle)
    {
        var match = GrauNumberRegex().Match(sectionTitle);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"^(\d+)º\s*GRAU")]
    private static partial Regex GrauNumberRegex();

    [GeneratedRegex(@"\*{0,2}Gasto:?\*{0,2}\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex GastoRegex();

    [GeneratedRegex(@"[EÉ]\s*obrigatória a compra de[^.]*\.?", RegexOptions.IgnoreCase)]
    private static partial Regex PreRequisitoRegex();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter GraduacaoEfeitoParserTests`
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/GraduacaoEfeito.cs src/RuinaRPG.Domain/Rules/GraduacaoEfeitoParser.cs tests/RuinaRPG.Tests.Unit/Rules/GraduacaoEfeitoParserTests.cs
git commit -m "feat: add a parser for graus e circulos named effects"
```

---

### Task 5: Domain — Regras (Sistema Básico) section index

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/RegraEntry.cs`
- Create: `src/RuinaRPG.Domain/Rules/RegraEntryParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/RegraEntryParserTests.cs`

**Interfaces:**
- Consumes: `MarkdownSectionParser.Parse` (Task 3).
- Produces: `RegraEntry(string Titulo, string Conteudo)`; `RegraEntryParser.Parse(string markdown) : IReadOnlyList<RegraEntry>` — flattens every H2 (`##`) section of the source doc into one searchable entry (Compêndio R0001 lists the topics by name — atributos, perícias, PA, ações em combate, etc. — which are exactly the `## N. Título` headings of `Ruína RPG - Sistema Básico.md`). Task 6 consumes this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Rules/RegraEntryParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class RegraEntryParserTests
{
    // Real excerpt from Docs/Sistema RPG/Ruína RPG - Sistema Básico.md.
    private const string Markdown = """
        ## 1. Atributos

        Os atributos definem as capacidades inatas do seu personagem. No início, o jogador distribui 9 pontos entre os oito atributos abaixo.

        - Instinto: Percepção, intuição e sentidos.

        ## 3. Pontos de Adrenalina (PA)

        Os Pontos de Adrenalina representam o esforço usado em cena.

        ### Escala de Dados

        Não há rolagem de dano separada.
        """;

    [Fact]
    public void Parse_flattens_H2_sections_into_searchable_entries()
    {
        var result = RegraEntryParser.Parse(Markdown);

        result.Should().Contain(r => r.Titulo == "1. Atributos" && r.Conteudo.Contains("Instinto"));
        result.Should().Contain(r => r.Titulo == "3. Pontos de Adrenalina (PA)" && r.Conteudo.Contains("esforço usado em cena"));
    }

    [Fact]
    public void Parse_includes_nested_H3_content_inside_its_parent_H2_entry()
    {
        var result = RegraEntryParser.Parse(Markdown);

        result.Single(r => r.Titulo == "3. Pontos de Adrenalina (PA)").Conteudo.Should().Contain("Escala de Dados");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RegraEntryParserTests`
Expected: FAIL to compile — `RegraEntry`/`RegraEntryParser` don't exist yet.

- [ ] **Step 3: Write `RegraEntry`**

`src/RuinaRPG.Domain/Rules/RegraEntry.cs`:

```csharp
namespace RuinaRPG.Domain.Rules;

public sealed record RegraEntry(string Titulo, string Conteudo);
```

- [ ] **Step 4: Write `RegraEntryParser`**

`src/RuinaRPG.Domain/Rules/RegraEntryParser.cs`:

```csharp
using System.Text;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static class RegraEntryParser
{
    public static IReadOnlyList<RegraEntry> Parse(string markdown)
    {
        // The doc's real top level is H2 ("## 1. Atributos"); MarkdownSectionParser.Parse starts
        // at level 1 (H1) and finds none, so re-parse starting one level down by prefixing a
        // synthetic H1 wrapper is unnecessary — instead walk every H2 directly via the section
        // parser's own recursive descent by treating H2 as this document's root level.
        var topLevelSections = MarkdownSectionParser.Parse(PromoteH2ToH1(markdown));

        return topLevelSections
            .Select(section => new RegraEntry(section.Title, FlattenContent(section)))
            .ToList();
    }

    private static string PromoteH2ToH1(string markdown) =>
        string.Join('\n', markdown.Replace("\r\n", "\n").Split('\n').Select(line =>
            line.TrimStart().StartsWith("## ") && !line.TrimStart().StartsWith("### ") ? line.TrimStart()[1..] : line));

    private static string FlattenContent(MarkdownSection section)
    {
        var builder = new StringBuilder(section.Body);
        foreach (var child in section.Children)
        {
            builder.AppendLine().AppendLine(child.Title).AppendLine(FlattenContent(child));
        }
        return builder.ToString().Trim();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RegraEntryParserTests`
Expected: PASS (2/2).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/RegraEntry.cs src/RuinaRPG.Domain/Rules/RegraEntryParser.cs tests/RuinaRPG.Tests.Unit/Rules/RegraEntryParserTests.cs
git commit -m "feat: add a parser for sistema basico rule sections"
```

---

### Task 6: Infrastructure — embed source docs and add `IRulesDataProvider`

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj`
- Create: `src/RuinaRPG.Domain/Rules/IRulesDataProvider.cs`
- Create: `src/RuinaRPG.Infrastructure/Rules/RulesDataProvider.cs`
- Modify: `src/RuinaRPG.Api/Program.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/RulesDataProviderTests.cs`

**Interfaces:**
- Consumes: `NivelBonusParser`, `VocacaoProgressaoParser`, `ArquetipoProgressaoParser`, `CirculoGrauPorEapParser`, `XpPorNivelParser` (Task 2), `GraduacaoEfeitoParser` (Task 4), `RegraEntryParser` (Task 5).
- Produces: `IRulesDataProvider` with properties `IReadOnlyList<LevelBonus> Niveis`, `IReadOnlyList<VocacaoProgressao> Vocacoes`, `IReadOnlyList<ArquetipoProgressao> Arquetipos`, `IReadOnlyList<CirculoGrauPorEap> CirculoGrauPorEap`, `IReadOnlyList<XpPorNivel> XpPorNivel`, `IReadOnlyList<GraduacaoEfeito> Efeitos`, `IReadOnlyList<RegraEntry> Regras` — all computed once and cached. Registered as a singleton. Task 10 (`CompendioSearchService`) and later Ficha de Personagem/Criaturas plans inject this interface. The **interface** lives in `RuinaRPG.Domain.Rules` (a pure data-shape contract, no I/O in the interface itself); the **implementation** lives in `RuinaRPG.Infrastructure.Rules` — this split lets `CompendioSearchService` (Task 10, itself in `RuinaRPG.Domain`) depend on the interface without `RuinaRPG.Domain` ever referencing `RuinaRPG.Infrastructure` (Técnico R0002).

- [ ] **Step 1: Embed the source docs as resources**

In `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj`, inside the existing `<ItemGroup>` (or a new one), add:

```xml
<ItemGroup>
  <EmbeddedResource Include="../../Docs/Sistema RPG/Tabela de Níveis.md" LogicalName="Tabela de Níveis.md" />
  <EmbeddedResource Include="../../Docs/Sistema RPG/Tabela de Vocação.md" LogicalName="Tabela de Vocação.md" />
  <EmbeddedResource Include="../../Docs/Sistema RPG/Tabela de Arquetipos.md" LogicalName="Tabela de Arquetipos.md" />
  <EmbeddedResource Include="../../Docs/Sistema RPG/Tabela de Circulo e Grau por EAP.md" LogicalName="Tabela de Circulo e Grau por EAP.md" />
  <EmbeddedResource Include="../../Docs/Sistema RPG/Tabelas de XP, Atributos, Características e EAP.md" LogicalName="Tabelas de XP, Atributos, Características e EAP.md" />
  <EmbeddedResource Include="../../Docs/Sistema RPG/GRAUS &amp; CÍRCULOS.md" LogicalName="GRAUS e CIRCULOS.md" />
  <EmbeddedResource Include="../../Docs/Sistema RPG/Ruína RPG - Sistema Básico.md" LogicalName="Sistema Basico.md" />
  <EmbeddedResource Include="../../Docs/Sistema RPG/Características.md" LogicalName="Caracteristicas.md" />
</ItemGroup>
```

`LogicalName` gives each resource a stable, ASCII-safe name independent of the source file's accented/special characters, so `Assembly.GetManifestResourceStream(logicalName)` is reliable regardless of how MSBuild would otherwise mangle the resource name.

- [ ] **Step 2: Write the failing test**

`tests/RuinaRPG.Tests.Unit/Rules/RulesDataProviderTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class RulesDataProviderTests
{
    [Fact]
    public void All_seven_collections_are_populated_from_the_real_embedded_docs()
    {
        IRulesDataProvider provider = new RulesDataProvider();

        provider.Niveis.Should().HaveCount(50);
        provider.Vocacoes.Should().HaveCountGreaterThan(0).And.Contain(v => v.Vocacao == "Campeão" && v.Nivel == 1);
        provider.Arquetipos.Should().Contain(a => a.Arquetipo == "Fisico" && a.Nivel == 1);
        provider.CirculoGrauPorEap.Should().HaveCount(10); // Círculo/Grau 0..9
        provider.XpPorNivel.Should().HaveCount(50);
        provider.Efeitos.Should().Contain(e => e.Nome == "Aumentar Armadura");
        provider.Regras.Should().Contain(r => r.Titulo.Contains("Adrenalina"));
    }

    [Fact]
    public void Repeated_access_reuses_the_cached_parse_result()
    {
        IRulesDataProvider provider = new RulesDataProvider();

        var firstCall = provider.Niveis;
        var secondCall = provider.Niveis;

        secondCall.Should().BeSameAs(firstCall);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RulesDataProviderTests`
Expected: FAIL to compile — `IRulesDataProvider`/`RulesDataProvider` don't exist yet.

- [ ] **Step 4: Write `IRulesDataProvider`**

`src/RuinaRPG.Domain/Rules/IRulesDataProvider.cs`:

```csharp
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.Rules;

public interface IRulesDataProvider
{
    IReadOnlyList<LevelBonus> Niveis { get; }
    IReadOnlyList<VocacaoProgressao> Vocacoes { get; }
    IReadOnlyList<ArquetipoProgressao> Arquetipos { get; }
    IReadOnlyList<CirculoGrauPorEap> CirculoGrauPorEap { get; }
    IReadOnlyList<XpPorNivel> XpPorNivel { get; }
    IReadOnlyList<GraduacaoEfeito> Efeitos { get; }
    IReadOnlyList<RegraEntry> Regras { get; }
}
```

- [ ] **Step 5: Write `RulesDataProvider`**

`src/RuinaRPG.Infrastructure/Rules/RulesDataProvider.cs`:

```csharp
using System.Reflection;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Infrastructure.Rules;

public class RulesDataProvider : IRulesDataProvider // IRulesDataProvider is RuinaRPG.Domain.Rules.IRulesDataProvider
{
    private readonly Lazy<IReadOnlyList<LevelBonus>> _niveis;
    private readonly Lazy<IReadOnlyList<VocacaoProgressao>> _vocacoes;
    private readonly Lazy<IReadOnlyList<ArquetipoProgressao>> _arquetipos;
    private readonly Lazy<IReadOnlyList<CirculoGrauPorEap>> _circuloGrauPorEap;
    private readonly Lazy<IReadOnlyList<XpPorNivel>> _xpPorNivel;
    private readonly Lazy<IReadOnlyList<GraduacaoEfeito>> _efeitos;
    private readonly Lazy<IReadOnlyList<RegraEntry>> _regras;

    public RulesDataProvider()
    {
        _niveis = new Lazy<IReadOnlyList<LevelBonus>>(() => NivelBonusParser.Parse(ReadResource("Tabela de Níveis.md")));
        _vocacoes = new Lazy<IReadOnlyList<VocacaoProgressao>>(() => VocacaoProgressaoParser.Parse(ReadResource("Tabela de Vocação.md")));
        _arquetipos = new Lazy<IReadOnlyList<ArquetipoProgressao>>(() => ArquetipoProgressaoParser.Parse(ReadResource("Tabela de Arquetipos.md")));
        _circuloGrauPorEap = new Lazy<IReadOnlyList<CirculoGrauPorEap>>(() => CirculoGrauPorEapParser.Parse(ReadResource("Tabela de Circulo e Grau por EAP.md")));
        _xpPorNivel = new Lazy<IReadOnlyList<XpPorNivel>>(() => XpPorNivelParser.Parse(ReadResource("Tabelas de XP, Atributos, Características e EAP.md")));
        _efeitos = new Lazy<IReadOnlyList<GraduacaoEfeito>>(() => GraduacaoEfeitoParser.Parse(ReadResource("GRAUS e CIRCULOS.md")));
        _regras = new Lazy<IReadOnlyList<RegraEntry>>(() => RegraEntryParser.Parse(ReadResource("Sistema Basico.md")));
    }

    public IReadOnlyList<LevelBonus> Niveis => _niveis.Value;
    public IReadOnlyList<VocacaoProgressao> Vocacoes => _vocacoes.Value;
    public IReadOnlyList<ArquetipoProgressao> Arquetipos => _arquetipos.Value;
    public IReadOnlyList<CirculoGrauPorEap> CirculoGrauPorEap => _circuloGrauPorEap.Value;
    public IReadOnlyList<XpPorNivel> XpPorNivel => _xpPorNivel.Value;
    public IReadOnlyList<GraduacaoEfeito> Efeitos => _efeitos.Value;
    public IReadOnlyList<RegraEntry> Regras => _regras.Value;

    internal static string ReadResource(string logicalName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

- [ ] **Step 6: Register the singleton**

In `src/RuinaRPG.Api/Program.cs`, add near the other service registrations:

```csharp
builder.Services.AddSingleton<IRulesDataProvider, RulesDataProvider>();
```

Add `using RuinaRPG.Domain.Rules;` and `using RuinaRPG.Infrastructure.Rules;` to the top of the file if not already present.

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RulesDataProviderTests`
Expected: PASS (2/2).

- [ ] **Step 8: Run the whole solution build to confirm the embedded-resource paths resolve**

Run: `dotnet build`
Expected: `Build succeeded`, 0 warnings, 0 errors — a wrong `Include` path here fails silently at the XML level (MSBuild just embeds zero bytes matching the glob), so the real proof is Step 7's test passing against actual parsed content, not just a green build.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj src/RuinaRPG.Domain/Rules/IRulesDataProvider.cs src/RuinaRPG.Infrastructure/Rules src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Unit/Rules/RulesDataProviderTests.cs
git commit -m "feat: embed sistema rpg docs and add IRulesDataProvider"
```

---

### Task 7: Domain — Trait record and Características.md parser

**Files:**
- Modify: `Docs/Sistema RPG/Características.md`
- Create: `src/RuinaRPG.Domain/Rules/TraitSeed.cs`
- Create: `src/RuinaRPG.Domain/Rules/TraitSeedParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/TraitSeedParserTests.cs`

**Interfaces:**
- Consumes: `MarkdownSectionParser.Parse` (Task 3).
- Produces: `TraitSeed(string Nome, string Descricao, int Custo, string Polaridade)` (`Polaridade` is `"Positiva"` or `"Negativa"`, matching the `Polaridade` enum's string values so Task 9's seeder can `Enum.Parse` it directly); `TraitSeedParser.Parse(string markdown) : IReadOnlyList<TraitSeed>`. Task 9 (seeder) consumes this exact signature.

- [ ] **Step 1: Fix a documentation gap in the source doc first**

`Docs/Sistema RPG/Características.md` line 77 reads "Imunidade de Venenos" as plain text, missing the `###` heading marker every other trait name has (a pre-existing inconsistency, visible by comparing it to "Lingüísta" right above and "Presença Invisível" right below). Without the marker this parser would either merge it into "Lingüísta" or drop it — fix the doc directly rather than special-case the parser for one broken heading:

Change line 77 from:

```
Imunidade de Venenos
```

to:

```
### Imunidade de Venenos
```

- [ ] **Step 2: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Rules/TraitSeedParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class TraitSeedParserTests
{
    // Real excerpt from Docs/Sistema RPG/Características.md: one single-tier Positiva, one
    // multi-tier Positiva, one single-tier Negativa (already-negative cost in the source), and
    // one Negativa whose cost line has a trailing qualifier word OTHER than "por X" ("cada"
    // instead of "por sentido") — a real phrasing this parser must not silently drop.
    private const string Markdown = """
        # Positivas

        ### Alfabetizado

        1 ponto: Saber ler e escrever é um conhecimento destinado a um número um tanto limitado de pessoas.

        ### Aparência Inofensiva

        2 ponto: você não aparenta ser perigoso.

        3 pontos: Considere que o personagem automaticamente você recebe vantagem em testes de Iniciativa.

        # Negativas

        ### Alergia

        -1 ponto: o Personagem é alérgico a alguma coisa.

        ### Código de Honra

        -1 ponto cada: o personagem segue algum rígido código de conduta e jamais poderá desobedecê-lo.
        """;

    [Fact]
    public void Parse_extracts_one_row_per_single_tier_trait()
    {
        var result = TraitSeedParser.Parse(Markdown);

        result.Should().ContainSingle(t => t.Nome == "Alfabetizado")
            .Which.Should().BeEquivalentTo(new { Custo = 1, Polaridade = "Positiva" });
    }

    [Fact]
    public void Parse_extracts_one_row_per_cost_tier_for_multi_tier_traits_with_a_disambiguating_name()
    {
        var result = TraitSeedParser.Parse(Markdown);

        var tiers = result.Where(t => t.Nome.StartsWith("Aparência Inofensiva")).ToList();
        tiers.Should().HaveCount(2);
        tiers.Should().Contain(t => t.Nome == "Aparência Inofensiva (2 pontos)" && t.Custo == 2);
        tiers.Should().Contain(t => t.Nome == "Aparência Inofensiva (3 pontos)" && t.Custo == 3);
    }

    [Fact]
    public void Parse_reads_already_negative_costs_for_Negativas_as_is()
    {
        var result = TraitSeedParser.Parse(Markdown);

        result.Should().ContainSingle(t => t.Nome == "Alergia")
            .Which.Should().BeEquivalentTo(new { Custo = -1, Polaridade = "Negativa" });
    }

    [Fact]
    public void Parse_does_not_drop_a_trait_whose_cost_line_has_a_non_por_qualifier_word()
    {
        // Regression guard: "ponto cada:" has a qualifier word ("cada") that is not the "por X"
        // shape seen elsewhere (e.g. "2 pontos por sentido:") — an earlier version of this parser's
        // regex only tolerated "por X" and silently dropped entries like this one.
        var result = TraitSeedParser.Parse(Markdown);

        result.Should().ContainSingle(t => t.Nome == "Código de Honra")
            .Which.Should().BeEquivalentTo(new { Custo = -1, Polaridade = "Negativa" });
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter TraitSeedParserTests`
Expected: FAIL to compile — `TraitSeed`/`TraitSeedParser` don't exist yet.

- [ ] **Step 4: Write `TraitSeed`**

`src/RuinaRPG.Domain/Rules/TraitSeed.cs`:

```csharp
namespace RuinaRPG.Domain.Rules;

public sealed record TraitSeed(string Nome, string Descricao, int Custo, string Polaridade);
```

- [ ] **Step 5: Write `TraitSeedParser`**

`src/RuinaRPG.Domain/Rules/TraitSeedParser.cs`:

```csharp
using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Domain.Rules;

public static partial class TraitSeedParser
{
    public static IReadOnlyList<TraitSeed> Parse(string markdown)
    {
        var sections = MarkdownSectionParser.Parse(markdown);
        var results = new List<TraitSeed>();

        foreach (var polaritySection in sections.Where(s => s.Title is "Positivas" or "Negativas"))
        {
            var polaridade = polaritySection.Title == "Positivas" ? "Positiva" : "Negativa";

            foreach (var trait in polaritySection.Children)
            {
                var tiers = CostTierRegex().Matches(trait.Body);
                if (tiers.Count == 0)
                    continue; // traits with no parseable cost line (e.g. a pure prose prerequisite paragraph) are skipped

                var multiTier = tiers.Count > 1;
                foreach (Match tier in tiers)
                {
                    var custo = int.Parse(tier.Groups[1].Value) * (tier.Groups[1].Value.StartsWith('-') ? 1 : polaridade == "Negativa" ? -1 : 1);
                    var nome = multiTier ? $"{trait.Title} ({tier.Groups[1].Value.TrimStart('-')} ponto{(Math.Abs(custo) == 1 ? "" : "s")})" : trait.Title;
                    results.Add(new TraitSeed(nome, tier.Groups[2].Value.Trim(), custo, polaridade));
                }
            }
        }

        return results;
    }

    // Matches "N ponto(s): description" or "-N ponto(s): description", tolerating a trailing
    // qualifier between "ponto(s)" and the colon (e.g. "2 pontos por sentido:", "-1 ponto cada:").
    // Description runs to the next cost-tier line or end of body.
    [GeneratedRegex(@"(-?\d+)\s*pontos?(?:\s+[^\n:]+)?:\s*(.+?)(?=\r?\n\r?\n-?\d+\s*pontos?|\z)", RegexOptions.Singleline)]
    private static partial Regex CostTierRegex();
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter TraitSeedParserTests`
Expected: PASS (4/4).

- [ ] **Step 7: Add a real-file regression test, then run it**

Append to `tests/RuinaRPG.Tests.Unit/Rules/TraitSeedParserTests.cs` (add `using RuinaRPG.Infrastructure.Rules;` at the top — this specific test crosses into Infrastructure to read the real embedded doc, the same cross-layer convention `CompendioSearchServiceTests` (Task 10) already uses):

```csharp
[Fact]
public void Parse_extracts_exactly_69_traits_from_the_real_source_document()
{
    // 30 Positivas (24 traits, 5 of them multi-tier: Aparência Inofensiva x2, Arma ou Artefato
    // Mágico x2, Dívida de Gratidão x3, Imunidade de Venenos x2, Sono Leve x2) +
    // 39 Negativas (34 traits, 4 of them multi-tier: Deficiente Físico x3, Fetiche Material x2,
    // Fobia x2, Mania de Perseguição x2) = 69. Counted by hand against the doc when this task was
    // planned — a real change to Características.md (a trait added/removed/re-tiered) is expected
    // to move this number, and this test's failure is exactly the signal that should happen.
    var result = TraitSeedParser.Parse(RulesDataProvider.ReadResource("Caracteristicas.md"));

    result.Should().HaveCount(69);
    result.Should().Contain(t => t.Nome == "Imunidade de Venenos (2 pontos)"); // proves Step 1's doc fix took effect
    result.Should().Contain(t => t.Nome == "Código de Honra"); // proves the "ponto cada:" qualifier is handled
}
```

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter TraitSeedParserTests`
Expected: PASS (5/5) — this exercises Task 6's embedded resource together with this task's parser against the real, current `Características.md`.

- [ ] **Step 8: Commit**

```bash
git add "Docs/Sistema RPG/Características.md" src/RuinaRPG.Domain/Rules/TraitSeed.cs src/RuinaRPG.Domain/Rules/TraitSeedParser.cs tests/RuinaRPG.Tests.Unit/Rules/TraitSeedParserTests.cs
git commit -m "feat: add a parser for caracteristicas trait seed data"
```

---

### Task 8: Infrastructure — `Trait` entity and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/Trait.cs`
- Create: `src/RuinaRPG.Domain/Enums/Polaridade.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/TraitMigrationTests.cs`

**Interfaces:**
- Produces: `enum Polaridade { Positiva, Negativa }` (Domain); `Trait` (`Guid Id`, `string Nome`, `string Descricao`, `int Custo`, `Polaridade Polaridade`), `RuinaRpgDbContext.Traits : DbSet<Trait>`. Task 9 (seeder) and Task 10 (search) depend on this shape exactly. The later Ficha de Personagem — Posses plan's `CharacterTraits.TraitId` FK depends on `Trait.Id` being a stable, real primary key.

- [ ] **Step 1: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/TraitMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class TraitMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TraitMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Traits_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddTraits"));

        db.Traits.Add(new Trait { Id = Guid.NewGuid(), Nome = "Teste", Descricao = "Descrição de teste", Custo = 2, Polaridade = Polaridade.Positiva });
        await db.SaveChangesAsync();

        (await db.Traits.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter TraitMigrationTests`
Expected: FAIL — no `AddTraits` migration exists yet (and `Trait`/`Polaridade` don't compile until Step 3-4 are saved; save those first, then this step should fail specifically on the missing migration).

- [ ] **Step 3: Write `Polaridade`**

`src/RuinaRPG.Domain/Enums/Polaridade.cs`:

```csharp
namespace RuinaRPG.Domain.Enums;

public enum Polaridade
{
    Positiva,
    Negativa
}
```

- [ ] **Step 4: Write `Trait`**

`src/RuinaRPG.Infrastructure/Rules/Trait.cs`:

```csharp
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Rules;

public class Trait
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Custo { get; set; }
    public Polaridade Polaridade { get; set; }
}
```

- [ ] **Step 5: Register the DbSet in `RuinaRpgDbContext`**

Add `using RuinaRPG.Infrastructure.Rules;` and:

```csharp
public DbSet<Trait> Traits => Set<Trait>();
```

No extra `OnModelCreating` configuration is needed — no unique constraint or FK on `Trait` yet (that arrives with `CharacterTraits` in a later plan).

- [ ] **Step 6: Create the migration**

```bash
dotnet ef migrations add AddTraits \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter TraitMigrationTests`
Expected: PASS (requires Docker running).

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Domain/Enums/Polaridade.cs src/RuinaRPG.Infrastructure/Rules/Trait.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/TraitMigrationTests.cs
git commit -m "feat: add Trait entity and migration"
```

---

### Task 9: Infrastructure — additive Traits startup seeder

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs`
- Modify: `src/RuinaRPG.Api/Program.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/TraitSeederTests.cs`

**Interfaces:**
- Consumes: `TraitSeedParser.Parse` (Task 7), `Trait`/`RuinaRpgDbContext.Traits` (Task 8).
- Produces: `TraitSeeder.SeedAsync(RuinaRpgDbContext db, string caracteristicasMarkdown) : Task<int>` (returns how many rows were inserted — used by the test and by the startup log line). Wired into `Program.cs` to run once at startup, after migrations are applied, reading the real embedded `Características.md` via `RulesDataProvider.ReadResource`.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Integration/Rules/TraitSeederTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class TraitSeederTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TraitSeederTests(PostgresFixture fixture) => _fixture = fixture;

    private const string Markdown = """
        # Positivas

        ### Alfabetizado

        1 ponto: Saber ler e escrever.

        # Negativas

        ### Alergia

        -1 ponto: o Personagem é alérgico a alguma coisa.
        """;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task SeedAsync_inserts_every_parsed_trait_into_an_empty_table()
    {
        await using var db = await NewDbAsync();

        var inserted = await TraitSeeder.SeedAsync(db, Markdown);

        inserted.Should().Be(2);
        (await db.Traits.CountAsync()).Should().Be(2);
        (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Custo.Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_is_additive_only_and_never_touches_an_existing_row()
    {
        await using var db = await NewDbAsync();
        await TraitSeeder.SeedAsync(db, Markdown);
        var existingId = (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Id;

        // Re-run against the SAME source: nothing new to insert, and the existing row's Id is untouched.
        var secondRunInserted = await TraitSeeder.SeedAsync(db, Markdown);

        secondRunInserted.Should().Be(0);
        (await db.Traits.CountAsync()).Should().Be(2);
        (await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado")).Id.Should().Be(existingId);
    }

    [Fact]
    public async Task SeedAsync_adds_a_new_row_alongside_an_existing_one_when_only_the_description_changed()
    {
        await using var db = await NewDbAsync();
        await TraitSeeder.SeedAsync(db, Markdown);

        const string revisedMarkdown = """
            # Positivas

            ### Alfabetizado

            1 ponto: Texto revisado, descrição diferente da original.

            # Negativas

            ### Alergia

            -1 ponto: o Personagem é alérgico a alguma coisa.
            """;

        var inserted = await TraitSeeder.SeedAsync(db, revisedMarkdown);

        // Matched by (Nome, Custo, Polaridade), not by Descricao — a changed description for the
        // SAME name+cost+polaridade is treated as the same trait already present, not re-inserted.
        inserted.Should().Be(0);
        (await db.Traits.CountAsync()).Should().Be(2);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter TraitSeederTests`
Expected: FAIL to compile — `TraitSeeder` doesn't exist yet.

- [ ] **Step 3: Write `TraitSeeder`**

`src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public static class TraitSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, string caracteristicasMarkdown)
    {
        var parsed = TraitSeedParser.Parse(caracteristicasMarkdown);
        var existing = await db.Traits
            .Select(t => new { t.Nome, t.Custo, t.Polaridade })
            .ToListAsync();
        var existingKeys = existing
            .Select(t => (t.Nome, t.Custo, Polaridade: t.Polaridade.ToString()))
            .ToHashSet();

        var toInsert = parsed
            .Where(seed => !existingKeys.Contains((seed.Nome, seed.Custo, seed.Polaridade)))
            .Select(seed => new Trait
            {
                Id = Guid.NewGuid(),
                Nome = seed.Nome,
                Descricao = seed.Descricao,
                Custo = seed.Custo,
                Polaridade = Enum.Parse<Polaridade>(seed.Polaridade)
            })
            .ToList();

        if (toInsert.Count == 0)
            return 0;

        db.Traits.AddRange(toInsert);
        await db.SaveChangesAsync();
        return toInsert.Count;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter TraitSeederTests`
Expected: PASS (3/3).

- [ ] **Step 5: Wire the seeder into startup**

In `src/RuinaRPG.Api/Program.cs`, find the existing Development-only automatic-migration block (added by the Foundation plan) and add the seed call right after migrations are applied, inside the same `if (app.Environment.IsDevelopment())`-guarded startup block if migrations are auto-applied there, **and** unconditionally reachable in Production too since Técnico R0009 only makes *migrations* an explicit `make migrate` step, not seeding — seeding an EMPTY, additive-only table on every boot is safe and cheap, so run it every startup regardless of environment, immediately after the app is built and before `app.Run()`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
    var caracteristicasMarkdown = RulesDataProvider.ReadResource("Caracteristicas.md");
    var insertedCount = await RuinaRPG.Infrastructure.Rules.TraitSeeder.SeedAsync(db, caracteristicasMarkdown);
    app.Logger.LogInformation("Trait seed: {InsertedCount} new row(s) inserted", insertedCount);
}
```

Place this after any existing `db.Database.MigrateAsync()` call (the Traits table must exist first) and before `app.Run()`.

- [ ] **Step 6: Run the whole integration suite to confirm startup still works**

Run: `dotnet test tests/RuinaRPG.Tests.Integration`
Expected: all existing tests still pass — `ApiFactory`-based tests boot the app through this new startup code path, so a mistake here would show up as every integration test failing, not just the new ones.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Integration/Rules/TraitSeederTests.cs
git commit -m "feat: seed Traits from caracteristicas.md additively at startup"
```

---

### Task 10: Domain — `CompendioSearchService`

**Files:**
- Create: `src/RuinaRPG.Domain/Rules/CompendioCategoria.cs`
- Create: `src/RuinaRPG.Domain/Rules/CompendioSearchResult.cs`
- Create: `src/RuinaRPG.Domain/Rules/CompendioSearchService.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Rules/CompendioSearchServiceTests.cs`

**Interfaces:**
- Consumes: `IRulesDataProvider` (Task 6, already defined in `RuinaRPG.Domain.Rules` — nothing new to add here), plus a `IReadOnlyList<TraitSeed>`-shaped list of Traits (the Api layer maps `Trait` DB rows to this before calling in, keeping `RuinaRPG.Domain` free of EF Core per Técnico R0002).
- Produces: `enum CompendioCategoria { Caracteristica, Efeito, Tabela, Regra }`; `CompendioSearchResult(CompendioCategoria Categoria, string Origem, string Titulo, string Conteudo)`; `CompendioSearchService.Search(string? query, IReadOnlyCollection<CompendioCategoria>? categorias, IReadOnlyList<TraitSeed> traits, IRulesDataProvider rules) : IReadOnlyList<CompendioSearchResult>`. Task 11 (Api endpoint) consumes this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Rules/CompendioSearchServiceTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class CompendioSearchServiceTests
{
    private static readonly IRulesDataProvider Rules = new RulesDataProvider();

    private static readonly IReadOnlyList<TraitSeed> Traits = new[]
    {
        new TraitSeed("Ambidestria", "Manuseia armas com as duas mãos.", 2, "Positiva"),
        new TraitSeed("Alergia", "É alérgico a alguma coisa.", -1, "Negativa")
    };

    [Fact]
    public void Search_with_no_query_and_no_category_filter_returns_results_from_all_four_categories()
    {
        var results = CompendioSearchService.Search(query: null, categorias: null, Traits, Rules);

        results.Select(r => r.Categoria).Distinct().Should()
            .BeEquivalentTo(new[] { CompendioCategoria.Caracteristica, CompendioCategoria.Efeito, CompendioCategoria.Tabela, CompendioCategoria.Regra });
    }

    [Fact]
    public void Search_by_text_matches_case_insensitively_across_title_and_content()
    {
        var results = CompendioSearchService.Search("ambidestria", categorias: null, Traits, Rules);

        results.Should().ContainSingle(r => r.Categoria == CompendioCategoria.Caracteristica && r.Titulo == "Ambidestria");
    }

    [Fact]
    public void Search_can_be_restricted_to_a_single_category()
    {
        var results = CompendioSearchService.Search(query: null, categorias: [CompendioCategoria.Caracteristica], Traits, Rules);

        results.Should().OnlyContain(r => r.Categoria == CompendioCategoria.Caracteristica);
        results.Should().HaveCount(2);
    }

    [Fact]
    public void Search_result_for_an_effect_names_its_grade_in_Origem()
    {
        var results = CompendioSearchService.Search("Aumentar Armadura", categorias: [CompendioCategoria.Efeito], Traits, Rules);

        results.Should().ContainSingle().Which.Origem.Should().Be("Efeito — 1º Grau/Círculo I");
    }

    [Fact]
    public void Search_result_for_a_table_row_names_the_table_and_level_in_Origem()
    {
        var results = CompendioSearchService.Search("Pontos de Atributo", categorias: [CompendioCategoria.Tabela], Traits, Rules);

        results.Should().Contain(r => r.Origem == "Tabela de Níveis — Nível 1");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter CompendioSearchServiceTests`
Expected: FAIL to compile — `CompendioCategoria`/`CompendioSearchResult`/`CompendioSearchService` don't exist yet.

- [ ] **Step 3: Write `CompendioCategoria` and `CompendioSearchResult`**

`src/RuinaRPG.Domain/Rules/CompendioCategoria.cs`:

```csharp
namespace RuinaRPG.Domain.Rules;

public enum CompendioCategoria
{
    Caracteristica,
    Efeito,
    Tabela,
    Regra
}
```

`src/RuinaRPG.Domain/Rules/CompendioSearchResult.cs`:

```csharp
namespace RuinaRPG.Domain.Rules;

public sealed record CompendioSearchResult(CompendioCategoria Categoria, string Origem, string Titulo, string Conteudo);
```

`IRulesDataProvider` (used as a parameter type below) is `RuinaRPG.Domain.Rules.IRulesDataProvider`, already defined in Task 6 — no new file or namespace work needed here, just consuming what Task 6 produced.

- [ ] **Step 4: Write `CompendioSearchService`**

`src/RuinaRPG.Domain/Rules/CompendioSearchService.cs`:

```csharp
namespace RuinaRPG.Domain.Rules;

public static class CompendioSearchService
{
    public static IReadOnlyList<CompendioSearchResult> Search(
        string? query,
        IReadOnlyCollection<CompendioCategoria>? categorias,
        IReadOnlyList<TraitSeed> traits,
        IRulesDataProvider rules)
    {
        var results = new List<CompendioSearchResult>();

        if (categorias is null || categorias.Contains(CompendioCategoria.Caracteristica))
        {
            results.AddRange(traits.Select(t =>
                new CompendioSearchResult(CompendioCategoria.Caracteristica, $"Característica {t.Polaridade}", t.Nome, $"{t.Custo} ponto(s): {t.Descricao}")));
        }

        if (categorias is null || categorias.Contains(CompendioCategoria.Efeito))
        {
            results.AddRange(rules.Efeitos.Select(e =>
                new CompendioSearchResult(CompendioCategoria.Efeito, $"Efeito — {e.Grau}º Grau/Círculo {ToRoman(e.Grau)}", e.Nome, e.Descricao)));
        }

        if (categorias is null || categorias.Contains(CompendioCategoria.Tabela))
        {
            results.AddRange(rules.Niveis.Select(n =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de Níveis — Nível {n.Nivel}", $"Nível {n.Nivel}", n.BonusText)));
            results.AddRange(rules.Vocacoes.Select(v =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de Vocação — {v.Vocacao}, Nível {v.Nivel}", v.Vocacao, $"Vida {v.Vida}, Arcana {v.Arcana}")));
            results.AddRange(rules.Arquetipos.Select(a =>
                new CompendioSearchResult(CompendioCategoria.Tabela, $"Tabela de Arquétipos — {a.Arquetipo}, Nível {a.Nivel}", a.Arquetipo, $"Vida {a.Vida}, Arcana {a.Arcana}")));
        }

        if (categorias is null || categorias.Contains(CompendioCategoria.Regra))
        {
            results.AddRange(rules.Regras.Select(r =>
                new CompendioSearchResult(CompendioCategoria.Regra, "Sistema Básico", r.Titulo, r.Conteudo)));
        }

        if (string.IsNullOrWhiteSpace(query))
            return results;

        return results
            .Where(r => r.Titulo.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || r.Conteudo.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static readonly string[] RomanNumerals = ["", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX"];
    private static string ToRoman(int grau) => grau >= 1 && grau <= 9 ? RomanNumerals[grau] : grau.ToString();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter CompendioSearchServiceTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/Rules/CompendioCategoria.cs src/RuinaRPG.Domain/Rules/CompendioSearchResult.cs src/RuinaRPG.Domain/Rules/CompendioSearchService.cs tests/RuinaRPG.Tests.Unit/Rules/CompendioSearchServiceTests.cs
git commit -m "feat: add compendio search combining traits, efeitos, tabelas and regras"
```

---

### Task 11: Api — `GET /api/compendio/search` (R0001, R0002, R0003, R0004)

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/CompendioSearchResultResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CompendioController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CompendioControllerTests.cs`

**Interfaces:**
- Consumes: `CompendioSearchService.Search` (Task 10), `RuinaRpgDbContext.Traits` (Task 8), `IRulesDataProvider` (Task 6).
- Produces: `GET /api/compendio/search?q=&categorias=` → `200` + `List<CompendioSearchResultResponse>`, `[Authorize]` (any authenticated role — R0004's "GM e jogadores têm acesso de leitura"). `CompendioSearchResultResponse(string Categoria, string Origem, string Titulo, string Conteudo)`.

- [ ] **Step 1: Write the response contract**

`src/RuinaRPG.Contracts/Rules/CompendioSearchResultResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record CompendioSearchResultResponse(string Categoria, string Origem, string Titulo, string Conteudo);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CompendioControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CompendioControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CompendioControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    [Fact]
    public async Task Search_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/compendio/search?q=ambidestria");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_finds_a_known_characteristic_by_name()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm1", "compendio1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search?q=Ambidestria", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Should().Contain(r => r.Categoria == "Caracteristica" && r.Titulo == "Ambidestria");
    }

    [Fact]
    public async Task Search_finds_a_known_effect_by_name()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm2", "compendio2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search?q=Aumentar+Armadura", token));

        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Should().Contain(r => r.Categoria == "Efeito" && r.Titulo == "Aumentar Armadura");
    }

    [Fact]
    public async Task Search_can_filter_to_a_single_category()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm3", "compendio3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search?categorias=Caracteristica", token));

        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Should().OnlyContain(r => r.Categoria == "Caracteristica");
    }

    [Fact]
    public async Task Search_with_no_query_and_no_filter_returns_results_from_multiple_categories()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm4", "compendio4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search", token));

        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Select(r => r.Categoria).Distinct().Should().HaveCountGreaterThan(1);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CompendioControllerTests`
Expected: FAIL — `/api/compendio/search` doesn't exist yet (404, not the expected 401/200).

- [ ] **Step 4: Write `CompendioController`**

`src/RuinaRPG.Api/Controllers/CompendioController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/compendio")]
[Authorize]
public class CompendioController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<List<CompendioSearchResultResponse>>> Search(
        [FromQuery] string? q,
        [FromQuery] List<CompendioCategoria>? categorias)
    {
        var traits = await db.Traits
            .Select(t => new TraitSeed(t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString()))
            .ToListAsync();

        var results = CompendioSearchService.Search(q, categorias, traits, rules);

        return results
            .Select(r => new CompendioSearchResultResponse(r.Categoria.ToString(), r.Origem, r.Titulo, r.Conteudo))
            .ToList();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CompendioControllerTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Rules src/RuinaRPG.Api/Controllers/CompendioController.cs tests/RuinaRPG.Tests.Integration/Controllers/CompendioControllerTests.cs
git commit -m "feat: add compendio search endpoint"
```

---

### Task 12: Client — Compêndio search page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/Compendio.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `CompendioSearchResultResponse` (Task 11), the authenticated `HttpClient` (already wired by the Convite de Jogador plan's `BearerTokenHandler`).
- Produces: the `/compendio` route. No later task depends on this file.

- [ ] **Step 1: Write the page**

`src/RuinaRPG.Client/Pages/Compendio.razor`:

```razor
@page "/compendio"
@inject HttpClient Http
@using RuinaRPG.Contracts.Rules

<h1>Compêndio de Regras</h1>

<div>
    <input @bind="_query" @bind:event="oninput" placeholder="Buscar..." />
    @foreach (var categoria in _categorias)
    {
        <label>
            <input type="checkbox" checked="@_selectedCategorias.Contains(categoria)"
                   @onchange="@(e => ToggleCategoria(categoria, (bool)e.Value!))" />
            @categoria
        </label>
    }
    <button @onclick="SearchAsync">Buscar</button>
</div>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<ul>
    @foreach (var result in _results)
    {
        <li>
            <strong>@result.Titulo</strong> — <em>@result.Origem</em>
            <p>@result.Conteudo</p>
        </li>
    }
</ul>

@code {
    private readonly string[] _categorias = ["Caracteristica", "Efeito", "Tabela", "Regra"];
    private readonly HashSet<string> _selectedCategorias = [];
    private string _query = "";
    private List<CompendioSearchResultResponse> _results = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => SearchAsync();

    private void ToggleCategoria(string categoria, bool isSelected)
    {
        if (isSelected)
            _selectedCategorias.Add(categoria);
        else
            _selectedCategorias.Remove(categoria);
    }

    private async Task SearchAsync()
    {
        var queryString = $"q={Uri.EscapeDataString(_query)}";
        foreach (var categoria in _selectedCategorias)
            queryString += $"&categorias={categoria}";

        var response = await Http.GetAsync($"compendio/search?{queryString}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível buscar no Compêndio.";
            return;
        }

        _results = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>() ?? new();
        _errorMessage = null;
    }
}
```

- [ ] **Step 2: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/compendio` following the file's established pattern (same CSS classes, same `<span class="bi bi-*-nav-menu" aria-hidden="true"></span>` icon structure as the existing links) — label it "Compêndio de Regras". This link is visible to both GM and Jogador (no role restriction), unlike the GM-only links added by the Convite de Jogador plan.

- [ ] **Step 3: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/Compendio.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add the compendio search page"
```

---

### Task 13: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files — this task only exercises what Tasks 1-12 built.

**Interfaces:**
- Consumes: the full stack, including everything this plan added.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers come up; migrations (including `AddTraits`) apply automatically in Development; the startup log shows a line like `Trait seed: 69 new row(s) inserted` (the exact count depends on the final state of `Características.md` — any positive count on first boot, and `0` on any subsequent restart, is the expected shape).

- [ ] **Step 2: Register a GM and get a token, through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/gm \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeCompendioGm","email":"smokecompendiogm@teste.com","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' \
  | tee /tmp/gm-register.json
TOKEN=$(jq -r .accessToken /tmp/gm-register.json)
```

Expected: HTTP 201 with `accessToken`/`refreshToken`.

- [ ] **Step 3: Search the Compêndio through nginx**

```bash
curl -sf "http://localhost/api/compendio/search?q=Ambidestria" -H "Authorization: Bearer $TOKEN"
```

Expected: HTTP 200 with a JSON array containing one result where `"categoria":"Caracteristica"` and `"titulo":"Ambidestria"`.

- [ ] **Step 4: Search by category filter through nginx**

```bash
curl -sf "http://localhost/api/compendio/search?categorias=Efeito" -H "Authorization: Bearer $TOKEN"
```

Expected: HTTP 200 with a JSON array where every entry has `"categoria":"Efeito"`.

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the compendio search flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Any consumption of `IRulesDataProvider` by Ficha de Personagem/Criaturas formulas (Vida/Arcana lookups, XP thresholds, level-up notice text) — those plans inject and use it, but building that consumption is their job, not this plan's.
- `CharacterTraits` (the join table linking a character sheet to selected Traits) — belongs to the Ficha de Personagem — Posses plan, which adds the FK against the `Trait.Id`s this plan seeds.
- Any reconciliation/cleanup tooling for stale Trait rows left behind by an edited source description (see the Global Constraints note on additive-only seeding) — flagged as a known, accepted limitation, not solved here.
- Full-text ranking/relevance scoring of search results — Task 10's `Search` does a simple case-insensitive substring match; this is sufficient for R0001-R0003 as written and avoids pulling in a search engine dependency for a low-traffic, small-corpus GM tool.
