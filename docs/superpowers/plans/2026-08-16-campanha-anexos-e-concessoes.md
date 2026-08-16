# Campanha — Anexos e Concessões Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the Campanha doc: attaching Catálogo items, Banco de Magias entries, and standalone images to a campaign with a public/private toggle (R0006-R0008), attaching NPC/Criatura sheets with their own Nome/Imagem visibility toggles (R0006/R0008-exception), the player-facing view that only shows public content plus the player's own sheets (R0009), granting NPC/Criatura sheets to members as pets/summons (R0010), and GM-authored Notas Secretas addressed to specific members (R0011).

**Architecture:** `CampaignAttachment` is one polymorphic table (Modelo de Dados §5) — exactly one of `ItemId`/`NpcSheetId`/`CreatureSheetId`/`SpellAbilityBankEntryId`/`ImageId` is set per row, validated by a Domain helper the same way `AddCharacterSpellAbilityRequest`'s "exactly one of X or Y" checks work elsewhere in this codebase. `IsPublic` drives visibility for Item/BankEntry/Image rows; NPC/Criatura rows ignore `IsPublic` entirely and use their own two independent booleans instead (R0008's stated exception). Granting a sheet (R0010) is a **deep copy**: a new `NpcSheet`/`CreatureSheet` row plus a full copy of every one of its already-built child tables (attributes, skills, weapons, spells, …) — reusing the two Ficha plans' entities directly, not inventing new ones. Notas Secretas (R0011) reuse `DiaryEntry`/`DiaryEntryRecipient` (Campanha — Fundação plan) with `IsSecretNote = true`, the one case that table family was built to support but the Fundação plan deliberately left unbuilt.

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Campanha.md` R0006-R0011, `Docs/Requisitos/Requisitos - Modelo de Dados.md` §5 (CampaignAttachments) and §7 (DiaryEntryRecipients).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Attachment write endpoints (attach, toggle visibility, remove) are GM-only, scoped to campaigns the caller owns — same isolation as every other Campanha endpoint.
- A new attachment defaults to **private** (R0008) unless it's an NPC/Criatura attachment, which defaults to both Nome and Imagem **off** (equivalent to fully private, per R0008's stated exception).
- Attaching never duplicates the source record — it's always a reference (`ItemId`/`NpcSheetId`/etc.), never a copy. Granting (R0010) is the one deliberate exception, and it's a full independent copy, not an attachment.
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Domain — attachment target validator

**Files:**
- Create: `src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTarget.cs`
- Create: `src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTargetValidator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Campaigns/CampaignAttachmentTargetValidatorTests.cs`

**Interfaces:**
- Produces: `enum CampaignAttachmentTarget { Item, NpcSheet, CreatureSheet, SpellAbilityBankEntry, Image }`; `CampaignAttachmentTargetValidator.ExactlyOneSet(string? itemId, string? npcSheetId, string? creatureSheetId, string? bankEntryId, string? imageId) : bool`. Task 3 (attach endpoint) calls this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Campaigns/CampaignAttachmentTargetValidatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Campaigns;

namespace RuinaRPG.Tests.Unit.Campaigns;

public class CampaignAttachmentTargetValidatorTests
{
    [Fact]
    public void ExactlyOneSet_is_true_when_only_one_id_is_provided()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet("item-1", null, null, null, null).Should().BeTrue();
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, "image-1").Should().BeTrue();
    }

    [Fact]
    public void ExactlyOneSet_is_false_when_none_are_provided()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, null).Should().BeFalse();
    }

    [Fact]
    public void ExactlyOneSet_is_false_when_more_than_one_is_provided()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet("item-1", "npc-1", null, null, null).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter CampaignAttachmentTargetValidatorTests`
Expected: FAIL to compile.

- [ ] **Step 3: Write the enum and validator**

`src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTarget.cs`:

```csharp
namespace RuinaRPG.Domain.Campaigns;

public enum CampaignAttachmentTarget
{
    Item,
    NpcSheet,
    CreatureSheet,
    SpellAbilityBankEntry,
    Image
}
```

`src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTargetValidator.cs`:

```csharp
namespace RuinaRPG.Domain.Campaigns;

public static class CampaignAttachmentTargetValidator
{
    public static bool ExactlyOneSet(string? itemId, string? npcSheetId, string? creatureSheetId, string? bankEntryId, string? imageId) =>
        new[] { itemId, npcSheetId, creatureSheetId, bankEntryId, imageId }.Count(id => id is not null) == 1;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter CampaignAttachmentTargetValidatorTests`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/Campaigns tests/RuinaRPG.Tests.Unit/Campaigns
git commit -m "feat: add campaign attachment target validator"
```

---

### Task 2: Infrastructure — `CampaignAttachment` entity and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Campaigns/CampaignAttachment.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CampaignAttachmentMigrationTests.cs`

**Interfaces:**
- Consumes: `Campaign` (Campanha — Fundação plan), `Item` (Catálogo plan), `NpcSheet`/`CreatureSheet` (their respective plans), `SpellAbilityBankEntry` (Banco de Magias plan), `Image` (Catálogo plan).
- Produces: `CampaignAttachment` with every column Modelo de Dados §5 names (`Guid Id`, `Guid CampaignId`, `Guid? ItemId`, `Guid? NpcSheetId`, `Guid? CreatureSheetId`, `Guid? SpellAbilityBankEntryId`, `Guid? ImageId`, `bool IsPublic`, `bool NpcNomePublico`, `bool NpcImagemPublica`, `bool CreatureNomePublico`, `bool CreatureImagemPublica`). `RuinaRpgDbContext.CampaignAttachments`. Tasks 3-4 depend on this shape.

