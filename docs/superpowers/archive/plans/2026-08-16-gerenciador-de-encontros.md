# Gerenciador de Encontros Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A GM-only combat-state tracker scoped to a campaign: add participants one at a time (from a member's own sheet — including a granted pet/invocation — or from the GM's own bestiary, each addition becoming an independent instance), order them by Iniciativa, track PV/PF/PA and remaining Actions per participant, and advance turns/rounds manually. No dice are rolled here — this manages state only (the doc's own preamble).

**Architecture:** `Encounter`/`EncounterParticipant`/`EncounterParticipantCondition` per Modelo de Dados §8, with one deliberate, explicit deviation: the schema names a single `SourceCharacterSheetId` FK, but R0002/R0003 clearly allow a participant to come from a `CharacterSheet` **or** a granted `NpcSheet`/`CreatureSheet` ("Ficha de Personagem, incluindo Fichas de NPC/Criatura concedida a ele") — a single FK to one table can't express that. This plan uses three nullable source FKs (`SourceCharacterSheetId`/`SourceNpcSheetId`/`SourceCreatureSheetId`, at most one set) instead, ruled necessary to match the actual requirement rather than the schema doc's simplified column list. Live PV/PF/PA reflection (R0003) is read-time only: the `List` action resolves a live-sourced participant's current Vitalidade/Foco/Adrenalina by querying its source sheet fresh on every request — no caching, no push updates from the sheet side. The SignalR hub Técnico R0001 names for this feature broadcasts a lightweight "something changed, refetch" ping to viewers of an open Encounter whenever a live-sourced sheet's resource fields change, rather than pushing the resolved values themselves — simpler, and the `List` endpoint is already the single source of truth for the resolved numbers.

