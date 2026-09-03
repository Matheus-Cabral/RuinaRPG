using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Recursos 1.b: "os bônus de PI por nível constam na Tabela de Níveis" — Pontos de Ignição Total
/// is the sum of every level's grant up to and including the current Nível, plus a manual bonus
/// the GM can add on top (CharacterSheet.PontosDeIgnicaoBonusManual — "o GM também pode conceder PI
/// para os jogadores acrescentarem neste contador").
/// </summary>
public static partial class PontosDeIgnicaoCalculator
{
    public static int ComputeTotal(int nivel, int bonusManual, IReadOnlyList<LevelBonus> niveis) =>
        niveis
            .Where(n => n.Nivel <= nivel)
            .Sum(n => PontosDeIgnicaoRegex().Matches(n.BonusText).Sum(m => int.Parse(m.Groups[1].Value)))
        + bonusManual;

    // IgnoreCase: Nível 11's own row spells it "pontos de Ignição", lowercase "p".
    [GeneratedRegex(@"\+(\d+)\s+Pontos?\s+de\s+Ignição\b", RegexOptions.IgnoreCase)]
    private static partial Regex PontosDeIgnicaoRegex();
}
