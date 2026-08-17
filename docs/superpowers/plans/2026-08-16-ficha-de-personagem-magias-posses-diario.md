# Ficha de Personagem — Magias & Habilidades, Posses & Diário Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the Ficha de Personagem: tab 4 (Magias & Habilidades — Habilidade Racial, Magias & Habilidades with Banco integration, Runas, Maestrias), tab 5 (Posses — Inventário, Artefatos, Afeições, Características), tab 6 (Diário). This is also where several deliberately-deferred cross-plan items get closed: **Banco de Magias e Habilidades R0001/R0003** (auto-copy to the bank; start from a bank entry), **Vitalidade/Foco/Adrenalina máximo** (needs Artefatos, built here in 5.b), and **Ficha de Personagem's own Características** (needs the `Trait` table, built by the Compêndio de Regras plan).

**Architecture:** `CharacterSpellAbility`/`CharacterSpellAbilityEffect` mirror `SpellAbilityBankEntry`/`SpellAbilityBankEffect` (Banco de Magias plan) field-for-field — an independent-copy pattern, not a live reference: creating one always also inserts a `SpellAbilityBankEntry` copy (R0001), and starting from an existing bank entry copies its fields in rather than referencing it (`SourceBankEntryId` is nullable, traceability-only, per Modelo de Dados). `CharacterInventoryItem`/`CharacterArtifact` are "live reference to Catálogo" entities, same pattern as tab 3's arsenal. `CharacterTrait` is a join row (`TraitId` FK into the Compêndio plan's `Trait` table) plus a `Polaridade` copy (a Trait's own Polaridade never changes, but the FK alone doesn't tell the query "which list is this row in" without a join — storing it denormalized on the join row avoids that join for a very hot read path). The Diário reuses `DiaryEntry`/`DiaryEntryImage` (Campanha — Fundação plan) with `CharacterSheetId` populated instead of `CampaignId` — the shared-table architecture that plan's Task 4 set up specifically for this reuse.

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Ficha de Personagem.md` R0001 tabs 4-6, `Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md` R0001/R0003, `Docs/Sistema RPG/Ruína RPG - Sistema Básico.md` §7 (racial abilities), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §6.1 (relevant child tables) and §7 (Diário, already modeled).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Edit authorization for every endpoint reuses `CharacterSheetAuthorization.CanEdit` (Ficha de Personagem — Fundação plan).
- Every Magia/Habilidade created on a sheet — from scratch or from a bank entry — is *also* inserted into `SpellAbilityBankEntry`/`SpellAbilityBankEffect` as an independent copy (Banco de Magias R0001) — editing the sheet copy afterward never touches the bank copy, and vice versa.
- The sheet's Diário (tab 6) is visible to the owning player **and** the GM (unlike the Campanha diary, which is GM-only) — a distinct visibility rule from the one Campanha — Fundação built; this plan's diary endpoints allow read access to both, not just the GM.
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Domain — racial ability lookup (4.a)

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/RacialAbility.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/RacialAbilityLookup.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/RacialAbilityLookupTests.cs`

