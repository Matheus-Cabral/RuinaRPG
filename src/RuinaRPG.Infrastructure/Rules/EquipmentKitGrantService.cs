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
/// A fixed item comes from the global EquipmentKitFixedItems base as a complete item: if the GM
/// already has an item of the same Nome+Tipo it is reused as is, otherwise a full independent copy
/// is created for THAT GM — applying a kit never fails for a missing item and never touches
/// another GM's catalog.
/// Shared by CharacterEquipagemController/NpcEquipagemController, which each own the actual
/// per-Tipo insert into CharacterWeapon/NpcWeapon etc. — this service only resolves/validates and
/// upserts the campaign-visibility side effect (Requisitos - Campanha's "itens de conhecimento
/// geral" exception to the normal manual-attach-and-publish flow).
/// </summary>
public class EquipmentKitGrantService(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades)
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
                if (slot.Rank is not null)
                    armaQuery = armaQuery.Where(a => a.Rank == slot.Rank);
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
            var resolved = await ResolveOrCreateFixedItemAsync(kitItem.FixedItemId, gmId);
            if (resolved is null)
                return (null, "O kit ainda não foi convertido para a base de itens fixos. Rode `make migrate`.");
            grants.Add(new EquipmentGrantPlanItem(resolved.Value.Tipo, resolved.Value.Id, kitItem.Qtd, resolved.Value.DurabilidadeMaxima, kitItem.ArmorSlot));
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
            RankDeItem? selectedRank;
            switch (slot.Tipo)
            {
                case ItemTipo.Arma:
                    var arma = await db.Set<Arma>().Where(a => a.Id == selectedItemId).Select(a => new { a.Subcategoria, a.Rank }).SingleAsync();
                    selectedSubcategoria = arma.Subcategoria; selectedRank = arma.Rank;
                    break;
                case ItemTipo.Armadura:
                    var armadura = await db.Set<Armadura>().Where(a => a.Id == selectedItemId).Select(a => new { a.Subcategoria, a.Rank }).SingleAsync();
                    selectedSubcategoria = armadura.Subcategoria; selectedRank = armadura.Rank;
                    break;
                case ItemTipo.Escudo:
                    var escudo = await db.Set<Escudo>().Where(e => e.Id == selectedItemId).Select(e => new { e.Subcategoria, e.Rank }).SingleAsync();
                    selectedSubcategoria = escudo.Subcategoria; selectedRank = escudo.Rank;
                    break;
                case ItemTipo.Artefato:
                    selectedSubcategoria = await db.Set<Artefato>().Where(a => a.Id == selectedItemId).Select(a => a.Subcategoria).SingleAsync();
                    selectedRank = null;
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled choice-slot Tipo {slot.Tipo}.");
            }
            var selectedDurabilidade = (await durabilidades.ResolverAsync(selectedRank)).Maxima;
            grants.Add(new EquipmentGrantPlanItem(slot.Tipo, selectedItemId, slot.Qtd, selectedDurabilidade, slot.ArmorSlot));

            var bonusMatches = slot.BonusFixedItemId is not null && (selectedSubcategoria == slot.BonusSubcategoria
                || (SubcategoriaBuilder.TryParse(selectedSubcategoria, out var selectedTipo, out _, out var selectedFamilia) && selectedTipo == slot.Tipo && selectedFamilia == slot.BonusSubcategoria));
            if (bonusMatches)
            {
                var bonusResolved = await ResolveOrCreateFixedItemAsync(slot.BonusFixedItemId, gmId);
                if (bonusResolved is not null)
                    grants.Add(new EquipmentGrantPlanItem(ItemTipo.ItemGeral, bonusResolved.Value.Id, slot.BonusQtd ?? 1, null, null));
            }
        }

        return (new EquipmentGrantPlan(grants, kit.Ciclos), null);
    }

    /// <summary>
    /// O item do catálogo do GM que corresponde a um item fixo: o de mesmo Nome+Tipo que o GM já tem (ou que já foi
    /// criado nesta aplicação) é reaproveitado como está; senão cria-se uma cópia completa, sempre com GmId = gmId.
    /// Null só quando não há item fixo (linha legada ainda não convertida).
    /// </summary>
    private async Task<(Guid Id, ItemTipo Tipo, int? DurabilidadeMaxima)?> ResolveOrCreateFixedItemAsync(Guid? fixedItemId, Guid gmId)
    {
        if (fixedItemId is null)
            return null;
        var fixo = await db.EquipmentKitFixedItems.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fixedItemId);
        if (fixo is null)
            return null;

        var discriminador = fixo.Tipo.ToString();
        var existente = db.Items.Local.FirstOrDefault(i => i.GmId == gmId && i.Nome == fixo.Nome && ItemFactory.TipoDe(i) == fixo.Tipo)
            ?? await db.Items.FirstOrDefaultAsync(i => i.GmId == gmId && i.Nome == fixo.Nome && EF.Property<string>(i, "Tipo") == discriminador);

        if (existente is null)
        {
            var request = ItemFactory.Desserializar(fixo.Dados);
            ItemFactory.TryParseRank(request.Rank, out var rank);
            existente = ItemFactory.Criar(fixo.Tipo, request, rank, fixo.Requisitos, fixo.PenalidadeDeRequisitos);
            existente.Id = Guid.NewGuid();
            existente.GmId = gmId;
            db.Items.Add(existente);
        }

        var rankDoItem = existente switch { Arma a => a.Rank, Armadura ar => ar.Rank, Escudo e => e.Rank, _ => null };
        return (existente.Id, fixo.Tipo, rankDoItem is null ? null : (await durabilidades.ResolverAsync(rankDoItem)).Maxima);
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

        // Um Artefato que a aplicação do kit está CRIANDO só existe no change tracker (ainda não foi salvo):
        // os que já estão no banco vêm da consulta, os pendentes do Local.
        var grantedTipos = await db.Set<Artefato>().AsNoTracking()
            .Where(a => artifactGrantItemIds.Contains(a.Id))
            .Select(a => a.TipoDeAlvo)
            .ToListAsync();
        var pendentes = db.Set<Artefato>().Local.Where(a => artifactGrantItemIds.Contains(a.Id) && db.Entry(a).State == EntityState.Added).Select(a => a.TipoDeAlvo);
        grantedTipos.AddRange(pendentes);

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
