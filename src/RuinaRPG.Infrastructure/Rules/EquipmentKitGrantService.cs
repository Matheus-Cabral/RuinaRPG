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

    /// <summary>
    /// Mirrors CharacterPossessionsController/NpcPossessionsController's AddArtifact "limite de 3
    /// por TipoDeAlvo validado na aplicação, não no schema" cap (Requisitos - Modelo de Dados). No
    /// seeded kit grants an Artefato today, but the Auditoria page's Tipo dropdown allows authoring
    /// one, so the same cap must hold when a kit's Artefato grant(s) land on a sheet — counting the
    /// sheet's existing Artefatos of that TipoDeAlvo plus any Artefato grants the kit itself carries
    /// (a single kit could grant more than one of the same TipoDeAlvo).
    /// </summary>
    public async Task<string?> CheckArtifactCapAsync(List<EquipmentGrantPlanItem> grants, IQueryable<TipoDeAlvo?> existingArtifactTiposOnSheet)
    {
        var artifactGrantItemIds = grants.Where(g => g.Tipo == ItemTipo.Artefato).Select(g => g.ItemId).ToList();
        if (artifactGrantItemIds.Count == 0)
            return null;

        var grantedTipos = await db.Set<Artefato>()
            .Where(a => artifactGrantItemIds.Contains(a.Id))
            .Select(a => a.TipoDeAlvo)
            .ToListAsync();

        foreach (var group in grantedTipos.GroupBy(t => t))
        {
            var existingCount = await existingArtifactTiposOnSheet.CountAsync(t => t == group.Key);
            if (existingCount + group.Count() > 3)
                return $"Limite de 3 Artefatos do tipo {group.Key} já atingido.";
        }

        return null;
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