**Interfaces:**
- Consumes: `Variante` (Ficha de Personagem — Fundação plan).
- Produces: `RacialAbility(string Nome, string Descricao)`; `RacialAbilityLookup.For(Variante variante) : RacialAbility`. Task 2 (sheet response) consumes this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/RacialAbilityLookupTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class RacialAbilityLookupTests
{
    // Real text from Ruína RPG - Sistema Básico.md §7.
    [Theory]
    [InlineData(Variante.Sinir, "Racial (Arca)")]
    [InlineData(Variante.Laonir, "Racial (Arca)")]
    [InlineData(Variante.PhylacTai, "Racial (Lei da Selva)")]
    [InlineData(Variante.EsPhylauc, "Racial (Lei da Selva)")]
    [InlineData(Variante.Yavos, "Racial (Sobre Voo)")]
    [InlineData(Variante.Koroanos, "Racial (Sobre Voo)")]
    [InlineData(Variante.Alora, "Racial (Amplificador Místico)")]
    public void For_returns_the_correct_racial_name_per_variante(Variante variante, string expectedNome)
    {
        var result = RacialAbilityLookup.For(variante);

        result.Nome.Should().Be(expectedNome);
    }

    [Fact]
    public void For_Alora_returns_the_amplificador_mistico_description()
    {
        var result = RacialAbilityLookup.For(Variante.Alora);

        result.Descricao.Should().Contain("aumentar a escala do dado da rolagem em +1 degrau");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RacialAbilityLookupTests`
Expected: FAIL to compile.

- [ ] **Step 3: Write `RacialAbility` and `RacialAbilityLookup`**

`src/RuinaRPG.Domain/CharacterSheets/RacialAbility.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public sealed record RacialAbility(string Nome, string Descricao);
```

`src/RuinaRPG.Domain/CharacterSheets/RacialAbilityLookup.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class RacialAbilityLookup
{
    private static readonly Dictionary<Variante, RacialAbility> Abilities = new()
    {
        [Variante.Sinir] = new("Racial (Arca)", "Role 1d18 na tabela de Arcas."),
        [Variante.Laonir] = new("Racial (Arca)", "Role 1d18 na tabela de Arcas."),
        [Variante.PhylacTai] = new("Racial (Lei da Selva)", "Recupera uma quantidade de PV igual ao Foco (PF) gasto em habilidades e magias."),
        [Variante.EsPhylauc] = new("Racial (Lei da Selva)", "Recupera uma quantidade de PV igual ao Foco (PF) gasto em habilidades e magias."),
        [Variante.Yavos] = new("Racial (Sobre Voo)", "Passiva. Capacidade de voo livre."),
        [Variante.Koroanos] = new("Racial (Sobre Voo)", "Passiva. Capacidade de voo livre."),
        [Variante.Alora] = new("Racial (Amplificador Místico)", "Ao usar qualquer item ou artefato de origem holística, o Alóra pode aumentar a escala do dado da rolagem em +1 degrau (ex: de d6 para d8, de d10 para d12).")
    };

    public static RacialAbility For(Variante variante) => Abilities[variante];
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RacialAbilityLookupTests`
Expected: PASS (8/8).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/RacialAbility.cs src/RuinaRPG.Domain/CharacterSheets/RacialAbilityLookup.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/RacialAbilityLookupTests.cs
git commit -m "feat: add racial ability lookup"
```

---

### Task 2: Api — racial ability read endpoint

**Files:**
- Create: `src/RuinaRPG.Contracts/CharacterSheets/RacialAbilityResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `RacialAbilityLookup.For` (Task 1).
- Produces: `GET /api/character-sheets/{id}/racial-ability` → `200` + `RacialAbilityResponse(string Nome, string Descricao)`, `null`/`—` fields when `Variante` isn't set yet (R0001 4.a: "Exibe travessão se nenhuma Variante foi escolhida").

- [ ] **Step 1: Write the contract**

`src/RuinaRPG.Contracts/CharacterSheets/RacialAbilityResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record RacialAbilityResponse(string? Nome, string? Descricao);
```

- [ ] **Step 2: Write the failing test**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`:

```csharp
[Fact]
public async Task RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmRacial1", "sheetracial1@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerRacial1", "sheetplayerracial1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Racial");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var beforeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken));
    (await beforeResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().BeNull();

    await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Nephrytes", Variante = "Yavos" }));

    var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken));
    (await afterResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().Be("Racial (Sobre Voo)");
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after`
Expected: FAIL — the route doesn't exist yet.

- [ ] **Step 4: Add the `RacialAbility` action**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, add:

```csharp
[HttpGet("api/character-sheets/{id}/racial-ability")]
public async Task<ActionResult<RacialAbilityResponse>> RacialAbility(Guid id)
{
    var sheet = await db.CharacterSheets.FindAsync(id);
    if (sheet is null)
        return NotFound();

    if (sheet.Variante is null)
        return new RacialAbilityResponse(null, null);

    var ability = RacialAbilityLookup.For(sheet.Variante.Value);
    return new RacialAbilityResponse(ability.Nome, ability.Descricao);
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/RacialAbilityResponse.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs
git commit -m "feat: add racial ability read endpoint"
```

---

### Task 3: Infrastructure — `CharacterSpellAbility`/`CharacterSpellAbilityEffect` entities and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbility.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbilityEffect.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CharacterSpellAbilityMigrationTests.cs`

**Interfaces:**
- Consumes: `SpellAbilityTipo` (Banco de Magias plan).
- Produces: `CharacterSpellAbility` (`Guid Id`, `Guid CharacterSheetId`, `Guid? SourceBankEntryId`, `string Nome`, `SpellAbilityTipo Tipo`, `int Grau`, `int GastoEmPI`, `int Custo`, `string Descricao`, `List<CharacterSpellAbilityEffect> Efeitos`); `CharacterSpellAbilityEffect` (same shape as `SpellAbilityBankEffect`, FK'd to `CharacterSpellAbilityId`). `RuinaRpgDbContext.CharacterSpellAbilities`/`CharacterSpellAbilityEffects`. Task 4 depends on this shape.

- [ ] **Step 1: Write the entities**

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbility.cs`:

```csharp
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSpellAbility
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid? SourceBankEntryId { get; set; }
    public required string Nome { get; set; }
    public SpellAbilityTipo Tipo { get; set; }
    public int Grau { get; set; }
    public int GastoEmPI { get; set; }
    public int Custo { get; set; }
    public required string Descricao { get; set; }
    public List<CharacterSpellAbilityEffect> Efeitos { get; set; } = [];
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbilityEffect.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSpellAbilityEffect
{
    public Guid Id { get; set; }
    public Guid CharacterSpellAbilityId { get; set; }
    public required string EfeitoNome { get; set; }
    public int? Quantidade { get; set; }
    public int CustoPI { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CharacterSpellAbilityMigrationTests.cs` — follow the exact structure of the Banco de Magias plan's `SpellAbilityBankMigrationTests` (Task 2 of that plan): set up a GM, player, campaign, `CharacterSheet`, add one `CharacterSpellAbility` with one `CharacterSpellAbilityEffect`, assert both tables exist and the effect cascades on entry deletion. Assert the migration name ends with `AddCharacterSpellAbilities`.

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSpellAbilityMigrationTests`
Expected: FAIL — no `AddCharacterSpellAbilities` migration exists yet.

- [ ] **Step 4: Register the DbSets and relationships**

Add to `RuinaRpgDbContext`:

```csharp
public DbSet<CharacterSpellAbility> CharacterSpellAbilities => Set<CharacterSpellAbility>();
public DbSet<CharacterSpellAbilityEffect> CharacterSpellAbilityEffects => Set<CharacterSpellAbilityEffect>();
```

Inside `OnModelCreating`, mirror the Banco de Magias plan's `SpellAbilityBankEntry`/`SpellAbilityBankEffect` configuration exactly, but keyed on `CharacterSheet`/`CharacterSpellAbility` instead:

```csharp
builder.Entity<CharacterSpellAbility>(entity =>
{
    entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(e => e.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
    entity.HasMany(e => e.Efeitos).WithOne().HasForeignKey(ef => ef.CharacterSpellAbilityId).OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 5: Create the migration and run the test to verify it passes**

```bash
dotnet ef migrations add AddCharacterSpellAbilities \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSpellAbilityMigrationTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbility.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbilityEffect.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/CharacterSpellAbilityMigrationTests.cs
git commit -m "feat: add character spell/ability entities and migration"
```

---

### Task 4: Api — Magias & Habilidades CRUD, closing Banco de Magias R0001/R0003

**Files:**
- Create: `src/RuinaRPG.Contracts/CharacterSheets/AddCharacterSpellAbilityRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSpellAbilityResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSpellAbilitiesControllerTests.cs`

**Interfaces:**
- Consumes: `SpellAbilityCostCalculator` (Banco de Magias plan), `CharacterSpellAbility`/`CharacterSpellAbilityEffect` (Task 3), `SpellAbilityBankEntry`/`SpellAbilityBankEffect` (Banco de Magias plan), `CharacterSheetAuthorization.CanEdit`.
- Produces: `POST /api/character-sheets/{sheetId}/spell-abilities` → `201`, accepting EITHER a from-scratch payload (`Nome`/`Tipo`/`Grau`/`Descricao`/`Efeitos`) OR a `SourceBankEntryId` to copy from — either way, ALSO inserts a new independent `SpellAbilityBankEntry` copy (R0001). `GET .../spell-abilities` → `200` + list. `DELETE .../spell-abilities/{id}` → `204`/`404` (removes the sheet copy only — the bank copy, if any resulted from this creation, is untouched, matching R0001's independence). `AddCharacterSpellAbilityRequest(string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos)` (reuses `SpellAbilityEffectRequest` from the Banco de Magias plan) — exactly one of `SourceBankEntryId` or the from-scratch fields must be provided. `CharacterSpellAbilityResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos)`.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/CharacterSheets/AddCharacterSpellAbilityRequest.cs`:

```csharp
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterSpellAbilityRequest(string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterSpellAbilityResponse.cs`:

```csharp
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterSpellAbilityResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterSpellAbilitiesControllerTests.cs` — reuse the fixture/helper pattern established across this plan and the Fundação plan (`AuthedRequest`, `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `SetUpSheetAsync`). Cases:

```csharp
[Fact]
public async Task AddFromScratch_creates_the_sheet_entry_and_an_independent_bank_copy()
// POST with Nome/Tipo/Grau/Descricao/Efeitos set, SourceBankEntryId null.
// Assert 201, GET .../spell-abilities contains it, AND a separate GET /api/spell-ability-bank
// (as the GM) contains a matching entry — proving R0001's auto-copy.

[Fact]
public async Task AddFromScratch_editing_the_sheet_copy_afterward_does_not_change_the_bank_copy()
// Create, then... (this plan doesn't build a PUT for spell abilities — Ficha de Personagem's
// 4.b describes add/remove, not edit-in-place; skip an update test, there's no endpoint to test)

[Fact]
public async Task AddFromBankEntry_copies_every_field_and_still_creates_a_second_independent_bank_copy()
// GM creates a bank entry directly via /api/spell-ability-bank, then the player POSTs to
// .../spell-abilities with SourceBankEntryId set to it. Assert the sheet copy matches the bank
// entry's fields, SourceBankEntryId round-trips for traceability, AND the bank now has 2 entries
// (the original + R0003's still-applies "every creation also lands a bank copy" per R0001).

[Fact]
public async Task Delete_removes_only_the_sheet_copy()
// create (from scratch), note the bank now has 1 entry, delete the sheet copy, assert the sheet
// list no longer has it but the bank still does.

[Fact]
public async Task Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400()

[Fact]
public async Task Add_by_an_unrelated_jogador_returns_403()
```

(Write all 6 in full, mirroring this plan's established test style — `SpellAbilityEffectRequest`/`SpellAbilityEntryResponse` come from `RuinaRPG.Contracts.SpellsAndAbilities`, the Banco de Magias plan's namespace.)

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSpellAbilitiesControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 4: Write `CharacterSpellAbilitiesController`**

`src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/spell-abilities")]
public class CharacterSpellAbilitiesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterSpellAbilityResponse>> Add(Guid sheetId, AddCharacterSpellAbilityRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var fromScratch = request.Nome is not null && request.Tipo is not null && request.Grau is not null && request.Descricao is not null && request.Efeitos is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither set
            return BadRequest("Informe exatamente um: os campos para montar do zero, ou SourceBankEntryId.");

        string nome; SpellAbilityTipo tipo; int grau; string descricao; List<SpellAbilityEffectRequest> efeitos;
        Guid? sourceBankEntryId = null;

        if (fromBank)
        {
            var bankEntryId = Guid.Parse(request.SourceBankEntryId!);
            var bankEntry = await db.SpellAbilityBankEntries.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == bankEntryId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            nome = bankEntry.Nome; tipo = bankEntry.Tipo; grau = bankEntry.Grau; descricao = bankEntry.Descricao;
            efeitos = bankEntry.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();
            sourceBankEntryId = bankEntryId;
        }
        else
        {
            if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out tipo))
                return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");
            nome = request.Nome!; grau = request.Grau!.Value; descricao = request.Descricao!; efeitos = request.Efeitos!;
        }

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(efeitos.Select(e => e.CustoPI));
        var custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        var sheetCopy = new CharacterSpellAbility
        {
            Id = Guid.NewGuid(), CharacterSheetId = sheetId, SourceBankEntryId = sourceBankEntryId,
            Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao
        };
        sheetCopy.Efeitos = efeitos.Select(e => new CharacterSpellAbilityEffect { Id = Guid.NewGuid(), CharacterSpellAbilityId = sheetCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
        db.CharacterSpellAbilities.Add(sheetCopy);

        // R0001: every creation — from scratch or from an existing bank entry — also lands an
        // independent copy in the GM's bank, whether the GM or the player created it.
        var bankCopy = new SpellAbilityBankEntry
        {
            Id = Guid.NewGuid(), GmId = campaignGmId, Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao
        };
        bankCopy.Efeitos = efeitos.Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = bankCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
        db.SpellAbilityBankEntries.Add(bankCopy);

        await db.SaveChangesAsync();
        return Created(string.Empty, ToResponse(sheetCopy));
    }

    [HttpGet]
    public async Task<ActionResult<List<CharacterSpellAbilityResponse>>> List(Guid sheetId)
    {
        var entries = await db.CharacterSpellAbilities.Include(e => e.Efeitos).Where(e => e.CharacterSheetId == sheetId).ToListAsync();
        return entries.Select(ToResponse).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var entry = await db.CharacterSpellAbilities.FirstOrDefaultAsync(e => e.Id == id && e.CharacterSheetId == sheetId);
        if (entry is null)
            return NotFound();

        db.CharacterSpellAbilities.Remove(entry); // the bank copy this creation also made is untouched — independent copies
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CharacterSpellAbilityResponse ToResponse(CharacterSpellAbility e) => new(
        e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao,
        e.Efeitos.Select(ef => new SpellAbilityEffectResponse(ef.EfeitoNome, ef.Quantidade, ef.CustoPI)).ToList());

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSpellAbilitiesControllerTests`
Expected: PASS (6/6).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/AddCharacterSpellAbilityRequest.cs src/RuinaRPG.Contracts/CharacterSheets/CharacterSpellAbilityResponse.cs src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSpellAbilitiesControllerTests.cs
git commit -m "feat: add character spell/ability CRUD, closing banco de magias R0001/R0003"
```

---

### Task 5: Infrastructure + Api — Runas and Maestrias (4.d, 4.e)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterRune.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterMastery.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/AddCharacterRuneRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterRuneResponse.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/AddCharacterMasteryRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterMasteryResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterRunesController.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CharacterRuneAndMasteryMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterRunesControllerTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterMasteriesControllerTests.cs`

**Interfaces:**
- Consumes: `SkillFormulas.Modificador` (Atributos/Combate plan, for Maestria's Total formula's "Bruto [Perícia]" term), `Pericia`, `Atributo`, `CharacterSheetAuthorization.CanEdit`.
- Produces: `CharacterRune` (`Guid Id`, `Guid CharacterSheetId`, `string Nome`, `string Descricao`, `int Grau`); `CharacterMastery` (`Guid Id`, `Guid CharacterSheetId`, `string Nome`, `Pericia Pericia`, `Atributo Atributo`, `int GastoMaestria`). `POST/GET/DELETE /api/character-sheets/{sheetId}/runes`; `POST/GET/DELETE /api/character-sheets/{sheetId}/masteries` — masteries' response includes a computed `Total = GastoMaestria + Bruto[Pericia] + AtributoTotal[Atributo]`, same "Bruto" caveat as the Atributos/Combate plan's sub-attributes task (uses `SkillFormulas.Modificador` on the chosen Pericia's `Gasto`, which IS available here since `Pericia` is fixed per mastery row, unlike the sub-attribute task's ambiguous "Bruto Prontidão").

- [ ] **Step 1: Write the entities**

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterRune.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterRune
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterMastery.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterMastery
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public required string Nome { get; set; }
    public Pericia Pericia { get; set; }
    public Atributo Atributo { get; set; }
    public int GastoMaestria { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CharacterRuneAndMasteryMigrationTests.cs` — same structure as this plan's Task 3 migration test: GM, player, campaign, sheet, one `CharacterRune` and one `CharacterMastery` row, assert migration name ends `AddCharacterRunesAndMasteries`.

- [ ] **Step 3: Run the test to verify it fails, then register the DbSets/relationships and create the migration**

Add to `RuinaRpgDbContext`:

```csharp
public DbSet<CharacterRune> CharacterRunes => Set<CharacterRune>();
public DbSet<CharacterMastery> CharacterMasteries => Set<CharacterMastery>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<CharacterRune>(entity => entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(r => r.CharacterSheetId).OnDelete(DeleteBehavior.Cascade));
builder.Entity<CharacterMastery>(entity => entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(m => m.CharacterSheetId).OnDelete(DeleteBehavior.Cascade));
```

```bash
dotnet ef migrations add AddCharacterRunesAndMasteries \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterRuneAndMasteryMigrationTests`
Expected: PASS.

- [ ] **Step 4: Write the contracts**

`src/RuinaRPG.Contracts/CharacterSheets/AddCharacterRuneRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterRuneRequest(string Nome, string Descricao, int Grau);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterRuneResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterRuneResponse(string Id, string Nome, string Descricao, int Grau);
```

`src/RuinaRPG.Contracts/CharacterSheets/AddCharacterMasteryRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterMasteryRequest(string Nome, string Pericia, string Atributo, int GastoMaestria);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterMasteryResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterMasteryResponse(string Id, string Nome, string Pericia, string Atributo, int GastoMaestria, int Total);
```

- [ ] **Step 5: Write the failing controller tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterRunesControllerTests.cs` and `CharacterMasteriesControllerTests.cs` — mirror `CharacterAffinitiesControllerTests` (Atributos/Combate plan, Task 5) exactly: add-returns-201, list, delete-returns-204-and-removes, add-by-unrelated-jogador-returns-403. For masteries, ALSO assert `Total` is computed correctly: given a `CharacterSkill` with `Gasto = 9` for the chosen Pericia (set via `PUT .../skills/{pericia}` from the Atributos/Combate plan, which this plan's tests can call directly) and an `Atributo` Total of, say, 4 (via `PUT .../attributes/{atributo}`), `Total = GastoMaestria + (9/3=3) + 4`.

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterRunesControllerTests|FullyQualifiedName~CharacterMasteriesControllerTests"`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 7: Write `CharacterRunesController` and `CharacterMasteriesController`**

`src/RuinaRPG.Api/Controllers/CharacterRunesController.cs` — same CRUD shape as `CharacterAffinitiesController` (Atributos/Combate plan, Task 5), swapping `CharacterAffinity`/`AddCharacterAffinityRequest`/`CharacterAffinityResponse` for `CharacterRune`/`AddCharacterRuneRequest`/`CharacterRuneResponse`, no Elemento/SubElemento validation step (Runas have no combination rule).

`src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs` — same shape, but `Add`/`List` compute `Total` per row by querying the matching `CharacterAttribute`/`CharacterSkill` rows for `Atributo`/`Pericia` (same pattern as `CharacterSkillsController.List`'s `atributoTotal` lookup, Task 4 of this plan's predecessor):

```csharp
private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, Atributo atributo, int gastoMaestria)
{
    var skill = await db.CharacterSkills.SingleAsync(s => s.CharacterSheetId == sheetId && s.Pericia == pericia);
    var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);
    var bruto = SkillFormulas.Modificador(skill.Gasto);
    var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
    return gastoMaestria + bruto + atributoTotal;
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterRunesControllerTests|FullyQualifiedName~CharacterMasteriesControllerTests"`
Expected: PASS (4/4 and 5/5).

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Infrastructure/CharacterSheets/CharacterRune.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterMastery.cs src/RuinaRPG.Contracts/CharacterSheets/AddCharacterRuneRequest.cs src/RuinaRPG.Contracts/CharacterSheets/CharacterRuneResponse.cs src/RuinaRPG.Contracts/CharacterSheets/AddCharacterMasteryRequest.cs src/RuinaRPG.Contracts/CharacterSheets/CharacterMasteryResponse.cs src/RuinaRPG.Api/Controllers/CharacterRunesController.cs src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration
git commit -m "feat: add runas and maestrias"
```

---

### Task 6: Infrastructure + Api — Inventário and Artefatos (5.a, 5.b), closing the deferred máximo computation

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterInventoryItem.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterArtifact.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/ResourceMaximumCalculator.cs`
- Create: contracts and controllers following this plan's established pattern (see Step 5)
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs` (wire máximo into `CharacterSheetResponse`)
- Test: multiple, see steps

**Interfaces:**
- Consumes: `ItemGeral`/`Artefato` (Catálogo plan), `AttributeTotalCalculator` (Atributos/Combate plan), `CharacterSheetAuthorization.CanEdit`.
- Produces: `CharacterInventoryItem` (`Guid Id`, `Guid CharacterSheetId`, `Guid ItemId`, `int Qtd`); `CharacterArtifact` (`Guid Id`, `Guid CharacterSheetId`, `Guid ArtifactItemId`) — max 3 per `TipoDeAlvo`, validated in the controller (Modelo de Dados: "limite de 3 por TipoDeAlvo validado na aplicação, não no schema"); `ResourceMaximumCalculator.Vitalidade(int vigor, int statusDeClasseVida) : int`, `.Foco(int astucia, int statusDeClasseFoco) : int`, `.Adrenalina(int artefatoBonus) : int` (Formulas.md: `10 + Artefato`), `.Estresse() : int` (flat 10). `POST/GET/DELETE` for both inventory and artifacts. `CharacterSheetResponse` gains 4 new `*Maximo` fields, computed live (never persisted, per the Fundação plan's Architecture note), closing that plan's deferral.

- [ ] **Step 1: Write the failing `ResourceMaximumCalculator` tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/ResourceMaximumCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class ResourceMaximumCalculatorTests
{
    [Fact]
    public void Vitalidade_is_vigor_times_2_plus_status_de_classe_vida()
    {
        // "Vitalidade = (Vigor × 2) + Status de classe Vida" — Formulas.md.
        ResourceMaximumCalculator.Vitalidade(vigor: 8, statusDeClasseVida: 12).Should().Be(28);
    }

    [Fact]
    public void Foco_is_astucia_times_2_plus_status_de_classe_foco()
    {
        // "Foco = Astúcia × 2 + Status de classe foco" — Formulas.md.
        ResourceMaximumCalculator.Foco(astucia: 6, statusDeClasseFoco: 8).Should().Be(20);
    }

    [Fact]
    public void Adrenalina_is_10_plus_artefato()
    {
        // "Adrenalina = 10 + Artefato" — Formulas.md.
        ResourceMaximumCalculator.Adrenalina(artefatoBonus: 3).Should().Be(13);
        ResourceMaximumCalculator.Adrenalina(artefatoBonus: 0).Should().Be(10);
    }

    [Fact]
    public void Estresse_is_a_flat_10()
    {
        ResourceMaximumCalculator.Estresse().Should().Be(10);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail, then write `ResourceMaximumCalculator`**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter ResourceMaximumCalculatorTests`
Expected: FAIL to compile.

`src/RuinaRPG.Domain/CharacterSheets/ResourceMaximumCalculator.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class ResourceMaximumCalculator
{
    public static int Vitalidade(int vigor, int statusDeClasseVida) => vigor * 2 + statusDeClasseVida;

    public static int Foco(int astucia, int statusDeClasseFoco) => astucia * 2 + statusDeClasseFoco;

    public static int Adrenalina(int artefatoBonus) => 10 + artefatoBonus;

    public static int Estresse() => 10;
}
```

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter ResourceMaximumCalculatorTests`
Expected: PASS (4/4). Commit: `git add src/RuinaRPG.Domain/CharacterSheets/ResourceMaximumCalculator.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/ResourceMaximumCalculatorTests.cs && git commit -m "feat: add resource maximum calculator"`.

**"Status de classe Vida/Foco" is not computed here** — it comes from `Tabela de Vocação`/`Tabela de Classes` (Vocação × Nível), which `IRulesDataProvider.Vocacoes` (Compêndio plan) already exposes, but that table's rows are keyed by the 5 base Vocação names, not by Sub-Vocação/Classe, and the sheet's `SubVocacao` is a free string (Fundação plan's known follow-up) rather than a validated key into `Tabela de Classes`. Task 7 wires `IRulesDataProvider.Vocacoes` in using the sheet's `Vocacao` (not `SubVocacao`) as the lookup key — an approximation the plan accepts explicitly (see Explicitly out of scope) rather than block the whole máximo feature on the Sub-Vocação validation follow-up.

- [ ] **Step 3: Write `CharacterInventoryItem` and `CharacterArtifact`, their migration test, and the migration**

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterInventoryItem.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterInventoryItem
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterArtifact.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterArtifact
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid ArtifactItemId { get; set; }
}
```

Write `tests/RuinaRPG.Tests.Integration/Persistence/CharacterInventoryAndArtifactMigrationTests.cs` following this plan's established migration-test pattern (Task 3), asserting a migration named `AddCharacterInventoryAndArtifacts`. Register both DbSets and `HasOne<CharacterSheet>()...Cascade` / `HasOne<Item>()...Restrict` relationships in `OnModelCreating`, same as the Atributos/Combate plan's arsenal tables. Run the migration command, then the test, to green.

- [ ] **Step 4: Run the migration test to verify it passes, then commit**

```bash
git add src/RuinaRPG.Infrastructure/CharacterSheets/CharacterInventoryItem.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterArtifact.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/CharacterInventoryAndArtifactMigrationTests.cs
git commit -m "feat: add character inventory and artifact entities and migration"
```

- [ ] **Step 5: Write contracts, controller, and tests for inventory and artifacts**

Contracts (all in `src/RuinaRPG.Contracts/CharacterSheets/`): `AddCharacterInventoryItemRequest(string ItemId, int Qtd)`, `CharacterInventoryItemResponse(string Id, string ItemId, string Nome, decimal Peso, int Qtd, decimal Total)`, `AddCharacterArtifactRequest(string ArtifactItemId)`, `CharacterArtifactResponse(string Id, string ArtifactItemId, string Nome, string TipoDeAlvo, string Alvo, int Valor)`.

`src/RuinaRPG.Api/Controllers/CharacterPossessionsController.cs` at route `api/character-sheets/{sheetId}` with `HttpPost("inventory")`/`HttpGet("inventory")`/`HttpDelete("inventory/{id}")` and `HttpPost("artifacts")`/`HttpGet("artifacts")`/`HttpDelete("artifacts/{id}")`, following `CharacterArsenalController`'s exact structure (Atributos/Combate plan, Task 6) for the auth-check/live-join pattern. The artifact `Add` action enforces the 3-per-`TipoDeAlvo` cap:

```csharp
var newItem = await db.Set<Artefato>().FirstOrDefaultAsync(a => a.Id == artifactItemId);
if (newItem is null)
    return BadRequest("Item de artefato não encontrado.");

var existingOfSameType = await db.CharacterArtifacts
    .Where(a => a.CharacterSheetId == sheetId)
    .Join(db.Set<Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i.TipoDeAlvo)
    .CountAsync(t => t == newItem.TipoDeAlvo);
if (existingOfSameType >= 3)
    return BadRequest($"Limite de 3 Artefatos do tipo {newItem.TipoDeAlvo} já atingido.");
```

Write `tests/RuinaRPG.Tests.Integration/Controllers/CharacterPossessionsControllerTests.cs` covering: add/list/delete inventory item with `Total = Peso × Qtd`; add/list/delete an artifact; a 4th artifact of the same `TipoDeAlvo` returns 400; unrelated jogador returns 403 — following this plan's established test style.

- [ ] **Step 6: Run the tests to verify they fail, implement, run again to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterPossessionsControllerTests` (expect FAIL, then implement per Step 5, then expect PASS — 6+ tests).

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets src/RuinaRPG.Api/Controllers/CharacterPossessionsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterPossessionsControllerTests.cs
git commit -m "feat: add inventory and artifacts"
```

- [ ] **Step 8: Wire máximo into `CharacterSheetResponse`**

Add 4 fields to `CharacterSheetResponse` (`int VitalidadeMaximo, int FocoMaximo, int AdrenalinaMaximo, int EstresseMaximo`) and compute them in `CharacterSheetsController.ToResponseAsync`:

```csharp
var vigorTotal = await GetAttributeTotalAsync(s.Id, RuinaRPG.Domain.CharacterSheets.Atributo.Vigor);
var astuciaTotal = await GetAttributeTotalAsync(s.Id, RuinaRPG.Domain.CharacterSheets.Atributo.Astucia);
var statusVida = s.Vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == s.Vocacao.Value.ToString() && v.Nivel == s.Nivel).Select(v => v.Vida).FirstOrDefault() : 0;
var statusFoco = s.Vocacao is not null ? rules.Vocacoes.Where(v => v.Vocacao == s.Vocacao.Value.ToString() && v.Nivel == s.Nivel).Select(v => v.Arcana).FirstOrDefault() : 0;
var artefatoBonusParaAdrenalina = 0; // Artefatos com TipoDeAlvo=SubAtributo/Alvo="Adrenalina" — não modelado ainda, ver Explicitly out of scope

var vitalidadeMaximo = ResourceMaximumCalculator.Vitalidade(vigorTotal, statusVida);
var focoMaximo = ResourceMaximumCalculator.Foco(astuciaTotal, statusFoco);
var adrenalinaMaximo = ResourceMaximumCalculator.Adrenalina(artefatoBonusParaAdrenalina);
var estresseMaximo = ResourceMaximumCalculator.Estresse();
```

with a small private `GetAttributeTotalAsync` helper mirroring the one already inlined in `SubAttributes` (Atributos/Combate plan, Task 7) — extract that inline logic into a shared private method on `CharacterSheetsController` reused by both actions, rather than duplicating it a third time.

Append the 4 new args to the `CharacterSheetResponse` constructor call and add a covering integration test (`Get_computes_vitalidade_and_foco_maximo_from_vigor_astucia_and_vocacao`) asserting a concrete value using a real `Tabela de Vocação` row (e.g. Campeão, Nível 1 → Vida 8, Arcana 4 — the same real excerpt used in the Compêndio plan's `VocacaoProgressaoParserTests`).

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: PASS (all, including the new máximo test).

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs
git commit -m "feat: compute vitalidade, foco, adrenalina and estresse maximo"
```

---

### Task 7: Infrastructure + Api — Afeições and Características (5.c, 5.d)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAffection.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterTrait.cs`
- Create: contracts and controllers following this plan's pattern
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: multiple, see steps

**Interfaces:**
- Consumes: `Trait`/`Polaridade` (Compêndio de Regras plan), `CharacterSheetAuthorization.CanEdit`.
- Produces: `CharacterAffection` (`Guid Id`, `Guid CharacterSheetId`, `string Nome`, `int Favorabilidade`); `CharacterTrait` (`Guid Id`, `Guid CharacterSheetId`, `Guid TraitId`, `Polaridade Polaridade`). `POST/GET/DELETE` for both. Traits endpoint list splits into Positivas/Negativas per R0001 5.d ("Duas listas incrementais") and each includes a computed list-level `Total` (sum of `Custo`).

- [ ] **Step 1: Write `CharacterAffection` and `CharacterTrait`, their migration test, and the migration**

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAffection.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterAffection
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public required string Nome { get; set; }
    public int Favorabilidade { get; set; }
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterTrait.cs`:

```csharp
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterTrait
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid TraitId { get; set; }
    public Polaridade Polaridade { get; set; }
}
```

Write `tests/RuinaRPG.Tests.Integration/Persistence/CharacterAffectionAndTraitMigrationTests.cs`, asserting a migration named `AddCharacterAffectionsAndTraits`, following this plan's established pattern — the `CharacterTrait` row in the test must reference a real, seeded `Trait` (query `db.Traits.First()` after confirming the Compêndio plan's startup seeder ran, rather than inserting a synthetic `Trait` row, to prove the FK genuinely targets that table). Register both DbSets, `HasOne<CharacterSheet>()...Cascade`, and `HasOne<Trait>()...Restrict` (never cascade-delete a Trait out from under a character who has it — Compêndio plan's Global Constraints already forbid deleting Traits at all, but the FK behavior should agree). Create the migration, run the test to green.

- [ ] **Step 2: Commit**

```bash
git add src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAffection.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterTrait.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/CharacterAffectionAndTraitMigrationTests.cs
git commit -m "feat: add character affection and trait entities and migration"
```

- [ ] **Step 3: Write contracts, controller, and tests**

Contracts: `AddCharacterAffectionRequest(string Nome, int Favorabilidade)`, `CharacterAffectionResponse(string Id, string Nome, int Favorabilidade)`, `AddCharacterTraitRequest(string TraitId)`, `CharacterTraitResponse(string Id, string TraitId, string Nome, string Descricao, int Custo, string Polaridade)`, `CharacterTraitsListResponse(List<CharacterTraitResponse> Positivas, int TotalPositivas, List<CharacterTraitResponse> Negativas, int TotalNegativas)`.

`src/RuinaRPG.Api/Controllers/CharacterPossessionsController.cs` (same controller as Task 6, extended) gains `HttpPost("affections")`/`HttpGet("affections")`/`HttpDelete("affections/{id}")` and `HttpPost("traits")`/`HttpGet("traits")`/`HttpDelete("traits/{id}")`. `List` for traits joins `CharacterTrait` → `Trait`, groups by `Polaridade`, sums `Custo` per group:

```csharp
[HttpGet("traits")]
public async Task<ActionResult<CharacterTraitsListResponse>> ListTraits(Guid sheetId)
{
    var rows = await db.CharacterTraits
        .Where(t => t.CharacterSheetId == sheetId)
        .Join(db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => new CharacterTraitResponse(ct.Id.ToString(), t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString()))
        .ToListAsync();

    var positivas = rows.Where(r => r.Polaridade == "Positiva").ToList();
    var negativas = rows.Where(r => r.Polaridade == "Negativa").ToList();
    return new CharacterTraitsListResponse(positivas, positivas.Sum(r => r.Custo), negativas, negativas.Sum(r => r.Custo));
}
```

`Add` for traits looks up the chosen `Trait` by Id to snapshot its `Polaridade` onto the new `CharacterTrait` row, returns `404`/`BadRequest` if the Trait doesn't exist.

Write `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffectionsAndTraitsControllerTests.cs` (or extend `CharacterPossessionsControllerTests` from Task 6 — either is fine, pick whichever keeps the file under a few hundred lines) covering: add/list/delete an affection; add a trait referencing a real seeded `Trait` and confirm it lands in the correct Positivas/Negativas bucket with the right `TotalPositivas`/`TotalNegativas`; add a nonexistent `TraitId` returns 400; unrelated jogador returns 403.

- [ ] **Step 4: Run the tests to verify they fail, implement, run again to verify they pass**

Run, implement per above, re-run to green (expect 6+ tests passing).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets src/RuinaRPG.Api/Controllers/CharacterPossessionsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffectionsAndTraitsControllerTests.cs
git commit -m "feat: add affections and traits"
```

---

### Task 8: Api — Diário (tab 6), reusing the Campanha diary tables

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/CharacterDiaryController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterDiaryControllerTests.cs`

**Interfaces:**
- Consumes: `DiaryEntry`/`DiaryEntryImage` (Campanha — Fundação plan), `CharacterSheetAuthorization.CanEdit`.
- Produces: `POST/GET/PUT/DELETE /api/character-sheets/{sheetId}/diary`, same contract shapes as `CampaignsController`'s diary actions (`CreateDiaryEntryRequest`/`UpdateDiaryEntryRequest`/`DiaryEntryResponse`, all from `RuinaRPG.Contracts.Diary`). Visibility differs from the campaign diary: **both the owning player and the campaign's GM can read** (not GM-only) — write access still follows `CharacterSheetAuthorization.CanEdit` (owner or GM).

- [ ] **Step 1: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterDiaryControllerTests.cs` — reuse this plan's fixture pattern. Cases:

```csharp
[Fact]
public async Task Create_and_list_round_trips_an_entry_as_the_owner()

[Fact]
public async Task Get_by_the_campaigns_gm_also_succeeds_readonly_visibility_shared_with_owner()

[Fact]
public async Task Update_by_an_unrelated_jogador_returns_403()

[Fact]
public async Task Delete_removes_the_entry()
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterDiaryControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 3: Write `CharacterDiaryController`**

`src/RuinaRPG.Api/Controllers/CharacterDiaryController.cs` — same shape as `CampaignsController`'s diary actions (Campanha — Fundação plan, Task 5), but:
- Route prefix `api/character-sheets/{sheetId}/diary`.
- `CreateDiaryEntry`/`UpdateDiaryEntry`/`DeleteDiaryEntry` gate on `CharacterSheetAuthorization.CanEdit` (owner or campaign GM) instead of "caller is the campaign's GM".
- `ListDiaryEntries`/a new `Get` action have **no** authorization gate beyond `[Authorize]` — any authenticated request for a real `sheetId` returns the entries; the Client is trusted to only link here from a sheet the caller can already see (same trust boundary the sheet's own `Get` action uses, Fundação plan Task 6).
- Entries filter on `CharacterSheetId == sheetId` instead of `CampaignId == campaignId`, and `IsSecretNote` is irrelevant here (always `false` for this table's sheet-diary rows — secret notes only ever have `CampaignId` set, never `CharacterSheetId`, per Modelo de Dados §7).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterDiaryControllerTests`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterDiaryController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterDiaryControllerTests.cs
git commit -m "feat: add character sheet diary"
```

---

### Task 9: Client — Magias & Habilidades, Posses, and Diário tabs

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`

**Interfaces:**
- Consumes: every Contracts type from Tasks 2, 4-8, the authenticated `HttpClient`.
- Produces: the final 3 sections of the `/fichas/{id}` page. No later task depends on this file.

- [ ] **Step 1: Add the three remaining sections**

Following the exact pattern established in the Atributos/Combate plan's Task 8 (list + inline add/remove controls per sub-section, one `Load*Async` per data source, all invoked from `OnInitializedAsync`'s `Task.WhenAll`), add to `FichaDePersonagem.razor`:

- **Magias & Habilidades**: Habilidade Racial (read-only, from `GET .../racial-ability`), a list of `CharacterSpellAbilityResponse` with an add form (from-scratch fields OR a bank-entry picker sourced from `GET /api/spell-ability-bank` scoped to... note: the Client doesn't have the GM's bank listing available to a Jogador caller, since `SpellAbilityBankController` is GM-only — for a Jogador, only the from-scratch path is offered in this form; the from-bank-entry path is GM-only in practice even though the Api doesn't hard-block it, and that's fine, not a gap to fix here), Runas list with add/remove, Maestrias list with add/remove.
- **Posses**: Ciclos (already part of Informações Básicas' form from the Fundação plan — no new field here), Inventário list with add/remove, Artefatos list with add/remove (grouped by `TipoDeAlvo` for the 3-per-type display, matching R0001 5.b's "separada em 4 grupos"), Afeições list with add/remove, Características as two lists (Positivas/Negativas) each with a Total, add/remove.
- **Diário**: entry list (newest first) with an add form, matching the pattern already built in `CampanhaDetalhe.razor`'s diary section (Campanha — Fundação plan, Task 6).

Add corresponding fields and `Load*Async`/`Add*Async`/`Delete*Async` methods to the `@code` block, following the exact conventions established by every earlier Client task in this plan and its predecessor — no new pattern introduced here.

- [ ] **Step 2: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: add magias, posses, and diario tabs to the character sheet page"
```

---

### Task 10: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files.

**Interfaces:**
- Consumes: the full stack. Produces: nothing new.

- [ ] **Step 1: Boot the stack, set up a GM/player/campaign/sheet (same sequence as prior Personagem plans' smoke tests)**

- [ ] **Step 2: Create a spell/ability from scratch and confirm it also landed in the GM's bank, through nginx**

```bash
curl -sf -X POST "http://localhost/api/character-sheets/$SHEET_ID/spell-abilities" \
  -H "Authorization: Bearer $PLAYER_TOKEN" -H "Content-Type: application/json" \
  -d '{"sourceBankEntryId":null,"nome":"Bola de Fogo","tipo":"Magia","grau":1,"descricao":"Fogo.","efeitos":[{"efeitoNome":"Dano","quantidade":2,"custoPI":4}]}'

curl -sf "http://localhost/api/spell-ability-bank" -H "Authorization: Bearer $GM_TOKEN"
```

Expected: sheet create returns 201; the bank list contains a matching "Bola de Fogo" entry (R0001 confirmed end-to-end).

- [ ] **Step 3: Confirm Vitalidade/Foco máximo compute on the sheet, through nginx**

```bash
curl -sf "http://localhost/api/character-sheets/$SHEET_ID" -H "Authorization: Bearer $PLAYER_TOKEN"
```

Expected: response includes `vitalidadeMaximo`/`focoMaximo` as real numbers (not both 0, once `Vocacao` and Vigor/Astúcia are set — set them first via the same PUT calls the Atributos/Combate plan's smoke test uses).

- [ ] **Step 4: Tear down**

```bash
make down
```

- [ ] **Step 5: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify magias, posses, and diario flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Adrenalina máximo's Artefato term — `CharacterArtifact`/`TipoDeAlvo` now exist (this plan), but summing "Valores de Artefatos equipados de um dado Alvo" (2.a/2.b's own formula language) into the Adrenalina-specific `Artefato` term isn't wired in Task 6 (hardcoded to 0) — a real, trackable follow-up now that both halves exist, not a structural blocker.
- Status de classe Vida/Foco keyed by Sub-Vocação/Classe rather than the coarser Vocação — flagged explicitly in Task 6; `Tabela de Classes` data isn't consumed anywhere in this plan.
- Contratos (4.c) — explicitly "pendente" in the spec (depends on Ficha de Criaturas, not yet built).
- Ambidestria's 2-artifact-of-Attribute-type exception or any other Característica-driven rule change to earlier tabs (e.g. arsenal's single-equipped-weapon default from the Atributos/Combate plan) — Características exist as data now (this plan), but no endpoint in either Personagem plan reads a character's owned Traits to change validation behavior elsewhere. Tracked as a real, if minor, follow-up.
- Diary image upload UI on the Client (Task 9) — the Api's `CreateDiaryEntryRequest.ImageIds` already works (Campanha — Fundação plan), but this plan's Client diary form, like `CampanhaDetalhe.razor`'s, doesn't yet offer an upload control — same gap, same follow-up as noted in the Catálogo plan.
