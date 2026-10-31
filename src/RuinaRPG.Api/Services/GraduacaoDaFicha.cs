using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Services;

/// <summary>
/// EAP Atual e Círculo/Grau de uma ficha de Personagem ou NPC (1.b): a EAP vem do Nível + Âmbares Absorvidos (Tabela de
/// Níveis da Auditoria) e a Graduação dela, da Vocação e do coração de mana. O único lugar dessa conta: resposta da
/// ficha, retrato dos requisitos de Passiva e máximo das Afinidades.
/// </summary>
public class GraduacaoDaFicha(ITabelaDeNiveis tabelaDeNiveis, IRulesDataProvider rules)
{
    public async Task<int> EapAtualAsync(IFichaComAfinidades f)
    {
        var tabela = await tabelaDeNiveis.ObterAsync();
        return EapCalculator.Compute(f.Nivel, f.NucleosRankF, f.NucleosRankE, f.NucleosRankD, f.NucleosRankC, f.NucleosRankB, f.NucleosRankA, f.NucleosRankS, tabela.ComoEapPorNivel());
    }

    /// <summary>A EAP Atual e o Círculo (Grau, nas marciais); sem Vocação a Graduação é 0.</summary>
    public async Task<(int EapAtual, int Graduacao)> CalcularAsync(IFichaComAfinidades f)
    {
        var eapAtual = await EapAtualAsync(f);
        var graduacao = f.Vocacao is { } vocacao ? GraduacaoCalculator.Compute(vocacao, eapAtual, f.PossuiCoracaoDeMana, rules.CirculoGrauPorEap) : 0;
        return (eapAtual, graduacao);
    }
}