- [ ] **Step 1: Write the entity**

`src/RuinaRPG.Infrastructure/Campaigns/CampaignAttachment.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Campaigns;

public class CampaignAttachment
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? NpcSheetId { get; set; }
    public Guid? CreatureSheetId { get; set; }
    public Guid? SpellAbilityBankEntryId { get; set; }
    public Guid? ImageId { get; set; }
    public bool IsPublic { get; set; }
    public bool NpcNomePublico { get; set; }
    public bool NpcImagemPublica { get; set; }
    public bool CreatureNomePublico { get; set; }
    public bool CreatureImagemPublica { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CampaignAttachmentMigrationTests.cs` — set up a GM, campaign, and one Catálogo `ItemGeral`, insert a `CampaignAttachment` with only `ItemId` set, assert it round-trips and the migration is named `AddCampaignAttachments`.

- [ ] **Step 3: Register the DbSet and relationships, create the migration, verify the test passes**

Add `public DbSet<CampaignAttachment> CampaignAttachments => Set<CampaignAttachment>();` and, in `OnModelCreating`:

```csharp
builder.Entity<CampaignAttachment>(entity =>
{
    entity.HasOne<Campaign>().WithMany().HasForeignKey(a => a.CampaignId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<NpcSheet>().WithMany().HasForeignKey(a => a.NpcSheetId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<CreatureSheet>().WithMany().HasForeignKey(a => a.CreatureSheetId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<SpellAbilityBankEntry>().WithMany().HasForeignKey(a => a.SpellAbilityBankEntryId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Image>().WithMany().HasForeignKey(a => a.ImageId).OnDelete(DeleteBehavior.Cascade);
});
```

All 5 target FKs cascade — removing the source item/sheet/entry/image also removes the attachment referencing it, which is correct (an attachment to nothing is meaningless).

```bash
dotnet ef migrations add AddCampaignAttachments --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignAttachmentMigrationTests` — expect PASS.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Campaigns/CampaignAttachment.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/CampaignAttachmentMigrationTests.cs
git commit -m "feat: add CampaignAttachment entity and migration"
```

---

### Task 3: Api — attach Item/BankEntry/Image, with public/private toggle (R0006, R0007, R0008)

**Files:**
- Create: `src/RuinaRPG.Contracts/Campaigns/AttachToCampaignRequest.cs`
- Create: `src/RuinaRPG.Contracts/Campaigns/CampaignAttachmentResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CampaignAttachmentsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignAttachmentsControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignAttachmentTargetValidator.ExactlyOneSet` (Task 1), `CampaignAttachment` (Task 2), `Item`/`SpellAbilityBankEntry`/`Image` (their respective plans).
- Produces: `POST /api/campaigns/{campaignId}/attachments` → `201`/`400`, `[Authorize(Roles = "GM")]`. `GET /api/campaigns/{campaignId}/attachments` → `200` + list, GM-only (the player-facing equivalent is Task 5). `PUT /api/campaigns/{campaignId}/attachments/{id}/visibility` → `204`/`404` (toggles `IsPublic` for Item/BankEntry/Image rows; `400` if called on an NPC/Criatura row — those use Task 4's toggle instead). `DELETE .../attachments/{id}` → `204`/`404`. `AttachToCampaignRequest(string? ItemId, string? NpcSheetId, string? CreatureSheetId, string? SpellAbilityBankEntryId, string? ImageId)`; `CampaignAttachmentResponse(string Id, string Tipo, string Nome, bool? IsPublic, bool? NpcNomePublico, bool? NpcImagemPublica, bool? CreatureNomePublico, bool? CreatureImagemPublica)`.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/Campaigns/AttachToCampaignRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record AttachToCampaignRequest(string? ItemId, string? NpcSheetId, string? CreatureSheetId, string? SpellAbilityBankEntryId, string? ImageId);
```

