using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Estatísticas da Ficha de Personagem sobre o núcleo comum (FichaStats): /sub-attributes, as listas de Atributos e
/// Perícias, os máximos de recurso e o retrato dos requisitos de Passiva compartilham uma só conta ao vivo.
/// </summary>
public class CharacterSheetStats(RuinaRpgDbContext db, IRulesDataProvider rules, ITabelaDeNiveis tabelaDeNiveis, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades, TabelaDeAfinidadesProvider tabelaDeAfinidades)
    : FichaStats<CharacterSheet, Atributo, CharacterAttribute, CharacterSkill, CharacterShield, CharacterArmorSlot, CharacterArtifact>(db, rules, pericias, penalidades)
{
    protected override Guid? HistoricoId(CharacterSheet sheet) => sheet.HistoricoId;

    protected override async Task<Carga> CargaAsync(CharacterSheet sheet, int forca, int vigor)
    {
        // Bônus de carga da Campanha (Requisitos - Campanha): só a Ficha de Personagem pertence a uma.
        var bonusDeCarga = await Db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.BonusDeCarga).FirstOrDefaultAsync();
        return await CargaDoInventarioAsync<CharacterWeapon, CharacterInventoryItem>(sheet.Id, forca, vigor, bonusDeCarga);
    }

    protected override Task<(int? Eficiencia, int? Dano)> ElementalAsync(CharacterSheet sheet, EfeitosDeCondicao efeitos) =>
        ElementalDasAfinidadesAsync<CharacterAffinity>(sheet.Id, sheet.Afinidade, efeitos, tabelaDeAfinidades);

    protected override Task<List<(Guid Id, Guid ItemId, bool IsEquipped)>> ArmasDoCatalogoAsync(Guid sheetId) => ArmasDoCatalogoAsync<CharacterWeapon>(sheetId);

    /// <summary>Mesmo Grau/Círculo que CharacterSheetsController.ToResponseAsync mostra em 1.b.</summary>
    protected override async Task<FichaParaRequisitos> RetratoAsync(CharacterSheet sheet, Dictionary<Atributo, int> atributos, Dictionary<SubAtributo, int> subAtributos, Dictionary<int, int?> pericias)
    {
        var tabela = await tabelaDeNiveis.ObterAsync();
        var eapAtual = EapCalculator.Compute(sheet.Nivel, sheet.NucleosRankF, sheet.NucleosRankE, sheet.NucleosRankD, sheet.NucleosRankC, sheet.NucleosRankB, sheet.NucleosRankA, sheet.NucleosRankS, tabela.ComoEapPorNivel());
        var graduacao = sheet.Vocacao is null ? 0 : GraduacaoCalculator.Compute(sheet.Vocacao.Value, eapAtual, sheet.PossuiCoracaoDeMana, Rules.CirculoGrauPorEap);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: true, sheet.Nivel, sheet.Vocacao, sheet.SubVocacao, sheet.Linhagem, sheet.Variante,
            graduacao, sheet.PossuiCoracaoDeMana, sheet.Afinidade, sheet.Estrela, sheet.HistoricoId,
            atributos, subAtributos, pericias);
    }

    protected override int OrdemDeExibicao(Atributo atributo) => AttributeDisplayOrder.Rank(atributo);

    /// <summary>
    /// Os quatro totais de recurso exibidos, com as Condições de <paramref name="efeitos"/>. Quem chama escolhe as
    /// Condições: a ficha usa as ativas (CondicoesAtivasDaFicha), o corte da ativação (CondicoesDoEncontro) as que
    /// estarão ativas depois da ação, com a Vocação e o Nível que a ficha terá.
    /// </summary>
    public Task<(int Vitalidade, int Foco, int Adrenalina, int Estresse)> MaximosAsync(CharacterSheet sheet, EfeitosDeCondicao efeitos, Vocacao? vocacao, int nivel) =>
        MaximosComStatusAsync(sheet, efeitos, StatusDaVocacao(vocacao, nivel));

    public Task<(int Vitalidade, int Foco, int Adrenalina, int Estresse)> MaximosAsync(CharacterSheet sheet, EfeitosDeCondicao efeitos) =>
        MaximosAsync(sheet, efeitos, sheet.Vocacao, sheet.Nivel);
}
