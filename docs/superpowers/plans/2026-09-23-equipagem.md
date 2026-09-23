# Equipagem (Initial Equipment Kits) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a player pick one starting-equipment kit (from `Docs/Sistema RPG/Equipagem.md`) on their Personagem/NPC sheet, once, with every item landing in its proper place automatically; give the Rules Auditor a CRUD page over the kit catalog; add an "Equipagem" tab to the Livro de Regras that mirrors that catalog live.

**Architecture:** `EquipmentKit`/`EquipmentKitItem`/`EquipmentKitChoiceSlot` are new **global** tables (audited like `Historico`), but `Item` rows are owned per-GM — so kit rows reference items by Nome/Tipo (fixed) or by a Tipo+Subcategoria+Tier filter (choice slots), resolved against the calling sheet's campaign GM's own catalog at apply time, never by a stored ItemId. A shared `EquipmentKitGrantService` does that resolution (and auto-creates a missing `ItemGeral`) for both Character and Npc controllers, which each own their own per-Tipo insert and campaign-attachment steps.

**Tech Stack:** ASP.NET Core 8 / EF Core / PostgreSQL, Blazor WebAssembly 8 + MudBlazor, xUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-23-equipagem-design.md`

## Global Constraints

- TDD mandatory (CLAUDE.md R0011): write the failing test, watch it fail for the right reason, then the minimal code to pass.
- `EquipmentKitItem.Tipo`/`EquipmentKitChoiceSlot.Tipo` never accept `Armadura` — reject with a clear message wherever they're set (no kit today needs it, and per-piece armor slots don't fit this model).
- Every fixed/choice item resolution is scoped to the calling sheet's **campaign GM's own** `Items` (never a global item lookup) — `Item.GmId` is the partition key everywhere.
- `EquipmentKit`/`EquipmentKitItem`/`EquipmentKitChoiceSlot` are global tables (no GmId) — CRUD is gated by `RequireRulesAuditorAsync()` (live DB check via `ApplicationUser.IsRulesAuditor`, same as `HistoricosController`), not a JWT claim.
- `CharacterSheet.EquipmentKitId`/`NpcSheet.EquipmentKitId`: null = button shows; non-null = one-time choice already made, second `POST .../equipagem/choose` returns 400.
- CreatureSheet is never touched by this feature (Espólios is loot, not starting gear).
- All new positional-record fields are appended at the end of existing records — never inserted mid-list — and every construction call site (including target-typed `new(...)` in tests) is found via grep, not memory.

---

### Task 1: `Requisitos - Modelo de Dados.md` — new tables

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`

**Interfaces:**
- Produces: none (doc-only prerequisite).

- [ ] **Step 1: Read the file's existing `CharacterSheets`/`NpcSheets` table sections and the legend at the top (referenced by Historico's own entry) to match its exact formatting conventions.**

- [ ] **Step 2: Add a new section documenting 3 new tables and 2 new columns.** Insert it near the existing `Historicos` table section (same legend conventions — cópia independente / live catalog reference / live-link), with this content:

```markdown
### EquipmentKits

Catálogo global (não por GM) de kits de equipamento inicial, cadastrado a partir de "[[Equipagem]]" e editável pelo Auditor de Regras (ver "[[Requisitos - Auditoria de Regras]]").

- `Id` (PK)
- `Nome` (string, obrigatório)
- `Descricao` (string, obrigatório — o parágrafo de sabor do kit)
- `Ciclos` (int — moeda concedida ao escolher o kit)
- `IsDeleted` (bool — soft delete)

### EquipmentKitItems

Linhas fixas de um `EquipmentKit` — referenciam um Item do catálogo do GM por **Nome + Tipo**, nunca por Id (cada GM tem sua própria cópia do catálogo de Itens).

- `Id` (PK)
- `KitId` (FK → EquipmentKits, cascade)
- `Nome` (string — Nome do Item alvo no catálogo do GM)
- `Tipo` (enum ItemTipo — ItemGeral, Arma, Escudo ou Artefato; nunca Armadura)
- `Qtd` (int)
- `SubcategoriaHint` (string?, opcional — usado só quando o Item precisa ser criado automaticamente no catálogo do GM por não existir ainda)

### EquipmentKitChoiceSlots

Linhas de escolha do jogador de um `EquipmentKit` (ex: "1 Arma Rank F de sua escolha"), resolvidas ao vivo contra o catálogo do GM no momento de aplicar o kit.

- `Id` (PK)
- `KitId` (FK → EquipmentKits, cascade)
- `Label` (string — ex: "Arma", "Condutor")
- `Tipo` (enum ItemTipo — sempre Arma nos dados de seed atuais)
- `SubcategoriasCsv` (string?, opcional — lista de Subcategoria aceitas separadas por vírgula; NULL = qualquer uma)
- `Tier` (enum Tier?, opcional — NULL = qualquer Tier)
- `Qtd` (int)
- `BonusSubcategoria` (string?, opcional — Subcategoria do item escolhido que ativa um bônus condicional)
- `BonusNome` (string?, opcional — Item concedido além da escolha, só se `BonusSubcategoria` bater)
- `BonusQtd` (int?, opcional)

### Novas colunas

- `CharacterSheets.EquipmentKitId` (Guid?, FK → EquipmentKits, SetNull) — NULL até o jogador escolher um kit; depois disso, permanente (não pode ser trocado).
- `NpcSheets.EquipmentKitId` (Guid?, FK → EquipmentKits, SetNull) — mesmo comportamento.
```

- [ ] **Step 3: Commit.**

```bash
git add "Docs/Requisitos/Requisitos - Modelo de Dados.md"
git commit -m "docs: modelo de dados do sistema de Equipagem"
```

---

### Task 2: New `Tônico` items in `DefaultCatalogItems`

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Items/DefaultCatalogItems.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Items/DefaultCatalogItemsTests.cs` (create if it doesn't already exist — check first)

**Interfaces:**
- Produces: `DefaultCatalogItems.Build(gmId)` now also returns two `ItemGeral` rows named "Tônico de Vida simples" and "Tônico de Foco simples" (Subcategoria "Poções e Tônicos").

- [ ] **Step 1: Check for an existing test file.** Run: `find tests -iname "DefaultCatalogItemsTests.cs"`. If found, read it and add the test there; if not, create the file below.

- [ ] **Step 2: Write the failing test.**

```csharp
using FluentAssertions;
using RuinaRPG.Infrastructure.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class DefaultCatalogItemsTests
{
    [Fact]
    public void Build_includes_the_two_simple_Tonico_items_used_by_Equipagem_kits()
    {
        var items = DefaultCatalogItems.Build(Guid.NewGuid());

        items.OfType<ItemGeral>().Should().Contain(i => i.Nome == "Tônico de Vida simples" && i.Subcategoria == "Poções e Tônicos");
        items.OfType<ItemGeral>().Should().Contain(i => i.Nome == "Tônico de Foco simples" && i.Subcategoria == "Poções e Tônicos");
    }
}
```

- [ ] **Step 3: Run test to verify it fails.**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~DefaultCatalogItemsTests -v`
Expected: FAIL — the two items aren't present yet.

- [ ] **Step 4: Add the two items.** Open `DefaultCatalogItems.cs`, find the `ItemGeral` block (near the other `Subcategoria = "Materiais de Estudo & Rituais"`/`"Alimentação"` entries) and add, right after the existing `Ração de Viagem` entries:

```csharp
            new ItemGeral { Nome = "Tônico de Vida simples", Subcategoria = "Poções e Tônicos", Descricao = "Restaura uma pequena quantidade de Vitalidade" },
            new ItemGeral { Nome = "Tônico de Foco simples", Subcategoria = "Poções e Tônicos", Descricao = "Restaura uma pequena quantidade de Foco" },
```

- [ ] **Step 5: Run test to verify it passes.**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~DefaultCatalogItemsTests -v`
Expected: PASS

- [ ] **Step 6: Run the full unit suite to confirm no regression.**

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: all pass.

- [ ] **Step 7: Commit.**

```bash
git add src/RuinaRPG.Infrastructure/Items/DefaultCatalogItems.cs tests/RuinaRPG.Tests.Unit/Items/DefaultCatalogItemsTests.cs
git commit -m "feat: adiciona Tônico de Vida/Foco simples ao catálogo padrão"
```

---

### Task 3: Entities, DbContext, migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/EquipmentKit.cs`
- Create: `src/RuinaRPG.Infrastructure/Rules/EquipmentKitItem.cs`
- Create: `src/RuinaRPG.Infrastructure/Rules/EquipmentKitChoiceSlot.cs`
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs`
- Modify: `src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Create: migration under `src/RuinaRPG.Infrastructure/Persistence/Migrations/`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/EquipmentKitSchemaTests.cs`

**Interfaces:**
- Produces: `EquipmentKit { Id, Nome, Descricao, Ciclos, IsDeleted }`; `EquipmentKitItem { Id, KitId, Nome, Tipo (ItemTipo), Qtd, SubcategoriaHint }`; `EquipmentKitChoiceSlot { Id, KitId, Label, Tipo (ItemTipo), SubcategoriasCsv, Tier (Tier?), Qtd, BonusSubcategoria, BonusNome, BonusQtd }`; `CharacterSheet.EquipmentKitId`/`NpcSheet.EquipmentKitId` (`Guid?`); `RuinaRpgDbContext.EquipmentKits`/`EquipmentKitItems`/`EquipmentKitChoiceSlots` DbSets.

- [ ] **Step 1: Write the failing integration test.**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EquipmentKitSchemaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public EquipmentKitSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task EquipmentKit_with_a_fixed_item_and_a_choice_slot_round_trips()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit de Teste", Descricao = "Descrição de teste", Ciclos = 5 };
        db.EquipmentKits.Add(kit);
        db.EquipmentKitItems.Add(new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = "Mochila", Tipo = ItemTipo.ItemGeral, Qtd = 1 });
        db.EquipmentKitChoiceSlots.Add(new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Tier = Tier.F, Qtd = 1 });
        await db.SaveChangesAsync();

        var reloadedItem = await db.EquipmentKitItems.SingleAsync(i => i.KitId == kit.Id);
        reloadedItem.Nome.Should().Be("Mochila");
        var reloadedSlot = await db.EquipmentKitChoiceSlots.SingleAsync(s => s.KitId == kit.Id);
        reloadedSlot.Tier.Should().Be(Tier.F);
    }
}
```

- [ ] **Step 2: Run test to verify it fails.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitSchemaTests -v`
Expected: FAIL to compile (`EquipmentKit`/`db.EquipmentKits` don't exist yet).

- [ ] **Step 3: Create the entities.**

`src/RuinaRPG.Infrastructure/Rules/EquipmentKit.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Rules;

