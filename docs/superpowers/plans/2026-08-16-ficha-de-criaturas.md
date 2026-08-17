# Ficha de Criaturas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a GM build and manage a bestiary of Criatura sheets — a simplified variant of Ficha de Personagem: 6 attributes instead of 8 (Ego fuses Instinto/Vontade/Influência), a fixed **Rank** (F-S) replacing the whole Círculo/Grau/EAP progression, hybrid weapons (Catálogo-linked or hand-typed natural attacks), **Espólios** instead of Inventário, and computed **Kill**/**Assistência** XP-award values — plus the filterable "Bestiário do GM" listing (R0002).

**Architecture:** Same mirroring strategy as the Ficha de NPCs plan: `CreatureSheet` follows `NpcSheet`'s no-`CampaignId`/nullable-`OwnerId`/`GmId`-owned shape, but its **field set** differs from `CharacterSheet`/`NpcSheet` exactly where `Requisitos - Ficha de Criaturas.md` R0004-R0008 say it does (see Global Constraints) — this plan copies from the Personagem/NPC pattern only where the doc is silent, and builds new logic wherever it explicitly diverges. `AtributoCriatura` (6 values) is a **new, separate enum** from `Atributo` (8 values) — Modelo de Dados is explicit that Criatura "usa um enum próprio de 6 valores." `CreatureSkills.Pericia`, by contrast, **reuses** the full `Pericia` enum from the Personagem plans, restricted to ~20 allowed values by an application-level check, not a second enum (Modelo de Dados: "restrição de aplicação, não de schema"). `CreatureWeapon.ItemId` is nullable — when null, four `Manual*` columns carry a natural attack's data instead.

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Ficha de Criaturas.md` (all 8 requirements), `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (base structure), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §6.3.

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Every endpoint is `[Authorize(Roles = "GM")]`, scoped by `CreatureSheet.GmId == caller` — same GM-only model as `NpcSheet`, for the same reason (R0001).
- Field-by-field diffs from `CharacterSheet`/its child tables (source: R0004-R0008), binding for every task in this plan:
  - 1.a: no Linhagem/Variante/Vocação/SubVocação/Trabalho — replaced by `Raca` (string), `Arquetipo` (enum `Fisico`\|`Arcano`), `SubArquetipo` (string). `Afinidade` unchanged.
  - 1.b: no Círculo/Grau/EAPAtual/NucleosRank* — replaced by `Rank` (enum `F`,`E`,`D`,`C`,`B`,`A`,`S`). `PontosDeIgnicao` is a single int, not an Atual/Total pair. Gains computed (never persisted) `Kill = floor(ExperienciaAtual × 0.15)` and `Assistência = floor(ExperienciaAtual × 0.12)`.
  - 1.c: `Foco` is labeled "Arcana" on the wire but same formula; "Status de classe" comes from `Tabela de Arquetipos` (Arquétipo × Nível), not `Tabela de Vocação`/`Classes`. No `EstresseAtual`.
  - 2.a: 6 attributes (`AtributoCriatura`), not 8 — same Gasto/Bônus/Maestria/Total structure and formula.
  - 2.c: no Afinidades incremental list at all.
  - 2.d: `Pericia` reuses the full enum, restricted at the application layer to: `Acrobacia, ArtefatosMagicos, Atletismo, Brigar, EmpatiaComAnimais, Enganacao, ForcaDeVontade, Fortitude, Furtividade, Intimidacao, Intuicao, Investigacao, Navegacao, Ocultismo, Percepcao, Pontaria, Prontidao, Reflexos, Seducao, Sobrevivencia`.
  - 3.a: `CreatureWeapon.ItemId` nullable — null means a natural attack, carried by `ManualNome`/`ManualTipoDeDano`/`ManualDados`/`ManualDano` instead (no Alcance/Crítico/Tier/Durabilidade for those).
  - 4.a, 4.c, 4.d: no Habilidade Racial, no Contratos, no Runas.
  - 5.a: `Inventário` → **`Espólios`** — different shape (`Item`, `Qtd`, `Custo` read from the Item's Preço, `CustoTotal = Custo × Qtd`, `DT`); no `Ciclos` field on the root.
  - Everywhere a "6 attributes" dropdown is needed (Artefato `Alvo` when `TipoDeAlvo = Atributo`, Maestria's `Atributo`), it's `AtributoCriatura`, not the 8-value `Atributo`.
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Domain — Criatura-specific enums and XP-award calculator

**Files:**
- Create: `src/RuinaRPG.Domain/CreatureSheets/AtributoCriatura.cs`
- Create: `src/RuinaRPG.Domain/CreatureSheets/Arquetipo.cs`
- Create: `src/RuinaRPG.Domain/CreatureSheets/Rank.cs`
- Create: `src/RuinaRPG.Domain/CreatureSheets/CreatureSkillAllowList.cs`
- Create: `src/RuinaRPG.Domain/CreatureSheets/XpAwardCalculator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CreatureSheets/CreatureSkillAllowListTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CreatureSheets/XpAwardCalculatorTests.cs`

**Interfaces:**
- Consumes: `Pericia` (Ficha de Personagem — Atributos & Combate plan).
- Produces: `enum AtributoCriatura { Forca, Vigor, Agilidade, Destreza, Astucia, Ego }`; `enum Arquetipo { Fisico, Arcano }`; `enum Rank { F, E, D, C, B, A, S }`; `CreatureSkillAllowList.IsAllowed(Pericia pericia) : bool`; `XpAwardCalculator.Kill(int experienciaAtual) : int`, `.Assistencia(int experienciaAtual) : int`. Every later task in this plan depends on these.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/CreatureSheets/CreatureSkillAllowListTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Tests.Unit.CreatureSheets;

public class CreatureSkillAllowListTests
{
    [Theory]
    [InlineData(Pericia.Acrobacia, true)]
    [InlineData(Pericia.Atletismo, true)]
    [InlineData(Pericia.Sobrevivencia, true)]
    [InlineData(Pericia.Alquimia, false)] // not in the R0005 list
    [InlineData(Pericia.Biblioteca, false)]
    [InlineData(Pericia.Linguistica, false)]
    public void IsAllowed_matches_the_R0005_subset(Pericia pericia, bool expected)
    {
        CreatureSkillAllowList.IsAllowed(pericia).Should().Be(expected);
    }

    [Fact]
    public void IsAllowed_permits_exactly_20_pericias()
    {
        var allowedCount = Enum.GetValues<Pericia>().Count(CreatureSkillAllowList.IsAllowed);

        allowedCount.Should().Be(20);
    }
}
```

`tests/RuinaRPG.Tests.Unit/CreatureSheets/XpAwardCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Tests.Unit.CreatureSheets;

public class XpAwardCalculatorTests
{
    [Fact]
    public void Kill_is_15_percent_of_experiencia_atual_rounded_down()
    {
        // "Kill = piso(Experiência atual × 0,15)" — Ficha de Criaturas R0004.
        XpAwardCalculator.Kill(experienciaAtual: 100).Should().Be(15);
        XpAwardCalculator.Kill(experienciaAtual: 97).Should().Be(14); // 14.55 -> 14
    }

    [Fact]
    public void Assistencia_is_12_percent_of_experiencia_atual_rounded_down()
    {
        // "Assistência = piso(Experiência atual × 0,12)" — Ficha de Criaturas R0004.
        XpAwardCalculator.Assistencia(experienciaAtual: 100).Should().Be(12);
        XpAwardCalculator.Assistencia(experienciaAtual: 97).Should().Be(11); // 11.64 -> 11
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~CreatureSkillAllowListTests|FullyQualifiedName~XpAwardCalculatorTests"`
Expected: FAIL to compile.

- [ ] **Step 3: Write the enums**

`src/RuinaRPG.Domain/CreatureSheets/AtributoCriatura.cs`:

```csharp
namespace RuinaRPG.Domain.CreatureSheets;

public enum AtributoCriatura
{
    Forca,
    Vigor,
    Agilidade,
    Destreza,
    Astucia,
    Ego
}
```

`src/RuinaRPG.Domain/CreatureSheets/Arquetipo.cs`:

```csharp
namespace RuinaRPG.Domain.CreatureSheets;

public enum Arquetipo
{
    Fisico,
    Arcano
}
```

`src/RuinaRPG.Domain/CreatureSheets/Rank.cs`:

```csharp
namespace RuinaRPG.Domain.CreatureSheets;

public enum Rank
{
    F,
    E,
    D,
    C,
    B,
    A,
    S
}
```

- [ ] **Step 4: Write `CreatureSkillAllowList`**

`src/RuinaRPG.Domain/CreatureSheets/CreatureSkillAllowList.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.CreatureSheets;

public static class CreatureSkillAllowList
{
    private static readonly HashSet<Pericia> Allowed =
    [
        Pericia.Acrobacia, Pericia.ArtefatosMagicos, Pericia.Atletismo, Pericia.Brigar,
        Pericia.EmpatiaComAnimais, Pericia.Enganacao, Pericia.ForcaDeVontade, Pericia.Fortitude,
        Pericia.Furtividade, Pericia.Intimidacao, Pericia.Intuicao, Pericia.Investigacao,
        Pericia.Navegacao, Pericia.Ocultismo, Pericia.Percepcao, Pericia.Pontaria,
        Pericia.Prontidao, Pericia.Reflexos, Pericia.Seducao, Pericia.Sobrevivencia
    ];

    public static bool IsAllowed(Pericia pericia) => Allowed.Contains(pericia);
}
```

- [ ] **Step 5: Write `XpAwardCalculator`**

`src/RuinaRPG.Domain/CreatureSheets/XpAwardCalculator.cs`:

```csharp
namespace RuinaRPG.Domain.CreatureSheets;

public static class XpAwardCalculator
{
    public static int Kill(int experienciaAtual) => (int)Math.Floor(experienciaAtual * 0.15);

    public static int Assistencia(int experienciaAtual) => (int)Math.Floor(experienciaAtual * 0.12);
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~CreatureSkillAllowListTests|FullyQualifiedName~XpAwardCalculatorTests"`
Expected: PASS (6/6 and 2/2).

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Domain/CreatureSheets tests/RuinaRPG.Tests.Unit/CreatureSheets
git commit -m "feat: add creature enums, skill allow-list, and xp award calculator"
```

---

### Task 2: Infrastructure — `CreatureSheet` root entity, migration, and GM-only CRUD

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CreatureSheets/CreatureSheet.cs`
- Create: `src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetResponse.cs`
- Create: `src/RuinaRPG.Contracts/CreatureSheets/UpdateCreatureSheetRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CreatureSheetMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `AfinidadeElemental`, `Cobertura` (Ficha de Personagem plans — unchanged for Criatura), `Arquetipo`, `Rank` (Task 1), `XpAwardCalculator` (Task 1).
- Produces: `CreatureSheet` (`Guid Id`, `Guid GmId`, `Guid? OwnerId`, `Guid? ImageId`, `string? Nome`, `string? Raca`, `Arquetipo? Arquetipo`, `string? SubArquetipo`, `AfinidadeElemental? Afinidade`, `string? Propriedade`, `Rank? Rank`, `int Nivel = 1`, `int ExperienciaAtual`, `int PontosDeIgnicao`, `int VitalidadeAtual`, `int FocoAtual`, `int AdrenalinaAtual`, `Cobertura Cobertura`, `int? LastDismissedLevelUpLevel`). `RuinaRpgDbContext.CreatureSheets`. `POST/GET/PUT/DELETE /api/creature-sheets[/{id}]`, all `[Authorize(Roles = "GM")]`, scoped to `GmId`. `CreatureSheetResponse` includes computed `Kill`/`Assistencia` (never persisted, same pattern as `CharacterSheetResponse.Graduacao`).

- [ ] **Step 1: Write the entity**

`src/RuinaRPG.Infrastructure/CreatureSheets/CreatureSheet.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSheet
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? ImageId { get; set; }
    public string? Nome { get; set; }
    public string? Raca { get; set; }
    public Arquetipo? Arquetipo { get; set; }
    public string? SubArquetipo { get; set; }
    public AfinidadeElemental? Afinidade { get; set; }
    public string? Propriedade { get; set; }
    public Rank? Rank { get; set; }
    public int Nivel { get; set; } = 1;
    public int ExperienciaAtual { get; set; }
    public int PontosDeIgnicao { get; set; }
    public int VitalidadeAtual { get; set; }
    public int FocoAtual { get; set; }
    public int AdrenalinaAtual { get; set; }
    public Cobertura Cobertura { get; set; }
    public int? LastDismissedLevelUpLevel { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CreatureSheetMigrationTests.cs` — same structure as `NpcSheetMigrationTests` (Ficha de NPCs plan, Task 1), asserting a migration named `AddCreatureSheets`.

- [ ] **Step 3: Register the DbSet, create the migration, verify the test passes**

Add `public DbSet<CreatureSheet> CreatureSheets => Set<CreatureSheet>();` and, in `OnModelCreating`, the same GM/Owner/Image FK triple `NpcSheet` uses (Ficha de NPCs plan, Task 1, Step 3).

```bash
dotnet ef migrations add AddCreatureSheets --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CreatureSheetMigrationTests` — expect PASS.

- [ ] **Step 4: Write the contracts**

`src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureSheetResponse(
    string Id, string? OwnerId, string? ImageUrl, string? Nome, string? Raca, string? Arquetipo, string? SubArquetipo,
    string? Afinidade, string? Propriedade, string? Rank, int Nivel, int ExperienciaAtual, int Kill, int Assistencia,
    int PontosDeIgnicao, int VitalidadeAtual, int FocoAtual, int AdrenalinaAtual, string Cobertura);
```

`src/RuinaRPG.Contracts/CreatureSheets/UpdateCreatureSheetRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CreatureSheets;

public record UpdateCreatureSheetRequest(
    string? ImageId, string? Nome, string? Raca, string? Arquetipo, string? SubArquetipo, string? Afinidade,
    string? Propriedade, string? Rank, int Nivel, int ExperienciaAtual, int PontosDeIgnicao,
    int VitalidadeAtual, int FocoAtual, int AdrenalinaAtual, string Cobertura);
```

- [ ] **Step 5: Write the failing controller tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs` — mirror `NpcSheetsControllerTests` (Ficha de NPCs plan, Task 1): create-returns-201-with-Nivel-1, create-by-jogador-returns-403, get/update/delete-by-owning-GM-succeeds, update/delete-by-a-different-GM-returns-404, PLUS one Criatura-specific case:

```csharp
[Fact]
public async Task Get_computes_Kill_and_Assistencia_from_ExperienciaAtual()
{
    // create, then PUT with ExperienciaAtual: 100, then GET and assert Kill == 15, Assistencia == 12.
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CreatureSheetsControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 7: Write `CreatureSheetsController`**

Mirror `NpcSheetsController`'s `Create`/`Get`/`Update`/`Delete` (Ficha de NPCs plan, Task 1) exactly, adding `Kill`/`Assistencia` computation via `XpAwardCalculator` in the response-mapping method.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CreatureSheetsControllerTests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Infrastructure/CreatureSheets src/RuinaRPG.Contracts/CreatureSheets src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration
git commit -m "feat: add creature sheet root entity and GM-only CRUD"
```

---

### Task 3: Infrastructure — mirror child tables, with the R0004-R0008 diffs applied

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CreatureSheets/CreatureAttribute.cs`, `CreatureSkill.cs`, `CreatureWeapon.cs`, `CreatureArmorSlot.cs`, `CreatureShield.cs`, `CreatureSpellAbility.cs`, `CreatureSpellAbilityEffect.cs`, `CreatureMastery.cs`, `CreatureSpoil.cs`, `CreatureArtifact.cs`, `CreatureAffection.cs`, `CreatureTrait.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CreatureChildTableMigrationTests.cs`

**Interfaces:**
- Consumes: `CreatureSheet` (Task 2), `AtributoCriatura` (Task 1), `Pericia` (Ficha de Personagem — Atributos & Combate plan, reused as-is).
- Produces: 12 entities (2 fewer than NPCs' 14 — no `CreatureAffinity`, no `CreatureRune`), each FK'd to `CreatureSheet`. `CreatureAttribute.Atributo` is `AtributoCriatura`, not `Atributo`. `CreatureMastery.Atributo` is likewise `AtributoCriatura`. `CreatureWeapon` gains 4 nullable `Manual*` columns and makes `ItemId` nullable. `CreatureSpoil` replaces `CreatureInventoryItem` with a different shape (`Custo`, `CustoTotal`, `DT` instead of a live Peso-based total). `RuinaRpgDbContext.CreatureAttributes`/`CreatureSkills`/`CreatureWeapons`/`CreatureArmorSlots`/`CreatureShields`/`CreatureSpellAbilities`/`CreatureSpellAbilityEffects`/`CreatureMasteries`/`CreatureSpoils`/`CreatureArtifacts`/`CreatureAffections`/`CreatureTraits`. Task 4 depends on every shape exactly.

- [ ] **Step 1: Write the mechanically-mirrored entities (identical shape, retargeted FK, Atributo type swapped)**

Copy from the Ficha de NPCs plan's Task 2 table, applying the same rename pattern (`Npc*`→`Creature*`, `NpcSheetId`→`CreatureSheetId`) for: `NpcSkill`→`CreatureSkill`, `NpcArmorSlot`→`CreatureArmorSlot`, `NpcShield`→`CreatureShield`, `NpcSpellAbility`/`NpcSpellAbilityEffect`→`CreatureSpellAbility`/`CreatureSpellAbilityEffect`, `NpcArtifact`→`CreatureArtifact`, `NpcAffection`→`CreatureAffection`, `NpcTrait`→`CreatureTrait`. For `CreatureAttribute` and `CreatureMastery`, additionally change the `Atributo` property's type from `Atributo` to `AtributoCriatura`.

- [ ] **Step 2: Write `CreatureWeapon` (hybrid: Catálogo-linked or manual)**

`src/RuinaRPG.Infrastructure/CreatureSheets/CreatureWeapon.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureWeapon
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Guid? ItemId { get; set; } // null = natural attack, uses the Manual* fields below instead
    public bool IsEquipped { get; set; }
    public int? DurabilidadeAtual { get; set; } // null for natural attacks — they have no durability
    public string? ManualNome { get; set; }
    public TipoDeDano? ManualTipoDeDano { get; set; }
    public string? ManualDados { get; set; }
    public int? ManualDano { get; set; }
}
```

- [ ] **Step 3: Write `CreatureSpoil` (replaces Inventário)**

`src/RuinaRPG.Infrastructure/CreatureSheets/CreatureSpoil.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSpoil
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
    public int DT { get; set; }
}
```

(`Custo`/`CustoTotal` are read live from the linked `Item.Preco` × `Qtd`, same "live reference" pattern as `CharacterInventoryItem`'s `Peso`/`Total` — no columns needed for them.)

- [ ] **Step 4: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CreatureChildTableMigrationTests.cs` — one assertion per table (12 total), following the Ficha de NPCs plan's `NpcChildTableMigrationTests` pattern, PLUS a specific case proving the weapon hybrid model: insert one `CreatureWeapon` with `ItemId: null` and `ManualNome`/`ManualTipoDeDano`/`ManualDados`/`ManualDano` set, confirm it round-trips without a `Item` FK violation (there's nothing to violate — the FK itself must be nullable).

- [ ] **Step 5: Run the test to verify it fails, register all 12 DbSets and relationships, create the migration, verify it passes**

Mirror the Ficha de NPCs plan's `OnModelCreating` additions, with `CreatureWeapon.ItemId`'s FK configured `IsRequired(false)` and `OnDelete(DeleteBehavior.Restrict)`; `CreatureSpoil.ItemId`'s FK `Restrict` (not nullable — Espólios always reference a real Catálogo item, per R0008: "vinculado a um item do Catálogo").

```bash
dotnet ef migrations add AddCreatureChildTables --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CreatureChildTableMigrationTests` — expect all assertions PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Infrastructure/CreatureSheets src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/CreatureChildTableMigrationTests.cs
git commit -m "feat: add creature sheet child tables with the personagem diffs applied"
```

---

### Task 4: Api — mirror every child-table controller, with the diffs applied

**Files:**
- Create: `src/RuinaRPG.Contracts/CreatureSheets/*` (mirroring the NPC contracts, minus Affinity/Rune, plus the weapon/spoil diffs)
- Create: `src/RuinaRPG.Api/Controllers/CreatureAttributesController.cs`, `CreatureSkillsController.cs`, `CreatureArsenalController.cs`, `CreatureSpellAbilitiesController.cs`, `CreatureMasteriesController.cs`, `CreaturePossessionsController.cs`
- Test: one integration test file per controller

**Interfaces:**
- Consumes: every entity from Task 3, `AttributeTotalCalculator`/`SkillFormulas`/`SpellAbilityCostCalculator` (reused unmodified — they're generic over plain numbers), `CreatureSkillAllowList` (Task 1).
- Produces: every route the Ficha de NPCs plan's Task 3 built, minus affinities/runes, under `api/creature-sheets/{sheetId}/...`, with these behavior diffs on top of the mechanical Npc→Creature rename:
  - `CreatureSkillsController.Update` returns `400` when the target `Pericia` fails `CreatureSkillAllowList.IsAllowed` — the ~20-skill restriction is enforced here, in the application layer, exactly as Modelo de Dados specifies.
  - `CreatureArsenalController`'s weapon endpoints accept EITHER `ItemId` (links to a Catálogo `Arma`, same as `CharacterArsenalController`) OR the four `Manual*` fields (a natural attack) — exactly one of the two, same validation shape as `CharacterSpellAbilitiesController.Add`'s "exactly one of `SourceBankEntryId` or from-scratch" check (Magias/Posses/Diário plan, Task 4).
  - `CreaturePossessionsController`'s spoils endpoints (`POST/GET/DELETE .../spoils`) compute `Custo`/`CustoTotal` live from the linked Item's `Preco`, and accept a `DT` field the equivalent Personagem/NPC inventory endpoint doesn't have.
  - `CreatureSpellAbilitiesController.Add` still inserts an independent `SpellAbilityBankEntry` copy (Banco de Magias R0001 applies to Criatura sheets too, same as NPC).

- [ ] **Step 1: Write every contract**

Mirror the NPC contracts (Ficha de NPCs plan, Task 3, Step 1) for Attribute (using `AtributoCriatura`), Skill, SpellAbility, Mastery (using `AtributoCriatura`), Artifact, Affection, Trait/TraitsList. New shapes for weapons and spoils:

`AddCreatureWeaponRequest(string? ItemId, string? ManualNome, string? ManualTipoDeDano, string? ManualDados, int? ManualDano)`, `CreatureWeaponResponse(string Id, string? ItemId, string Nome, string? TipoDeDano, string? Dados, int? Dano, int? Alcance, string? Critico, string? Tier, bool IsEquipped, int? DurabilidadeAtual, int? DurabilidadeMaximo)` (the last 4 fields are `null` for a natural attack).

`AddCreatureSpoilRequest(string ItemId, int Qtd, int DT)`, `CreatureSpoilResponse(string Id, string ItemId, string Nome, int Custo, int Qtd, int CustoTotal, int DT)`.

- [ ] **Step 2: Write the failing tests, one file per controller**

Mirror the Ficha de NPCs plan's Task 3, Step 2 pattern (GM-only setup, cross-GM-404 case instead of jogador-403), PLUS these Criatura-specific cases:

```csharp
// CreatureSkillsControllerTests
[Fact]
public async Task Update_a_disallowed_Pericia_returns_400()
// e.g. PUT .../skills/Alquimia (not in the R0005 subset) → 400.

// CreatureArsenalControllerTests
[Fact]
public async Task AddWeapon_from_the_catalogo_links_the_item_same_as_a_character()

[Fact]
public async Task AddWeapon_manual_creates_a_natural_attack_with_no_durability_or_alcance()

[Fact]
public async Task AddWeapon_with_both_ItemId_and_manual_fields_returns_400()

[Fact]
public async Task AddWeapon_with_neither_ItemId_nor_manual_fields_returns_400()

// CreaturePossessionsControllerTests
[Fact]
public async Task AddSpoil_computes_CustoTotal_from_the_items_Preco_and_Qtd()
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Creature"`
Expected: FAIL — none of the routes exist yet.

- [ ] **Step 4: Write every controller**

Mirror the Ficha de NPCs plan's Task 3, Step 4 controllers, applying the diffs from this task's Interfaces block. For `CreatureArsenalController.AddWeapon`:

```csharp
[HttpPost("weapons")]
public async Task<ActionResult<CreatureWeaponResponse>> AddWeapon(Guid sheetId, AddCreatureWeaponRequest request)
{
    var authError = await CheckGmOwnershipAsync(sheetId);
    if (authError is not null) return authError;

    var isFromCatalogo = request.ItemId is not null;
    var isManual = request.ManualNome is not null && request.ManualTipoDeDano is not null && request.ManualDados is not null && request.ManualDano is not null;
    if (isFromCatalogo == isManual)
        return BadRequest("Informe exatamente um: ItemId (vincular ao Catálogo) ou os 4 campos manuais (ataque natural).");

    var weapon = new CreatureWeapon { Id = Guid.NewGuid(), CreatureSheetId = sheetId, IsEquipped = false };
    if (isFromCatalogo)
    {
        var itemId = Guid.Parse(request.ItemId!);
        var item = await db.Set<Arma>().FirstOrDefaultAsync(a => a.Id == itemId);
        if (item is null) return BadRequest("Item de arma não encontrado.");
        weapon.ItemId = itemId;
        weapon.DurabilidadeAtual = item.DurabilidadeMaxima ?? 0;
    }
    else
    {
        if (!Enum.TryParse<TipoDeDano>(request.ManualTipoDeDano, out var tipoDeDano))
            return BadRequest("Tipo de Dano desconhecido.");
        weapon.ManualNome = request.ManualNome;
        weapon.ManualTipoDeDano = tipoDeDano;
        weapon.ManualDados = request.ManualDados;
        weapon.ManualDano = request.ManualDano;
    }

    db.CreatureWeapons.Add(weapon);
    await db.SaveChangesAsync();
    return Created(string.Empty, await ToWeaponResponseAsync(weapon));
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Creature"`
Expected: PASS across all test files.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/CreatureSheets src/RuinaRPG.Api/Controllers/Creature*.cs tests/RuinaRPG.Tests.Integration/Controllers/Creature*.cs
git commit -m "feat: mirror every character sheet child-table controller for creature sheets, with diffs"
```

---

### Task 5: Api — Bestiário listing with filters (R0002)

**Files:**
- Create: `src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetSummaryResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `CreatureSheetsController` (Task 2).
- Produces: `GET /api/creature-sheets?nome=&raca=&arquetipo=&rank=` → `200` + `List<CreatureSheetSummaryResponse>`, scoped to the caller's own creatures. Same "Campanha vinculada is a no-op until Campanha — Anexos" caveat as the Ficha de NPCs plan's Task 4. `CreatureSheetSummaryResponse(string Id, string Nome, string? Raca, string? Arquetipo, string? Rank, int Nivel)`.

- [ ] **Step 1: Write the contract, failing tests, and the `List` action**

Follow the exact structure of the Ficha de NPCs plan's Task 4 (contract → 2 failing tests → `List` action filtering on `Nome`/`Raca`/`Arquetipo`/`Rank` → green). No new patterns here.

- [ ] **Step 2: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CreatureSheetsControllerTests`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetSummaryResponse.cs src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs
git commit -m "feat: add creature sheet listing with filters"
```

---

### Task 6: Client — Bestiário do GM list page and sheet page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/BestiarioDoGm.razor`
- Create: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: every Contracts type from Tasks 2, 4-5, the authenticated `HttpClient`.
- Produces: the `/bestiario` and `/criaturas/{id}` routes. No later task depends on these files.

- [ ] **Step 1: Write the list page**

`src/RuinaRPG.Client/Pages/BestiarioDoGm.razor` — same shape as `NpcsDoGm.razor` (Ficha de NPCs plan, Task 5), filter bar for Nome/Raça/Arquétipo/Rank.

- [ ] **Step 2: Write the sheet page**

`src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` — same structure as `FichaDeNpc.razor`, adapted for the field diffs (Raça/Arquétipo/SubArquétipo instead of Linhagem/Variante/Vocação/SubVocação; Rank instead of Círculo/Grau; Kill/Assistência displayed read-only; 6-attribute dropdowns everywhere `AtributoCriatura` applies; weapon form offers both "vincular ao Catálogo" and "ataque natural" inputs; Espólios section instead of Inventário, with a DT column; no Afinidades, Habilidade Racial, Contratos, or Runas sections).

- [ ] **Step 3: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/bestiario` following the file's established pattern — label it "Bestiário do GM".

- [ ] **Step 4: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Pages/BestiarioDoGm.razor src/RuinaRPG.Client/Pages/FichaDeCriatura.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add bestiario do gm list and sheet pages"
```

---

### Task 7: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files.

**Interfaces:**
- Consumes: the full stack. Produces: nothing new.

- [ ] **Step 1: Boot the stack, register a GM through nginx**

- [ ] **Step 2: Create a Criatura, set Rank and ExperienciaAtual, through nginx**

```bash
curl -sf -X POST http://localhost/api/creature-sheets -H "Authorization: Bearer $GM_TOKEN"
# → CREATURE_ID
curl -sf -X PUT "http://localhost/api/creature-sheets/$CREATURE_ID" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d '{"imageId":null,"nome":"Goblin Batedor","raca":"Goblin","arquetipo":"Fisico","subArquetipo":"Batedor","afinidade":null,"propriedade":"","rank":"E","nivel":3,"experienciaAtual":100,"pontosDeIgnicao":10,"vitalidadeAtual":20,"focoAtual":5,"adrenalinaAtual":10,"cobertura":"Nenhuma"}'
```

- [ ] **Step 3: Confirm Kill/Assistência compute, through nginx**

```bash
curl -sf "http://localhost/api/creature-sheets/$CREATURE_ID" -H "Authorization: Bearer $GM_TOKEN"
```

Expected: `"kill":15,"assistencia":12`.

- [ ] **Step 4: Add a natural-attack weapon and confirm it has no durability, through nginx**

```bash
curl -sf -X POST "http://localhost/api/creature-sheets/$CREATURE_ID/weapons" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d '{"itemId":null,"manualNome":"Garra","manualTipoDeDano":"Cortante","manualDados":"1D6","manualDano":1}'
```

Expected: HTTP 201, `"durabilidadeAtual":null`.

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify creature sheet flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- R0003 (jogador visibility of Nome/Imagem via campaign attachment) — same `CampaignAttachments` dependency as the Ficha de NPCs plan.
- Filtering by "Campanha vinculada" (R0002) — same dependency, same no-op-until-then treatment.
- Granting a Criatura sheet to a player (Campanha R0010) — Campanha — Anexos plan's job; `OwnerId` exists but nothing sets it here.
- The Rank↔Grau equivalence for capping a Criatura's Magia/Habilidade Grau (R0007) — explicitly "pendente" in the spec itself ("a equivalência exata entre Rank e Grau máximo ainda não está definida nas regras"); `CreatureSpellAbilitiesController` accepts any `Grau` without a Rank-based ceiling.
- Resistência Física/Arcana, Dano Cortante (2.b) — explicitly "pendente" / "sem fórmula definida ainda" in the spec.
