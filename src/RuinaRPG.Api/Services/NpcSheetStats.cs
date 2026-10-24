using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Estatísticas da Ficha de NPC sobre o núcleo comum (FichaStats) — as mesmas contas do Personagem, sem bônus de
/// carga de campanha (o NPC não pertence a uma).
/// </summary>
public class NpcSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, EquipmentPenaltyService penalidades, TabelaDeAfinidadesProvider tabelaDeAfinidades)
    : FichaStats<NpcSheet, Atributo, NpcAttribute, NpcSkill, NpcShield, NpcArmorSlot, NpcArtifact>(db, rules, pericias, penalidades)
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

    protected override Guid? HistoricoId(NpcSheet sheet) => sheet.HistoricoId;

    protected override Task<Carga> CargaAsync(NpcSheet sheet, int forca, int vigor) =>
        CargaDoInventarioAsync<NpcWeapon, NpcInventoryItem>(sheet.Id, forca, vigor, bonusDeCarga: 0m);

    protected override Task<(int? Eficiencia, int? Dano)> ElementalAsync(NpcSheet sheet, EfeitosDeCondicao efeitos) =>
        ElementalDasAfinidadesAsync<NpcAffinity>(sheet.Id, sheet.Afinidade, efeitos, tabelaDeAfinidades);

    protected override Task<List<(Guid Id, Guid ItemId, bool IsEquipped)>> ArmasDoCatalogoAsync(Guid sheetId) => ArmasDoCatalogoAsync<NpcWeapon>(sheetId);

    /// <summary>Graduação usa o VIS calculado (EapAtualAsync) — o mesmo de NpcSheetsController.ToResponseAsync e da Ficha de Personagem.</summary>
    protected override async Task<FichaParaRequisitos> RetratoAsync(NpcSheet sheet, Dictionary<Atributo, int> atributos, Dictionary<SubAtributo, int> subAtributos, Dictionary<int, int?> pericias)
    {
        var graduacao = sheet.Vocacao is null ? 0 : GraduacaoCalculator.Compute(sheet.Vocacao.Value, await EapAtualAsync(sheet), sheet.PossuiCoracaoDeMana, Rules.CirculoGrauPorEap);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: true, sheet.Nivel, sheet.Vocacao, sheet.SubVocacao, sheet.Linhagem, sheet.Variante,
            graduacao, sheet.PossuiCoracaoDeMana, sheet.Afinidade, sheet.Estrela, sheet.HistoricoId,
            atributos, subAtributos, pericias);
    }

    protected override int OrdemDeExibicao(Atributo atributo) => AttributeDisplayOrder.Rank(atributo);

    /// <summary>Os quatro totais de recurso exibidos, com as Condições de <paramref name="efeitos"/> — mesma conta de CharacterSheetStats.MaximosAsync.</summary>
    public Task<(int Vitalidade, int Foco, int Adrenalina, int Estresse)> MaximosAsync(NpcSheet sheet, EfeitosDeCondicao efeitos) =>
        MaximosComStatusAsync(sheet, efeitos, StatusDaVocacao(sheet.Vocacao, sheet.Nivel));
}
