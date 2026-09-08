using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Características 5.d: the budget applies independently to each list — Positivas total may not
/// exceed it, and Negativas total (magnitude; Custo is stored negative — TraitSeedParser flips its
/// sign) may not exceed it either. A character that spends the full budget on both sides nets to 0.
/// Every character gets a flat 5-point creation grant that never appears as "+X Espaço de
/// Característica" text in Tabela de Níveis (unlike Atributo/Perícia, whose level-1 row IS their
/// creation grant — Característica's table only starts granting bonus points at level 4), plus
/// whatever "+X Espaço de Característica" bonuses the table grants at every level up to the
/// character's own.
/// </summary>
public static partial class TraitPointBudgetCalculator
{
    private const int CriacaoBase = 5;

    public static int Compute(int nivel, IReadOnlyList<LevelBonus> niveis) =>
        CriacaoBase + niveis
            .Where(n => n.Nivel <= nivel)
            .Sum(n => EspacoDeCaracteristicaRegex().Matches(n.BonusText).Sum(m => int.Parse(m.Groups[1].Value)));

    // Caracter[ií]stica + IgnoreCase: defensive, matching the same spelling/casing inconsistencies
    // already found in this table's "Atributo"/"Perícia" bonus text (this one happens to be
    // consistently spelled "Caracteristica", unaccented, across every row today).
    [GeneratedRegex(@"\+(\d+)\s+Espaços?\s+de\s+Caracter[ií]stica\b", RegexOptions.IgnoreCase)]
    private static partial Regex EspacoDeCaracteristicaRegex();
}