public class EquipmentKit
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Ciclos { get; set; }
    public bool IsDeleted { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Rules/EquipmentKitItem.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public class EquipmentKitItem
{
    public Guid Id { get; set; }
    public Guid KitId { get; set; }
    public required string Nome { get; set; }
    public ItemTipo Tipo { get; set; }
    public int Qtd { get; set; }
    public string? SubcategoriaHint { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Rules/EquipmentKitChoiceSlot.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public class EquipmentKitChoiceSlot
{
    public Guid Id { get; set; }
    public Guid KitId { get; set; }
    public required string Label { get; set; }
    public ItemTipo Tipo { get; set; }
    public string? SubcategoriasCsv { get; set; }
    public Tier? Tier { get; set; }
    public int Qtd { get; set; }
    public string? BonusSubcategoria { get; set; }
    public string? BonusNome { get; set; }
    public int? BonusQtd { get; set; }
}
```

- [ ] **Step 4: Add `EquipmentKitId` to the two sheets.** In `CharacterSheet.cs`, add right after the `HistoricoId` property:

```csharp
    public Guid? EquipmentKitId { get; set; }
```

Same edit, same relative position, in `NpcSheet.cs`.

- [ ] **Step 5: Wire up `RuinaRpgDbContext`.** Add the 3 DbSets near `public DbSet<Historico> Historicos => Set<Historico>();`:

```csharp
    public DbSet<EquipmentKit> EquipmentKits => Set<EquipmentKit>();
    public DbSet<EquipmentKitItem> EquipmentKitItems => Set<EquipmentKitItem>();
    public DbSet<EquipmentKitChoiceSlot> EquipmentKitChoiceSlots => Set<EquipmentKitChoiceSlot>();
```

Add relationship config in `OnModelCreating`, near the other `builder.Entity<...>` blocks:

```csharp
        builder.Entity<EquipmentKitItem>(entity =>
        {
            entity.HasOne<EquipmentKit>().WithMany().HasForeignKey(i => i.KitId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EquipmentKitChoiceSlot>(entity =>
        {
            entity.HasOne<EquipmentKit>().WithMany().HasForeignKey(s => s.KitId).OnDelete(DeleteBehavior.Cascade);
        });
```

Then, inside the existing `builder.Entity<CharacterSheet>(entity => { ... })` block, right after the `Historico` FK config, add:

```csharp
            entity.HasOne<EquipmentKit>()
                .WithMany()
                .HasForeignKey(s => s.EquipmentKitId)
                .OnDelete(DeleteBehavior.SetNull);
```

And inside the existing `builder.Entity<NpcSheet>(entity => { ... })` one-liner block, right after the `Historico` line, add:

```csharp
            entity.HasOne<EquipmentKit>().WithMany().HasForeignKey(s => s.EquipmentKitId).OnDelete(DeleteBehavior.SetNull);
```

- [ ] **Step 6: Generate the migration.**

Run: `dotnet ef migrations add AddEquipmentKitsAndSheetEquipmentKitId --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`

- [ ] **Step 7: Read the generated migration file and confirm it creates `EquipmentKits`, `EquipmentKitItems`, `EquipmentKitChoiceSlots` and adds `EquipmentKitId` (uuid, nullable, indexed, SetNull FK) to `CharacterSheets` and `NpcSheets`.** If anything is missing or wrong, fix the DbContext config from Step 5 and regenerate (delete the migration files first).

- [ ] **Step 8: Run test to verify it passes.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitSchemaTests -v`
Expected: PASS (Testcontainers applies the new migration automatically).

- [ ] **Step 9: Run the full build to confirm 0 warnings/0 errors.**

Run: `dotnet build`

- [ ] **Step 10: Commit.**

```bash
git add src/RuinaRPG.Infrastructure/Rules/EquipmentKit.cs src/RuinaRPG.Infrastructure/Rules/EquipmentKitItem.cs src/RuinaRPG.Infrastructure/Rules/EquipmentKitChoiceSlot.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs src/RuinaRPG.Infrastructure/Persistence/Migrations/ tests/RuinaRPG.Tests.Integration/Persistence/EquipmentKitSchemaTests.cs
git commit -m "feat: entidades, DbContext e migração do sistema de Equipagem"
```

---

### Task 4: Seed data + seeder + `Program.cs` wiring

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/EquipmentKitSeedData.cs`
- Create: `src/RuinaRPG.Infrastructure/Rules/EquipmentKitSeeder.cs`
- Modify: `src/RuinaRPG.Api/Program.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/EquipmentKitSeederTests.cs`

**Interfaces:**
- Consumes: `EquipmentKit`/`EquipmentKitItem`/`EquipmentKitChoiceSlot` (Task 3).
- Produces: `EquipmentKitSeedData.All` (`IReadOnlyList<EquipmentKitSeed>`, 12 entries); `EquipmentKitSeeder.SeedAsync(RuinaRpgDbContext db) : Task<int>` (returns count inserted).

- [ ] **Step 1: Write the failing integration test.**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class EquipmentKitSeederTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public EquipmentKitSeederTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task SeedAsync_inserts_all_12_kits_with_their_items_and_choice_slots_on_an_empty_table()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();

        var inserted = await EquipmentKitSeeder.SeedAsync(db);

        inserted.Should().Be(12);
        (await db.EquipmentKits.CountAsync()).Should().Be(12);
        var patrulheiro = await db.EquipmentKits.SingleAsync(k => k.Nome == "Patrulheiro");
        (await db.EquipmentKitItems.Where(i => i.KitId == patrulheiro.Id).CountAsync()).Should().Be(3); // Tampa de Madeira, Mochila, Tônico de Vida simples
        (await db.EquipmentKitChoiceSlots.Where(s => s.KitId == patrulheiro.Id).CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_is_idempotent_and_never_touches_a_kit_the_Auditor_already_edited()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await EquipmentKitSeeder.SeedAsync(db);

        var viajante = await db.EquipmentKits.SingleAsync(k => k.Nome == "Viajante");
        viajante.Descricao = "Editado pelo Auditor";
        await db.SaveChangesAsync();

        var secondRun = await EquipmentKitSeeder.SeedAsync(db);

        secondRun.Should().Be(0);
        (await db.EquipmentKits.CountAsync()).Should().Be(12);
        (await db.EquipmentKits.SingleAsync(k => k.Nome == "Viajante")).Descricao.Should().Be("Editado pelo Auditor");
    }
}
```

- [ ] **Step 2: Run test to verify it fails.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitSeederTests -v`
Expected: FAIL to compile (`EquipmentKitSeeder` doesn't exist yet).

- [ ] **Step 3: Write the seed data.** `src/RuinaRPG.Infrastructure/Rules/EquipmentKitSeedData.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public record EquipmentKitSeed(string Nome, string Descricao, int Ciclos, List<EquipmentKitItemSeed> Items, List<EquipmentKitChoiceSlotSeed> ChoiceSlots);
public record EquipmentKitItemSeed(string Nome, ItemTipo Tipo, int Qtd, string? SubcategoriaHint = null);
public record EquipmentKitChoiceSlotSeed(string Label, ItemTipo Tipo, List<string>? Subcategorias, Tier? Tier, int Qtd,
    string? BonusSubcategoria = null, string? BonusNome = null, int? BonusQtd = null);

/// <summary>
/// Hand-authored from Docs/Sistema RPG/Equipagem.md — not parsed live from that file, unlike
/// Historico.md, because Equipagem.md's prose format (bare item lines, "de sua escolha"/"ou"
/// choices, a conditional-bonus footnote) can't be parsed reliably without inventing a bespoke
/// mini-language. Mirrors the existing DefaultCatalogItems.cs precedent. Item Nomes here are the
/// exact catalog Nomes (not always Equipagem.md's prose wording — see the aliases noted per kit
/// below), resolved per-GM at apply time by EquipmentKitGrantService.
/// </summary>
public static class EquipmentKitSeedData
{
    public static readonly IReadOnlyList<EquipmentKitSeed> All =
    [
        new("Viajante",
            "Preparado para longas jornadas, o Viajante aprendeu a carregar consigo aquilo que precisa para permanecer dias longe de casa.",
            25,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Saco de Dormir", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Tocha", ItemTipo.ItemGeral, 1),
                new("Ração de Viagem", ItemTipo.ItemGeral, 2),
            ], []),

        new("Explorador",
            "Equipado para atravessar lugares abandonados e superar obstáculos encontrados pelo caminho.",
            15,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Tocha", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Pé de Cabra", ItemTipo.ItemGeral, 1),
                new("Gazúa", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ], []),

        new("Patrulheiro",
            "Preparado para enfrentar perigos de perto, carregando uma arma adequada ao seu estilo de combate.",
            10,
            [
                new("Tampa de Madeira", ItemTipo.Escudo, 1), // alias: Equipagem.md's "Escudo de Madeira"
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ],
            [
                new("Arma", ItemTipo.Arma, null, RuinaRPG.Domain.Items.Tier.F, 1),
            ]),

        new("Caçador",
            "Preparado para perseguir criaturas e enfrentar ameaças à distância.",
            10,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Ração de Viagem", ItemTipo.ItemGeral, 2),
            ],
            [
                new("Arma à distância", ItemTipo.Arma, ["Arcos", "Fundas e Baladeiras"], RuinaRPG.Domain.Items.Tier.F, 1,
                    BonusSubcategoria: "Arcos", BonusNome: "Flecha de Madeira", BonusQtd: 10),
            ]),

        new("Arcano",
            "Um conjunto de materiais básicos para aqueles que estudam e canalizam forças arcanas.",
            0,
            [
                new("Manuscrito Arcano Vol.1", ItemTipo.ItemGeral, 1), // alias: "Manuscrito Arcano (Volume 1)"
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Tinta", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Tônico de Foco simples", ItemTipo.ItemGeral, 1),
            ],
            [
                new("Condutor", ItemTipo.Arma, ["Varinhas Mágicas", "Cajados Mágicos"], RuinaRPG.Domain.Items.Tier.F, 1),
            ]),

        new("Ocultista",
            "Materiais reunidos por aqueles que decidiram estudar conhecimentos que muitos preferem deixar intocados.",
            0,
            [
                new("Cera-viz (Material Ritualístico)", ItemTipo.ItemGeral, 1), // alias: "Cera-viz"
                new("Manuscrito Arcano Vol.1", ItemTipo.ItemGeral, 1),
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Tônico de Foco simples", ItemTipo.ItemGeral, 1),
            ],
            [
                new("Condutor", ItemTipo.Arma, ["Varinhas Mágicas", "Cajados Mágicos"], RuinaRPG.Domain.Items.Tier.F, 1),
            ]),

        new("Devoto",
            "Pertences de alguém que mantém sua fé consigo mesmo quando está distante de templos e lugares sagrados.",
            15,
            [
                new("Símbolo Sagrado", ItemTipo.ItemGeral, 1),
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ], []),

        new("Artesão",
            "Ferramentas e materiais para aqueles acostumados a construir, reparar e trabalhar com as próprias mãos.",
            15,
            [
                new("Pé de Cabra", ItemTipo.ItemGeral, 1),
                new("Gazúa", ItemTipo.ItemGeral, 1),
                new("Tinta", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
                new("Mochila", ItemTipo.ItemGeral, 1),
            ], []),

        new("Sobrevivente",
            "Recursos básicos de quem aprendeu a se virar mesmo quando não há ninguém por perto para ajudar.",
            0,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Saco de Dormir", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Ração de Viagem", ItemTipo.ItemGeral, 3),
                new("Vara de Madeira", ItemTipo.ItemGeral, 1),
                new("Isca de Pesca", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ], []),

        new("Investigador",
            "Materiais de alguém acostumado a observar, registrar e buscar respostas para aquilo que não compreende.",
            15,
            [
                new("Luneta", ItemTipo.ItemGeral, 1),
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Tinta", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
                new("Espelho", ItemTipo.ItemGeral, 1),
            ], []),

        new("Negociante",
            "Uma reserva financeira acompanhada de materiais simples para registrar acordos, valores e informações importantes.",
            80,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
            ], []),

        new("Aprendiz",
            "Um conjunto simples e versátil para quem ainda está começando a construir seu próprio caminho.",
            10,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Tocha", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
                new("Tônico de Foco simples", ItemTipo.ItemGeral, 1),
            ], []),
    ];
}
```

- [ ] **Step 4: Write the seeder.** `src/RuinaRPG.Infrastructure/Rules/EquipmentKitSeeder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Insert-if-missing-by-Nome, same treatment as HistoricoSeeder — an Auditor's edit via
/// EquipmentKitsController is never overwritten by a later restart/migrate. Same known limitation
/// as HistoricoSeeder: renaming a kit via Auditoria doesn't update this match key, so the old Nome
/// re-seeds as a duplicate on the next restart; recovering means the Auditor deletes the duplicate.
/// </summary>
public static class EquipmentKitSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db)
    {
        var existingNomes = (await db.EquipmentKits.Select(k => k.Nome).ToListAsync()).ToHashSet();

        var toInsert = EquipmentKitSeedData.All.Where(seed => !existingNomes.Contains(seed.Nome)).ToList();
        if (toInsert.Count == 0)
            return 0;

        foreach (var seed in toInsert)
        {
            var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = seed.Nome, Descricao = seed.Descricao, Ciclos = seed.Ciclos };
            db.EquipmentKits.Add(kit);

            foreach (var item in seed.Items)
                db.EquipmentKitItems.Add(new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = item.Nome, Tipo = item.Tipo, Qtd = item.Qtd, SubcategoriaHint = item.SubcategoriaHint });

            foreach (var slot in seed.ChoiceSlots)
                db.EquipmentKitChoiceSlots.Add(new EquipmentKitChoiceSlot
                {
                    Id = Guid.NewGuid(),
                    KitId = kit.Id,
                    Label = slot.Label,
                    Tipo = slot.Tipo,
                    SubcategoriasCsv = slot.Subcategorias is null ? null : string.Join(",", slot.Subcategorias),
                    Tier = slot.Tier,
                    Qtd = slot.Qtd,
                    BonusSubcategoria = slot.BonusSubcategoria,
                    BonusNome = slot.BonusNome,
                    BonusQtd = slot.BonusQtd,
                });
        }

        await db.SaveChangesAsync();
        return toInsert.Count;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitSeederTests -v`
Expected: PASS

- [ ] **Step 6: Wire into `Program.cs`.** Find both call sites of `HistoricoSeeder.SeedAsync` (the `--migrate` CLI path and the normal-startup Development path) and add, right after each:

```csharp
    var equipmentKitSeedResult = await EquipmentKitSeeder.SeedAsync(migrateDb); // or `db` on the second call site
    app.Logger.LogInformation("EquipmentKit seed: {InsertedCount} new kit(s) inserted", equipmentKitSeedResult);
