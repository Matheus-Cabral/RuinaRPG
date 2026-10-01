using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

public static class LevelUpNoticeCalculator
{
    public static IReadOnlyList<LevelBonus> PendingBonuses(int? lastDismissedLevel, int currentLevel, IReadOnlyList<LevelBonus> niveis)
    {
        var floor = lastDismissedLevel ?? 0;
        return niveis
            .Where(n => n.Nivel > floor && n.Nivel <= currentLevel)
            .OrderBy(n => n.Nivel)
            .ToList();
    }

    /// <summary>
    /// BonusText packs a level's several bonuses into one string separated by literal
    /// "&lt;br&gt;" (ProgressaoDeNivel.ComoLevelBonus: the numeric columns first, then the
    /// free-text lines). This flattens it for display, so a client renders each bonus on its own
    /// line instead of a raw "&lt;br&gt;" showing up as literal text.
    /// </summary>
    public static List<string> FlattenBonusLines(IReadOnlyList<LevelBonus> bonuses) => bonuses
        .SelectMany(b => b.BonusText.Split("<br>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        .ToList();
}
