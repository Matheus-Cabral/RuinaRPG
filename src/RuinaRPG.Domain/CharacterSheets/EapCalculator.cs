using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// EAP atual (Ficha de Personagem 1.b): "Segue a tabela [Tabelas de XP, Atributos, Características
/// e EAP] e é somado pelo resultado de Âmbares Absorvidos" — the level's base EAP (that table's
/// "Valores absolutos" column) plus the absorbed Âmbares, one per Rank (F=5, E=15, D=40, C=120,
/// B=350, A=1000, S=3000). No longer a directly-editable field — same treatment already given to
/// Círculo/Grau, which became a pure function of EAP instead of a stored value.
/// </summary>
public static class EapCalculator
{
    private const int RankF = 5, RankE = 15, RankD = 40, RankC = 120, RankB = 350, RankA = 1000, RankS = 3000;

    public static int Compute(int nivel, int nucleosRankF, int nucleosRankE, int nucleosRankD, int nucleosRankC,
        int nucleosRankB, int nucleosRankA, int nucleosRankS, IReadOnlyList<EapPorNivel> tabela)
    {
        var baseDoNivel = tabela.FirstOrDefault(t => t.Nivel == nivel)?.ValorAbsoluto ?? 0;
        var deAmbares = nucleosRankF * RankF + nucleosRankE * RankE + nucleosRankD * RankD + nucleosRankC * RankC
            + nucleosRankB * RankB + nucleosRankA * RankA + nucleosRankS * RankS;
        return baseDoNivel + deAmbares;
    }
}
