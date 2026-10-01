using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Sub-Atributos and the FichaParaRequisitos snapshot for a Ficha de Personagem — moved out of
/// CharacterSheetsController so both /sub-attributes (read-only, unchanged response) and the
/// Passiva requisitos check (Task 5) share one live computation instead of two. Read-only,
/// everything derived live — nothing here is persisted.
/// </summary>
public class CharacterSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias)
{
    /// <summary>
    /// "Bruto [Perícia]" terms (Prontidão, Reflexos, Fortitude) mean that Perícia's Modificador
    /// alone (Sistema Básico §2 / SkillFormulas.Modificador), sourced from the sheet's own
    /// CharacterSkill rows below.
    /// </summary>
    public async Task<SubAttributesResponse> SubAtributosAsync(CharacterSheet sheet)
    {
        var id = sheet.Id;
        var artefatos = await GetArtifactBonusInputsAsync(id);

        var agilidade = await GetAttributeTotalAsync(id, Atributo.Agilidade, artefatos);
        var vigor = await GetAttributeTotalAsync(id, Atributo.Vigor, artefatos);
        var forca = await GetAttributeTotalAsync(id, Atributo.Forca, artefatos);

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);

        var brutoSkills = await db.CharacterSkills
            .Where(s => s.CharacterSheetId == id && (s.PericiaId == PericiasDeSistema.Prontidao || s.PericiaId == PericiasDeSistema.Reflexos || s.PericiaId == PericiasDeSistema.Fortitude))
            .ToListAsync();
        int BrutoOf(int periciaId) => SkillFormulas.Modificador(
            brutoSkills.Single(s => s.PericiaId == periciaId).Gasto,
            HistoricoBonusCalculator.For(periciaId, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));

        var brutoProntidao = BrutoOf(PericiasDeSistema.Prontidao);
        var brutoReflexos = BrutoOf(PericiasDeSistema.Reflexos);
        var brutoFortitude = BrutoOf(PericiasDeSistema.Fortitude);

        var weapons = await db.CharacterWeapons.Where(w => w.CharacterSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var shields = await db.CharacterShields.Where(s => s.CharacterSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => new { s.IsEquipped, i.Peso }).ToListAsync();
        var inventoryItems = await db.CharacterInventoryItems.Where(i => i.CharacterSheetId == id)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>(), i => i.ItemId, g => g.Id, (i, g) => new { i.Qtd, g.Peso, g.CapacidadeExtra })
            .ToListAsync();