```

(Match the exact local variable name — `migrateDb` on the first call site, `db` on the second — exactly as `HistoricoSeeder.SeedAsync` already does on each.)

- [ ] **Step 7: Run the full build and integration suite for this area.**

Run: `dotnet build && dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKit`
Expected: 0 warnings/errors, all pass.

- [ ] **Step 8: Commit.**

```bash
git add src/RuinaRPG.Infrastructure/Rules/EquipmentKitSeedData.cs src/RuinaRPG.Infrastructure/Rules/EquipmentKitSeeder.cs src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Integration/Rules/EquipmentKitSeederTests.cs
git commit -m "feat: dados de seed e seeder dos 12 kits de Equipagem"
```

---

### Task 5: Contracts + sheet Response/Request `EquipmentKitId`

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/EquipmentKitResponse.cs`
- Create: `src/RuinaRPG.Contracts/Rules/CreateEquipmentKitRequest.cs`
- Create: `src/RuinaRPG.Contracts/Rules/UpdateEquipmentKitRequest.cs`
- Create: `src/RuinaRPG.Contracts/Rules/EquipmentKitOptionResponse.cs`
- Create: `src/RuinaRPG.Contracts/Rules/ChooseEquipmentKitRequest.cs`
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs`
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs`
- Modify: `src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs`
- Modify: `src/RuinaRPG.Contracts/NpcSheets/UpdateNpcSheetRequest.cs`
- Modify: every file a `grep -rn "new CharacterSheetResponse(\|new UpdateCharacterSheetRequest(\|new NpcSheetResponse(\|new UpdateNpcSheetRequest("` finds (production and test code)

**Interfaces:**
- Produces: the 4 sheet contracts gain a trailing `string? EquipmentKitId`; the new Equipagem contracts below are consumed by Tasks 6/9/10.

- [ ] **Step 1: Create the Auditoria-facing contracts.**

`src/RuinaRPG.Contracts/Rules/EquipmentKitResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record EquipmentKitResponse(string Id, string Nome, string Descricao, int Ciclos,
    List<EquipmentKitItemResponse> Items, List<EquipmentKitChoiceSlotResponse> ChoiceSlots);

public record EquipmentKitItemResponse(string Id, string Nome, string Tipo, int Qtd, string? SubcategoriaHint);

public record EquipmentKitChoiceSlotResponse(string Id, string Label, string Tipo, List<string>? Subcategorias,
    string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd);
```

`src/RuinaRPG.Contracts/Rules/CreateEquipmentKitRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record CreateEquipmentKitRequest(string Nome, string Descricao, int Ciclos,
    List<EquipmentKitItemInput> Items, List<EquipmentKitChoiceSlotInput> ChoiceSlots);

public record EquipmentKitItemInput(string Nome, string Tipo, int Qtd, string? SubcategoriaHint);

public record EquipmentKitChoiceSlotInput(string Label, string Tipo, List<string>? Subcategorias,
    string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd);
```

`src/RuinaRPG.Contracts/Rules/UpdateEquipmentKitRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record UpdateEquipmentKitRequest(string Nome, string Descricao, int Ciclos,
    List<EquipmentKitItemInput> Items, List<EquipmentKitChoiceSlotInput> ChoiceSlots);
```

- [ ] **Step 2: Create the apply-flow (player-facing) contracts.**

`src/RuinaRPG.Contracts/Rules/EquipmentKitOptionResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record EquipmentKitOptionResponse(string Id, string Nome, string Descricao, int Ciclos,
    List<EquipmentKitChoiceSlotOptionResponse> ChoiceSlots);

public record EquipmentKitChoiceSlotOptionResponse(string SlotId, string Label, List<EquipmentKitEligibleItemResponse> Options);

public record EquipmentKitEligibleItemResponse(string ItemId, string Nome);
```

`src/RuinaRPG.Contracts/Rules/ChooseEquipmentKitRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record ChooseEquipmentKitRequest(string KitId, List<ChoiceSlotSelectionRequest> ChoiceSelections);

public record ChoiceSlotSelectionRequest(string SlotId, string ItemId);
```

- [ ] **Step 3: Append `EquipmentKitId` to the 4 sheet contracts.** In each of `CharacterSheetResponse.cs`, `UpdateCharacterSheetRequest.cs`, `NpcSheetResponse.cs`, `UpdateNpcSheetRequest.cs`, change the trailing `string? HistoricoId);` to:

```csharp
    string? HistoricoId,
    string? EquipmentKitId);
```

- [ ] **Step 4: Find every construction call site.**

Run: `grep -rn "new CharacterSheetResponse(\|new UpdateCharacterSheetRequest(\|new NpcSheetResponse(\|new UpdateNpcSheetRequest(" src tests --include=*.cs`

For every match in `src/` (the real `ToResponseAsync`/`Update` construction sites in `CharacterSheetsController.cs`/`NpcSheetsController.cs`), Task 7 supplies the real value — for this task, append a literal `null` as the trailing argument so the solution compiles. For every match in `tests/` (including target-typed `new(...)` via `ValidUpdate()`-style helpers — grep also for `new(` near `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest` return types if the plain grep above misses any), append trailing `null` too.

- [ ] **Step 5: Build to confirm every call site was caught.**

Run: `dotnet build`
Expected: any missed call site fails with CS7036 ("required parameter... has no argument") naming the exact file/line — fix each until 0 errors.

- [ ] **Step 6: Run the full unit + client test suites (integration deferred to Task 7, which needs `EquipmentKitId` to actually persist).**

Run: `dotnet test tests/RuinaRPG.Tests.Unit && dotnet test src/RuinaRPG.Client.Tests` (use whichever exact client test project path `dotnet build` reports — check `RuinaRPG.sln` if unsure).

- [ ] **Step 7: Commit.**

```bash
git add src/RuinaRPG.Contracts/Rules/EquipmentKitResponse.cs src/RuinaRPG.Contracts/Rules/CreateEquipmentKitRequest.cs src/RuinaRPG.Contracts/Rules/UpdateEquipmentKitRequest.cs src/RuinaRPG.Contracts/Rules/EquipmentKitOptionResponse.cs src/RuinaRPG.Contracts/Rules/ChooseEquipmentKitRequest.cs src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs src/RuinaRPG.Contracts/NpcSheets/UpdateNpcSheetRequest.cs
git add -u
git commit -m "feat: contratos do sistema de Equipagem e campo EquipmentKitId nas fichas"
```

---

