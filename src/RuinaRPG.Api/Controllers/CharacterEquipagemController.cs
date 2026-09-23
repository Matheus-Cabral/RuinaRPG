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
        var (plan, error) = await grantService.BuildPlanAsync(kit, fixedItems, choiceSlots, campaignGmId, request.ChoiceSelections ?? []);
        if (plan is null)
            return BadRequest(error);

        var existingArtifactTipos = db.CharacterArtifacts.Where(a => a.CharacterSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i.TipoDeAlvo);
        var artifactCapError = await grantService.CheckArtifactCapAsync(plan.Grants, existingArtifactTipos);
        if (artifactCapError is not null)
            return BadRequest(artifactCapError);

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
