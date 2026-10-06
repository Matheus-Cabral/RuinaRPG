using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Sub-Atributos and the FichaParaRequisitos snapshot for a Ficha de NPC — mirrors
/// CharacterSheetStats table-for-table, scoped to NpcAttributes/NpcSkills/NpcArtifacts. Moved out
/// of NpcSheetsController so both /sub-attributes (unchanged response) and the Passiva requisitos
/// check (Task 5) share one live computation. Read-only, everything derived live.
/// </summary>
public class NpcSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, EquipmentPenaltyService penalidades)
{
    /// <summary>
    /// VIS/EAP Atual do NPC: base do Nível + Âmbares Absorvidos, exatamente como na Ficha de Personagem.
    /// Único ponto de cálculo (resposta, Graduação, requisitos de Passiva); NpcSheet.EAPAtual é vestigial.
    /// </summary>
    public async Task<int> EapAtualAsync(NpcSheet sheet)
    {
        var tabela = await tabelaDeNiveis.ObterAsync();
        return EapCalculator.Compute(sheet.Nivel, sheet.NucleosRankF, sheet.NucleosRankE, sheet.NucleosRankD, sheet.NucleosRankC, sheet.NucleosRankB, sheet.NucleosRankA, sheet.NucleosRankS, tabela.ComoEapPorNivel());
    }

    public async Task<SubAttributesResponse> SubAtributosAsync(NpcSheet sheet) =>
        await SubAtributosAsync(sheet, await ModificadoresAsync(sheet));

    private async Task<SubAttributesResponse> SubAtributosAsync(NpcSheet sheet, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var id = sheet.Id;
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
        var pesoMaximo = CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal, bonusDeCarga: 0m);

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
            // penalidadeArmadura fica 0: penalidades de equipamento chegam como modificadores negativos de Sub-Atributo (ver EquipmentPenaltyService), não por este termo.
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
    /// Graduação usa o VIS calculado (EapAtualAsync) — o mesmo de
    /// NpcSheetsController.ToResponseAsync e da Ficha de Personagem.
    /// </summary>
    public async Task<FichaParaRequisitos> FichaParaRequisitosAsync(NpcSheet sheet)
    {
        // Sempre SEM penalidades de equipamento: é contra este retrato que os Requisitos são avaliados.
        var artefatos = await ArtefatosAsync(sheet.Id);
        var atributos = await db.NpcAttributes.Where(a => a.NpcSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);
        var skills = await db.NpcSkills.Where(s => s.NpcSheetId == sheet.Id).ToDictionaryAsync(s => s.PericiaId);
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

        var sub = await SubAtributosAsync(sheet, artefatos);

        var graduacao = sheet.Vocacao is null ? 0 : GraduacaoCalculator.Compute(sheet.Vocacao.Value, await EapAtualAsync(sheet), sheet.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: true, sheet.Nivel, sheet.Vocacao, sheet.SubVocacao, sheet.Linhagem, sheet.Variante,
            graduacao, sheet.PossuiCoracaoDeMana, sheet.Afinidade, sheet.Estrela, sheet.HistoricoId,
            atributos, CharacterSheetStats.SubAtributosPorEnum(sub), totaisDePericia);
    }

    /// <summary>
    /// Every NpcArtifact on the sheet, projected down to (TipoDeAlvo, Alvo, Valor) — Posses 5.b has
    /// no equip/unequip toggle for Artefatos, so simply being on the sheet counts as equipped.
    /// Só Artefatos: o que as telas exibem soma também as penalidades (<see cref="ModificadoresAsync"/>).
    /// </summary>
    public async Task<List<ArtifactBonusInput>> ArtefatosAsync(Guid sheetId) =>
        await db.NpcArtifacts
            .Where(a => a.NpcSheetId == sheetId)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.Artefato>(), a => a.ArtifactItemId, i => i.Id, (a, i) => i)
            .Where(i => i.TipoDeAlvo != null)
            .Select(i => new ArtifactBonusInput(i.TipoDeAlvo!.Value, i.Alvo, i.Valor ?? 0))
            .ToListAsync();

    /// <summary>Os equipamentos da ficha e se estão em uso (Requisitos - Catálogo de Itens, Requisitos/Penalidade).</summary>
    private async Task<List<EquipamentoDaFicha>> EquipamentosAsync(Guid sheetId)
    {
        var armas = await db.NpcWeapons.Where(w => w.NpcSheetId == sheetId).Select(w => new EquipamentoDaFicha(w.ItemId, w.IsEquipped)).ToListAsync();
        var escudos = await db.NpcShields.Where(s => s.NpcSheetId == sheetId).Select(s => new EquipamentoDaFicha(s.ItemId, s.IsEquipped)).ToListAsync();
        var armaduras = await db.NpcArmorSlots.Where(a => a.NpcSheetId == sheetId && a.ItemId != null).Select(a => new EquipamentoDaFicha(a.ItemId!.Value, true)).ToListAsync();
        var artefatos = await db.NpcArtifacts.Where(a => a.NpcSheetId == sheetId).Select(a => new EquipamentoDaFicha(a.ArtifactItemId, true)).ToListAsync();
        return [.. armas, .. escudos, .. armaduras, .. artefatos];
    }

    public async Task<List<PenalidadeAtiva>> PenalidadesAtivasAsync(NpcSheet sheet) =>
        await penalidades.AtivasAsync(await EquipamentosAsync(sheet.Id), () => FichaParaRequisitosAsync(sheet));

    /// <summary>Artefatos + penalidades de equipamento ativas — o que as fórmulas exibidas somam no termo Artefatos.</summary>
    public async Task<List<ArtifactBonusInput>> ModificadoresAsync(NpcSheet sheet)
    {
        var modificadores = await ArtefatosAsync(sheet.Id);
        modificadores.AddRange(await penalidades.ComoModificadoresAsync(await PenalidadesAtivasAsync(sheet)));
        return modificadores;
    }

    private async Task<int> GetAttributeTotalAsync(Guid sheetId, Atributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos)
    {
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString()));
    }
}