### Task 6: `EquipmentKitsController` (Auditoria CRUD)

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/EquipmentKitsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EquipmentKitsControllerTests.cs`

**Interfaces:**
- Consumes: `EquipmentKit`/`EquipmentKitItem`/`EquipmentKitChoiceSlot` (Task 3), the Auditoria contracts (Task 5).
- Produces: `GET api/equipment-kits` (open), `POST`/`PUT api/equipment-kits/{id}`/`DELETE api/equipment-kits/{id}` (auditor-gated).

- [ ] **Step 1: Write the failing tests.**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EquipmentKitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EquipmentKitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    // Mirrors the exact established helpers in CharacterSheetsControllerTests.cs — copy them
    // verbatim (RegisterJogadorLinkedToAsync, CreateCampaignAsync) rather than reinventing a
    // shorter path, since Create_for_a_campaign_member requires the OwnerId to be a real
    // CampaignMember (CharacterSheetsController.Create enforces this).
    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meBody = await (await _client.SendAsync(me)).Content.ReadFromJsonAsync<MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private async Task<string> CreateCharacterSheetAsync(string gmToken, string playerId, string campaignId)
    {
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    // Copied verbatim from HistoricosControllerTests.cs — this codebase's established way to grant
    // the Rules Auditor role in a test (a direct DB write, not an API call — no grant endpoint exists,
    // real grants happen via `make grant-rules-auditor`).
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    private static CreateEquipmentKitRequest ValidCreate() => new(
        "Kit de Teste", "Descrição de teste", 10,
        [new EquipmentKitItemInput("Mochila", "ItemGeral", 1, null)],
        [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, "F", 1, null, null, null)]);

    [Fact]
    public async Task List_is_open_to_any_authenticated_caller_and_returns_the_12_seeded_kits()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm1", "equipkits1@teste.com");
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/equipment-kits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitResponse>>();
        kits!.Should().HaveCountGreaterOrEqualTo(12);
        kits.Should().Contain(k => k.Nome == "Viajante");
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_GM_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm2", "equipkits2@teste.com");
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_rejects_an_item_of_Tipo_Armadura()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm3", "equipkits3@teste.com");
        await GrantRulesAuditorAsync("equipkits3@teste.com");

        var invalid = ValidCreate() with { Items = [new EquipmentKitItemInput("Armadura de Couro", "Armadura", 1, null)] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_by_an_Auditor_persists_items_and_choice_slots()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm4", "equipkits4@teste.com");
        await GrantRulesAuditorAsync("equipkits4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<EquipmentKitResponse>();
        kit!.Items.Should().ContainSingle(i => i.Nome == "Mochila");
        kit.ChoiceSlots.Should().ContainSingle(s => s.Label == "Arma" && s.Tier == "F");
    }

    [Fact]
    public async Task Update_replaces_the_kit_s_items_and_choice_slots_wholesale()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm5", "equipkits5@teste.com");
        await GrantRulesAuditorAsync("equipkits5@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate())))
            .Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var updated = ValidCreate() with { Items = [new EquipmentKitItemInput("Corda", "ItemGeral", 2, null)], ChoiceSlots = [] };
        var putResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/equipment-kits/{created!.Id}", gmToken, updated));
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/equipment-kits", gmToken));
        var kit = (await listResponse.Content.ReadFromJsonAsync<List<EquipmentKitResponse>>())!.Single(k => k.Id == created.Id);
        kit.Items.Should().ContainSingle(i => i.Nome == "Corda" && i.Qtd == 2);
        kit.ChoiceSlots.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_a_kit_already_chosen_by_a_CharacterSheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsGm6", "equipkits6@teste.com");
        await GrantRulesAuditorAsync("equipkits6@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, ValidCreate())))
            .Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "EquipKitsPlayer6", "equipkitsplayer6@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha de Teste");
        var sheetId = await CreateCharacterSheetAsync(gmToken, playerId, campaignId);

        // Directly through the DB, to isolate this test from the full choose-flow this same plan builds in Task 9.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var s = await db.CharacterSheets.FindAsync(Guid.Parse(sheetId));
            s!.EquipmentKitId = Guid.Parse(created!.Id);
            await db.SaveChangesAsync();
        }

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/equipment-kits/{created.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitsControllerTests -v`
Expected: FAIL to compile (`EquipmentKitsController` doesn't exist).

- [ ] **Step 3: Write the controller.** `src/RuinaRPG.Api/Controllers/EquipmentKitsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Global (not per-GM) catalog of Equipagem kits — same treatment as HistoricosController. List
/// (GET) stays open; Create/Update/Delete are gated to the Rules Auditor, checked directly
/// against the DB, not a JWT claim.
/// </summary>
[ApiController]
[Route("api/equipment-kits")]
[Authorize]
public class EquipmentKitsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EquipmentKitResponse>>> List()
    {
        var kits = await db.EquipmentKits.Where(k => !k.IsDeleted).OrderBy(k => k.Nome).ToListAsync();
        var responses = new List<EquipmentKitResponse>();
        foreach (var kit in kits)
            responses.Add(await ToResponseAsync(kit));
        return responses;
    }

    [HttpPost]
    public async Task<ActionResult<EquipmentKitResponse>> Create(CreateEquipmentKitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var validationError = ValidateRequest(request.Nome, request.Descricao, request.Ciclos, request.Items, request.ChoiceSlots, out var parsedItems, out var parsedSlots);
        if (validationError is not null)
            return validationError;

        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = request.Nome, Descricao = request.Descricao, Ciclos = request.Ciclos };
        db.EquipmentKits.Add(kit);
        AddChildren(kit.Id, parsedItems!, parsedSlots!);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(kit));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateEquipmentKitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var kit = await db.EquipmentKits.FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted);
        if (kit is null)
            return NotFound();

        var validationError = ValidateRequest(request.Nome, request.Descricao, request.Ciclos, request.Items, request.ChoiceSlots, out var parsedItems, out var parsedSlots);
        if (validationError is not null)
            return validationError;

        kit.Nome = request.Nome;
        kit.Descricao = request.Descricao;
        kit.Ciclos = request.Ciclos;

        db.EquipmentKitItems.RemoveRange(db.EquipmentKitItems.Where(i => i.KitId == id));
        db.EquipmentKitChoiceSlots.RemoveRange(db.EquipmentKitChoiceSlots.Where(s => s.KitId == id));
        AddChildren(kit.Id, parsedItems!, parsedSlots!);

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var kit = await db.EquipmentKits.FirstOrDefaultAsync(k => k.Id == id && !k.IsDeleted);
        if (kit is null)
            return NotFound();

        var inUse = await db.CharacterSheets.AnyAsync(s => s.EquipmentKitId == id)
            || await db.NpcSheets.AnyAsync(s => s.EquipmentKitId == id);
        if (inUse)
            return Conflict("Este kit de Equipagem está em uso em pelo menos uma ficha e não pode ser excluído.");

        kit.IsDeleted = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private void AddChildren(Guid kitId, List<EquipmentKitItem> items, List<EquipmentKitChoiceSlot> slots)
    {
        foreach (var item in items)
        {
            item.KitId = kitId;
            db.EquipmentKitItems.Add(item);
        }
        foreach (var slot in slots)
        {
            slot.KitId = kitId;
            db.EquipmentKitChoiceSlots.Add(slot);
        }
    }

    private ActionResult? ValidateRequest(string nome, string descricao, int ciclos,
        List<EquipmentKitItemInput> items, List<EquipmentKitChoiceSlotInput> choiceSlots,
        out List<EquipmentKitItem>? parsedItems, out List<EquipmentKitChoiceSlot>? parsedSlots)
    {
        parsedItems = null;
        parsedSlots = null;

        if (string.IsNullOrWhiteSpace(nome))
            return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(descricao))
            return BadRequest("Descrição é obrigatória.");
        if (ciclos < 0)
            return BadRequest("Ciclos não pode ser negativo.");

        var items2 = new List<EquipmentKitItem>();
        foreach (var item in items)
        {
            if (!Enum.TryParse<ItemTipo>(item.Tipo, out var tipo) || tipo == ItemTipo.Armadura)
                return BadRequest($"Tipo de item inválido: \"{item.Tipo}\". Armadura não é suportada em kits de Equipagem.");
            if (string.IsNullOrWhiteSpace(item.Nome))
                return BadRequest("Nome do item é obrigatório.");
            if (item.Qtd < 1)
                return BadRequest("Qtd do item deve ser pelo menos 1.");
            items2.Add(new EquipmentKitItem { Id = Guid.NewGuid(), Nome = item.Nome, Tipo = tipo, Qtd = item.Qtd, SubcategoriaHint = item.SubcategoriaHint });
        }

        var slots2 = new List<EquipmentKitChoiceSlot>();
        foreach (var slot in choiceSlots)
        {
            if (!Enum.TryParse<ItemTipo>(slot.Tipo, out var tipo) || tipo == ItemTipo.Armadura)
                return BadRequest($"Tipo de slot de escolha inválido: \"{slot.Tipo}\".");
            if (string.IsNullOrWhiteSpace(slot.Label))
                return BadRequest("Label do slot de escolha é obrigatório.");
            if (slot.Qtd < 1)
                return BadRequest("Qtd do slot de escolha deve ser pelo menos 1.");
            Tier? tier = null;
            if (!string.IsNullOrWhiteSpace(slot.Tier))
            {
                if (!Enum.TryParse<Tier>(slot.Tier, out var parsedTier))
                    return BadRequest($"Tier inválido: \"{slot.Tier}\".");
                tier = parsedTier;
            }
            if ((slot.BonusSubcategoria is null) != (slot.BonusNome is null))
                return BadRequest("BonusSubcategoria e BonusNome devem ser informados juntos, ou nenhum dos dois.");
            if (slot.BonusNome is not null && (slot.BonusQtd is null || slot.BonusQtd < 1))
                return BadRequest("BonusQtd deve ser pelo menos 1 quando BonusNome é informado.");

            slots2.Add(new EquipmentKitChoiceSlot
            {
                Id = Guid.NewGuid(),
                Label = slot.Label,
                Tipo = tipo,
                SubcategoriasCsv = slot.Subcategorias is null ? null : string.Join(",", slot.Subcategorias),
                Tier = tier,
                Qtd = slot.Qtd,
                BonusSubcategoria = slot.BonusSubcategoria,
                BonusNome = slot.BonusNome,
                BonusQtd = slot.BonusQtd,
            });
        }

        parsedItems = items2;
        parsedSlots = slots2;
        return null;
    }

    private async Task<EquipmentKitResponse> ToResponseAsync(EquipmentKit kit)
    {
        var items = await db.EquipmentKitItems.Where(i => i.KitId == kit.Id).ToListAsync();
        var slots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kit.Id).ToListAsync();

        return new EquipmentKitResponse(kit.Id.ToString(), kit.Nome, kit.Descricao, kit.Ciclos,
            items.Select(i => new EquipmentKitItemResponse(i.Id.ToString(), i.Nome, i.Tipo.ToString(), i.Qtd, i.SubcategoriaHint)).ToList(),
            slots.Select(s => new EquipmentKitChoiceSlotResponse(s.Id.ToString(), s.Label, s.Tipo.ToString(),
                s.SubcategoriasCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                s.Tier?.ToString(), s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd)).ToList());
    }

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 4: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitsControllerTests -v`
Expected: PASS

- [ ] **Step 5: Commit.**

```bash
git add src/RuinaRPG.Api/Controllers/EquipmentKitsController.cs tests/RuinaRPG.Tests.Integration/Controllers/EquipmentKitsControllerTests.cs
git commit -m "feat: CRUD de Auditoria para o catálogo de kits de Equipagem"
```

---

### Task 7: `CharacterSheetsController`/`NpcSheetsController` — `EquipmentKitId` in response + persisted-but-immutable in Update

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs` (add cases to the existing files)

**Interfaces:**
- Consumes: `EquipmentKitId` on `CharacterSheet`/`NpcSheet` (Task 3), trailing param on the 4 sheet contracts (Task 5).
- Produces: `CharacterSheetResponse.EquipmentKitId`/`NpcSheetResponse.EquipmentKitId` reflect the sheet's real value; `Update` never lets `EquipmentKitId` be set/cleared through this endpoint (only `CharacterEquipagemController`/`NpcEquipagemController`, Task 9, can set it).

- [ ] **Step 1: Write the failing tests.** In `CharacterSheetsControllerTests.cs`, add:

```csharp
    [Fact]
    public async Task Update_never_changes_EquipmentKitId_even_if_the_request_tries_to()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitImmutableGm", "equipkitimmutable@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // reuse this file's existing helper

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));
        var before = await getResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        before!.EquipmentKitId.Should().BeNull();

        // ValidUpdate() (this file's existing helper) doesn't carry a real EquipmentKitId — the
        // request contract simply has no field for it, since Update never accepts one; this test
        // only confirms the response surfaces the sheet's real (still-null) value after an
        // unrelated update.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, ValidUpdate()));

        var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));
        var after = await afterResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        after!.EquipmentKitId.Should().BeNull();
    }
```

Add the mirrored test (using this file's own `ValidUpdate()`/`CreateSheetAsync()` helpers) to `NpcSheetsControllerTests.cs`.

- [ ] **Step 2: Run tests to verify they fail or pass for the wrong reason** (right now `EquipmentKitId` is always `null` from Task 5's placeholder wiring, so this specific test may already pass — that's expected; the point of Steps 3-4 below is making `ToResponseAsync` return the sheet's *real* value, which the next test in Step 5 exercises properly).

- [ ] **Step 3: Fix `CharacterSheetsController.ToResponseAsync`.** Find the trailing `s.HistoricoId?.ToString());` in the `CharacterSheetResponse` construction and change to:

```csharp
            s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString(), s.EquipmentKitId?.ToString());
```

Same edit (find the exact equivalent trailing line) in `NpcSheetsController.ToResponseAsync`.

- [ ] **Step 4: Confirm `Update` doesn't touch `sheet.EquipmentKitId` anywhere** (it shouldn't — `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest` gained the field in Task 5 only because every trailing-param edit was mechanical across all 4 contracts, but this endpoint's `Update` method body must never read or assign `request.EquipmentKitId`). Grep `request.EquipmentKitId` in both controllers — it must have zero real usages (only the Task 5 placeholder wiring, if any leaked in, which it shouldn't have since Task 5 only appended contract fields, not controller code).

- [ ] **Step 5: Write a second failing test proving the real (non-null) case**, added right after the first one in each file:

```csharp
    [Fact]
    public async Task GetSheet_surfaces_the_sheet_s_real_EquipmentKitId_once_one_is_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitRealGm", "equipkitreal@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        Guid kitId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            kitId = (await db.EquipmentKits.FirstAsync()).Id;
            var sheet = await db.CharacterSheets.FindAsync(Guid.Parse(sheetId));
            sheet!.EquipmentKitId = kitId;
            await db.SaveChangesAsync();
        }

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.EquipmentKitId.Should().Be(kitId.ToString());
    }
```

Mirror in `NpcSheetsControllerTests.cs` (using `db.NpcSheets`).

- [ ] **Step 6: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests|FullyQualifiedName~NpcSheetsControllerTests" -v`
Expected: PASS

- [ ] **Step 7: Commit.**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs
git commit -m "feat: EquipmentKitId real na resposta das fichas"
```

---

### Task 8: `EquipmentKitGrantService`

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/EquipmentKitGrantService.cs`
- Modify: `Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/EquipmentKitGrantServiceTests.cs`

**Interfaces:**
- Consumes: `EquipmentKit`/`EquipmentKitItem`/`EquipmentKitChoiceSlot` (Task 3), `CampaignAttachment` (existing).
- Produces:
  - `Task<List<EquipmentKitEligibleItemResponse>> ResolveEligibleOptionsAsync(EquipmentKitChoiceSlot slot, Guid gmId)`
  - `Task<(EquipmentGrantPlan? Plan, string? Error)> BuildPlanAsync(EquipmentKit kit, List<EquipmentKitItem> fixedItems, List<EquipmentKitChoiceSlot> choiceSlots, Guid gmId, List<ChoiceSlotSelectionRequest> selections)`
  - `Task UpsertCampaignAttachmentAsync(Guid campaignId, Guid itemId)`
  - `record EquipmentGrantPlan(List<EquipmentGrantPlanItem> Grants, int Ciclos)`
  - `record EquipmentGrantPlanItem(ItemTipo Tipo, Guid ItemId, int Qtd, int? DurabilidadeMaxima)` — `DurabilidadeMaxima` is only meaningful for `Arma`/`Escudo` grants (null otherwise), so `CharacterEquipagemController`/`NpcEquipagemController` (Task 9) can set `DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0` the same way `CharacterArsenalController.AddWeapon`/`AddShield` already do, instead of always writing 0.
  These are consumed by `CharacterEquipagemController`/`NpcEquipagemController` in Task 9.

