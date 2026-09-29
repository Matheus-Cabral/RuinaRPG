# Durabilidade por Rank Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the free-typed Durabilidade of Arma/Armadura/Escudo with a durability derived from the item's Rank (F–SS) through a global, Auditor-editable Rank → Durabilidade table.

**Architecture:** The max durability is no longer stored on the item; it is resolved on read from `Rank` + the `DurabilidadesPorRank` table (seeded from a new Tabela in `Docs/Sistema RPG/`). Sheets already store only `DurabilidadeAtual` and read the max live, so they follow automatically; atual is capped on read/save. The Arma's `Tier` becomes `Rank` (new enum `RankDeItem`, with SS).

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8 + Npgsql, Blazor WASM + MudBlazor, xUnit + FluentAssertions + bUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-29-durabilidade-por-rank-design.md`

## Global Constraints

- `dotnet build` must end with **0 warnings, 0 errors**. TDD mandatory (failing test first).
- Initial table, verbatim: F 20, E 45, D 80, C 125, B 180, A 245, S Inquebrável, SS Inquebrável.
- `RankDeItem` values in order `F, E, D, C, B, A, S, SS` (SS appended; stored as int — existing Tier ints keep their meaning).
- NULL Rank = no durability (max NULL, not inquebrável). Inquebrável = max NULL + `Inquebravel = true`.
- Atual on add = `Maxima ?? 0`; atual shown/saved = `Math.Clamp(atual, 0, Maxima ?? 0)`; raising the max never refills atual.
- Criatura `Rank` enum (`RuinaRPG.Domain.CreatureSheets.Rank`) is **not** touched.
- UI label is "Rank" (never "Tier") for Arma/Armadura/Escudo. Sheet text for unbreakable items: "Inquebrável".
- The table does not appear in the Livro de Regras.
- Auditoria endpoints: GET open to any authenticated user; PUT only for the Rules Auditor (DB check `ApplicationUser.IsRulesAuditor`, 403 otherwise), like `HistoricosController`.
- Enums cross the wire as strings. All user-facing text in Brazilian Portuguese. `Docs/` edits use `[[wikilinks]]` and continue R-number sequences.
- The integration suite fills the nearly-full disk if run whole: run it with `--filter` in chunks (e.g. by `FullyQualifiedName~Items`, `~Arsenal`, `~Equipagem`/`~EquipmentKit`, `~Persistence`, `~Durabilidade`).

## Review Focus

- An Arma whose Tier was set before this change keeps the same letter as Rank after the migration (int values preserved) — Task 2 migration test.
- An Auditor lowering a Rank's value below a sheet's current atual: the sheet shows the capped value, and the next atual save can't exceed it — Task 3 test.
- Marking a Rank Inquebrável while sheets hold items of that Rank: sheets show "Inquebrável", atual updates are ignored (stored 0) — Task 3 test.
- A Rank with no durability number and not Inquebrável can't be saved on the Auditoria (Durabilidade ≥ 1 required) — Task 4 test.
- The Equipagem grant of an Armadura/Escudo with no Rank gives atual 0 without error — Task 3 test.

---

### Task 1: Domain — RankDeItem, durability resolver, Tabela

**Files:**
- Rename: `src/RuinaRPG.Domain/Items/Tier.cs` → `src/RuinaRPG.Domain/Items/RankDeItem.cs` (enum `RankDeItem { F, E, D, C, B, A, S, SS }`); update every reference to the type `Tier` across `src/` and `tests/` (type only — **property names stay `Tier` in this task**; they're renamed in Task 2/3).
- Create: `src/RuinaRPG.Domain/Items/DurabilidadeDeRank.cs`, `src/RuinaRPG.Domain/Items/DurabilidadeDeItem.cs`, `src/RuinaRPG.Domain/Rules/TabelaDeDurabilidadeParser.cs`
- Create: `Docs/Sistema RPG/Tabela de Durabilidade por Rank.md`
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj` (embed the Tabela as `LogicalName="Tabela de Durabilidade por Rank.md"`, next to the other `EmbeddedResource` lines)
- Test: `tests/RuinaRPG.Tests.Unit/Items/DurabilidadeDeItemTests.cs`, `tests/RuinaRPG.Tests.Unit/Rules/TabelaDeDurabilidadeParserTests.cs`

