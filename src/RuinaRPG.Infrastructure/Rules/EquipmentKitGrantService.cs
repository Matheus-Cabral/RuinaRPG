using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public record EquipmentGrantPlan(List<EquipmentGrantPlanItem> Grants, int Ciclos);
public record EquipmentGrantPlanItem(ItemTipo Tipo, Guid ItemId, int Qtd, int? DurabilidadeMaxima, ArmorSlotType? ArmorSlot);

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
        var allowedValues = slot.SubcategoriasCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        bool Matches(string? itemSubcategoria)
        {
            if (allowedValues.Length == 0)
                return true;
            if (allowedValues.Contains(itemSubcategoria))
                return true; // legacy: raw Subcategoria string equals one of the stored values
            if (SubcategoriaBuilder.TryParse(itemSubcategoria, out var tipo, out _, out var familia) && tipo == slot.Tipo && allowedValues.Contains(familia))
                return true; // new: parsed Família equals one of the stored values
            return false;
        }

        var candidates = new List<(Guid Id, string Nome)>();
        switch (slot.Tipo)
        {
            case ItemTipo.Arma:
                var armaQuery = db.Set<Arma>().Where(a => a.GmId == gmId);
                if (slot.Tier is not null)
                    armaQuery = armaQuery.Where(a => a.Tier == slot.Tier);
                var armas = await armaQuery.Select(a => new { a.Id, a.Nome, a.Subcategoria }).ToListAsync();
                candidates.AddRange(armas.Where(a => Matches(a.Subcategoria)).Select(a => (a.Id, a.Nome)));
                break;
            case ItemTipo.Armadura:
                var armaduras = await db.Set<Armadura>().Where(a => a.GmId == gmId).Select(a => new { a.Id, a.Nome, a.Subcategoria }).ToListAsync();
                candidates.AddRange(armaduras.Where(a => Matches(a.Subcategoria)).Select(a => (a.Id, a.Nome)));
                break;
            case ItemTipo.Escudo:
                var escudos = await db.Set<Escudo>().Where(e => e.GmId == gmId).Select(e => new { e.Id, e.Nome, e.Subcategoria }).ToListAsync();
                candidates.AddRange(escudos.Where(e => Matches(e.Subcategoria)).Select(e => (e.Id, e.Nome)));
                break;
            case ItemTipo.Artefato:
                var artefatos = await db.Set<Artefato>().Where(a => a.GmId == gmId).Select(a => new { a.Id, a.Nome, a.Subcategoria }).ToListAsync();
                candidates.AddRange(artefatos.Where(a => Matches(a.Subcategoria)).Select(a => (a.Id, a.Nome)));
                break;
        }

        return candidates.OrderBy(c => c.Nome).Select(c => new EquipmentKitEligibleItemResponse(c.Id.ToString(), c.Nome)).ToList();
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
            grants.Add(new EquipmentGrantPlanItem(kitItem.Tipo, resolved.Value.Id, kitItem.Qtd, resolved.Value.DurabilidadeMaxima, null));
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

            string? selectedSubcategoria;
            int? selectedDurabilidade;
            switch (slot.Tipo)
            {
                case ItemTipo.Arma:
                    var arma = await db.Set<Arma>().Where(a => a.Id == selectedItemId).Select(a => new { a.Subcategoria, a.DurabilidadeMaxima }).SingleAsync();
                    selectedSubcategoria = arma.Subcategoria; selectedDurabilidade = arma.DurabilidadeMaxima;
                    break;
                case ItemTipo.Armadura:
                    var armadura = await db.Set<Armadura>().Where(a => a.Id == selectedItemId).Select(a => new { a.Subcategoria, a.DurabilidadeMaxima }).SingleAsync();
                    selectedSubcategoria = armadura.Subcategoria; selectedDurabilidade = armadura.DurabilidadeMaxima;
                    break;
                case ItemTipo.Escudo:
                    var escudo = await db.Set<Escudo>().Where(e => e.Id == selectedItemId).Select(e => new { e.Subcategoria, e.DurabilidadeMaxima }).SingleAsync();
                    selectedSubcategoria = escudo.Subcategoria; selectedDurabilidade = escudo.DurabilidadeMaxima;
                    break;
                case ItemTipo.Artefato:
                    selectedSubcategoria = await db.Set<Artefato>().Where(a => a.Id == selectedItemId).Select(a => a.Subcategoria).SingleAsync();
                    selectedDurabilidade = null;
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled choice-slot Tipo {slot.Tipo}.");
            }
            grants.Add(new EquipmentGrantPlanItem(slot.Tipo, selectedItemId, slot.Qtd, selectedDurabilidade, slot.ArmorSlot));

            var bonusMatches = slot.BonusNome is not null && (selectedSubcategoria == slot.BonusSubcategoria
                || (SubcategoriaBuilder.TryParse(selectedSubcategoria, out var selectedTipo, out _, out var selectedFamilia) && selectedTipo == slot.Tipo && selectedFamilia == slot.BonusSubcategoria));
            if (bonusMatches)
            {
                var bonusResolved = await ResolveOrCreateFixedItemAsync(
                    new EquipmentKitItem { Nome = slot.BonusNome!, Tipo = ItemTipo.ItemGeral, Qtd = slot.BonusQtd ?? 1 }, gmId);
                if (bonusResolved is not null)
                    grants.Add(new EquipmentGrantPlanItem(ItemTipo.ItemGeral, bonusResolved.Value.Id, slot.BonusQtd ?? 1, null, null));
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