- [ ] **Step 1: Write the failing tests.**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class EquipmentKitGrantServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public EquipmentKitGrantServiceTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task BuildPlanAsync_auto_creates_a_missing_fixed_ItemGeral_in_the_GM_s_own_catalog()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = "Item Inexistente XYZ", Tipo = ItemTipo.ItemGeral, Qtd = 2, SubcategoriaHint = "Equipamentos de Aventura" };
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [kitItem], [], gmId, []);

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 2);
        var created = await db.Set<ItemGeral>().SingleAsync(i => i.GmId == gmId && i.Nome == "Item Inexistente XYZ");
        created.Subcategoria.Should().Be("Equipamentos de Aventura");
        created.Peso.Should().Be(0);
        created.Preco.Should().Be(0);
    }

    [Fact]
    public async Task BuildPlanAsync_reuses_an_existing_item_instead_of_creating_a_duplicate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var existing = new ItemGeral { Id = Guid.NewGuid(), GmId = gmId, Nome = "Mochila", Subcategoria = "Equipamentos de Aventura", Peso = 5, Preco = 20 };
        db.Add(existing);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, Nome = "Mochila", Tipo = ItemTipo.ItemGeral, Qtd = 1 };
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, _) = await service.BuildPlanAsync(kit, [kitItem], [], gmId, []);

        plan!.Grants.Single().ItemId.Should().Be(existing.Id);
        (await db.Set<ItemGeral>().CountAsync(i => i.GmId == gmId && i.Nome == "Mochila")).Should().Be(1);
    }

    [Fact]
    public async Task BuildPlanAsync_rejects_a_missing_choice_slot_selection()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Tier = Tier.F, Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, []);

        plan.Should().BeNull();
        error.Should().Contain("Arma");
    }

    [Fact]
    public async Task BuildPlanAsync_rejects_a_choice_selection_outside_the_slot_s_filter()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var outOfTierWeapon = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Espada Lendária", Subcategoria = "Espadas", Tier = Tier.S, Peso = 1, Preco = 0 };
        db.Add(outOfTierWeapon);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Tier = Tier.F, Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), outOfTierWeapon.Id.ToString())]);

        plan.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildPlanAsync_grants_the_conditional_bonus_only_when_the_matching_alternative_is_chosen()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var bow = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arco de Teste", Subcategoria = "Arcos", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(bow);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Arcos,Fundas e Baladeiras", Tier = Tier.F, Qtd = 1,
            BonusSubcategoria = "Arcos", BonusNome = "Flecha de Madeira", BonusQtd = 10,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), bow.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().HaveCount(2);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == bow.Id);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 10);
    }

    [Fact]
    public async Task UpsertCampaignAttachmentAsync_creates_a_public_attachment_when_none_exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var campaignId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var service = new EquipmentKitGrantService(db);

        await service.UpsertCampaignAttachmentAsync(campaignId, itemId);
        await db.SaveChangesAsync();

        var attachment = await db.CampaignAttachments.SingleAsync(a => a.CampaignId == campaignId && a.ItemId == itemId);
        attachment.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertCampaignAttachmentAsync_flips_an_existing_private_attachment_to_public()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var campaignId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        db.CampaignAttachments.Add(new RuinaRPG.Infrastructure.Campaigns.CampaignAttachment { Id = Guid.NewGuid(), CampaignId = campaignId, ItemId = itemId, IsPublic = false });
        await db.SaveChangesAsync();
        var service = new EquipmentKitGrantService(db);

        await service.UpsertCampaignAttachmentAsync(campaignId, itemId);
        await db.SaveChangesAsync();

        (await db.CampaignAttachments.CountAsync(a => a.CampaignId == campaignId && a.ItemId == itemId)).Should().Be(1);
        (await db.CampaignAttachments.SingleAsync(a => a.CampaignId == campaignId && a.ItemId == itemId)).IsPublic.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitGrantServiceTests -v`
Expected: FAIL to compile.

- [ ] **Step 3: Write the service.** `src/RuinaRPG.Infrastructure/Rules/EquipmentKitGrantService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public record EquipmentGrantPlan(List<EquipmentGrantPlanItem> Grants, int Ciclos);
public record EquipmentGrantPlanItem(ItemTipo Tipo, Guid ItemId, int Qtd, int? DurabilidadeMaxima);

/// <summary>
/// Resolves an EquipmentKit's fixed items and choice slots against one specific GM's own Item
/// catalog (Item.GmId is the partition key — a kit definition is global, but Items are per-GM).
/// Shared by CharacterEquipagemController/NpcEquipagemController, which each own the actual
/// per-Tipo insert into CharacterWeapon/NpcWeapon etc. — this service only resolves/validates and
/// upserts the campaign-visibility side effect (Requisitos - Campanha's "itens de conhecimento
/// geral" exception to the normal manual-attach-and-publish flow).
/// </summary>
public class EquipmentKitGrantService(RuinaRpgDbContext db)
{
    public async Task<List<EquipmentKitEligibleItemResponse>> ResolveEligibleOptionsAsync(EquipmentKitChoiceSlot slot, Guid gmId)
    {
        var query = db.Set<Arma>().Where(a => a.GmId == gmId);

        var subcategorias = slot.SubcategoriasCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (subcategorias is { Length: > 0 })
            query = query.Where(a => subcategorias.Contains(a.Subcategoria));
        if (slot.Tier is not null)
            query = query.Where(a => a.Tier == slot.Tier);

        var items = await query.OrderBy(a => a.Nome).ToListAsync();
        return items.Select(a => new EquipmentKitEligibleItemResponse(a.Id.ToString(), a.Nome)).ToList();
    }

    public async Task<(EquipmentGrantPlan? Plan, string? Error)> BuildPlanAsync(EquipmentKit kit,
        List<EquipmentKitItem> fixedItems, List<EquipmentKitChoiceSlot> choiceSlots, Guid gmId,
        List<ChoiceSlotSelectionRequest> selections)
    {
        var grants = new List<EquipmentGrantPlanItem>();

        foreach (var kitItem in fixedItems)
        {
            var resolved = await ResolveOrCreateFixedItemAsync(kitItem, gmId);
            if (resolved is null)
                return (null, $"O item \"{kitItem.Nome}\" não está cadastrado no catálogo deste GM.");
            grants.Add(new EquipmentGrantPlanItem(kitItem.Tipo, resolved.Value.Id, kitItem.Qtd, resolved.Value.DurabilidadeMaxima));
        }

        foreach (var slot in choiceSlots)
        {
            var selection = selections.FirstOrDefault(s => s.SlotId == slot.Id.ToString());
            if (selection is null)
                return (null, $"Escolha obrigatória para \"{slot.Label}\" não foi informada.");
            if (!Guid.TryParse(selection.ItemId, out var selectedItemId))
                return (null, "ItemId inválido em uma seleção de equipagem.");

            var eligible = await ResolveEligibleOptionsAsync(slot, gmId);
            if (!eligible.Any(e => e.ItemId == selectedItemId.ToString()))
                return (null, $"O item escolhido não é uma opção válida para \"{slot.Label}\".");

            var selectedItem = await db.Set<Arma>().SingleAsync(a => a.Id == selectedItemId);
            grants.Add(new EquipmentGrantPlanItem(slot.Tipo, selectedItemId, slot.Qtd, selectedItem.DurabilidadeMaxima));

            if (slot.BonusNome is not null && selectedItem.Subcategoria == slot.BonusSubcategoria)
            {
                var bonusResolved = await ResolveOrCreateFixedItemAsync(
                    new EquipmentKitItem { Nome = slot.BonusNome, Tipo = ItemTipo.ItemGeral, Qtd = slot.BonusQtd ?? 1 }, gmId);
                if (bonusResolved is not null)
                    grants.Add(new EquipmentGrantPlanItem(ItemTipo.ItemGeral, bonusResolved.Value.Id, slot.BonusQtd ?? 1, null));
            }
        }

        return (new EquipmentGrantPlan(grants, kit.Ciclos), null);
    }

    private async Task<(Guid Id, int? DurabilidadeMaxima)?> ResolveOrCreateFixedItemAsync(EquipmentKitItem kitItem, Guid gmId)
    {
        switch (kitItem.Tipo)
        {
            case ItemTipo.ItemGeral:
                var existing = await db.Set<ItemGeral>().FirstOrDefaultAsync(i => i.GmId == gmId && i.Nome == kitItem.Nome);
                if (existing is not null)
                    return (existing.Id, null);
                var created = new ItemGeral
                {
                    Id = Guid.NewGuid(), GmId = gmId, Nome = kitItem.Nome,
                    Subcategoria = kitItem.SubcategoriaHint ?? "Equipamentos de Aventura",
                    Peso = 0, Preco = 0,
                };
                db.Add(created);
                return (created.Id, null);
            case ItemTipo.Arma:
                var arma = await db.Set<Arma>().FirstOrDefaultAsync(a => a.GmId == gmId && a.Nome == kitItem.Nome);
                return arma is null ? null : (arma.Id, arma.DurabilidadeMaxima);
            case ItemTipo.Escudo:
                var escudo = await db.Set<Escudo>().FirstOrDefaultAsync(e => e.GmId == gmId && e.Nome == kitItem.Nome);
                return escudo is null ? null : (escudo.Id, escudo.DurabilidadeMaxima);
            case ItemTipo.Artefato:
                var artefato = await db.Set<Artefato>().FirstOrDefaultAsync(a => a.GmId == gmId && a.Nome == kitItem.Nome);
                return artefato is null ? null : (artefato.Id, (int?)null);
            default:
                return null;
        }
    }

    public async Task UpsertCampaignAttachmentAsync(Guid campaignId, Guid itemId)
    {
        var existing = await db.CampaignAttachments.FirstOrDefaultAsync(a => a.CampaignId == campaignId && a.ItemId == itemId);
        if (existing is not null)
        {
            existing.IsPublic = true;
            return;
        }

        db.CampaignAttachments.Add(new CampaignAttachment { Id = Guid.NewGuid(), CampaignId = campaignId, ItemId = itemId, IsPublic = true });
    }
}
```

- [ ] **Step 4: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitGrantServiceTests -v`
Expected: PASS

