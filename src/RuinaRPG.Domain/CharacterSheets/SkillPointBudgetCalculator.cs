using System.Text.RegularExpressions;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Perícias 2.d: "ver Tabela de Níveis para os Pontos de Perícia concedidos por nível" — the
/// level-derived half of the budget only. A Perícia can also gain points from a Critical Hit
/// mid-session (§2 of Ruína RPG - Sistema Básico), a source this table knows nothing about;
/// CharacterSheet.PontosDePericiaBonusCritico tracks that separately and is subtracted from Gasto
/// Total by the caller (CharacterSkillsController.Budget) so it isn't flagged as over budget.
/// Still displayed for reference only, not enforced server-side like AttributePointBudgetCalculator's.
/// </summary>
public static partial class SkillPointBudgetCalculator
{
    public static int Compute(int nivel, IReadOnlyList<LevelBonus> niveis) =>
        niveis
            .Where(n => n.Nivel <= nivel)
            .Sum(n => PontosDePericiaRegex().Matches(n.BonusText).Sum(m => int.Parse(m.Groups[1].Value)));

    // Per[ií]cia + IgnoreCase: the source table spells this inconsistently — some rows use
    // "Perícia", others the unaccented "Pericia" (e.g. Nível 12, 22, 32, 42).
    [GeneratedRegex(@"\+(\d+)\s+Pontos?\s+de\s+Per[ií]cia\b", RegexOptions.IgnoreCase)]
    private static partial Regex PontosDePericiaRegex();
}