**Tech Stack:** Same as established, plus `Microsoft.AspNetCore.SignalR` server-side (already implied by the solution's ASP.NET Core 8 base, no new package) and `Microsoft.AspNetCore.SignalR.Client` on the Blazor WebAssembly Client (new).

**Spec:** `Docs/Requisitos/Requisitos - Gerenciador de Encontros.md` (all 6 requirements), `Docs/Requisitos/Requisitos - Técnico.md` R0001 (SignalR, JWT-authenticated Hub), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §8.

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Every endpoint is `[Authorize(Roles = "GM")]`, scoped to campaigns the caller owns — "acesso 100% do GM... Não há visualização de jogador para o Encontro" (doc preamble).
- At most one of `SourceCharacterSheetId`/`SourceNpcSheetId`/`SourceCreatureSheetId` is set on a participant; when one is set, `PVAtual`/`PFAtual`/`PAAtual` stay `null` forever and are resolved live on read; when none is set, those three columns are the participant's own independently-editable state, initialized once at add-time from the GM's bestiary sheet and never touching it again.
- No secret ever hardcoded — unaffected by this plan (the SignalR Hub reuses the existing JWT bearer scheme, no new secret).

---

### Task 1: Domain — turn-advance calculator

**Files:**
- Create: `src/RuinaRPG.Domain/Encounters/TurnAdvanceCalculator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Encounters/TurnAdvanceCalculatorTests.cs`

**Interfaces:**
- Produces: `TurnAdvanceCalculator.Advance(int currentRound, int currentParticipantIndex, int participantCount) : (int NextRound, int NextParticipantIndex)`. Task 5 (advance-turn endpoint) calls this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Encounters/TurnAdvanceCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Encounters;

namespace RuinaRPG.Tests.Unit.Encounters;

public class TurnAdvanceCalculatorTests
{
    [Fact]
    public void Advance_moves_to_the_next_participant_within_the_same_round()
    {
        var (round, index) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantIndex: 0, participantCount: 3);

        round.Should().Be(1);
        index.Should().Be(1);
    }

    [Fact]
    public void Advance_past_the_last_participant_starts_a_new_round_at_the_first_participant()
    {
        // "Ao passar do último participante da lista, o Encontro inicia uma nova Rodada... e volta ao
        // primeiro participante da ordem." — R0006.
        var (round, index) = TurnAdvanceCalculator.Advance(currentRound: 1, currentParticipantIndex: 2, participantCount: 3);

        round.Should().Be(2);
        index.Should().Be(0);
    }

    [Fact]
    public void Advance_with_a_single_participant_always_starts_a_new_round()
    {
        var (round, index) = TurnAdvanceCalculator.Advance(currentRound: 5, currentParticipantIndex: 0, participantCount: 1);

        round.Should().Be(6);
        index.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter TurnAdvanceCalculatorTests`
Expected: FAIL to compile.

- [ ] **Step 3: Write `TurnAdvanceCalculator`**

`src/RuinaRPG.Domain/Encounters/TurnAdvanceCalculator.cs`:

```csharp
namespace RuinaRPG.Domain.Encounters;

public static class TurnAdvanceCalculator
{
    public static (int NextRound, int NextParticipantIndex) Advance(int currentRound, int currentParticipantIndex, int participantCount)
    {
        var nextIndex = currentParticipantIndex + 1;
        return nextIndex < participantCount
            ? (currentRound, nextIndex)
            : (currentRound + 1, 0);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter TurnAdvanceCalculatorTests`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/Encounters tests/RuinaRPG.Tests.Unit/Encounters
git commit -m "feat: add turn-advance calculator"
```

---

### Task 2: Infrastructure — `Encounter`/`EncounterParticipant`/`EncounterParticipantCondition` entities and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Encounters/Encounter.cs`
- Create: `src/RuinaRPG.Infrastructure/Encounters/EncounterParticipant.cs`
- Create: `src/RuinaRPG.Infrastructure/Encounters/EncounterParticipantCondition.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/EncounterMigrationTests.cs`

**Interfaces:**
- Consumes: `Campaign` (Campanha — Fundação plan), `CharacterSheet`/`NpcSheet`/`CreatureSheet` (their respective plans).
- Produces: `Encounter` (`Guid Id`, `Guid CampaignId`, `string? Nome`, `int CurrentRound = 1`, `int CurrentParticipantIndex`); `EncounterParticipant` (`Guid Id`, `Guid EncounterId`, `Guid? SourceCharacterSheetId`, `Guid? SourceNpcSheetId`, `Guid? SourceCreatureSheetId`, `string Nome`, `int Iniciativa`, `int? PVAtual`, `int? PFAtual`, `int? PAAtual`, `int AcoesRestantes`); `EncounterParticipantCondition` (`Guid Id`, `Guid EncounterParticipantId`, `string Texto`). `RuinaRpgDbContext.Encounters`/`EncounterParticipants`/`EncounterParticipantConditions`. Tasks 3-6 depend on this shape.

- [ ] **Step 1: Write the entities**

`src/RuinaRPG.Infrastructure/Encounters/Encounter.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Encounters;

public class Encounter
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string? Nome { get; set; }
    public int CurrentRound { get; set; } = 1;
    public int CurrentParticipantIndex { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Encounters/EncounterParticipant.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Encounters;

public class EncounterParticipant
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Guid? SourceCharacterSheetId { get; set; }
    public Guid? SourceNpcSheetId { get; set; }
    public Guid? SourceCreatureSheetId { get; set; }
    public required string Nome { get; set; }
    public int Iniciativa { get; set; }
    public int? PVAtual { get; set; }
    public int? PFAtual { get; set; }
    public int? PAAtual { get; set; }
    public int AcoesRestantes { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Encounters/EncounterParticipantCondition.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Encounters;

public class EncounterParticipantCondition
{
    public Guid Id { get; set; }
    public Guid EncounterParticipantId { get; set; }
    public required string Texto { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/EncounterMigrationTests.cs` — set up a GM, campaign, insert an `Encounter`, an `EncounterParticipant` with all 3 source FKs null and `PVAtual`/`PFAtual`/`PAAtual` set (the independent-instance case), and an `EncounterParticipantCondition` on it. Assert the migration is named `AddEncounters` and every row round-trips.

- [ ] **Step 3: Register the DbSets and relationships, create the migration, verify the test passes**

Add to `RuinaRpgDbContext`:

```csharp
public DbSet<Encounter> Encounters => Set<Encounter>();
public DbSet<EncounterParticipant> EncounterParticipants => Set<EncounterParticipant>();
public DbSet<EncounterParticipantCondition> EncounterParticipantConditions => Set<EncounterParticipantCondition>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<Encounter>(entity => entity.HasOne<Campaign>().WithMany().HasForeignKey(e => e.CampaignId).OnDelete(DeleteBehavior.Cascade));

builder.Entity<EncounterParticipant>(entity =>
{
    entity.HasOne<Encounter>().WithMany().HasForeignKey(p => p.EncounterId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(p => p.SourceCharacterSheetId).OnDelete(DeleteBehavior.SetNull);
    entity.HasOne<NpcSheet>().WithMany().HasForeignKey(p => p.SourceNpcSheetId).OnDelete(DeleteBehavior.SetNull);
    entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(p => p.SourceCreatureSheetId).OnDelete(DeleteBehavior.SetNull);
});

builder.Entity<EncounterParticipantCondition>(entity =>
    entity.HasOne<EncounterParticipant>().WithMany().HasForeignKey(c => c.EncounterParticipantId).OnDelete(DeleteBehavior.Cascade));
```

`SetNull` (not `Cascade`) on the three source FKs — deleting a character/NPC/creature sheet mid-encounter shouldn't silently delete the participant tracking it; it becomes a source-less "orphaned" participant instead, which `Nome` (a snapshot, per the doc: "pra exibição mesmo se a origem for excluída depois") still displays correctly.

```bash
dotnet ef migrations add AddEncounters --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter EncounterMigrationTests` — expect PASS.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Encounters src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/EncounterMigrationTests.cs
git commit -m "feat: add encounter entities and migration"
```

---

### Task 3: Api — create and list encounters (R0001)

**Files:**
- Create: `src/RuinaRPG.Contracts/Encounters/CreateEncounterRequest.cs`
- Create: `src/RuinaRPG.Contracts/Encounters/EncounterResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/EncountersController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs`

**Interfaces:**
- Consumes: `Encounter` (Task 2).
- Produces: `POST /api/campaigns/{campaignId}/encounters` → `201` + `EncounterResponse`, `[Authorize(Roles = "GM")]`. `GET /api/campaigns/{campaignId}/encounters` → `200` + list. `CreateEncounterRequest(string? Nome)`; `EncounterResponse(string Id, string? Nome, int CurrentRound, int CurrentParticipantIndex)`.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/Encounters/CreateEncounterRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Encounters;

public record CreateEncounterRequest(string? Nome);
```

`src/RuinaRPG.Contracts/Encounters/EncounterResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Encounters;

public record EncounterResponse(string Id, string? Nome, int CurrentRound, int CurrentParticipantIndex);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs` — set up a GM and a campaign (via the Campanha — Fundação plan's endpoints). Cases:

```csharp
[Fact]
public async Task Create_returns_201_starting_at_Round_1()

[Fact]
public async Task List_returns_only_encounters_for_campaigns_the_caller_owns()

[Fact]
public async Task Create_by_a_jogador_returns_403()

[Fact]
public async Task Create_in_a_campaign_owned_by_another_gm_returns_404()
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter EncountersControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 4: Write `EncountersController`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/encounters")]
public class EncountersController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EncounterResponse>> Create(Guid campaignId, CreateEncounterRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var encounter = new Encounter { Id = Guid.NewGuid(), CampaignId = campaignId, Nome = request.Nome };
        db.Encounters.Add(encounter);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(encounter));
    }

    [HttpGet]
    public async Task<ActionResult<List<EncounterResponse>>> List(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        return await db.Encounters.Where(e => e.CampaignId == campaignId).Select(e => ToResponse(e)).ToListAsync();
    }

    private static EncounterResponse ToResponse(Encounter e) => new(e.Id.ToString(), e.Nome, e.CurrentRound, e.CurrentParticipantIndex);

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter EncountersControllerTests`
Expected: PASS (4/4).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Encounters src/RuinaRPG.Api/Controllers/EncountersController.cs tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs
git commit -m "feat: add encounter creation and listing"
```

---

### Task 4: Api — add and list participants, ordered by Iniciativa, with live PV/PF/PA (R0002, R0003, R0004)

**Files:**
- Create: `src/RuinaRPG.Contracts/Encounters/AddParticipantRequest.cs`
- Create: `src/RuinaRPG.Contracts/Encounters/EncounterParticipantResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/EncounterParticipantsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EncounterParticipantsControllerTests.cs`

**Interfaces:**
- Consumes: `EncounterParticipant` (Task 2), `CharacterSheet`/`NpcSheet`/`CreatureSheet` (their plans).
- Produces: `POST /api/encounters/{encounterId}/participants` → `201`, `[Authorize(Roles = "GM")]`. `GET .../participants` → `200` + list **ordered by `Iniciativa` descending** (R0004). `AddParticipantRequest(string? SourceCharacterSheetId, string? SourceNpcSheetId, string? SourceCreatureSheetId, int Iniciativa)` — exactly one source id, same "exactly one" validation shape used throughout this codebase (Banco de Magias-sourcing in the Personagem plan, attachment targets in the Campanha plan); when the source is an `NpcSheet`/`CreatureSheet` NOT owned by any player (the GM's own bestiary, R0002's second case), PV/PF/PA are copied once at add-time and become independently editable; when it's a `CharacterSheet` or a **granted** `NpcSheet`/`CreatureSheet` (`OwnerId` set — R0002's first case, "incluindo Fichas de NPC/Criatura concedida a ele"), they're read live on every `List` call instead, and this endpoint leaves the copied columns null. `EncounterParticipantResponse(string Id, string Nome, int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, bool IsLiveSourced)`.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/Encounters/AddParticipantRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Encounters;

public record AddParticipantRequest(string? SourceCharacterSheetId, string? SourceNpcSheetId, string? SourceCreatureSheetId, int Iniciativa);
```

`src/RuinaRPG.Contracts/Encounters/EncounterParticipantResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Encounters;

public record EncounterParticipantResponse(string Id, string Nome, int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, bool IsLiveSourced);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/EncounterParticipantsControllerTests.cs` — set up a GM, campaign, encounter, and (via the earlier plans' endpoints) one GM-owned `NpcSheet` with `VitalidadeAtual` set, and one player-owned `CharacterSheet` in the same campaign with its own `VitalidadeAtual`. Cases:

```csharp
[Fact]
public async Task AddParticipant_from_a_gm_owned_npc_copies_PV_once_and_it_becomes_independently_editable()
// add from the NpcSheet, assert PV matches its VitalidadeAtual; then change the NPC's
// VitalidadeAtual via PUT /api/npc-sheets/{id} and confirm the participant's PV in
// GET .../participants is UNCHANGED (proves it's a copy, not live).

[Fact]
public async Task AddParticipant_from_a_character_sheet_reflects_PV_live()
// add from the CharacterSheet, assert PV matches VitalidadeAtual; then change the sheet's
// VitalidadeAtual via PUT /api/character-sheets/{id} and confirm the participant's PV in
// GET .../participants DOES change (proves it's live, IsLiveSourced == true).

[Fact]
public async Task AddParticipant_with_two_sources_set_returns_400()

[Fact]
public async Task List_orders_participants_by_Iniciativa_descending()

[Fact]
public async Task Add_by_a_jogador_returns_403()
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter EncounterParticipantsControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 4: Write `EncounterParticipantsController`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/encounters/{encounterId}/participants")]
public class EncounterParticipantsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EncounterParticipantResponse>> Add(Guid encounterId, AddParticipantRequest request)
    {
        var authError = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;

        var sourceCount = new[] { request.SourceCharacterSheetId, request.SourceNpcSheetId, request.SourceCreatureSheetId }.Count(id => id is not null);
        if (sourceCount != 1)
            return BadRequest("Informe exatamente uma origem: Ficha de Personagem, de NPC ou de Criatura.");

        var participant = new EncounterParticipant { Id = Guid.NewGuid(), EncounterId = encounterId, Iniciativa = request.Iniciativa, AcoesRestantes = 3, Nome = "" };

        if (request.SourceCharacterSheetId is not null)
        {
            var sheet = await db.CharacterSheets.FindAsync(Guid.Parse(request.SourceCharacterSheetId));
            if (sheet is null) return BadRequest("Ficha de Personagem não encontrada.");
            participant.SourceCharacterSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            // PV/PF/PA stay null — always live-sourced for a CharacterSheet (owner's own, or a granted pet).
        }
        else if (request.SourceNpcSheetId is not null)
        {
            var sheet = await db.NpcSheets.FindAsync(Guid.Parse(request.SourceNpcSheetId));
            if (sheet is null) return BadRequest("Ficha de NPC não encontrada.");
            participant.SourceNpcSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            if (sheet.OwnerId is null) // GM's own bestiary entry (R0002's 2nd case) — copy once, then independent.
            {
                participant.PVAtual = sheet.VitalidadeAtual;
                participant.PFAtual = sheet.FocoAtual;
                participant.PAAtual = sheet.AdrenalinaAtual;
            }
            // else: granted to a player — stays live-sourced, same as a CharacterSheet.
        }
        else
        {
            var sheet = await db.CreatureSheets.FindAsync(Guid.Parse(request.SourceCreatureSheetId!));
            if (sheet is null) return BadRequest("Ficha de Criatura não encontrada.");
            participant.SourceCreatureSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            if (sheet.OwnerId is null)
            {
                participant.PVAtual = sheet.VitalidadeAtual;
                participant.PFAtual = sheet.FocoAtual;
                participant.PAAtual = sheet.AdrenalinaAtual;
            }
        }

        db.EncounterParticipants.Add(participant);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(participant));
    }

    [HttpGet]
    public async Task<ActionResult<List<EncounterParticipantResponse>>> List(Guid encounterId)
    {
        var authError = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;

        var participants = await db.EncounterParticipants
            .Where(p => p.EncounterId == encounterId)
            .OrderByDescending(p => p.Iniciativa)
            .ToListAsync();

        var responses = new List<EncounterParticipantResponse>();
        foreach (var participant in participants)
            responses.Add(await ToResponseAsync(participant));
        return responses;
    }

    private async Task<EncounterParticipantResponse> ToResponseAsync(EncounterParticipant p)
    {
        if (p.SourceCharacterSheetId is not null)
        {
            var sheet = await db.CharacterSheets.FindAsync(p.SourceCharacterSheetId.Value);
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa, sheet?.VitalidadeAtual, sheet?.FocoAtual, sheet?.AdrenalinaAtual, p.AcoesRestantes, IsLiveSourced: true);
        }
        if (p.SourceNpcSheetId is not null)
        {
            var sheet = await db.NpcSheets.FindAsync(p.SourceNpcSheetId.Value);
            var isLive = sheet?.OwnerId is not null;
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa,
                isLive ? sheet?.VitalidadeAtual : p.PVAtual, isLive ? sheet?.FocoAtual : p.PFAtual, isLive ? sheet?.AdrenalinaAtual : p.PAAtual,
                p.AcoesRestantes, isLive);
        }
        if (p.SourceCreatureSheetId is not null)
        {
            var sheet = await db.CreatureSheets.FindAsync(p.SourceCreatureSheetId.Value);
            var isLive = sheet?.OwnerId is not null;
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa,
                isLive ? sheet?.VitalidadeAtual : p.PVAtual, isLive ? sheet?.FocoAtual : p.PFAtual, isLive ? sheet?.AdrenalinaAtual : p.PAAtual,
                p.AcoesRestantes, isLive);
        }
        // Source sheet was deleted (SetNull cascaded) — Nome snapshot still displays, PV/PF/PA frozen at whatever the columns last held.
        return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa, p.PVAtual, p.PFAtual, p.PAAtual, p.AcoesRestantes, IsLiveSourced: false);
    }

    private async Task<ActionResult?> CheckEncounterOwnershipAsync(Guid encounterId)
    {
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return NotFound();

        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == CurrentGmId());
        return isOwner ? null : NotFound();
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter EncounterParticipantsControllerTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Encounters src/RuinaRPG.Api/Controllers/EncounterParticipantsController.cs tests/RuinaRPG.Tests.Integration/Controllers/EncounterParticipantsControllerTests.cs
git commit -m "feat: add encounter participants with live and independent PV/PF/PA sourcing"
```

---

### Task 5: Api — edit participant state and advance turns (R0005, R0006)

**Files:**
- Create: `src/RuinaRPG.Contracts/Encounters/UpdateParticipantRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/EncounterParticipantsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/EncountersController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/EncounterParticipantsControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs`

**Interfaces:**
- Consumes: `TurnAdvanceCalculator.Advance` (Task 1), `EncounterParticipant`/`EncounterParticipantCondition` (Task 2).
- Produces: `PUT /api/encounters/{encounterId}/participants/{id}` → `204`/`400`/`404` — updates `Iniciativa`, `AcoesRestantes`, and (only for a non-live-sourced participant — `400` otherwise) `PVAtual`/`PFAtual`/`PAAtual`; also replaces its condition tags wholesale (simplest correct semantics for a short free-text tag list). `POST /api/encounters/{encounterId}/advance-turn` → `204`, resets the new current participant's `AcoesRestantes` to 3 (R0005) and updates `CurrentRound`/`CurrentParticipantIndex` via `TurnAdvanceCalculator`. `UpdateParticipantRequest(int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, List<string> Condicoes)`.

- [ ] **Step 1: Write the contract**

`src/RuinaRPG.Contracts/Encounters/UpdateParticipantRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Encounters;

public record UpdateParticipantRequest(int Iniciativa, int? PV, int? PF, int? PA, int AcoesRestantes, List<string> Condicoes);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/EncounterParticipantsControllerTests.cs`:

```csharp
[Fact]
public async Task Update_a_non_live_sourced_participant_changes_PV_and_condicoes()

[Fact]
public async Task Update_PV_on_a_live_sourced_participant_returns_400()
// R0003: PV/PF/PA are read-only on the Encounter screen for a live-sourced participant.
```

Append to `tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs`:

```csharp
[Fact]
public async Task AdvanceTurn_moves_to_the_next_participant_and_resets_their_AcoesRestantes()
// Also create a SECOND, unrelated encounter (different campaign or the same one, doesn't matter)
// with its own participants at Iniciativa values that would shift the Skip(nextIndex) result if the
// query were ever accidentally unscoped by EncounterId — this is a real regression this task's
// implementation must guard against, not a hypothetical.

[Fact]
public async Task AdvanceTurn_past_the_last_participant_starts_a_new_round()
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EncounterParticipantsControllerTests|FullyQualifiedName~EncountersControllerTests"`
Expected: the 4 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 4: Add `Update` to `EncounterParticipantsController`**

```csharp
[HttpPut("{id}")]
public async Task<IActionResult> Update(Guid encounterId, Guid id, UpdateParticipantRequest request)
{
    var authError = await CheckEncounterOwnershipAsync(encounterId);
    if (authError is not null)
        return authError;

    var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == id && p.EncounterId == encounterId);
    if (participant is null)
        return NotFound();

    var isLive = participant.SourceCharacterSheetId is not null
        || (participant.SourceNpcSheetId is not null && (await db.NpcSheets.FindAsync(participant.SourceNpcSheetId.Value))?.OwnerId is not null)
        || (participant.SourceCreatureSheetId is not null && (await db.CreatureSheets.FindAsync(participant.SourceCreatureSheetId.Value))?.OwnerId is not null);
    if (isLive && (request.PV is not null || request.PF is not null || request.PA is not null))
        return BadRequest("PV/PF/PA de um participante vindo de Ficha de Personagem são somente leitura aqui — edite a ficha diretamente.");

    participant.Iniciativa = request.Iniciativa;
    participant.AcoesRestantes = request.AcoesRestantes;
    if (!isLive)
    {
        participant.PVAtual = request.PV;
        participant.PFAtual = request.PF;
        participant.PAAtual = request.PA;
    }

    var existingConditions = await db.EncounterParticipantConditions.Where(c => c.EncounterParticipantId == id).ToListAsync();
    db.EncounterParticipantConditions.RemoveRange(existingConditions);
    foreach (var texto in request.Condicoes)
        db.EncounterParticipantConditions.Add(new EncounterParticipantCondition { Id = Guid.NewGuid(), EncounterParticipantId = id, Texto = texto });

    await db.SaveChangesAsync();
    return NoContent();
}
```

- [ ] **Step 5: Add `AdvanceTurn` to `EncountersController`**

```csharp
[HttpPost("~/api/encounters/{encounterId}/advance-turn")]
public async Task<IActionResult> AdvanceTurn(Guid encounterId)
{
    var gmId = CurrentGmId();
    var encounter = await db.Encounters.FindAsync(encounterId);
    if (encounter is null)
        return NotFound();
    var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == gmId);
    if (!isOwner)
        return NotFound();

    var participantCount = await db.EncounterParticipants.CountAsync(p => p.EncounterId == encounterId);
    if (participantCount == 0)
        return BadRequest("Adicione ao menos um participante antes de avançar o turno.");

    var (nextRound, nextIndex) = RuinaRPG.Domain.Encounters.TurnAdvanceCalculator.Advance(encounter.CurrentRound, encounter.CurrentParticipantIndex, participantCount);
    encounter.CurrentRound = nextRound;
    encounter.CurrentParticipantIndex = nextIndex;

    var nextParticipant = await db.EncounterParticipants
        .Where(p => p.EncounterId == encounterId)
        .OrderByDescending(p => p.Iniciativa)
        .Skip(nextIndex)
        .FirstAsync();
    nextParticipant.AcoesRestantes = 3;

    await db.SaveChangesAsync();
    return NoContent();
}
```

The `~/` prefix override is needed for the same reason as the Campanha — Anexos plan's `ListSecretNotes` action — `EncountersController`'s class route is `api/campaigns/{campaignId}/encounters`, but this action's real path has no `campaignId` segment.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EncounterParticipantsControllerTests|FullyQualifiedName~EncountersControllerTests"`
Expected: PASS across both files.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Contracts/Encounters/UpdateParticipantRequest.cs src/RuinaRPG.Api/Controllers/EncounterParticipantsController.cs src/RuinaRPG.Api/Controllers/EncountersController.cs tests/RuinaRPG.Tests.Integration/Controllers/EncounterParticipantsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/EncountersControllerTests.cs
git commit -m "feat: add participant editing and manual turn advancement"
```

---

### Task 6: Infrastructure + Api — real-time reflection Hub (Técnico R0001)

**Files:**
- Create: `src/RuinaRPG.Api/Hubs/EncounterHub.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Program.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Hubs/EncounterHubTests.cs`

**Interfaces:**
- Consumes: `CharacterSheetsController.Update` (Ficha de Personagem — Fundação plan).
- Produces: a SignalR Hub at `/hubs/encounters` (nginx already proxies `/hubs/` to the API with WebSocket upgrade headers — Foundation plan's `nginx.conf`, no Client-facing infra change needed), JWT-authenticated same as the rest of the Api. Clients call `JoinEncounter(string encounterId)` to subscribe to a SignalR group named `encounter-{encounterId}`; the server pushes a `ParticipantsChanged` message to that group whenever `CharacterSheetsController.Update` saves changes to a sheet that's a **live-sourced** participant in ANY open encounter — the Client's job (Task 8) is just to re-`GET .../participants` on receipt, not consume pushed values directly.

- [ ] **Step 1: Write the Hub**

`src/RuinaRPG.Api/Hubs/EncounterHub.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

[Authorize]
public class EncounterHub : Hub
{
    public async Task JoinEncounter(string encounterId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"encounter-{encounterId}");

    public async Task LeaveEncounter(string encounterId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"encounter-{encounterId}");
}
```

- [ ] **Step 2: Register the Hub**

In `src/RuinaRPG.Api/Program.cs`, add `builder.Services.AddSignalR();` near the other service registrations, and `app.MapHub<EncounterHub>("/hubs/encounters");` near the controller mapping (`app.MapControllers()`). Add `using RuinaRPG.Api.Hubs;`.

- [ ] **Step 3: Write the failing test**

`tests/RuinaRPG.Tests.Integration/Hubs/EncounterHubTests.cs` — connect a `HubConnection` (via `Microsoft.AspNetCore.SignalR.Client`, add the package to the test project if not already present) authenticated with a GM's JWT, call `JoinEncounter`, register a handler for `ParticipantsChanged`, then — from the same test, using the plain `_client` `HttpClient` — `PUT` an update to a `CharacterSheet` that's a live-sourced participant of that encounter (set up via Task 4's `POST .../participants`), and assert the handler fires within a short timeout (e.g. `TaskCompletionSource` + `Task.WhenAny` with a 5-second timeout, standard SignalR integration-test pattern).

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter EncounterHubTests`
Expected: FAIL — nothing broadcasts yet.

- [ ] **Step 5: Broadcast from `CharacterSheetsController.Update`**

Change the constructor to also take `IHubContext<EncounterHub>`:

```csharp
public class CharacterSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules, IHubContext<EncounterHub> hub) : ControllerBase
```

At the end of `Update`, right before `return NoContent();`:

```csharp
    var affectedEncounterIds = await db.EncounterParticipants
        .Where(p => p.SourceCharacterSheetId == id)
        .Select(p => p.EncounterId)
        .Distinct()
        .ToListAsync();
    foreach (var encounterId in affectedEncounterIds)
        await hub.Clients.Group($"encounter-{encounterId}").SendAsync("ParticipantsChanged");
```

Add `using Microsoft.AspNetCore.SignalR;` and `using RuinaRPG.Api.Hubs;` to the top of the file.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter EncounterHubTests`
Expected: PASS.

- [ ] **Step 7: Run the whole integration suite to confirm the constructor change didn't break anything else**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: all still pass — `ApiFactory`'s DI container resolves `IHubContext<EncounterHub>` automatically once `AddSignalR()` is registered (Step 2), no test-specific wiring needed.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Api/Hubs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Integration/Hubs/EncounterHubTests.cs
git commit -m "feat: broadcast live-sourced encounter participant changes over signalr"
```

---

### Task 7: Client — Gerenciador de Encontros page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/GerenciadorDeEncontros.razor`
- Modify: `src/RuinaRPG.Client/RuinaRPG.Client.csproj`
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`

**Interfaces:**
- Consumes: `EncounterResponse`/`CreateEncounterRequest` (Task 3), `EncounterParticipantResponse`/`AddParticipantRequest`/`UpdateParticipantRequest` (Tasks 4-5), the authenticated `HttpClient`, `Microsoft.AspNetCore.SignalR.Client`.
- Produces: the `/campanhas/{campaignId}/encontros/{encounterId}` route, and an "Encontros" list/create control on `/campanhas/{id}` (Campanha — Fundação plan's page). No later task depends on these files.

- [ ] **Step 1: Add the SignalR client package**

```bash
dotnet add src/RuinaRPG.Client package Microsoft.AspNetCore.SignalR.Client --version 8.0.11
```

- [ ] **Step 2: Add an Encontros list/create section to `CampanhaDetalhe.razor`**

Same pattern as the Diário section already on that page (Campanha — Fundação plan): a "Novo Encontro" button + list linking to `/campanhas/{CampaignId}/encontros/{id}`.

- [ ] **Step 3: Write the Encounter page**

`src/RuinaRPG.Client/Pages/GerenciadorDeEncontros.razor`:

```razor
@page "/campanhas/{CampaignId}/encontros/{EncounterId}"
@implements IAsyncDisposable
@inject HttpClient Http
@inject NavigationManager Navigation
@using Microsoft.AspNetCore.SignalR.Client
@using RuinaRPG.Contracts.Encounters

<h1>Encontro — Rodada @_encounter?.CurrentRound</h1>

<button @onclick="AddParticipantAsync">Adicionar Participante</button>
<button @onclick="AdvanceTurnAsync">Próximo Turno</button>

<table>
    <thead><tr><th>Nome</th><th>Iniciativa</th><th>PV</th><th>PF</th><th>PA</th><th>Ações</th><th>Condições</th></tr></thead>
    <tbody>
        @for (var i = 0; i < _participants.Count; i++)
        {
            var participant = _participants[i];
            var isCurrentTurn = i == _encounter?.CurrentParticipantIndex;
            <tr style="@(isCurrentTurn ? "font-weight: bold" : "")">
                <td>@participant.Nome</td>
                <td>@participant.Iniciativa</td>
                <td>@participant.PV</td>
                <td>@participant.PF</td>
                <td>@participant.PA</td>
                <td>@participant.AcoesRestantes</td>
                <td>@(participant.IsLiveSourced ? "(somente leitura)" : "")</td>
            </tr>
        }
    </tbody>
</table>

@code {
    [Parameter] public string CampaignId { get; set; } = "";
    [Parameter] public string EncounterId { get; set; } = "";

    private EncounterResponse? _encounter;
    private List<EncounterParticipantResponse> _participants = new();
    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(Http.BaseAddress!, "/"), "hubs/encounters"))
            .Build();
        _hubConnection.On("ParticipantsChanged", async () => { await LoadAsync(); StateHasChanged(); });
        await _hubConnection.StartAsync();
        await _hubConnection.SendAsync("JoinEncounter", EncounterId);
    }

    private async Task LoadAsync()
    {
        var encountersResponse = await Http.GetAsync($"campaigns/{CampaignId}/encounters");
        var encounters = await encountersResponse.Content.ReadFromJsonAsync<List<EncounterResponse>>() ?? new();
        _encounter = encounters.SingleOrDefault(e => e.Id == EncounterId);

        _participants = await Http.GetFromJsonAsync<List<EncounterParticipantResponse>>($"encounters/{EncounterId}/participants") ?? new();
    }

    private async Task AddParticipantAsync()
    {
        // Minimal form omitted for brevity in this plan — the implementer should add a small inline
        // form (source sheet id text input + a dropdown for which of the 3 source kinds, matching
        // AddParticipantRequest's shape) before wiring this button to a real POST call.
        await LoadAsync();
    }

    private async Task AdvanceTurnAsync()
    {
        await Http.PostAsync($"encounters/{EncounterId}/advance-turn", null);
        await LoadAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
```

**`AddParticipantAsync` needs a real form, not the stub above** — add inline inputs for the source sheet id and a radio/select for which of `SourceCharacterSheetId`/`SourceNpcSheetId`/`SourceCreatureSheetId` it targets, plus an `Iniciativa` number input, then `await Http.PostAsJsonAsync($"encounters/{EncounterId}/participants", new AddParticipantRequest(...))` before `await LoadAsync()`. The stub is left here only to keep this plan's page listing focused on the SignalR wiring, which is the part genuinely new to this plan — the form itself follows the exact `EditForm`/`InputText` pattern every other Client page in this codebase already uses.

- [ ] **Step 4: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Pages/GerenciadorDeEncontros.razor src/RuinaRPG.Client/RuinaRPG.Client.csproj src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor
git commit -m "feat: add the gerenciador de encontros page with real-time pv/pf/pa updates"
```

---

### Task 8: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files.

**Interfaces:**
- Consumes: the full stack. Produces: nothing new.

- [ ] **Step 1: Boot the stack, set up a GM, campaign, and a GM-owned NPC with VitalidadeAtual set, through nginx**

- [ ] **Step 2: Create an encounter and add the NPC as a participant, through nginx**

```bash
curl -sf -X POST "http://localhost/api/campaigns/$CAMPAIGN_ID/encounters" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" -d '{"nome":"Emboscada"}' | tee /tmp/encounter.json
ENCOUNTER_ID=$(jq -r .id /tmp/encounter.json)

curl -sf -X POST "http://localhost/api/encounters/$ENCOUNTER_ID/participants" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d "{\"sourceCharacterSheetId\":null,\"sourceNpcSheetId\":\"$NPC_ID\",\"sourceCreatureSheetId\":null,\"iniciativa\":15}"
```

Expected: HTTP 201.

- [ ] **Step 3: Confirm the participant list is ordered and advancing the turn resets Ações, through nginx**

```bash
curl -sf "http://localhost/api/encounters/$ENCOUNTER_ID/participants" -H "Authorization: Bearer $GM_TOKEN"
curl -sf -X POST "http://localhost/api/encounters/$ENCOUNTER_ID/advance-turn" -H "Authorization: Bearer $GM_TOKEN"
```

Expected: GET shows the participant with `iniciativa: 15`; advance-turn returns HTTP 204.

- [ ] **Step 4: Tear down**

```bash
make down
```

- [ ] **Step 5: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the encounter flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Rolling dice, resolving attacks/damage, or any other rule mechanic — the doc's own preamble: "não rola dados nem resolve ataques/dano."
- Removing a participant from an encounter or deleting an encounter entirely — no requirement asks for either.
- The `AddParticipantAsync` Client form's real fields (Task 7's stub) — flagged explicitly in that task as needing the standard `EditForm` treatment before shipping, deliberately left minimal here since the SignalR wiring is what's new to this plan.
- Any live reflection for participants sourced from a granted NPC/Criatura where the GRANTING GM is different from the Encounter's GM — can't happen under this codebase's authorization model (an Encounter's campaign always belongs to one GM, and a sheet can only be granted by that same campaign's GM), so this isn't a gap, just noted as an assumption this plan relies on.
