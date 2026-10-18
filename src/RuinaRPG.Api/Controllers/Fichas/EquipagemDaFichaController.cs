using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Escolha da Equipagem inicial da ficha (Personagem e NPC): os kits disponíveis e a concessão dos itens do kit.
/// A rota (<c>api/&lt;tipo&gt;-sheets/{sheetId}/equipagem</c>) fica na subclasse de cada tipo.
/// </summary>
public abstract class EquipagemDaFichaController<TSheet, TArma, TSlot, TEscudo, TItem, TArtefato>(RuinaRpgDbContext db, EquipmentKitGrantService grantService, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFichaComEquipagem
    where TArma : class, IEquipavelDoCatalogo, new()
    where TSlot : class, ISlotDeArmaduraDaFicha
    where TEscudo : class, IEquipavelDoCatalogo, new()
    where TItem : class, IItemDoInventarioDaFicha, new()
    where TArtefato : class, IArtefatoDaFicha, new()
{
    protected RuinaRpgDbContext Db { get; } = db;
    protected EquipmentKitGrantService GrantService { get; } = grantService;

    [HttpGet("kits")]
    public async Task<ActionResult<List<EquipmentKitOptionResponse>>> ListKits(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        return await BuildOptionsAsync(await Tipo.MestreAsync(sheet));
    }

    [HttpPost("choose")]
    public async Task<IActionResult> Choose(Guid sheetId, ChooseEquipmentKitRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        if (sheet.EquipmentKitId is not null)
            return BadRequest("Equipagem inicial já escolhida.");

        if (!Guid.TryParse(request.KitId, out var kitId))
            return BadRequest("KitId inválido.");
        var kit = await Db.EquipmentKits.FirstOrDefaultAsync(k => k.Id == kitId && !k.IsDeleted);
        if (kit is null)
            return NotFound();

        var fixedItems = await Db.EquipmentKitItems.Where(i => i.KitId == kitId).ToListAsync();
        var choiceSlots = await Db.EquipmentKitChoiceSlots.Where(s => s.KitId == kitId).ToListAsync();
        var (plan, error) = await GrantService.BuildPlanAsync(kit, fixedItems, choiceSlots, await Tipo.MestreAsync(sheet), request.ChoiceSelections ?? []);
        if (plan is null)
            return BadRequest(error);

        var existingArtifactTipos = Db.Set<TArtefato>().Where(a => a.SheetId == sheetId)
            .Join(Db.Set<Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i.TipoDeAlvo);
        var artifactCapError = await GrantService.CheckArtifactCapAsync(plan.Grants, existingArtifactTipos);
        if (artifactCapError is not null)
            return BadRequest(artifactCapError);

        foreach (var grant in plan.Grants)
            await AddGrantAsync(sheetId, grant);

        // Um NPC do próprio Mestre ainda não concedido não tem campanha: aí não há o que anexar (não é erro).
        var campaignId = await Tipo.CampanhaAsync(sheet);
        if (campaignId is not null)
        {
            foreach (var itemId in plan.Grants.Select(g => g.ItemId).Distinct())
                await GrantService.UpsertCampaignAttachmentAsync(campaignId.Value, itemId);
        }

        sheet.Ciclos += plan.Ciclos;
        sheet.EquipmentKitId = kit.Id;
        await Db.SaveChangesAsync();

        return NoContent();
    }

    private async Task AddGrantAsync(Guid sheetId, EquipmentGrantPlanItem grant)
    {
        switch (grant.Tipo)
        {
            case ItemTipo.Arma:
                Db.Set<TArma>().Add(new TArma { Id = Guid.NewGuid(), SheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case ItemTipo.Armadura:
                var armorSlot = await Db.Set<TSlot>().SingleAsync(s => s.SheetId == sheetId && s.Slot == grant.ArmorSlot);
                armorSlot.ItemId = grant.ItemId;
                armorSlot.DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0;
                break;
            case ItemTipo.Escudo:
                Db.Set<TEscudo>().Add(new TEscudo { Id = Guid.NewGuid(), SheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case ItemTipo.ItemGeral:
                Db.Set<TItem>().Add(new TItem { Id = Guid.NewGuid(), SheetId = sheetId, ItemId = grant.ItemId, Qtd = grant.Qtd });
                break;
            case ItemTipo.Artefato:
                Db.Set<TArtefato>().Add(new TArtefato { Id = Guid.NewGuid(), SheetId = sheetId, ArtifactItemId = grant.ItemId });
                break;
        }
    }

    private async Task<List<EquipmentKitOptionResponse>> BuildOptionsAsync(Guid gmId)
    {
        var kits = await Db.EquipmentKits.Where(k => !k.IsDeleted).OrderBy(k => k.Nome).ToListAsync();
        var responses = new List<EquipmentKitOptionResponse>();
        foreach (var kit in kits)
        {
            var slots = await Db.EquipmentKitChoiceSlots.Where(s => s.KitId == kit.Id).ToListAsync();
            var slotOptions = new List<EquipmentKitChoiceSlotOptionResponse>();
            foreach (var slot in slots)
                slotOptions.Add(new EquipmentKitChoiceSlotOptionResponse(slot.Id.ToString(), slot.Label, await GrantService.ResolveEligibleOptionsAsync(slot, gmId)));
            responses.Add(new EquipmentKitOptionResponse(kit.Id.ToString(), kit.Nome, kit.Descricao, kit.Ciclos, slotOptions));
        }
        return responses;
    }
}