        // Peso Total 2.b (corrected — see docs/superpowers/specs/2026-09-08-inventory-weight-and-
        // capacity-design.md): Inventário (5.a) + Armas/Escudos DESequipados. Armaduras never count
        // — a CharacterArmorSlot with an ItemId is inherently worn (no unequipped state exists for
        // armor in this schema). A Capacidade Extra ("mochila") item doesn't add its own Peso here —
        // it raises pesoMaximo instead.
        var pesoAtual = inventoryItems.Where(i => CarryWeightCalculator.CountsTowardPesoAtual(i.CapacidadeExtra)).Sum(i => i.Peso * i.Qtd)
            + weapons.Where(w => !w.IsEquipped).Sum(w => w.Peso)
            + shields.Where(s => !s.IsEquipped).Sum(s => s.Peso);
        var capacidadeExtraTotal = inventoryItems.Sum(i => (i.CapacidadeExtra ?? 0m) * i.Qtd);
        var pesoMaximo = CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal);

        var equippedShield = await db.CharacterShields
            .Where(s => s.CharacterSheetId == id && s.IsEquipped)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => i.BonusDefesa)
            .FirstOrDefaultAsync();
        var coberturaBonus = sheet.Cobertura switch { Cobertura.Parcial => 5, Cobertura.Completa => 10, _ => 0 };

        // Every CharacterArmorSlot with an ItemId is inherently worn (no separate IsEquipped
        // flag, unlike weapons/shields) — so, unlike Peso Total Carregado, this is already
        // scoped to equipped armor only.
        var armorRfRm = await db.CharacterArmorSlots
            .Where(a => a.CharacterSheetId == id && a.ItemId != null)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Armadura>(), a => a.ItemId!.Value, i => i.Id, (a, i) => new { i.RF, i.RM })
            .ToListAsync();
        var armaduraRf = armorRfRm.Sum(a => a.RF ?? 0);
        var armaduraRm = armorRfRm.Sum(a => a.RM ?? 0);

        var linhasDeAfinidade = await db.CharacterAffinities.Where(a => a.CharacterSheetId == id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor))
            .ToListAsync();
        var valorDaAfinidade = SubAttributeFormulas.ValorDaAfinidadeCorrespondente(sheet.Afinidade, linhasDeAfinidade);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Unlike Bruto/Artefatos above, this is a real, still-open gap.
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
    /// What the sheet has to compare against a Passiva's Requisitos (Task 1's FichaParaRequisitos).
    /// Atributos/Perícias/Graduação here must equal what CharacterAttributesController/
    /// CharacterSkillsController.List/CharacterSheetsController's own response already show —
    /// same source data, same formulas, just gathered in one place instead of three.
    /// </summary>
    public async Task<FichaParaRequisitos> FichaParaRequisitosAsync(CharacterSheet sheet)
    {
        var artefatos = await GetArtifactBonusInputsAsync(sheet.Id);
        var atributos = await db.CharacterAttributes.Where(a => a.CharacterSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        // Mesmo Total da 2.d que CharacterSkillsController.List mostra.
        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);
        var skills = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
        var porId = await pericias.PorIdAsync();
        // Toda Perícia ativa entra (linha ausente = Gasto 0 e Atributo sugerido, como na lista da ficha);
        // removidas ficam de fora e o avaliador ignora requisitos sobre elas.
        var totaisDePericia = porId.Values.Where(p => !p.IsDeleted).ToDictionary(p => p.Id, p =>
        {
            skills.TryGetValue(p.Id, out var s);
            var modificador = SkillFormulas.Modificador(s?.Gasto ?? 0, HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));
            var atributo = s?.AtributoEscolhido ?? p.AtributoSugerido;
            return atributo is { } chosen && atributos.TryGetValue(chosen, out var atributoTotal)
                ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave))
                : (int?)null;
        });

        var sub = await SubAtributosAsync(sheet);

        // Mesmo Grau/Círculo que CharacterSheetsController.ToResponseAsync mostra em 1.b.
        var eapAtual = EapCalculator.Compute(sheet.Nivel, sheet.NucleosRankF, sheet.NucleosRankE, sheet.NucleosRankD, sheet.NucleosRankC, sheet.NucleosRankB, sheet.NucleosRankA, sheet.NucleosRankS, rules.EapPorNivel);
        var graduacao = sheet.Vocacao is null ? 0 : GraduacaoCalculator.Compute(sheet.Vocacao.Value, eapAtual, sheet.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: true, sheet.Nivel, sheet.Vocacao, sheet.SubVocacao, sheet.Linhagem, sheet.Variante,
            graduacao, sheet.PossuiCoracaoDeMana, sheet.Afinidade, sheet.Estrela, sheet.HistoricoId,
            atributos, SubAtributosPorEnum(sub), totaisDePericia);
    }

    /// <summary>Shared by NpcSheetStats/CreatureSheetStats — same SubAttributesResponse shape, keyed by SubAtributo instead.</summary>
    internal static Dictionary<SubAtributo, int> SubAtributosPorEnum(SubAttributesResponse sub) => new()
    {
        [SubAtributo.Iniciativa] = sub.Iniciativa,
        [SubAtributo.Movimentacao] = sub.Movimentacao,
        [SubAtributo.EsquivaNatural] = sub.EsquivaNatural,
        [SubAtributo.DefesaNatural] = sub.DefesaNatural,
        [SubAtributo.ReducaoFisica] = sub.ReducaoFisica,
        [SubAtributo.ReducaoMagica] = sub.ReducaoMagica,
    };

    /// <summary>
    /// Every CharacterArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses 5.b
    /// has no separate equip/unequip toggle for Artefatos, so being on the sheet is being "equipped".
    /// Moved here from CharacterSheetsController; that controller keeps its own copy too since
    /// ComputeResourceMaximumsAsync still needs it independently of SubAttributes/FichaParaRequisitos.
    /// </summary>
    private async Task<List<ArtifactBonusInput>> GetArtifactBonusInputsAsync(Guid sheetId) =>
        await db.CharacterArtifacts
            .Where(a => a.CharacterSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    /// <summary>Single-row lookup + AttributeTotalCalculator.Total. See GetArtifactBonusInputsAsync's remark on the controller keeping its own copy.</summary>
    private async Task<int> GetAttributeTotalAsync(Guid sheetId, Atributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);
        var artefatoBonus = ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString());
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: artefatoBonus);
    }
}