**Interfaces (produced):**
- `enum RankDeItem { F, E, D, C, B, A, S, SS }` (namespace `RuinaRPG.Domain.Items`)
- `sealed record DurabilidadeDeRank(RankDeItem Rank, int? Durabilidade, bool Inquebravel)`
- `static class DurabilidadeDeItem { (int? Maxima, bool Inquebravel) Resolver(RankDeItem? rank, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela); int LimitarAtual(int atual, int? maxima); }`
- `static class TabelaDeDurabilidadeParser { IReadOnlyList<DurabilidadeDeRank> Parse(string markdown); }`

- [ ] **Step 1: Write the Tabela file** (`Docs/Sistema RPG/Tabela de Durabilidade por Rank.md`):

```markdown
Durabilidade máxima de Armas, Armaduras e Escudos conforme o Rank do item. Itens sem Rank não têm durabilidade. Os valores podem ser ajustados pelo Auditor de Regras (ver [[Requisitos - Auditoria de Regras]]).

| Rank | Durabilidade |
| ---- | ------------ |
| F    | 20           |
| E    | 45           |
| D    | 80           |
| C    | 125          |
| B    | 180          |
| A    | 245          |
| S    | Inquebrável  |
| SS   | Inquebrável  |
```

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/RuinaRPG.Tests.Unit/Items/DurabilidadeDeItemTests.cs
using FluentAssertions;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class DurabilidadeDeItemTests
{
    private static readonly Dictionary<RankDeItem, DurabilidadeDeRank> Tabela = new()
    {
        [RankDeItem.F] = new(RankDeItem.F, 20, false),
        [RankDeItem.S] = new(RankDeItem.S, null, true),
    };

    [Fact]
    public void No_rank_means_no_durability() =>
        DurabilidadeDeItem.Resolver(null, Tabela).Should().Be(((int?)null, false));

    [Fact]
    public void A_numeric_rank_resolves_its_value() =>
        DurabilidadeDeItem.Resolver(RankDeItem.F, Tabela).Should().Be(((int?)20, false));

    [Fact]
    public void An_unbreakable_rank_has_no_max_and_is_inquebravel() =>
        DurabilidadeDeItem.Resolver(RankDeItem.S, Tabela).Should().Be(((int?)null, true));

    [Fact]
    public void A_rank_missing_from_the_table_means_no_durability() =>
        DurabilidadeDeItem.Resolver(RankDeItem.C, Tabela).Should().Be(((int?)null, false));

    [Theory]
    [InlineData(10, 20, 10)]
    [InlineData(30, 20, 20)]
    [InlineData(-5, 20, 0)]
    [InlineData(7, null, 0)]
    public void LimitarAtual_clamps_between_zero_and_the_max(int atual, int? maxima, int esperado) =>
        DurabilidadeDeItem.LimitarAtual(atual, maxima).Should().Be(esperado);
}
```

```csharp
// tests/RuinaRPG.Tests.Unit/Rules/TabelaDeDurabilidadeParserTests.cs
using FluentAssertions;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class TabelaDeDurabilidadeParserTests
{
    [Fact]
    public void Parses_the_real_tabela()
    {
        var markdown = File.ReadAllText(Path.Combine(RepoRoot(), "Docs", "Sistema RPG", "Tabela de Durabilidade por Rank.md"));

        TabelaDeDurabilidadeParser.Parse(markdown).Should().Equal(
            new DurabilidadeDeRank(RankDeItem.F, 20, false),
            new DurabilidadeDeRank(RankDeItem.E, 45, false),
            new DurabilidadeDeRank(RankDeItem.D, 80, false),
            new DurabilidadeDeRank(RankDeItem.C, 125, false),
            new DurabilidadeDeRank(RankDeItem.B, 180, false),
            new DurabilidadeDeRank(RankDeItem.A, 245, false),
            new DurabilidadeDeRank(RankDeItem.S, null, true),
            new DurabilidadeDeRank(RankDeItem.SS, null, true));
    }

    [Fact]
    public void Throws_on_an_unknown_rank_or_value() =>
        FluentActions.Invoking(() => TabelaDeDurabilidadeParser.Parse("| Rank | Durabilidade |\n|---|---|\n| Z | 10 |"))
            .Should().Throw<FormatException>();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RuinaRPG.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
```
(If other unit tests already read `Docs/` files through a helper, reuse that helper instead of `RepoRoot()`.)

- [ ] **Step 3: Run to verify they fail** — `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~DurabilidadeDeItem|FullyQualifiedName~TabelaDeDurabilidade"` → build FAIL.

- [ ] **Step 4: Implement**

```csharp
// RankDeItem.cs
namespace RuinaRPG.Domain.Items;

/// <summary>Rank de Arma, Armadura e Escudo (antes "Tier"). Gravado como inteiro — SS acrescentado no fim.
/// Não confundir com o Rank de Criatura (RuinaRPG.Domain.CreatureSheets.Rank).</summary>
public enum RankDeItem
{
    F,
    E,
    D,
    C,
    B,
    A,
    S,
    SS
}
```

```csharp
// DurabilidadeDeRank.cs
namespace RuinaRPG.Domain.Items;

/// <summary>Uma linha da Tabela de Durabilidade por Rank: Durabilidade nula quando Inquebrável.</summary>
public sealed record DurabilidadeDeRank(RankDeItem Rank, int? Durabilidade, bool Inquebravel);
```

```csharp
// DurabilidadeDeItem.cs
namespace RuinaRPG.Domain.Items;

/// <summary>
/// Durabilidade máxima de Arma/Armadura/Escudo derivada do Rank (Tabela de Durabilidade por Rank).
/// Sem Rank = sem durabilidade; Rank inquebrável = sem máximo e Inquebravel.
/// </summary>
public static class DurabilidadeDeItem
{
    public static (int? Maxima, bool Inquebravel) Resolver(RankDeItem? rank, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        if (rank is null || !tabela.TryGetValue(rank.Value, out var linha))
            return (null, false);
        return linha.Inquebravel ? (null, true) : (linha.Durabilidade, false);
    }

    /// <summary>A durabilidade atual nunca passa do máximo (nem fica negativa); sem máximo, é 0.</summary>
    public static int LimitarAtual(int atual, int? maxima) => Math.Clamp(atual, 0, maxima ?? 0);
}
```

```csharp
// TabelaDeDurabilidadeParser.cs
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Domain.Rules;

/// <summary>Lê a tabela "| Rank | Durabilidade |" de Tabela de Durabilidade por Rank.md.</summary>
public static class TabelaDeDurabilidadeParser
{
    public static IReadOnlyList<DurabilidadeDeRank> Parse(string markdown)
    {
        var linhas = new List<DurabilidadeDeRank>();
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|'))
                continue;
            var cells = line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 2 || cells[0] == "Rank" || cells[0].StartsWith('-'))
                continue;

            if (!Enum.TryParse<RankDeItem>(cells[0], out var rank) || !Enum.IsDefined(rank))
                throw new FormatException($"Rank desconhecido na Tabela de Durabilidade: {cells[0]}");

            if (cells[1].Equals("Inquebrável", StringComparison.OrdinalIgnoreCase))
                linhas.Add(new DurabilidadeDeRank(rank, null, true));
            else if (int.TryParse(cells[1], out var valor) && valor >= 1)
                linhas.Add(new DurabilidadeDeRank(rank, valor, false));
            else
                throw new FormatException($"Durabilidade inválida para o Rank {cells[0]}: {cells[1]}");
        }
        return linhas;
    }
}
```

Rename the `Tier` type everywhere: `grep -rln "\bTier\b" src tests --include=*.cs --include=*.razor` — change only type usages (`Tier?`, `Tier.F`, `Enum.TryParse<Tier>`, `using`), leave member names (`.Tier`, `Tier =`, record params named `Tier`) for later tasks. EF migrations' designer files reference the CLR type by name — **do not edit existing migration files**; the snapshot is regenerated in Task 2.

- [ ] **Step 5: Run tests** — the two new classes PASS; `dotnet build` 0 warnings; `dotnet test tests/RuinaRPG.Tests.Unit` and `tests/RuinaRPG.Tests.Client` all green.

- [ ] **Step 6: Commit** — `git commit -m "feat(domain): RankDeItem (com SS) e durabilidade por rank"`

---

### Task 2: Persistence — DurabilidadesPorRank table, Rank columns, seeder

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Items/DurabilidadePorRank.cs`, `src/RuinaRPG.Infrastructure/Items/DurabilidadePorRankSeeder.cs`
- Modify: `Arma.cs` (`Tier` → `Rank`), `Armadura.cs`/`Escudo.cs` (add `RankDeItem? Rank`), `Rules/EquipmentKitChoiceSlot.cs` (`Tier` → `Rank`), `Persistence/RuinaRpgDbContext.cs`, `src/RuinaRPG.Api/Program.cs` (seed in both the `--migrate` branch and the Development branch, after `EquipmentKitSeeder`), plus every C# reference to the renamed properties (API controllers, `EquipmentKitGrantService`, `EquipmentKitSeeder`/`SeedData`, `DefaultCatalogItems`, tests). **Contracts keep their `Tier` parameter names in this task** (wire rename is Task 3) — map `Rank` ↔ contract `Tier` in controllers for now.
- Create: migration `AddDurabilidadePorRank`
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/DurabilidadePorRankMigrationTests.cs`

**Interfaces (produced):**
- `class DurabilidadePorRank { RankDeItem Rank (PK); int? Durabilidade; bool Inquebravel; }` — `DbSet<DurabilidadePorRank> DurabilidadesPorRank`.
- `static class DurabilidadePorRankSeeder { Task<int> SeedAsync(RuinaRpgDbContext db, string markdown); }` — inserts missing ranks from `TabelaDeDurabilidadeParser.Parse(markdown)`, never overwrites existing rows; returns inserted count.
- `Arma.Rank`, `Armadura.Rank`, `Escudo.Rank`, `EquipmentKitChoiceSlot.Rank` — all `RankDeItem?`.
- `DurabilidadeMaxima` properties **still exist** after this task (removed in Task 3).

- [ ] **Step 1: Failing test** (model on an existing `Persistence/*MigrationTests.cs`):
  - after `MigrateAsync`, `GetAppliedMigrationsAsync()` contains `...AddDurabilidadePorRank`;
  - `DurabilidadePorRankSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Durabilidade por Rank.md"))` returns 8 on an empty table and 0 on a second call; the rows equal the Tabela (F 20 … SS Inquebrável);
  - an edited row (`Durabilidade = 99`) survives a second seed;
  - an `Arma` saved with `Rank = RankDeItem.C` and an `Armadura`/`Escudo` saved with `Rank = RankDeItem.SS` round-trip.
  For the "Tier preserved" review-focus item: the migration must use `RenameColumn` (not drop+add) for Arma's Tier column and for `EquipmentKitChoiceSlots.Tier`. Assert it by inspecting the generated migration in review, and in the test by inserting a row through raw SQL into the pre-migration shape is not needed — the `RenameColumn` in the migration is the check.

- [ ] **Step 2: Run → FAIL** (build).

- [ ] **Step 3: Implement.**
  - Entity + `DbSet`; `builder.Entity<DurabilidadePorRank>().HasKey(d => d.Rank);`
  - Item TPH columns: Subcategoria is mapped with explicit per-type column names (see `RuinaRpgDbContext` ~line 156). Do the same for Rank: `Arma_Rank`, `Armadura_Rank`, `Escudo_Rank` — and check what column name Arma's `Tier` has today (look at the model snapshot) so the migration **renames** it to `Arma_Rank` rather than dropping it. Edit the generated migration by hand if EF emits drop+add.
  - `EquipmentKitChoiceSlot`: rename column `Tier` → `Rank` (RenameColumn).
  - Seeder:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Items;

/// <summary>
/// Insere as linhas da Tabela de Durabilidade por Rank que ainda faltam — nunca sobrescreve uma linha
/// existente, porque o Auditor de Regras pode tê-la editado (mesmo tratamento do HistoricoSeeder).
/// </summary>
public static class DurabilidadePorRankSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, string markdown)
    {
        var existentes = await db.DurabilidadesPorRank.Select(d => d.Rank).ToListAsync();
        var novas = TabelaDeDurabilidadeParser.Parse(markdown).Where(l => !existentes.Contains(l.Rank)).ToList();
        foreach (var linha in novas)
            db.DurabilidadesPorRank.Add(new DurabilidadePorRank { Rank = linha.Rank, Durabilidade = linha.Durabilidade, Inquebravel = linha.Inquebravel });
        await db.SaveChangesAsync();
        return novas.Count;
    }
}
```
  - `Program.cs`: in both seed blocks, `var ... = await DurabilidadePorRankSeeder.SeedAsync(db, RulesDataProvider.ReadResource("Tabela de Durabilidade por Rank.md"));` + a log line like the others. Check `tests/RuinaRPG.Tests.Integration/ApiFactory.cs`: if integration tests don't run in Development (so seeders don't run), make the factory seed the durability table after migrating, the same way it handles other seeded catalogs (read how Históricos/Efeitos get into the test DB and follow that).
  - `dotnet ef migrations add AddDurabilidadePorRank --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`; inspect: CreateTable DurabilidadesPorRank, RenameColumn ×2, AddColumn ×2 (Armadura_Rank, Escudo_Rank), nothing else.
  - Modelo de Dados doc: new table section; Arma `Tier` → `Rank` (F–SS); Armadura/Escudo `Rank`; `EquipmentKitChoiceSlots.Rank`. (The Durabilidade column rows are removed in Task 3.)

- [ ] **Step 4: Run** the new test + `--filter "FullyQualifiedName~Persistence|FullyQualifiedName~Item|FullyQualifiedName~Equip"` → PASS; build 0 warnings.

- [ ] **Step 5: Commit** — `git commit -m "feat(db): tabela de Durabilidade por Rank e Rank em Arma/Armadura/Escudo"`

---

### Task 3: API — resolved durability everywhere; drop DurabilidadeMaxima

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Items/DurabilidadePorRankProvider.cs` (Scoped; register in `Program.cs`)
- Modify contracts: `Items/CreateItemRequest.cs`, `UpdateItemRequest.cs` (remove `DurabilidadeMaxima`; rename `Tier` → `Rank`), `Items/ItemResponse.cs` (`Tier` → `Rank`; keep `int? DurabilidadeMaxima` as resolved; append `bool Inquebravel = false`), the 9 sheet arsenal responses under `Contracts/{CharacterSheets,NpcSheets,CreatureSheets}` (`Tier` → `Rank` where present; append `bool Inquebravel = false`), `Rules/CreateEquipmentKitRequest.cs` + `EquipmentKitResponse.cs` (`Tier` → `Rank`).
- Modify: `ItemsController`, `CampaignCatalogController`, `Character/Npc/CreatureArsenalController`, `Character/NpcEquipagemController` (if they read durability), `EquipmentKitsController`, `Infrastructure/Rules/EquipmentKitGrantService.cs`, `CampaignGrantsController` (copies atual only — verify nothing reads the item max).
- Remove: `DurabilidadeMaxima` from `Arma`, `Armadura`, `Escudo`; migration `RemoveDurabilidadeMaximaDosItens`.
- Modify: `Infrastructure/Items/DefaultCatalogItems.cs` (drop any DurabilidadeMaxima assignment; keep Rank).
- Modify: every test constructing the changed contracts (`grep -rln "CreateItemRequest(\|UpdateItemRequest(\|ItemResponse(\|DurabilidadeMaxima" tests`).
- Modify: `Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md`, `Requisitos - Modelo de Dados.md`.
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/DurabilidadePorRankItemsTests.cs` (+ extend arsenal/equipagem tests).

**Interfaces:**
- Consumes: Task 1 `DurabilidadeDeItem`, Task 2 `DurabilidadesPorRank`/`Rank` properties.
- Produces: `class DurabilidadePorRankProvider(RuinaRpgDbContext db) { Task<IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank>> TabelaAsync(); Task<(int? Maxima, bool Inquebravel)> ResolverAsync(RankDeItem? rank); }` — loads the table once per instance (cache the dictionary in a field).
- Wire: `ItemResponse.Rank`, `ItemResponse.DurabilidadeMaxima` (resolved), `ItemResponse.Inquebravel`; sheet responses `Rank`, capped `DurabilidadeAtual`, resolved max, `Inquebravel`.

- [ ] **Step 1: Failing tests** (copy the auth/setup helpers from `ItemsControllerTests` and `CharacterArsenalControllerTests`):
  - Create Arma/Armadura/Escudo with `Rank = "D"` → response `Rank == "D"`, `DurabilidadeMaxima == 80`, `Inquebravel == false`; with `Rank = "SS"` → `DurabilidadeMaxima == null`, `Inquebravel == true`; with no Rank → `null`/`false`. Unknown Rank string → 400. Rank on an ItemGeral/Artefato is ignored or 400 — follow how `Tier` on a non-Arma is handled today and keep that behaviour.
  - Add a Rank-C weapon to a character sheet → `DurabilidadeAtual == 125`, `DurabilidadeMaxima == 125`.
  - Lower Rank C's value directly in the DB to 50 (the Auditoria API arrives in Task 4) → the sheet's weapon list shows `DurabilidadeAtual == 50`; PUT atual 120 → stored/returned 50.
  - Mark Rank C `Inquebravel = true` in the DB → the sheet shows `Inquebravel == true`; PUT atual 10 → returned 0.
  - Equipagem: granting a kit whose Armadura has no Rank → atual 0, no error; a kit Arma of Rank F → atual 20. (Extend the existing Equipagem grant tests.)
  - NPC and Criatura arsenal: one test each for resolved max + Inquebrável.

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement.**
  - Provider (Infrastructure, so `EquipmentKitGrantService` can use it):

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Items;

/// <summary>Tabela de Durabilidade por Rank carregada uma vez por escopo; resolve o máximo de um item.</summary>
public class DurabilidadePorRankProvider(RuinaRpgDbContext db)
{
    private IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank>? _tabela;

    public async Task<IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank>> TabelaAsync() =>
        _tabela ??= await db.DurabilidadesPorRank.AsNoTracking()
            .ToDictionaryAsync(d => d.Rank, d => new DurabilidadeDeRank(d.Rank, d.Durabilidade, d.Inquebravel));

    public async Task<(int? Maxima, bool Inquebravel)> ResolverAsync(RankDeItem? rank) =>
        DurabilidadeDeItem.Resolver(rank, await TabelaAsync());
}
```
  - Every `item.DurabilidadeMaxima` read becomes `(await provider.ResolverAsync(item.Rank)).Maxima`; every `Math.Min(x, item.DurabilidadeMaxima ?? 0)` becomes `DurabilidadeDeItem.LimitarAtual(x, maxima)`; responses return `LimitarAtual(stored atual, maxima)` and the `Inquebravel` flag. Where a list maps many items, call `TabelaAsync()` once and use `DurabilidadeDeItem.Resolver` per item (no per-item await).
  - Inquebrável: atual update stores 0.
  - `EquipmentKitGrantService`: `EquipmentGrantPlanItem.DurabilidadeMaxima` is computed via the provider (inject it; register the service's dependency).
  - Remove the three `DurabilidadeMaxima` properties + mapping; generate `RemoveDurabilidadeMaximaDosItens` (DropColumn ×3 only).
  - Contracts: apply the renames/removals above. Update all tests that construct them (mechanical: drop the `DurabilidadeMaxima` argument, rename named `Tier:` args to `Rank:`).
  - Docs:
    - Catálogo de Itens: in the Arma section, "Tier" → **Rank** with values F, E, D, C, B, A, S, SS; Armadura and Escudo sections get a **Rank** field (same dropdown, opcional); each "Durabilidade" bullet becomes: "*Durabilidade*: não é mais digitada — vem do Rank, conforme a [[Tabela de Durabilidade por Rank]] (editável pelo Auditor de Regras); sem Rank, o item não tem durabilidade; Rank inquebrável → a ficha mostra 'Inquebrável'." Update the section-32 summary line ("Tier / Categoria") accordingly.
    - Modelo de Dados: remove the DurabilidadeMaxima rows of the three item types.

- [ ] **Step 4: Run** the new tests and the chunked regression filters `~Item`, `~Arsenal`, `~Equip`, `~CampaignCatalog`, `~CampaignGrants`, `~Persistence`; `dotnet test tests/RuinaRPG.Tests.Unit`; build 0 warnings. (Client tests may fail to compile if they construct changed contracts — fix those constructions here; UI behaviour changes are Task 5.)

- [ ] **Step 5: Commit** — `git commit -m "feat(api): durabilidade dos itens vem do Rank"`

---

### Task 4: Auditoria — API and page for Durabilidade por Rank

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/DurabilidadePorRankResponse.cs` (`record DurabilidadePorRankResponse(string Rank, int? Durabilidade, bool Inquebravel)`), `UpdateDurabilidadePorRankRequest.cs` (`record UpdateDurabilidadePorRankRequest(int? Durabilidade, bool Inquebravel)`)
- Create: `src/RuinaRPG.Api/Controllers/DurabilidadesPorRankController.cs`
- Create: `src/RuinaRPG.Client/Pages/AuditoriaDurabilidadePorRank.razor`
- Modify: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor` (new `MudNavLink Href="auditoria/durabilidade-por-rank"` "Durabilidade por Rank", after Equipagem)
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md` (new R, next number)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/DurabilidadesPorRankControllerTests.cs`, `tests/RuinaRPG.Tests.Client/Pages/AuditoriaDurabilidadePorRankTests.cs`

**Interfaces:** `GET api/durabilidades-por-rank` → `List<DurabilidadePorRankResponse>` ordered F…SS; `PUT api/durabilidades-por-rank/{rank}` → 204.

- [ ] **Step 1: Failing tests.**
  - Integration (copy the Rules-Auditor setup from `HistoricosControllerTests` — how a test user becomes auditor):
    - GET as any authenticated GM/Jogador → 8 rows, `F` 20 … `SS` null/true.
    - PUT `C` `{ Durabilidade: 130, Inquebravel: false }` as auditor → 204, GET shows 130.
    - PUT `A` `{ Durabilidade: null, Inquebravel: true }` → stored null/true; PUT `S` `{ Durabilidade: 300, Inquebravel: false }` → 300/false.
    - PUT `{ Durabilidade: null, Inquebravel: false }` → 400; `{ Durabilidade: 0, Inquebravel: false }` → 400; unknown rank `Z` → 404 (or 400 — pick one, document it); non-auditor GM → 403.
    - `Inquebravel: true` with a Durabilidade value → stored Durabilidade null.
  - bUnit: page renders 8 rows with labels F…SS; toggling "Inquebrável" on a row disables its number and PUTs `{ null, true }`; editing a number PUTs it; the ⓘ popup has title "Como funciona a Durabilidade por Rank". Follow `AuditoriaHistoricos`' test patterns (FakeHttpMessageHandler, autosave waits).

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement.**
  - Controller: `[Authorize]`, route `api/durabilidades-por-rank`, `RequireRulesAuditorAsync()` copied from `HistoricosController`. On PUT: parse rank strictly (`Enum.TryParse` + `IsDefined`); find row (404 if missing); `Inquebravel` → `Durabilidade = null`; else require `Durabilidade >= 1` (400 `"Informe a durabilidade (mínimo 1) ou marque Inquebrável."`).
  - Page `/auditoria/durabilidade-por-rank`: same layout, breadcrumbs and access guard as `AuditoriaHistoricos.razor` (read it first; non-auditors see what they see there). `Section Title="Durabilidade por Rank"` with `TitleInfo` → `InfoPopup Title="Como funciona a Durabilidade por Rank"`: "A durabilidade máxima de Armas, Armaduras e Escudos vem do Rank do item, conforme esta tabela. Marque Inquebrável para que os itens daquele Rank nunca quebrem — a ficha mostra 'Inquebrável' e não controla a durabilidade atual. Itens sem Rank não têm durabilidade. Ao reduzir um valor, a durabilidade atual das fichas que passar do novo máximo é limitada a ele; ao aumentar, ela não é recarregada." Table: Rank | Durabilidade (`MudNumericField<int?>` Min 1, disabled when inquebrável) | Inquebrável (`MudCheckBox`); save per row on change (debounced like the other Auditoria pages' autosave) and show server errors in a `DismissibleAlert`.
  - Docs: new R in Auditoria de Regras: "O Auditor de Regras edita a Durabilidade por Rank." — the 8 fixed ranks (F–SS), number or Inquebrável, no create/delete, initial values from [[Tabela de Durabilidade por Rank]], effect on sheets (cap when lowered, no refill when raised), not shown in the Livro de Regras.

- [ ] **Step 4: Run** new tests + `tests/RuinaRPG.Tests.Client` fully; build 0 warnings.

- [ ] **Step 5: Commit** — `git commit -m "feat: Auditoria de Durabilidade por Rank"`

---

### Task 5: Client — Rank in the Catálogo and Inquebrável on the sheets

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`, `Catalogo.razor`, `AuditoriaEquipagem.razor`, `FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor` (+ any other client file that shows `Tier` or `DurabilidadeMaxima`: `grep -rn "Tier\|DurabilidadeMaxim" src/RuinaRPG.Client --include=*.razor --include=*.cs`)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (3.a–3.c durability bullets: Inquebrável display)
- Test: extend `tests/RuinaRPG.Tests.Client/Pages/CatalogoItemFormTests.cs` (or the existing catalog form test file), plus a sheet-level test if one exists for the arsenal tables; otherwise add `tests/RuinaRPG.Tests.Client/Pages/FichaArsenalDurabilidadeTests.cs` rendering the relevant sheet section with a stubbed HTTP client.

- [ ] **Step 1: Failing tests.**
  - Catalog form for an Arma: there is **no** "Durabilidade" input; a select labelled "Rank" offers F, E, D, C, B, A, S, SS; choosing "D" shows "Durabilidade: 80" (from the stubbed `durabilidades-por-rank` GET); choosing "S" shows "Inquebrável"; clearing shows "Sem durabilidade". Same Rank select exists for Armadura and Escudo, not for Item Geral/Artefato. Saving sends `Rank`.
  - Catalog list: column header "Rank", not "Tier".
  - AuditoriaEquipagem: the choice-slot filter label reads "Rank" and offers SS.
  - Sheet arsenal: a weapon with `Inquebravel = true` renders "Inquebrável" and no atual input; a normal one renders atual/max as today.

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** the UI changes above. The resolved-durability hint in the form reads the table once on init (`GET durabilidades-por-rank`) and resolves locally with `RuinaRPG.Domain.Items.DurabilidadeDeItem.Resolver`. Update Ficha de Personagem 3.a–3.c doc bullets: Durabilidade máxima vem do Rank do item ([[Tabela de Durabilidade por Rank]]); itens de Rank inquebrável mostram "Inquebrável" sem durabilidade atual. (NPC/Criatura docs inherit from Ficha de Personagem — add a note only if they restate durability.)

- [ ] **Step 4: Run** `tests/RuinaRPG.Tests.Client` fully + build 0 warnings.

- [ ] **Step 5: Commit** — `git commit -m "feat(client): Rank no Catálogo e durabilidade inquebrável nas fichas"`

---

### Task 6: Full verification

- [ ] `dotnet build` → 0/0. Unit + Client suites. Integration suite in chunks (see Global Constraints) — every chunk green; rerun any single failure in isolation (known ~2-3% timeout flake).
- [ ] `grep -rn "\bTier\b" src --include=*.cs --include=*.razor | grep -v Migrations` → no remaining user-facing "Tier" (only historical migration files may mention it).
- [ ] `grep -rn "DurabilidadeMaxima" src --include=*.cs | grep -v Migrations` → only resolved/response usages, no item property.
