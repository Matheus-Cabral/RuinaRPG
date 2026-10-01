using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Sub-Atributos and the FichaParaRequisitos snapshot for a Ficha de NPC — mirrors
/// CharacterSheetStats table-for-table, scoped to NpcAttributes/NpcSkills/NpcArtifacts. Moved out
/// of NpcSheetsController so both /sub-attributes (unchanged response) and the Passiva requisitos
/// check (Task 5) share one live computation. Read-only, everything derived live.
/// </summary>
public class NpcSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias)
{
    public async Task<SubAttributesResponse> SubAtributosAsync(NpcSheet sheet)
    {
        var id = sheet.Id;
        var artefatos = await GetArtifactBonusInputsAsync(id);
        var agilidade = await GetAttributeTotalAsync(id, Atributo.Agilidade, artefatos);
        var vigor = await GetAttributeTotalAsync(id, Atributo.Vigor, artefatos);
        var forca = await GetAttributeTotalAsync(id, Atributo.Forca, artefatos);

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        var brutoSkills = await db.NpcSkills
            .Where(s => s.NpcSheetId == id && (s.PericiaId == PericiasDeSistema.Prontidao || s.PericiaId == PericiasDeSistema.Reflexos || s.PericiaId == PericiasDeSistema.Fortitude))
            .ToListAsync();
        int BrutoOf(int periciaId) => SkillFormulas.Modificador(
            brutoSkills.Single(s => s.PericiaId == periciaId).Gasto,
            HistoricoBonusCalculator.For(periciaId, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));

        var brutoProntidao = BrutoOf(PericiasDeSistema.Prontidao);
        var brutoReflexos = BrutoOf(PericiasDeSistema.Reflexos);
        var brutoFortitude = BrutoOf(PericiasDeSistema.Fortitude);

        var weapons = await db.NpcWeapons.Where(w => w.NpcSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var shields = await db.NpcShields.Where(s => s.NpcSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => new { s.IsEquipped, i.Peso }).ToListAsync();
        var inventoryItems = await db.NpcInventoryItems.Where(i => i.NpcSheetId == id)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>(), i => i.ItemId, g => g.Id, (i, g) => new { i.Qtd, g.Peso, g.CapacidadeExtra })
            .ToListAsync();

        // Peso Total 2.b: Inventário (5.a) + Armas/Escudos DESequipados. Armaduras never count —
        // an NpcArmorSlot with an ItemId is inherently worn (no unequipped state exists for armor
        // in this schema). A Capacidade Extra ("mochila") item doesn't add its own Peso here — it
        // raises pesoMaximo instead.
        var pesoAtual = inventoryItems.Where(i => CarryWeightCalculator.CountsTowardPesoAtual(i.CapacidadeExtra)).Sum(i => i.Peso * i.Qtd)
            + weapons.Where(w => !w.IsEquipped).Sum(w => w.Peso)
            + shields.Where(s => !s.IsEquipped).Sum(s => s.Peso);
        var capacidadeExtraTotal = inventoryItems.Sum(i => (i.CapacidadeExtra ?? 0m) * i.Qtd);
        var pesoMaximo = CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal);

        var equippedShield = await db.NpcShields
            .Where(s => s.NpcSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => i.BonusDefesa)
            .FirstOrDefaultAsync();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every NpcArmorSlot with an ItemId is inherently worn (no separate IsEquipped flag,
        // unlike weapons/shields) — so, unlike Peso Total Carregado, this is already scoped to
        // equipped armor only.
        var armorRfRm = await db.NpcArmorSlots
            .Where(a => a.NpcSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { i.RF, i.RM })
            .ToListAsync();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        var linhasDeAfinidade = await db.NpcAffinities.Where(a => a.NpcSheetId == id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor))
            .ToListAsync();
        var valorDaAfinidade = SubAttributeFormulas.ValorDaAfinidadeCorrespondente(sheet.Afinidade, linhasDeAfinidade);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo, not a number — same real, still-open gap as the Ficha de Personagem version.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo,
            EficienciaElemental: SubAttributeFormulas.EficienciaElemental(valorDaAfinidade),
            DanoElemental: SubAttributeFormulas.DanoElemental(valorDaAfinidade));
    }

    /// <summary>
    /// Graduação uses the stored EAPAtual directly — exactly like
    /// NpcSheetsController.ToResponseAsync (unlike Ficha de Personagem, where EAPAtual is a pure
    /// function of Nível/Núcleos).
    /// </summary>
    public async Task<FichaParaRequisitos> FichaParaRequisitosAsync(NpcSheet sheet)
    {
        var artefatos = await GetArtifactBonusInputsAsync(sheet.Id);
        var atributos = await db.NpcAttributes.Where(a => a.NpcSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);
        var skills = await db.NpcSkills.Where(s => s.NpcSheetId == sheet.Id).ToListAsync();
        var porId = await pericias.PorIdAsync();
        // Só Perícias ativas: um requisito sobre uma Perícia removida é ignorado pelo avaliador.
        var totaisDePericia = skills.Where(s => porId.TryGetValue(s.PericiaId, out var p) && !p.IsDeleted).ToDictionary(s => s.PericiaId, s =>
        {
            var modificador = SkillFormulas.Modificador(s.Gasto, HistoricoBonusCalculator.For(s.PericiaId, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));
            return s.AtributoEscolhido is { } atributo && atributos.TryGetValue(atributo, out var atributoTotal)
                ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, porId[s.PericiaId].Chave))
                : (int?)null;
        });

        var sub = await SubAtributosAsync(sheet);

        var graduacao = sheet.Vocacao is null ? 0 : GraduacaoCalculator.Compute(sheet.Vocacao.Value, sheet.EAPAtual, sheet.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: true, sheet.Nivel, sheet.Vocacao, sheet.SubVocacao, sheet.Linhagem, sheet.Variante,
            graduacao, sheet.PossuiCoracaoDeMana, sheet.Afinidade, sheet.Estrela, sheet.HistoricoId,
            atributos, CharacterSheetStats.SubAtributosPorEnum(sub), totaisDePericia);
    }

    /// <summary>
    /// Every NpcArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor). Moved here from
    /// NpcSheetsController; that controller keeps its own copy too since ModificadorDeDano/
    /// ToResponseAsync still need it independently of SubAttributes/FichaParaRequisitos.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.NpcArtifacts
            .Where(a => a.NpcSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    private async Task<int> GetAttributeTotalAsync(Guid sheetId, Atributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString()));
    }
}
