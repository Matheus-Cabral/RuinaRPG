using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Características 5.d: budget for the net Custo spent across both Positivas and Negativas lists
/// (Negativas' Custo is already stored negative — TraitSeedParser flips its sign — so summing both
/// lists' totals together already nets out the points a Negativa characteristic gives back).
/// "Espaço de Característica" in Tabela de Níveis grants the points available to spend.
/// </summary>
public static partial class TraitPointBudgetCalculator
{
    public static int Compute(int nivel, IReadOnlyList<LevelBonus> niveis) =>
        niveis
            .Where(n => n.Nivel <= nivel)
            .Sum(n => EspacoDeCaracteristicaRegex().Matches(n.BonusText).Sum(m => int.Parse(m.Groups[1].Value)));

    // Caracter[ií]stica + IgnoreCase: defensive, matching the same spelling/casing inconsistencies
    // already found in this table's "Atributo"/"Perícia" bonus text (this one happens to be
    // consistently spelled "Caracteristica", unaccented, across every row today).
    [GeneratedRegex(@"\+(\d+)\s+Espaços?\s+de\s+Caracter[ií]stica\b", RegexOptions.IgnoreCase)]
    private static partial Regex EspacoDeCaracteristicaRegex();
}
