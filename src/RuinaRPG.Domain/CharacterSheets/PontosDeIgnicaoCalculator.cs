using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Recursos 1.b: "os bônus de PI por nível constam na Tabela de Níveis" — Pontos de Ignição Total
/// is the sum of every level's grant up to and including the current Nível, plus a manual bonus
/// the GM can add on top (CharacterSheet.PontosDeIgnicaoBonusManual — "o GM também pode conceder PI
/// para os jogadores acrescentarem neste contador").
/// </summary>
public static class PontosDeIgnicaoCalculator
{
    public static int ComputeTotal(int nivel, int bonusManual, ProgressaoDeNivel tabela) =>
        tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivel) + bonusManual;
}
