using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/equipagem")]
public class NpcEquipagemController(RuinaRpgDbContext db, EquipmentKitGrantService grantService) : ControllerBase
{
    [HttpGet("kits")]
    public async Task<ActionResult<List<EquipmentKitOptionResponse>>> ListKits(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();
        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await BuildOptionsAsync(sheet.GmId);
    }

    [HttpPost("choose")]
    public async Task<IActionResult> Choose(Guid sheetId, ChooseEquipmentKitRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();
        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        if (sheet.EquipmentKitId is not null)
            return BadRequest("Equipagem inicial já escolhida.");

        if (!Guid.TryParse(request.KitId, out var kitId))
            return BadRequest("KitId inválido.");
        var kit = await db.EquipmentKits.FirstOrDefaultAsync(k => k.Id == kitId && !k.IsDeleted);
        if (kit is null)
            return NotFound();

        var fixedItems = await db.EquipmentKitItems.Where(i => i.KitId == kitId).ToListAsync();
        var choiceSlots = await db.EquipmentKitChoiceSlots.Where(s => s.KitId == kitId).ToListAsync();
        var (plan, error) = await grantService.BuildPlanAsync(kit, fixedItems, choiceSlots, sheet.GmId, request.ChoiceSelections);
        if (plan is null)
            return BadRequest(error);

        foreach (var grant in plan.Grants)
            AddGrant(sheetId, grant);

        // NpcSheet has no direct CampaignId column — an NPC only has a campaign in the specific
        // context of "the campaign where it's attached and its current OwnerId is a member",
        // resolved exactly the way NpcSheetsController.ToResponseAsync does. A GM applying a kit to
        // their own not-yet-granted NPC resolves to null here — a correct no-op, not an error.
        var campaignId = await db.CampaignAttachments
            .Where(a => a.NpcSheetId == sheetId && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();
        if (campaignId is not null)
        {
            foreach (var itemId in plan.Grants.Select(g => g.ItemId).Distinct())
                await grantService.UpsertCampaignAttachmentAsync(campaignId.Value, itemId);
        }

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
                db.NpcWeapons.Add(new NpcWeapon { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.Escudo:
                db.NpcShields.Add(new NpcShield { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.ItemGeral:
                db.NpcInventoryItems.Add(new NpcInventoryItem { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = grant.ItemId, Qtd = grant.Qtd });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.Artefato:
                db.NpcArtifacts.Add(new NpcArtifact { Id = Guid.NewGuid(), NpcSheetId = sheetId, ArtifactItemId = grant.ItemId });
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