`src/RuinaRPG.Contracts/Campaigns/CampaignAttachmentResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record CampaignAttachmentResponse(string Id, string Tipo, string Nome, bool? IsPublic, bool? NpcNomePublico, bool? NpcImagemPublica, bool? CreatureNomePublico, bool? CreatureImagemPublica);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CampaignAttachmentsControllerTests.cs` — set up a GM, a campaign, and (via the Catálogo plan's `POST /api/items`) one `ItemGeral`. Cases:

```csharp
[Fact]
public async Task Attach_an_item_defaults_to_private()
// POST with ItemId set → 201, body.IsPublic == false.

[Fact]
public async Task Attach_with_no_target_set_returns_400()

[Fact]
public async Task Attach_with_two_targets_set_returns_400()

[Fact]
public async Task ToggleVisibility_flips_IsPublic_for_an_item_attachment()

[Fact]
public async Task Delete_removes_the_attachment_but_not_the_underlying_item()
// delete the attachment, then GET /api/items (Catálogo plan) still shows the item exists.

[Fact]
public async Task Attach_by_a_different_gm_to_someone_elses_campaign_returns_404()
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignAttachmentsControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 4: Write `CampaignAttachmentsController`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Domain.Campaigns;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/attachments")]
public class CampaignAttachmentsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CampaignAttachmentResponse>> Attach(Guid campaignId, AttachToCampaignRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        if (!CampaignAttachmentTargetValidator.ExactlyOneSet(request.ItemId, request.NpcSheetId, request.CreatureSheetId, request.SpellAbilityBankEntryId, request.ImageId))
            return BadRequest("Informe exatamente um alvo para o anexo.");

        var attachment = new CampaignAttachment
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            ItemId = request.ItemId is not null ? Guid.Parse(request.ItemId) : null,
            NpcSheetId = request.NpcSheetId is not null ? Guid.Parse(request.NpcSheetId) : null,
            CreatureSheetId = request.CreatureSheetId is not null ? Guid.Parse(request.CreatureSheetId) : null,
            SpellAbilityBankEntryId = request.SpellAbilityBankEntryId is not null ? Guid.Parse(request.SpellAbilityBankEntryId) : null,
            ImageId = request.ImageId is not null ? Guid.Parse(request.ImageId) : null,
            IsPublic = false, NpcNomePublico = false, NpcImagemPublica = false, CreatureNomePublico = false, CreatureImagemPublica = false
        };
        db.CampaignAttachments.Add(attachment);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(attachment));
    }

    [HttpGet]
    public async Task<ActionResult<List<CampaignAttachmentResponse>>> List(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var attachments = await db.CampaignAttachments.Where(a => a.CampaignId == campaignId).ToListAsync();
        var responses = new List<CampaignAttachmentResponse>();
        foreach (var attachment in attachments)
            responses.Add(await ToResponseAsync(attachment));
        return responses;
    }

    [HttpPut("{id}/visibility")]
    public async Task<IActionResult> ToggleVisibility(Guid campaignId, Guid id, [FromBody] bool isPublic)
    {
        var attachment = await FindOwnedAttachmentAsync(campaignId, id);
        if (attachment is null)
            return NotFound();

        if (attachment.NpcSheetId is not null || attachment.CreatureSheetId is not null)
            return BadRequest("Anexos de NPC/Criatura usam os toggles de Nome/Imagem, não este endpoint.");

        attachment.IsPublic = isPublic;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Remove(Guid campaignId, Guid id)
    {
        var attachment = await FindOwnedAttachmentAsync(campaignId, id);
        if (attachment is null)
            return NotFound();

        db.CampaignAttachments.Remove(attachment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CampaignAttachment?> FindOwnedAttachmentAsync(Guid campaignId, Guid id)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return null;

        return await db.CampaignAttachments.FirstOrDefaultAsync(a => a.Id == id && a.CampaignId == campaignId);
    }

    private async Task<CampaignAttachmentResponse> ToResponseAsync(CampaignAttachment a)
    {
        if (a.ItemId is not null)
        {
            var item = await db.Items.FindAsync(a.ItemId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "Item", item!.Nome, a.IsPublic, null, null, null, null);
        }
        if (a.SpellAbilityBankEntryId is not null)
        {
            var entry = await db.SpellAbilityBankEntries.FindAsync(a.SpellAbilityBankEntryId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "SpellAbilityBankEntry", entry!.Nome, a.IsPublic, null, null, null, null);
        }
        if (a.ImageId is not null)
        {
            var image = await db.Images.FindAsync(a.ImageId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "Image", image!.Path, a.IsPublic, null, null, null, null);
        }
        if (a.NpcSheetId is not null)
        {
            var npc = await db.NpcSheets.FindAsync(a.NpcSheetId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "NpcSheet", npc!.Nome ?? "", null, a.NpcNomePublico, a.NpcImagemPublica, null, null);
        }
        var creature = await db.CreatureSheets.FindAsync(a.CreatureSheetId!.Value);
        return new CampaignAttachmentResponse(a.Id.ToString(), "CreatureSheet", creature!.Nome ?? "", null, null, null, a.CreatureNomePublico, a.CreatureImagemPublica);
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignAttachmentsControllerTests`
Expected: PASS (6/6).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Campaigns src/RuinaRPG.Api/Controllers/CampaignAttachmentsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignAttachmentsControllerTests.cs
git commit -m "feat: add item/bank-entry/image campaign attachments with visibility toggle"
```

---

### Task 4: Api — NPC/Criatura attachment visibility toggles (R0006, R0008-exception)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CampaignAttachmentsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignAttachmentsControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignAttachmentsController` (Task 3).
- Produces: `PUT /api/campaigns/{campaignId}/attachments/{id}/npc-visibility` → `204`/`400`/`404`, body `{ "nomePublico": bool, "imagemPublica": bool }`; `PUT .../creature-visibility`, same shape. `400` if called on a non-matching attachment type (an Item attachment can't use the NPC toggle, and vice versa).

- [ ] **Step 1: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CampaignAttachmentsControllerTests.cs`:

```csharp
[Fact]
public async Task Attach_an_npc_defaults_both_toggles_off()
// POST with NpcSheetId set (create one via /api/npc-sheets first) → 201, npcNomePublico == false, npcImagemPublica == false.

[Fact]
public async Task NpcVisibility_can_toggle_Nome_and_Imagem_independently()

[Fact]
public async Task NpcVisibility_on_an_item_attachment_returns_400()

[Fact]
public async Task CreatureVisibility_can_toggle_Nome_and_Imagem_independently()
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignAttachmentsControllerTests`
Expected: the 4 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 3: Add the two toggle actions**

```csharp
[HttpPut("{id}/npc-visibility")]
public async Task<IActionResult> ToggleNpcVisibility(Guid campaignId, Guid id, [FromBody] NpcVisibilityRequest request)
{
    var attachment = await FindOwnedAttachmentAsync(campaignId, id);
    if (attachment is null)
        return NotFound();
    if (attachment.NpcSheetId is null)
        return BadRequest("Este anexo não é uma Ficha de NPC.");

    attachment.NpcNomePublico = request.NomePublico;
    attachment.NpcImagemPublica = request.ImagemPublica;
    await db.SaveChangesAsync();
    return NoContent();
}

[HttpPut("{id}/creature-visibility")]
public async Task<IActionResult> ToggleCreatureVisibility(Guid campaignId, Guid id, [FromBody] CreatureVisibilityRequest request)
{
    var attachment = await FindOwnedAttachmentAsync(campaignId, id);
    if (attachment is null)
        return NotFound();
    if (attachment.CreatureSheetId is null)
        return BadRequest("Este anexo não é uma Ficha de Criatura.");

    attachment.CreatureNomePublico = request.NomePublico;
    attachment.CreatureImagemPublica = request.ImagemPublica;
    await db.SaveChangesAsync();
    return NoContent();
}

public record NpcVisibilityRequest(bool NomePublico, bool ImagemPublica);
public record CreatureVisibilityRequest(bool NomePublico, bool ImagemPublica);
```

(These two small records live at the bottom of the controller file rather than in `RuinaRPG.Contracts` — they're a request shape used by exactly one action each, following the same lightweight pattern already used for `bool isPublic` bodies elsewhere in this controller.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignAttachmentsControllerTests`
Expected: PASS (10/10).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CampaignAttachmentsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignAttachmentsControllerTests.cs
git commit -m "feat: add npc/creature attachment visibility toggles"
```

---

### Task 5: Api — player-facing campaign content view (R0009)

**Files:**
- Create: `src/RuinaRPG.Contracts/Campaigns/PlayerCampaignViewResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignMember` (Campanha — Fundação plan), `CampaignAttachment` (Task 2), `CharacterSheet`/`NpcSheet`/`CreatureSheet` (their plans).
- Produces: `GET /api/campaigns/{campaignId}/player-view` → `200` + `PlayerCampaignViewResponse`, `[Authorize]` (any role, but `403` if the caller isn't a member of the campaign — GM or otherwise). Returns: the caller's own `CharacterSheet`s in that campaign; every `NpcSheet`/`CreatureSheet` granted to the caller (`OwnerId == caller`, R0010, built in Task 6); every **public** Item/BankEntry/Image attachment; every NPC/Criatura attachment showing only the fields toggled public (Nome and/or Imagem, `—` otherwise) — never the rest of those sheets, no matter what (R0004/R0003 of the NPC/Criatura plans, R0009's own explicit "o restante dela... é sempre visível somente ao GM"). `PlayerCampaignViewResponse(List<CharacterSheetSummary> MinhasFichas, List<GrantedSheetSummary> MeusCompanheiros, List<PublicAttachmentSummary> AnexosPublicos)` — 3 small nested records, defined inline in the same file since nothing else consumes them.

- [ ] **Step 1: Write the contract**

`src/RuinaRPG.Contracts/Campaigns/PlayerCampaignViewResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record CharacterSheetSummary(string Id, string? Nome, int Nivel);
public record GrantedSheetSummary(string Id, string Tipo, string? Nome); // Tipo: "Npc" | "Creature"
public record PublicAttachmentSummary(string Id, string Tipo, string? Nome, string? ImageUrl);

public record PlayerCampaignViewResponse(List<CharacterSheetSummary> MinhasFichas, List<GrantedSheetSummary> MeusCompanheiros, List<PublicAttachmentSummary> AnexosPublicos);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs` — set up a GM, a linked player added as a member, a campaign with: one `CharacterSheet` owned by the player, one public Item attachment, one private Item attachment, one NPC attachment with only `NpcNomePublico = true`. Cases:

```csharp
[Fact]
public async Task PlayerView_includes_the_players_own_sheet()

[Fact]
public async Task PlayerView_includes_only_the_public_item_not_the_private_one()

[Fact]
public async Task PlayerView_for_an_npc_attachment_shows_only_the_toggled_public_fields()
// AnexosPublicos entry for the NPC has Nome set (toggled public) but ImageUrl null (not toggled).

[Fact]
public async Task PlayerView_by_a_non_member_returns_403()

[Fact]
public async Task PlayerView_by_the_gm_also_succeeds_gm_is_always_allowed_to_see_it_too()
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignPlayerViewControllerTests`
Expected: FAIL — the route doesn't exist yet.

- [ ] **Step 4: Write `CampaignPlayerViewController`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId}/player-view")]
public class CampaignPlayerViewController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlayerCampaignViewResponse>> Get(Guid campaignId)
    {
        var callerId = CurrentUserId();
        var campaign = await db.Campaigns.FindAsync(campaignId);
        if (campaign is null)
            return NotFound();

        var isMember = campaign.GmId == callerId || await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == callerId);
        if (!isMember)
            return Forbid();

        var minhasFichas = await db.CharacterSheets
            .Where(s => s.CampaignId == campaignId && s.OwnerId == callerId)
            .Select(s => new CharacterSheetSummary(s.Id.ToString(), s.Nome, s.Nivel))
            .ToListAsync();

        var meusNpcs = await db.NpcSheets.Where(s => s.OwnerId == callerId).Select(s => new GrantedSheetSummary(s.Id.ToString(), "Npc", s.Nome)).ToListAsync();
        var meusCriaturas = await db.CreatureSheets.Where(s => s.OwnerId == callerId).Select(s => new GrantedSheetSummary(s.Id.ToString(), "Creature", s.Nome)).ToListAsync();

        var attachments = await db.CampaignAttachments.Where(a => a.CampaignId == campaignId).ToListAsync();
        var anexosPublicos = new List<PublicAttachmentSummary>();

        foreach (var a in attachments.Where(a => a.IsPublic && a.ItemId is not null))
        {
            var item = await db.Items.FindAsync(a.ItemId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Item", item!.Nome, item.ImageId is not null ? $"/{(await db.Images.FindAsync(item.ImageId.Value))!.Path}" : null));
        }
        foreach (var a in attachments.Where(a => a.IsPublic && a.SpellAbilityBankEntryId is not null))
        {
            var entry = await db.SpellAbilityBankEntries.FindAsync(a.SpellAbilityBankEntryId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "SpellAbilityBankEntry", entry!.Nome, null));
        }
        foreach (var a in attachments.Where(a => a.IsPublic && a.ImageId is not null))
        {
            var image = await db.Images.FindAsync(a.ImageId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Image", null, $"/{image!.Path}"));
        }
        foreach (var a in attachments.Where(a => a.NpcSheetId is not null && (a.NpcNomePublico || a.NpcImagemPublica)))
        {
            var npc = await db.NpcSheets.FindAsync(a.NpcSheetId!.Value);
            var nome = a.NpcNomePublico ? npc!.Nome : null;
            var imageUrl = a.NpcImagemPublica && npc!.ImageId is not null ? $"/{(await db.Images.FindAsync(npc.ImageId.Value))!.Path}" : null;
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "NpcSheet", nome, imageUrl));
        }
        foreach (var a in attachments.Where(a => a.CreatureSheetId is not null && (a.CreatureNomePublico || a.CreatureImagemPublica)))
        {
            var creature = await db.CreatureSheets.FindAsync(a.CreatureSheetId!.Value);
            var nome = a.CreatureNomePublico ? creature!.Nome : null;
            var imageUrl = a.CreatureImagemPublica && creature!.ImageId is not null ? $"/{(await db.Images.FindAsync(creature.ImageId.Value))!.Path}" : null;
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "CreatureSheet", nome, imageUrl));
        }

        return new PlayerCampaignViewResponse(minhasFichas, meusNpcs.Concat(meusCriaturas).ToList(), anexosPublicos);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignPlayerViewControllerTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Campaigns/PlayerCampaignViewResponse.cs src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs
git commit -m "feat: add the player-facing campaign content view"
```

---

### Task 6: Api — grant an NPC/Criatura sheet to a member (R0010)

**Files:**
- Create: `src/RuinaRPG.Contracts/Campaigns/GrantSheetRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/CampaignGrantsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignGrantsControllerTests.cs`

**Interfaces:**
- Consumes: `NpcSheet`/`CreatureSheet` and every one of their child tables (their respective plans), `CampaignMember` (Campanha — Fundação plan).
- Produces: `POST /api/campaigns/{campaignId}/grants` → `201` + `{ "sheetId": string, "tipo": "Npc"|"Creature" }`, `[Authorize(Roles = "GM")]`. `GrantSheetRequest(string PlayerId, string Tipo, string? SourceSheetId)` — `SourceSheetId` null means "em branco" (R0010's second option); set means "cópia de uma existente" (deep-copies every child row). Either way the new sheet's `OwnerId` is set to `PlayerId` immediately and `GmId` stays the calling GM's.

- [ ] **Step 1: Write the contract**

`src/RuinaRPG.Contracts/Campaigns/GrantSheetRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record GrantSheetRequest(string PlayerId, string Tipo, string? SourceSheetId);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CampaignGrantsControllerTests.cs` — set up a GM, a linked+member player, a campaign. Cases:

```csharp
[Fact]
public async Task Grant_blank_creates_a_new_owned_Npc_sheet()
// POST {"playerId": ..., "tipo": "Npc", "sourceSheetId": null} → 201; GET /api/npc-sheets/{sheetId}
// (as the GM, since NpcSheetsController is GM-only regardless of OwnerId in this plan's scope)
// shows ownerId == playerId, every field blank/zeroed.

[Fact]
public async Task Grant_from_an_existing_Npc_deep_copies_attributes_and_stays_independent()
// create a source Npc via /api/npc-sheets, set one attribute's Gasto via PUT, grant a copy from it,
// assert the new sheet's matching attribute has the same Gasto, THEN change the ORIGINAL's Gasto
// and confirm the granted copy's value is unaffected (independent copy, not a reference).

[Fact]
public async Task Grant_to_a_non_member_returns_400()

[Fact]
public async Task Grant_with_an_invalid_Tipo_returns_400()
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignGrantsControllerTests`
Expected: FAIL — the route doesn't exist yet.

- [ ] **Step 4: Write `CampaignGrantsController`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/grants")]
public class CampaignGrantsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Grant(Guid campaignId, GrantSheetRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var playerId = Guid.Parse(request.PlayerId);
        var isMember = await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == playerId);
        if (!isMember)
            return BadRequest("O jogador informado não é membro desta campanha.");

        if (request.Tipo == "Npc")
        {
            var newSheet = request.SourceSheetId is null
                ? new NpcSheet { Id = Guid.NewGuid(), GmId = gmId, OwnerId = playerId }
                : await DeepCopyNpcAsync(Guid.Parse(request.SourceSheetId), gmId, playerId);
            if (newSheet is null)
                return BadRequest("Ficha de NPC de origem não encontrada.");
            db.NpcSheets.Add(newSheet);
            await db.SaveChangesAsync();
            return Created(string.Empty, new { sheetId = newSheet.Id.ToString(), tipo = "Npc" });
        }
        if (request.Tipo == "Creature")
        {
            var newSheet = request.SourceSheetId is null
                ? new CreatureSheet { Id = Guid.NewGuid(), GmId = gmId, OwnerId = playerId }
                : await DeepCopyCreatureAsync(Guid.Parse(request.SourceSheetId), gmId, playerId);
            if (newSheet is null)
                return BadRequest("Ficha de Criatura de origem não encontrada.");
            db.CreatureSheets.Add(newSheet);
            await db.SaveChangesAsync();
            return Created(string.Empty, new { sheetId = newSheet.Id.ToString(), tipo = "Creature" });
        }

        return BadRequest("Tipo deve ser 'Npc' ou 'Creature'.");
    }

    private async Task<NpcSheet?> DeepCopyNpcAsync(Guid sourceId, Guid gmId, Guid ownerId)
    {
        var source = await db.NpcSheets.FindAsync(sourceId);
        if (source is null)
            return null;

        var copy = new NpcSheet
        {
            Id = Guid.NewGuid(), GmId = gmId, OwnerId = ownerId, ImageId = source.ImageId, Nome = source.Nome,
            Linhagem = source.Linhagem, Variante = source.Variante, Vocacao = source.Vocacao, SubVocacao = source.SubVocacao,
            Afinidade = source.Afinidade, Propriedade = source.Propriedade, Nivel = source.Nivel, Circulo = source.Circulo,
            Grau = source.Grau, PossuiCoracaoDeMana = source.PossuiCoracaoDeMana, ExperienciaAtual = source.ExperienciaAtual,
            EAPAtual = source.EAPAtual, NucleosRankF = source.NucleosRankF, NucleosRankE = source.NucleosRankE,
            NucleosRankD = source.NucleosRankD, NucleosRankC = source.NucleosRankC, NucleosRankB = source.NucleosRankB,
            NucleosRankA = source.NucleosRankA, NucleosRankS = source.NucleosRankS,
            PontosDeIgnicaoAtual = source.PontosDeIgnicaoAtual, PontosDeIgnicaoTotal = source.PontosDeIgnicaoTotal,
            VitalidadeAtual = source.VitalidadeAtual, FocoAtual = source.FocoAtual, AdrenalinaAtual = source.AdrenalinaAtual,
            EstresseAtual = source.EstresseAtual, Cobertura = source.Cobertura, Ciclos = source.Ciclos
        };

        // Deep-copy every child table — mechanical, one loop per table, same shape each time.
        foreach (var a in await db.NpcAttributes.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcAttributes.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Atributo = a.Atributo, Gasto = a.Gasto, Bonus = a.Bonus, TemMaestria = a.TemMaestria });
        foreach (var s in await db.NpcSkills.Where(x => x.NpcSheetId == sourceId).ToListAsync())
            db.NpcSkills.Add(new() { Id = Guid.NewGuid(), NpcSheetId = copy.Id, Pericia = s.Pericia, Gasto = s.Gasto });
        // ... repeat for NpcAffinities, NpcWeapons, NpcArmorSlots, NpcShields, NpcSpellAbilities (+ effects),
        // NpcRunes, NpcMasteries, NpcInventoryItems, NpcArtifacts, NpcAffections, NpcTraits — same
        // "select all rows for sourceId, re-Guid, re-parent to copy.Id, Add" pattern for every one.
        // Write out all 14 loops explicitly; this comment is a to-do marker for the implementer, not
        // something to ship literally — every table this plan's Ficha de NPCs predecessor built needs
        // its own copy loop here, or the deep copy is silently incomplete.

        return copy;
    }

    private async Task<CreatureSheet?> DeepCopyCreatureAsync(Guid sourceId, Guid gmId, Guid ownerId)
    {
        // Same pattern as DeepCopyNpcAsync, adapted for CreatureSheet's field set (Raca/Arquetipo/
        // SubArquetipo/Rank instead of Linhagem/Variante/Vocacao/SubVocacao/Circulo/Grau/EAP/Nucleos,
        // single PontosDeIgnicao, no EstresseAtual) and its 12 child tables (no Affinity/Rune, plus
        // CreatureWeapon's Manual* fields and CreatureSpoil instead of CreatureInventoryItem).
        var source = await db.CreatureSheets.FindAsync(sourceId);
        if (source is null)
            return null;

        var copy = new CreatureSheet
        {
            Id = Guid.NewGuid(), GmId = gmId, OwnerId = ownerId, ImageId = source.ImageId, Nome = source.Nome,
            Raca = source.Raca, Arquetipo = source.Arquetipo, SubArquetipo = source.SubArquetipo, Afinidade = source.Afinidade,
            Propriedade = source.Propriedade, Rank = source.Rank, Nivel = source.Nivel, ExperienciaAtual = source.ExperienciaAtual,
            PontosDeIgnicao = source.PontosDeIgnicao, VitalidadeAtual = source.VitalidadeAtual, FocoAtual = source.FocoAtual,
            AdrenalinaAtual = source.AdrenalinaAtual, Cobertura = source.Cobertura
        };

        foreach (var a in await db.CreatureAttributes.Where(x => x.CreatureSheetId == sourceId).ToListAsync())
            db.CreatureAttributes.Add(new() { Id = Guid.NewGuid(), CreatureSheetId = copy.Id, Atributo = a.Atributo, Gasto = a.Gasto, Bonus = a.Bonus, TemMaestria = a.TemMaestria });
        // ... same "copy every child table" pattern as DeepCopyNpcAsync, for all 12 Creature child tables.

        return copy;
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

**This task's implementer must expand every `// ...` comment above into real, compiling copy loops for all 14 NPC child tables and all 12 Creature child tables** — that is the actual work of this task, not optional detail. Use the exact entity shapes each Ficha plan already built; there is no ambiguity left to resolve, only volume to write. The two Step 2 tests (`Grant_blank_creates...`, `Grant_from_an_existing_Npc_deep_copies_attributes_and_stays_independent`) will not pass with the loops left as comments.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignGrantsControllerTests`
Expected: PASS (4/4).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Campaigns/GrantSheetRequest.cs src/RuinaRPG.Api/Controllers/CampaignGrantsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignGrantsControllerTests.cs
git commit -m "feat: add granting npc/creature sheets to campaign members"
```

---

### Task 7: Api — Notas Secretas (R0011)

**Files:**
- Create: `src/RuinaRPG.Contracts/Diary/CreateSecretNoteRequest.cs`
- Create: `src/RuinaRPG.Contracts/Diary/SecretNoteResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`

**Interfaces:**
- Consumes: `DiaryEntry`/`DiaryEntryRecipient` (Campanha — Fundação plan).
- Produces: `POST /api/campaigns/{campaignId}/secret-notes` → `201`, GM-only, `CreateSecretNoteRequest(string Texto, List<string> RecipientUserIds)` (every id must be a campaign member, `400` otherwise). `GET .../secret-notes` → `200` + list, **visible to the GM and to each note's own recipients only** — a member who isn't a recipient of a given note doesn't see it at all, not even that it exists (R0011: "os demais membros... não a veem, nem sabem que ela existe"). `PUT`/`DELETE` GM-only. `SecretNoteResponse(string Id, string Texto, DateTime CreatedAt, List<string> RecipientUserIds)`.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/Diary/CreateSecretNoteRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Diary;

public record CreateSecretNoteRequest(string Texto, List<string> RecipientUserIds);
```

`src/RuinaRPG.Contracts/Diary/SecretNoteResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Diary;

public record SecretNoteResponse(string Id, string Texto, DateTime CreatedAt, List<string> RecipientUserIds);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`:

```csharp
[Fact]
public async Task SecretNote_is_visible_to_the_gm_and_its_recipient()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SecretGm1", "secret1@teste.com");
    var playerId = await RegisterJogadorLinkedToAsync(gmToken, "SecretPlayer1", "secretplayer1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Secreta");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

    var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/secret-notes", gmToken,
        new CreateSecretNoteRequest("Você percebe algo estranho.", [playerId])));
    createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var gmListResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/secret-notes", gmToken));
    (await gmListResponse.Content.ReadFromJsonAsync<List<SecretNoteResponse>>())!.Should().ContainSingle();
}

[Fact]
public async Task SecretNote_is_invisible_to_a_member_who_is_not_a_recipient()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SecretGm2", "secret2@teste.com");
    var (recipientId, _) = await RegisterJogadorLinkedToAsyncWithToken(gmToken, "SecretRecipient2", "secretrecipient2@teste.com");
    var (otherMemberId, otherMemberToken) = await RegisterJogadorLinkedToAsyncWithToken(gmToken, "SecretOther2", "secretother2@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Secreta 2");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(recipientId)));
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(otherMemberId)));

    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/secret-notes", gmToken,
        new CreateSecretNoteRequest("Pista só para um deles.", [recipientId])));

    var otherListResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/secret-notes", otherMemberToken));

    otherListResponse.StatusCode.Should().Be(HttpStatusCode.OK); // R0011: not an error, just nothing to show
    (await otherListResponse.Content.ReadFromJsonAsync<List<SecretNoteResponse>>())!.Should().BeEmpty();
}

[Fact]
public async Task Create_with_a_non_member_recipient_returns_400()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SecretGm3", "secret3@teste.com");
    var (nonMemberId, _) = await RegisterJogadorLinkedToAsyncWithToken(gmToken, "SecretNonMember3", "secretnonmember3@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Secreta 3");
    // nonMemberId deliberately never added as a member

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/secret-notes", gmToken,
        new CreateSecretNoteRequest("Não deveria ser possível.", [nonMemberId])));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

`RegisterJogadorLinkedToAsyncWithToken` is a small addition to this test file's existing `RegisterJogadorLinkedToAsync` helper (Campanha — Fundação plan) that also returns the caller's access token — add it alongside the existing helper if it isn't already present:

```csharp
private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsyncWithToken(string gmToken, string nickname, string email)
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignsControllerTests`
Expected: the 3 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 4: Add `CreateSecretNote` (GM-only) to `CampaignsController`, and `ListSecretNotes` (any member) to `CampaignPlayerViewController`**

**Why two controllers:** ASP.NET Core combines a class-level `[Authorize(Roles = "GM")]` with any method-level `[Authorize]` by requiring *both* to pass — a method-level attribute cannot loosen a class-level role restriction, only add to it. `CampaignsController` (Campanha — Fundação plan) carries `[Authorize(Roles = "GM")]` at the class level, so `CreateSecretNote` (GM-only — correct) belongs there, but `ListSecretNotes` (readable by the GM *or* a note's recipients) cannot live in that same controller no matter what attribute is put on the action. It goes in `CampaignPlayerViewController` (Task 5 of this plan) instead, which is already `[Authorize]` with no role restriction and already has the member-check plumbing this action needs.

In `src/RuinaRPG.Api/Controllers/CampaignsController.cs`, add:

```csharp
[HttpPost("{campaignId}/secret-notes")]
public async Task<ActionResult<SecretNoteResponse>> CreateSecretNote(Guid campaignId, CreateSecretNoteRequest request)
{
    var gmId = CurrentGmId();
    var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
    if (!campaignExists)
        return NotFound();

    var recipientIds = request.RecipientUserIds.Select(Guid.Parse).ToList();
    var memberIds = await db.CampaignMembers.Where(m => m.CampaignId == campaignId).Select(m => m.UserId).ToListAsync();
    if (recipientIds.Any(id => !memberIds.Contains(id)))
        return BadRequest("Todo destinatário deve ser membro da campanha.");

    var note = new DiaryEntry { Id = Guid.NewGuid(), AuthorUserId = gmId, CampaignId = campaignId, IsSecretNote = true, Texto = request.Texto, CreatedAt = DateTime.UtcNow };
    db.DiaryEntries.Add(note);
    foreach (var recipientId in recipientIds)
        db.DiaryEntryRecipients.Add(new DiaryEntryRecipient { DiaryEntryId = note.Id, UserId = recipientId });
    await db.SaveChangesAsync();

    return Created(string.Empty, new SecretNoteResponse(note.Id.ToString(), note.Texto, note.CreatedAt, request.RecipientUserIds));
}
```

In `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs` (Task 5 of this plan), add — reusing that controller's existing `CurrentUserId()`:

```csharp
[HttpGet("~/api/campaigns/{campaignId}/secret-notes")]
public async Task<ActionResult<List<SecretNoteResponse>>> ListSecretNotes(Guid campaignId)
{
    var callerId = CurrentUserId();
    var isGm = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == callerId);

    var notes = await db.DiaryEntries.Where(d => d.CampaignId == campaignId && d.IsSecretNote).ToListAsync();
    var results = new List<SecretNoteResponse>();
    foreach (var note in notes)
    {
        var recipientIds = await db.DiaryEntryRecipients.Where(r => r.DiaryEntryId == note.Id).Select(r => r.UserId).ToListAsync();
        if (!isGm && !recipientIds.Contains(callerId))
            continue; // R0011: a non-recipient sees nothing, not even that the note exists

        results.Add(new SecretNoteResponse(note.Id.ToString(), note.Texto, note.CreatedAt, recipientIds.Select(id => id.ToString()).ToList()));
    }
    return results;
}
```

The `~/` route prefix override is needed because `CampaignPlayerViewController`'s class-level route is `api/campaigns/{campaignId}/player-view` — without `~/`, this action's route would nest under `.../player-view/secret-notes` instead of the intended `.../secret-notes`. Add `using RuinaRPG.Contracts.Diary;` to that file's usings.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignsControllerTests|FullyQualifiedName~CampaignPlayerViewControllerTests"`
Expected: PASS (all, including the 3 new secret-note tests).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Diary/CreateSecretNoteRequest.cs src/RuinaRPG.Contracts/Diary/SecretNoteResponse.cs src/RuinaRPG.Api/Controllers/CampaignsController.cs src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs
git commit -m "feat: add secret notes addressed to specific campaign members"
```

---

### Task 8: Client — attachments, player view, grants, and secret notes UI

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`

**Interfaces:**
- Consumes: every Contracts type from Tasks 3-7, the authenticated `HttpClient`.
- Produces: new sections on the existing `/campanhas/{id}` page. No later task depends on this file.

- [ ] **Step 1: Add the four sections**

Following the established pattern in `CampanhaDetalhe.razor` (member search/add, diary add/list, both from the Campanha — Fundação plan): an "Anexos" section (attach-by-ID inputs for Item/BankEntry/Image/NpcSheet/CreatureSheet, a list with visibility toggle controls matching each row's type), a "Conceder Ficha" control per member (choose Npc/Creature, blank or from an existing sheet, POST to `/grants`), a "Notas Secretas" section (recipient multi-select from the member list, add/list — GM's own view always shows every note; nothing here needs to render a player-restricted view since this whole page is already GM-only). Add a **separate, new page** `src/RuinaRPG.Client/Pages/MinhaCampanha.razor` at `/campanhas/{CampaignId}/jogador` calling `GET .../player-view`, rendering `MinhasFichas`/`MeusCompanheiros`/`AnexosPublicos` read-only — this is the page a Jogador actually uses (the GM's `/campanhas/{id}` stays GM-only, matching every other GM page in this Client).

- [ ] **Step 2: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor src/RuinaRPG.Client/Pages/MinhaCampanha.razor
git commit -m "feat: add attachments, grants, secret notes, and the player campaign view"
```

---

### Task 9: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files.

**Interfaces:**
- Consumes: the full stack. Produces: nothing new.

- [ ] **Step 1: Boot the stack, set up a GM, linked+member player, campaign, and one Catálogo item, through nginx**

- [ ] **Step 2: Attach the item, toggle it public, confirm it appears in the player view, through nginx**

```bash
curl -sf -X POST "http://localhost/api/campaigns/$CAMPAIGN_ID/attachments" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d "{\"itemId\":\"$ITEM_ID\",\"npcSheetId\":null,\"creatureSheetId\":null,\"spellAbilityBankEntryId\":null,\"imageId\":null}" | tee /tmp/attach.json
ATTACHMENT_ID=$(jq -r .id /tmp/attach.json)

curl -sf -X PUT "http://localhost/api/campaigns/$CAMPAIGN_ID/attachments/$ATTACHMENT_ID/visibility" \
  -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" -d 'true'

curl -sf "http://localhost/api/campaigns/$CAMPAIGN_ID/player-view" -H "Authorization: Bearer $PLAYER_TOKEN"
```

Expected: the player-view response's `anexosPublicos` contains the item.

- [ ] **Step 3: Grant a blank NPC to the player and confirm it's owned, through nginx**

```bash
curl -sf -X POST "http://localhost/api/campaigns/$CAMPAIGN_ID/grants" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d "{\"playerId\":\"$PLAYER_ID\",\"tipo\":\"Npc\",\"sourceSheetId\":null}"

curl -sf "http://localhost/api/campaigns/$CAMPAIGN_ID/player-view" -H "Authorization: Bearer $PLAYER_TOKEN"
```

Expected: `meusCompanheiros` contains one entry.

- [ ] **Step 4: Tear down**

```bash
make down
```

- [ ] **Step 5: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify attachments, grants, and secret notes flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Real-time reflection of a granted pet/summon's PV/PF/PA into the Gerenciador de Encontros — that plan's own job, consuming this plan's grant flow only for "where did this participant come from."
- Un-granting / revoking a granted sheet — no requirement asks for it; R0010 only describes granting.
- Bulk attach/detach UI polish — one attach call per item, matching every other list-based Client page's pattern in this codebase.