- [ ] **Step 5: Update `Requisitos - Catálogo de Itens e Equipamentos.md`.** Add a short paragraph after R0007 (or wherever fits the doc's existing numbering — continue the `**R00NN**` sequence) documenting: applying an Equipagem kit may auto-create a missing Item Geral in the acting GM's own catalog, with Peso/Preço 0, same convention as the "catálogo inicial" preamble.

- [ ] **Step 6: Commit.**

```bash
git add src/RuinaRPG.Infrastructure/Rules/EquipmentKitGrantService.cs tests/RuinaRPG.Tests.Integration/Rules/EquipmentKitGrantServiceTests.cs "Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md"
git commit -m "feat: serviço de resolução/concessão de itens do sistema de Equipagem"
```

---

### Task 9: `CharacterEquipagemController` + `NpcEquipagemController`

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/CharacterEquipagemController.cs`
- Create: `src/RuinaRPG.Api/Controllers/NpcEquipagemController.cs`
- Modify: `Docs/Requisitos/Requisitos - Campanha.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterEquipagemControllerTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/NpcEquipagemControllerTests.cs`

**Interfaces:**
- Consumes: `EquipmentKitGrantService` (Task 8), `EquipmentKitOptionResponse`/`ChooseEquipmentKitRequest` (Task 5).
- Produces: `GET api/character-sheets/{sheetId}/equipagem/kits`, `POST api/character-sheets/{sheetId}/equipagem/choose`, and the `NpcSheet` mirrors under `api/npc-sheets/{sheetId}/equipagem/...`.

- [ ] **Step 1: Write the failing Character-side tests.**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterEquipagemControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterEquipagemControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    // Copied verbatim from CharacterSheetsControllerTests.cs's established helpers —
    // CharacterSheetsController.Create requires OwnerId to be a real CampaignMember.
    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meBody = await (await _client.SendAsync(me)).Content.ReadFromJsonAsync<MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<(string CampaignId, string SheetId)> CreateCampaignAndSheetAsync(string gmToken)
    {
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, $"EquipagemPlayer{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@teste.com");
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>();
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaign!.Id}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        return (campaign.Id, sheet!.Id);
    }

    [Fact]
    public async Task ListKits_returns_every_seeded_kit_with_choice_slot_options_resolved_against_the_GM_s_catalog()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm1", "equipagem1@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        kits!.Should().HaveCountGreaterOrEqualTo(12);
        var patrulheiro = kits.Single(k => k.Nome == "Patrulheiro");
        patrulheiro.ChoiceSlots.Should().ContainSingle();
        patrulheiro.ChoiceSlots.Single().Options.Should().NotBeEmpty(); // Varinha de Carvalho and every default Tier-F weapon are seeded per-GM already
    }

    [Fact]
    public async Task Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm2", "equipagem2@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(viajante.Id, [])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));
        var sheet = await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        sheet!.EquipmentKitId.Should().Be(viajante.Id);
        sheet.Ciclos.Should().Be(25);

        var inventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<CharacterInventoryItemResponse>>();
        inventory!.Should().Contain(i => i.Nome == "Mochila");
        inventory.Should().Contain(i => i.Nome == "Ração de Viagem" && i.Qtd == 2);
    }

    [Fact]
    public async Task Choose_a_kit_with_a_choice_slot_places_the_selected_weapon()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm3", "equipagem3@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var patrulheiro = kits!.Single(k => k.Nome == "Patrulheiro");
        var chosenWeapon = patrulheiro.ChoiceSlots.Single().Options.First();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(patrulheiro.Id, [new ChoiceSlotSelectionRequest(patrulheiro.ChoiceSlots.Single().SlotId, chosenWeapon.ItemId)])));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var weaponsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/weapons", gmToken));
        var weapons = await weaponsResponse.Content.ReadFromJsonAsync<List<CharacterWeaponResponse>>();
        weapons!.Should().ContainSingle(w => w.Nome == chosenWeapon.Nome);

        var shieldsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/shields", gmToken));
        var shields = await shieldsResponse.Content.ReadFromJsonAsync<List<CharacterShieldResponse>>();
        shields!.Should().ContainSingle(s => s.Nome == "Tampa de Madeira");
    }

    [Fact]
    public async Task Choose_Cacador_with_the_Arco_alternative_also_grants_10_Flechas_de_Madeira()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm4", "equipagem4@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);

        // DefaultCatalogItems.cs's Tier-F rows for "Arcos"/"Fundas e Baladeiras" are empty (its lowest
        // bow is Arco Curto at Tier E) — this test can't rely on incidental default-catalog data for
        // Caçador's choice slot, so it seeds its own Tier-F "Arcos" weapon directly, the same way a GM
        // would need to before this kit's choice slot has any real option.
        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        Guid testBowId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var bow = new RuinaRPG.Infrastructure.Items.Arma
            {
                Id = Guid.NewGuid(), GmId = Guid.Parse(gmId), Nome = "Arco de Teste F", Subcategoria = "Arcos",
                Tier = RuinaRPG.Domain.Items.Tier.F, Peso = 1, Preco = 0,
            };
            db.Add(bow);
            await db.SaveChangesAsync();
            testBowId = bow.Id;
        }

        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var cacador = kits!.Single(k => k.Nome == "Caçador");
        var slot = cacador.ChoiceSlots.Single();
        var chosen = slot.Options.Single(o => o.ItemId == testBowId.ToString());

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(cacador.Id, [new ChoiceSlotSelectionRequest(slot.SlotId, chosen.ItemId)])));

        var inventoryResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/inventory", gmToken));
        var inventory = await inventoryResponse.Content.ReadFromJsonAsync<List<CharacterInventoryItemResponse>>();
        inventory!.Should().Contain(i => i.Nome == "Flecha de Madeira" && i.Qtd == 10);
    }

    [Fact]
    public async Task Choose_a_second_time_on_an_already_chosen_sheet_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm5", "equipagem5@teste.com");
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var negociante = kits!.Single(k => k.Nome == "Negociante");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choose_auto_attaches_every_granted_item_to_the_sheet_s_campaign_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemGm6", "equipagem6@teste.com");
        var (campaignId, sheetId) = await CreateCampaignAndSheetAsync(gmToken);
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var viajante = kits!.Single(k => k.Nome == "Viajante");

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(viajante.Id, [])));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var mochila = await db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>().SingleAsync(i => i.Nome == "Mochila");
        var attachment = await db.CampaignAttachments.SingleAsync(a => a.CampaignId == Guid.Parse(campaignId) && a.ItemId == mochila.Id);
        attachment.IsPublic.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~CharacterEquipagemControllerTests -v`
Expected: FAIL to compile (`CharacterEquipagemController` doesn't exist).

- [ ] **Step 3: Write `CharacterEquipagemController`.**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/equipagem")]
public class CharacterEquipagemController(RuinaRpgDbContext db, EquipmentKitGrantService grantService) : ControllerBase
{
    [HttpGet("kits")]
    public async Task<ActionResult<List<EquipmentKitOptionResponse>>> ListKits(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();
        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        return await BuildOptionsAsync(campaignGmId);
    }

    [HttpPost("choose")]
    public async Task<IActionResult> Choose(Guid sheetId, ChooseEquipmentKitRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();
        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        if (sheet.EquipmentKitId is not null)
            return BadRequest("Equipagem inicial já escolhida.");

        if (!Guid.TryParse(request.KitId, out var kitId))
            return BadRequest("KitId inválido.");
        var kit = await db.EquipmentKits.FirstOrDefaultAsync(k => k.Id == kitId && !k.IsDeleted);
        if (kit is null)
            return NotFound();

        var fixedItems = await db.EquipmentKitItems.Where(i => i.KitId == kitId).ToListAsync();
        var choiceSlots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kitId).ToListAsync();
        var (plan, error) = await grantService.BuildPlanAsync(kit, fixedItems, choiceSlots, campaignGmId, request.ChoiceSelections);
        if (plan is null)
            return BadRequest(error);

        foreach (var grant in plan.Grants)
            AddGrant(sheetId, grant);

        foreach (var itemId in plan.Grants.Select(g => g.ItemId).Distinct())
            await grantService.UpsertCampaignAttachmentAsync(sheet.CampaignId, itemId);

        sheet.Ciclos += plan.Ciclos;
        sheet.EquipmentKitId = kit.Id;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private void AddGrant(Guid sheetId, EquipmentGrantPlanItem grant)
    {
        switch (grant.Tipo)
        {
            case RuinaRPG.Domain.Items.ItemTipo.Arma:
                db.CharacterWeapons.Add(new CharacterWeapon { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.Escudo:
                db.CharacterShields.Add(new CharacterShield { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.ItemGeral:
                db.CharacterInventoryItems.Add(new CharacterInventoryItem { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = grant.ItemId, Qtd = grant.Qtd });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.Artefato:
                db.CharacterArtifacts.Add(new CharacterArtifact { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ArtifactItemId = grant.ItemId });
                break;
        }
    }

    private async Task<List<EquipmentKitOptionResponse>> BuildOptionsAsync(Guid gmId)
    {
        var kits = await db.EquipmentKits.Where(k => !k.IsDeleted).OrderBy(k => k.Nome).ToListAsync();
        var responses = new List<EquipmentKitOptionResponse>();
        foreach (var kit in kits)
        {
            var slots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kit.Id).ToListAsync();
            var slotOptions = new List<EquipmentKitChoiceSlotOptionResponse>();
            foreach (var slot in slots)
                slotOptions.Add(new EquipmentKitChoiceSlotOptionResponse(slot.Id.ToString(), slot.Label, await grantService.ResolveEligibleOptionsAsync(slot, gmId)));
            responses.Add(new EquipmentKitOptionResponse(kit.Id.ToString(), kit.Nome, kit.Descricao, kit.Ciclos, slotOptions));
        }
        return responses;
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 4: Write `NpcEquipagemController`.** Same route shape under `api/npc-sheets/{sheetId}/equipagem`, same two actions, with these NPC-specific differences:
  - Auth: `GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId)` (no Campaign lookup — `NpcSheet.GmId` is direct), matching `NpcArsenalController.CheckAuthorizationAsync`.
  - `gmId` for `BuildOptionsAsync`/`BuildPlanAsync` is `sheet.GmId` directly.
  - **`NpcSheet` has no `CampaignId` column** (unlike `CharacterSheet`) — an NPC only has a campaign in the specific context of "the campaign where it's attached and its current `OwnerId` is a member," resolved exactly the way `NpcSheetsController.ToResponseAsync` already does:
    ```csharp
    var campaignId = await db.CampaignAttachments
        .Where(a => a.NpcSheetId == sheetId && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
        .Select(a => (Guid?)a.CampaignId)
        .FirstOrDefaultAsync();
    ```
    In `Choose`, only run the `UpsertCampaignAttachmentAsync` loop `if (campaignId is not null)` — a GM applying a kit to their own not-yet-granted NPC has no campaign to auto-attach into, and that's a correct no-op, not an error.
  - `AddGrant` targets `NpcWeapon`/`NpcShield`/`NpcInventoryItem`/`NpcArtifact` (all keyed by `NpcSheetId`, `Artefato` grants use `ArtifactItemId`), same `DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0` treatment as the Character side.

- [ ] **Step 5: Register `EquipmentKitGrantService` for DI.** In `Program.cs`, find where other scoped services are registered (e.g. `builder.Services.AddScoped<IRulebookRenderer, RulebookRenderer>();`) and add: `builder.Services.AddScoped<EquipmentKitGrantService>();`.

- [ ] **Step 6: Write the mirrored `NpcEquipagemControllerTests.cs`**, adapted from the Character-side tests above (same 6 cases), using this repo's existing `NpcSheetsController`/`NpcArsenalController` test helpers (`CreateSheetAsync`, `SetVarianteAsync`-style patterns from `NpcRacialTraitsControllerTests.cs`) for setup, plus one NPC-only case:

```csharp
    [Fact]
    public async Task Choose_on_a_GM_owned_Npc_not_attached_to_any_campaign_skips_the_auto_attach_step_without_erroring()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemNpcGm7", "equipagemnpc7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // this repo's existing Npc-sheet-creation helper — not attached to any campaign
        var kits = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/equipagem/kits", gmToken)))
            .Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>();
        var negociante = kits!.Single(k => k.Nome == "Negociante");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/equipagem/choose", gmToken, new ChooseEquipmentKitRequest(negociante.Id, [])));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
```

- [ ] **Step 7: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterEquipagemControllerTests|FullyQualifiedName~NpcEquipagemControllerTests" -v`
Expected: PASS

- [ ] **Step 8: Update `Requisitos - Campanha.md`.** Add a short paragraph (continue the doc's `**R00NN**` sequence, near R0008/R0009's public/private attachment rules) documenting the one narrow exception: applying an Equipagem kit auto-attaches and publishes whatever items it grants, since Equipagem items are "de conhecimento geral" — this bypasses the normal manual attach-then-publish flow for exactly this action.

- [ ] **Step 9: Run the full build.**

Run: `dotnet build`
Expected: 0 warnings/errors.

- [ ] **Step 10: Commit.**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterEquipagemController.cs src/RuinaRPG.Api/Controllers/NpcEquipagemController.cs src/RuinaRPG.Api/Program.cs "Docs/Requisitos/Requisitos - Campanha.md" tests/RuinaRPG.Tests.Integration/Controllers/CharacterEquipagemControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcEquipagemControllerTests.cs
git commit -m "feat: endpoints de escolha da Equipagem inicial (Personagem e NPC)"
```

---

### Task 10: Client — button, dialog, page wiring

**Files:**
- Create: `src/RuinaRPG.Client/Shared/EscolherEquipagemDialog.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`
- Modify: `Docs/Requisitos/Requisitos - Ficha de Criaturas.md`

**Interfaces:**
- Consumes: `EquipmentKitOptionResponse`/`ChooseEquipmentKitRequest` (Task 5), `GET .../equipagem/kits`/`POST .../equipagem/choose` (Task 9).
- Produces: the "Escolher Equipamento Inicial" button + dialog on both sheet pages.

- [ ] **Step 1: Add `EquipmentKitId` to both pages' `SheetFormModel` and load it.** In `FichaDePersonagem.razor`, find `public string? HistoricoId { get; set; }` inside `SheetFormModel` and add right after:

```csharp
        public string? EquipmentKitId { get; set; }
```

Find `_form.HistoricoId = sheet.HistoricoId;` (in the sheet-load method) and add right after:

```csharp
        _form.EquipmentKitId = sheet.EquipmentKitId;
```

Mirror both edits in `FichaDeNpc.razor` (same property name, same load-site pattern — find its own equivalent `_form.HistoricoId = sheet.HistoricoId;` line).

Do **not** add `EquipmentKitId` to either page's `PUT`-construction argument list (the trailing `_form.HistoricoId)` call in the general `SaveIfValidAsync`/`Update*Request` construction) — `UpdateCharacterSheetRequest`/`UpdateNpcSheetRequest` never gained a real `EquipmentKitId` write path (Task 7 confirmed `Update` ignores it entirely); only the dedicated `.../equipagem/choose` endpoint (Task 9) ever sets it.

- [ ] **Step 2: Create `EscolherEquipagemDialog.razor`.**

```razor
@using MudBlazor
@using RuinaRPG.Contracts.Rules
@inject HttpClient Http

@* Requisitos - Ficha de Personagem: botão "Escolher Equipamento Inicial" na aba Posses. Kits vêm
   do catálogo ao vivo (GET .../equipagem/kits); slots de escolha ("de sua escolha"/"ou") mostram
   um segundo select cada, resolvido contra o catálogo do GM da campanha da ficha. *@
<MudDialog @bind-Visible="Visible">
    <TitleContent>Escolher Equipamento Inicial</TitleContent>
    <DialogContent>
        <MudSelect T="string" Label="Kit" @bind-Value="_selectedKitId" Placeholder="Escolha um kit">
            @foreach (var kit in _kits)
            {
                <MudSelectItem Value="@kit.Id">@kit.Nome</MudSelectItem>
            }
        </MudSelect>

        @if (SelectedKit is not null)
        {
            <MudText Class="mt-2">@SelectedKit.Descricao</MudText>
            @if (SelectedKit.Ciclos > 0)
            {
                <MudText Typo="Typo.body2" Class="mt-1"><em>+@SelectedKit.Ciclos Ciclos</em></MudText>
            }

            @foreach (var slot in SelectedKit.ChoiceSlots)
            {
                <MudSelect T="string" Label="@slot.Label" Class="mt-3" Value="@_selections.GetValueOrDefault(slot.SlotId)"
                           ValueChanged="@(v => OnSlotSelectionChanged(slot.SlotId, v))" Placeholder="@($"Escolha: {slot.Label}")">
                    @foreach (var option in slot.Options)
                    {
                        <MudSelectItem Value="@option.ItemId">@option.Nome</MudSelectItem>
                    }
                </MudSelect>
            }
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancelar</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" Disabled="!CanConfirm" OnClick="ConfirmAsync">Escolher Equipagem</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public string SheetKind { get; set; } = "character-sheets"; // or "npc-sheets"
    [Parameter] public string SheetId { get; set; } = "";
    [Parameter] public EventCallback OnChosen { get; set; }

    private List<EquipmentKitOptionResponse> _kits = new();
    private string? _selectedKitId;
    private readonly Dictionary<string, string> _selections = new();

    private EquipmentKitOptionResponse? SelectedKit => _kits.FirstOrDefault(k => k.Id == _selectedKitId);
    private bool CanConfirm => SelectedKit is not null && SelectedKit.ChoiceSlots.All(s => _selections.ContainsKey(s.SlotId));

    protected override async Task OnParametersSetAsync()
    {
        if (Visible && _kits.Count == 0)
        {
            var response = await Http.GetAsync($"{SheetKind}/{SheetId}/equipagem/kits");
            if (response.IsSuccessStatusCode)
                _kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitOptionResponse>>() ?? new();
        }
    }

    private void OnSlotSelectionChanged(string slotId, string? itemId)
    {
        if (itemId is null)
            _selections.Remove(slotId);
        else
            _selections[slotId] = itemId;
    }

    private async Task Cancel()
    {
        Visible = false;
        await VisibleChanged.InvokeAsync(false);
    }

    private async Task ConfirmAsync()
    {
        if (SelectedKit is null)
            return;

        var selections = SelectedKit.ChoiceSlots.Select(s => new ChoiceSlotSelectionRequest(s.SlotId, _selections[s.SlotId])).ToList();
        var response = await Http.PostAsJsonAsync($"{SheetKind}/{SheetId}/equipagem/choose", new ChooseEquipmentKitRequest(SelectedKit.Id, selections));
        if (!response.IsSuccessStatusCode)
            return;

        Visible = false;
        await VisibleChanged.InvokeAsync(false);
        await OnChosen.InvokeAsync();
    }
}
```

- [ ] **Step 3: Wire the button into `FichaDePersonagem.razor`'s Posses tab.** Find:

```razor
    <MudTabPanel Text="Posses">
        <Section Title="Inventário">
```

Replace with:

```razor
    <MudTabPanel Text="Posses">
        @if (_form.EquipmentKitId is null)
        {
            <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mb-3" OnClick="@(() => _equipagemDialogOpen = true)">Escolher Equipamento Inicial</MudButton>
            <EscolherEquipagemDialog @bind-Visible="_equipagemDialogOpen" SheetKind="character-sheets" SheetId="@SheetId" OnChosen="OnEquipagemChosenAsync" />
        }
        <Section Title="Inventário">
```

Add the backing field and handler near the other `private bool _...` dialog-visibility fields and `OnInitializedAsync`:

```csharp
    private bool _equipagemDialogOpen;

    private async Task OnEquipagemChosenAsync()
    {
        await LoadSheetAsync();
        await Task.WhenAll(LoadTabs2And3Async(), LoadInventoryAsync(), LoadArtifactsAsync());
    }
```

- [ ] **Step 4: Mirror Step 3 in `FichaDeNpc.razor`** — same button/dialog placement (its own `<MudTabPanel Text="Posses"><Section Title="Inventário">` pair), `SheetKind="npc-sheets"`, and whatever this page's own equivalents of `LoadSheetAsync`/`LoadTabs2And3Async`/`LoadInventoryAsync`/`LoadArtifactsAsync` are named (check the file — they should closely mirror `FichaDePersonagem.razor`'s names, but confirm before writing `OnEquipagemChosenAsync`).

- [ ] **Step 5: Build the client.**

Run: `dotnet build src/RuinaRPG.Client`
Expected: 0 warnings/errors.

- [ ] **Step 6: Manually verify in the browser** (per CLAUDE.md's UI-change guidance): start the client (`dotnet run --project src/RuinaRPG.Client` or the repo's usual dev-server command), log in as a GM, open a Personagem sheet's Posses tab, confirm the button appears, click it, pick "Viajante", confirm, verify the button disappears and Mochila/Corda/Tocha/Ração de Viagem/25 Ciclos show up in the right places. Repeat once for a kit with a choice slot (Patrulheiro) to confirm the second select appears and is required. Repeat on a NPC sheet.

- [ ] **Step 7: Update `Requisitos - Ficha de Personagem.md`.** In section 5 (Posses), add a bullet/R00NN documenting the button: where it sits (above Inventário), that it's a one-time choice (disappears once made), and that each kit's items land in Armas/Escudos/Inventário automatically per their Tipo.

- [ ] **Step 8: Update `Requisitos - Ficha de Criaturas.md`.** Add a one-line diff note: Criatura has no Equipagem button (Espólios is loot, not starting gear) — matching how that doc already notes other Personagem-only fields it excludes.

- [ ] **Step 9: Commit.**

```bash
git add src/RuinaRPG.Client/Shared/EscolherEquipagemDialog.razor src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor "Docs/Requisitos/Requisitos - Ficha de Personagem.md" "Docs/Requisitos/Requisitos - Ficha de Criaturas.md"
git commit -m "feat: botão e popup de Equipagem inicial nas fichas de Personagem e NPC"
```

---

### Task 11: Livro de Regras "Equipagem" tab

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj`
- Modify: `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs`
- Modify: `Docs/Requisitos/Requisitos - Livro de Regras.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs` (add cases)

**Interfaces:**
- Consumes: `db.EquipmentKits`/`EquipmentKitItems`/`EquipmentKitChoiceSlots` (Task 3/4).
- Produces: a 7th `RulebookDocument` (slug `"equipagem"`) in `IRulebookRenderer.GetDocuments()`.

- [ ] **Step 1: Embed `Equipagem.md` as a build resource.** In `RuinaRPG.Infrastructure.csproj`, add, right after the `Historico.md` line:

```xml
    <EmbeddedResource Include="../../Docs/Sistema RPG/Equipagem.md" LogicalName="Equipagem.md" />
```

- [ ] **Step 2: Write the failing test.** Add to `RulebookControllerTests.cs` (reuse the file's existing `AuthedRequest` helper — by now it should already carry the optional 4th `body` param from the Histórico round):

```csharp
    [Fact]
    public async Task GetDocuments_includes_an_Equipagem_tab_built_from_the_live_kit_catalog()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookEquipGm1", "rulebookequip1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var documents = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var equipagem = documents!.Single(d => d.Slug == "equipagem");
        equipagem.IntroHtml.Should().NotBeNullOrWhiteSpace();
        equipagem.Sections.Should().Contain(s => s.Titulo == "Viajante");
    }
```

(`RulebookDocumentResponse` is this file's real, already-established response DTO type — confirmed via the file's own existing `ReadFromJsonAsync<List<RulebookDocumentResponse>>()` calls.)

- [ ] **Step 3: Run test to verify it fails.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RulebookControllerTests -v`
Expected: the new test FAILs (no "equipagem" slug yet); pre-existing tests in the file still pass.

- [ ] **Step 4: Add `BuildEquipagemAsync` to `RulebookRenderer.cs`.** Add `"equipagem"` to the `GetDocuments()` array (after `await BuildHistoricosAsync(),`):

```csharp
        await BuildEquipagemAsync(),
```

Then add the method, right after `BuildHistoricosAsync`:

```csharp
    /// <summary>
    /// Same hybrid-source treatment as BuildHistoricosAsync: IntroHtml from Equipagem.md's own
    /// lead-in paragraph, but the per-kit Sections come from the live EquipmentKits table instead
    /// of the Markdown (editing a kit via EquipmentKitsController is what changes those).
    /// </summary>
    private async Task<RulebookDocument> BuildEquipagemAsync()
    {
        var (intro, _) = SplitIntoSections(RulesDataProvider.ReadResource("Equipagem.md"), splitLevel: 1);

        var kits = await db.EquipmentKits.Where(k => !k.IsDeleted).OrderBy(k => k.Nome).ToListAsync();

        var sections = new List<RulebookSection>();
        foreach (var kit in kits)
        {
            var items = await db.EquipmentKitItems.Where(i => i.KitId == kit.Id).ToListAsync();
            var slots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kit.Id).ToListAsync();

            var html = WebUtility.HtmlEncode(kit.Descricao).Replace("\n", "<br />") + "<ul>"
                + string.Join("", items.Select(i => $"<li>{WebUtility.HtmlEncode(i.Nome)} x{i.Qtd}</li>"))
                + string.Join("", slots.Select(s => $"<li>{WebUtility.HtmlEncode(s.Label)} (escolha){(s.BonusNome is not null ? $" — +{s.BonusQtd} {WebUtility.HtmlEncode(s.BonusNome)} se escolher da subcategoria {WebUtility.HtmlEncode(s.BonusSubcategoria)}" : "")}</li>"))
                + "</ul>"
                + (kit.Ciclos > 0 ? $"<p><em>+{kit.Ciclos} Ciclos</em></p>" : "");

            sections.Add(new RulebookSection(Slugify(kit.Nome), kit.Nome, html));
        }

        return new RulebookDocument("equipagem", "Equipagem", intro, sections);
    }
```

- [ ] **Step 5: Update the class's doc comment.** The comment above `GetDocuments()` currently says "6 documents" / lists 6 — update it to 7, mentioning Equipagem gets the same treatment as Históricos.

- [ ] **Step 6: Run test to verify it passes.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RulebookControllerTests -v`
Expected: PASS

- [ ] **Step 7: Update `Requisitos - Livro de Regras.md`.** Add "Equipagem" to R0001's document table (same row shape as the existing "Históricos" row — "uma por kit, vem do catálogo de EquipmentKits — não do Markdown"), and add a new R00NN mirroring R0007 ("A aba de Equipagem reflete o catálogo de kits em tempo real"), naming `EquipmentKitsController` as what changes it.

- [ ] **Step 8: Run the full build.**

Run: `dotnet build`
Expected: 0 warnings/errors.

- [ ] **Step 9: Commit.**

```bash
git add src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs "Docs/Requisitos/Requisitos - Livro de Regras.md" tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs
git commit -m "feat: aba Equipagem no Livro de Regras"
```

---

### Task 12: Auditoria: Equipagem page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/AuditoriaEquipagem.razor`
- Modify: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md`

**Interfaces:**
- Consumes: `EquipmentKitResponse`/`CreateEquipmentKitRequest`/`UpdateEquipmentKitRequest` (Task 5), `api/equipment-kits` (Task 6).

- [ ] **Step 1: Add the NavLink.** In `RulesAuditorNavLinks.razor`, add right after the `auditoria/historicos` line:

```razor
    <MudNavLink Href="auditoria/equipagem">Auditoria: Equipagem</MudNavLink>
```

- [ ] **Step 2: Write `AuditoriaEquipagem.razor`.** Mirror `AuditoriaHistoricos.razor`'s structure (list/create/edit/delete, `_forbidden` handling, `DismissibleAlert`, `Breadcrumbs`), extended with sub-tables for Items and ChoiceSlots per kit:

```razor
@page "/auditoria/equipagem"
@inject HttpClient Http
@using RuinaRPG.Contracts.Rules
@using MudBlazor

<Breadcrumbs Items="@Crumbs" />
<MudText Typo="Typo.h3">Auditoria: Equipagem</MudText>

<DismissibleAlert @bind-Message="_errorMessage" Class="mt-3" />

@if (_forbidden)
{
    <MudAlert Severity="Severity.Warning" Variant="Variant.Filled" Class="mt-3">Você não é o Auditor de Regras designado — sem permissão para editar.</MudAlert>
}
else
{
    <Section Title="Adicionar Kit">
        <MudTextField T="string" @bind-Value="_newForm.Nome" Label="Nome" />
        <MudTextField T="string" @bind-Value="_newForm.Descricao" Label="Descrição" Lines="3" />
        <MudNumericField T="int" @bind-Value="_newForm.Ciclos" Label="Ciclos" />
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-2" OnClick="AddAsync">Adicionar</MudButton>
    </Section>

    <Section Title="Kits">
        @foreach (var kit in _kits)
        {
            <MudExpansionPanel Text="@kit.Nome">
                <MudTextField T="string" Value="@kit.Nome" ValueChanged="@(v => UpdateAsync(kit, nome: v))" Label="Nome" />
                <MudTextField T="string" Value="@kit.Descricao" ValueChanged="@(v => UpdateAsync(kit, descricao: v))" Label="Descrição" Lines="3" />
                <MudNumericField T="int" Value="@kit.Ciclos" ValueChanged="@(v => UpdateAsync(kit, ciclos: v))" Label="Ciclos" />

                <MudText Typo="Typo.h6" Class="mt-3">Itens fixos</MudText>
                <MudSimpleTable Dense="true">
                    <thead><tr><th>Nome</th><th>Tipo</th><th>Qtd</th><th></th></tr></thead>
                    <tbody>
                        @foreach (var item in kit.Items)
                        {
                            <tr>
                                <td>@item.Nome</td><td>@item.Tipo</td><td>@item.Qtd</td>
                                <td><MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" OnClick="@(() => RemoveItemAsync(kit, item))" /></td>
                            </tr>
                        }
                    </tbody>
                </MudSimpleTable>
                <div class="d-flex" style="gap:8px">
                    <MudTextField T="string" @bind-Value="_itemForm[kit.Id].Nome" Label="Nome do item" />
                    <MudSelect T="string" @bind-Value="_itemForm[kit.Id].Tipo" Label="Tipo">
                        <MudSelectItem Value="@("ItemGeral")">Item Geral</MudSelectItem>
                        <MudSelectItem Value="@("Arma")">Arma</MudSelectItem>
                        <MudSelectItem Value="@("Escudo")">Escudo</MudSelectItem>
                        <MudSelectItem Value="@("Artefato")">Artefato</MudSelectItem>
                    </MudSelect>
                    <MudNumericField T="int" @bind-Value="_itemForm[kit.Id].Qtd" Label="Qtd" />
                    <MudButton Variant="Variant.Outlined" OnClick="@(() => AddItemAsync(kit))">Adicionar item</MudButton>
                </div>

                <MudText Typo="Typo.h6" Class="mt-3">Slots de escolha</MudText>
                <MudSimpleTable Dense="true">
                    <thead><tr><th>Label</th><th>Subcategorias</th><th>Tier</th><th>Qtd</th><th></th></tr></thead>
                    <tbody>
                        @foreach (var slot in kit.ChoiceSlots)
                        {
                            <tr>
                                <td>@slot.Label</td><td>@(slot.Subcategorias is null ? "qualquer" : string.Join(", ", slot.Subcategorias))</td><td>@(slot.Tier ?? "qualquer")</td><td>@slot.Qtd</td>
                                <td><MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" OnClick="@(() => RemoveSlotAsync(kit, slot))" /></td>
                            </tr>
                        }
                    </tbody>
                </MudSimpleTable>
                <div class="d-flex" style="gap:8px">
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].Label" Label="Label" />
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].SubcategoriasText" Label="Subcategorias (vírgula, vazio = qualquer)" />
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].Tier" Label="Tier (vazio = qualquer)" />
                    <MudNumericField T="int" @bind-Value="_slotForm[kit.Id].Qtd" Label="Qtd" />
                    <MudButton Variant="Variant.Outlined" OnClick="@(() => AddSlotAsync(kit))">Adicionar slot</MudButton>
                </div>

                <MudButton Variant="Variant.Outlined" Color="Color.Error" Class="mt-3" OnClick="@(() => DeleteAsync(kit.Id))">Excluir Kit</MudButton>
            </MudExpansionPanel>
        }
    </Section>
}

@code {
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Auditoria: Equipagem"),
    };

    private List<EquipmentKitResponse> _kits = new();
    private string? _errorMessage;
    private bool _forbidden;
    private readonly NewKitFormModel _newForm = new();
    private readonly Dictionary<string, NewItemFormModel> _itemForm = new();
    private readonly Dictionary<string, NewSlotFormModel> _slotForm = new();

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var response = await Http.GetAsync("equipment-kits");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar os kits de Equipagem.";
            return;
        }

        _kits = await response.Content.ReadFromJsonAsync<List<EquipmentKitResponse>>() ?? new();
        foreach (var kit in _kits)
        {
            _itemForm.TryAdd(kit.Id, new NewItemFormModel());
            _slotForm.TryAdd(kit.Id, new NewSlotFormModel());
        }
    }

    private async Task AddAsync()
    {
        var response = await Http.PostAsJsonAsync("equipment-kits", new CreateEquipmentKitRequest(_newForm.Nome, _newForm.Descricao, _newForm.Ciclos, new(), new()));
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            _errorMessage = string.IsNullOrWhiteSpace(detail) ? "Não foi possível adicionar o kit." : detail;
            return;
        }

        _newForm.Nome = "";
        _newForm.Descricao = "";
        _newForm.Ciclos = 0;
        await LoadAsync();
    }

    private async Task UpdateAsync(EquipmentKitResponse current, string? nome = null, string? descricao = null, int? ciclos = null)
    {
        var itemsInput = current.Items.Select(i => new EquipmentKitItemInput(i.Nome, i.Tipo, i.Qtd, i.SubcategoriaHint)).ToList();
        var slotsInput = current.ChoiceSlots.Select(s => new EquipmentKitChoiceSlotInput(s.Label, s.Tipo, s.Subcategorias, s.Tier, s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd)).ToList();

        var response = await Http.PutAsJsonAsync($"equipment-kits/{current.Id}", new UpdateEquipmentKitRequest(
            nome ?? current.Nome, descricao ?? current.Descricao, ciclos ?? current.Ciclos, itemsInput, slotsInput));
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

    private async Task AddItemAsync(EquipmentKitResponse kit)
    {
        var form = _itemForm[kit.Id];
        var itemsInput = kit.Items.Select(i => new EquipmentKitItemInput(i.Nome, i.Tipo, i.Qtd, i.SubcategoriaHint))
            .Append(new EquipmentKitItemInput(form.Nome, form.Tipo, form.Qtd, null)).ToList();
        var slotsInput = kit.ChoiceSlots.Select(s => new EquipmentKitChoiceSlotInput(s.Label, s.Tipo, s.Subcategorias, s.Tier, s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd)).ToList();

        var response = await Http.PutAsJsonAsync($"equipment-kits/{kit.Id}", new UpdateEquipmentKitRequest(kit.Nome, kit.Descricao, kit.Ciclos, itemsInput, slotsInput));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar o item.";
            return;
        }

        _itemForm[kit.Id] = new NewItemFormModel();
        await LoadAsync();
    }

    private async Task AddSlotAsync(EquipmentKitResponse kit)
    {
        var form = _slotForm[kit.Id];
        var subcategorias = string.IsNullOrWhiteSpace(form.SubcategoriasText) ? null : form.SubcategoriasText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var itemsInput = kit.Items.Select(i => new EquipmentKitItemInput(i.Nome, i.Tipo, i.Qtd, i.SubcategoriaHint)).ToList();
        var slotsInput = kit.ChoiceSlots.Select(s => new EquipmentKitChoiceSlotInput(s.Label, s.Tipo, s.Subcategorias, s.Tier, s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd))
            .Append(new EquipmentKitChoiceSlotInput(form.Label, "Arma", subcategorias, string.IsNullOrWhiteSpace(form.Tier) ? null : form.Tier, form.Qtd, null, null, null)).ToList();

        var response = await Http.PutAsJsonAsync($"equipment-kits/{kit.Id}", new UpdateEquipmentKitRequest(kit.Nome, kit.Descricao, kit.Ciclos, itemsInput, slotsInput));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar o slot de escolha.";
            return;
        }

        _slotForm[kit.Id] = new NewSlotFormModel();
        await LoadAsync();
    }

    private async Task RemoveItemAsync(EquipmentKitResponse kit, EquipmentKitItemResponse item)
    {
        var itemsInput = kit.Items.Where(i => i.Id != item.Id).Select(i => new EquipmentKitItemInput(i.Nome, i.Tipo, i.Qtd, i.SubcategoriaHint)).ToList();
        var slotsInput = kit.ChoiceSlots.Select(s => new EquipmentKitChoiceSlotInput(s.Label, s.Tipo, s.Subcategorias, s.Tier, s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd)).ToList();

        var response = await Http.PutAsJsonAsync($"equipment-kits/{kit.Id}", new UpdateEquipmentKitRequest(kit.Nome, kit.Descricao, kit.Ciclos, itemsInput, slotsInput));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível remover o item.";
            return;
        }

        await LoadAsync();
    }

    private async Task RemoveSlotAsync(EquipmentKitResponse kit, EquipmentKitChoiceSlotResponse slot)
    {
        var itemsInput = kit.Items.Select(i => new EquipmentKitItemInput(i.Nome, i.Tipo, i.Qtd, i.SubcategoriaHint)).ToList();
        var slotsInput = kit.ChoiceSlots.Where(s => s.Id != slot.Id)
            .Select(s => new EquipmentKitChoiceSlotInput(s.Label, s.Tipo, s.Subcategorias, s.Tier, s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd)).ToList();

        var response = await Http.PutAsJsonAsync($"equipment-kits/{kit.Id}", new UpdateEquipmentKitRequest(kit.Nome, kit.Descricao, kit.Ciclos, itemsInput, slotsInput));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível remover o slot de escolha.";
            return;
        }

        await LoadAsync();
    }

    private async Task DeleteAsync(string id)
    {
        var response = await Http.DeleteAsync($"equipment-kits/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível excluir — este kit pode estar em uso em alguma ficha.";
            return;
        }

        await LoadAsync();
    }

    private class NewKitFormModel
    {
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        public int Ciclos { get; set; }
    }

    private class NewItemFormModel
    {
        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "ItemGeral";
        public int Qtd { get; set; } = 1;
    }

    private class NewSlotFormModel
    {
        public string Label { get; set; } = "";
        public string SubcategoriasText { get; set; } = "";
        public string Tier { get; set; } = "";
        public int Qtd { get; set; } = 1;
    }
}
```

- [ ] **Step 3: Build the client.**

Run: `dotnet build src/RuinaRPG.Client`
Expected: 0 warnings/errors.

- [ ] **Step 4: Manually verify in the browser.** Log in as the Rules Auditor, open `/auditoria/equipagem`, confirm the 12 seeded kits list, add a test item/slot to one, remove it, confirm a non-Auditor GM sees the "sem permissão" alert instead.

- [ ] **Step 5: Update `Requisitos - Auditoria de Regras.md`.** Add a new R00NN (continue the doc's sequence) documenting Auditor CRUD over the Equipagem-kit catalog (fixed items + choice slots), the in-use delete guard, and — mirroring the caveat already added for Históricos (R0008) — a one-sentence note that renaming a kit risks a duplicate reseed on next restart (same `EquipmentKitSeeder` Nome-only-match limitation documented in Task 4).

- [ ] **Step 6: Commit.**

```bash
git add src/RuinaRPG.Client/Pages/AuditoriaEquipagem.razor src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor "Docs/Requisitos/Requisitos - Auditoria de Regras.md"
git commit -m "feat: página de Auditoria para gerenciar o catálogo de Equipagem"
```
