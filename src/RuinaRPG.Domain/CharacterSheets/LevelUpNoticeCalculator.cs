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
    /// Tabela de Níveis.md packs a level's several bonuses into one markdown-table cell separated
    /// by literal "&lt;br&gt;" (a level's real bonus count doesn't fit one line). BonusText itself
    /// stays a single raw string — the point-budget calculators regex-scan the whole cell — this
    /// only flattens it for display, so a client renders each bonus on its own line instead of a
    /// raw "&lt;br&gt;" showing up as literal text.
    /// </summary>
    public static List<string> FlattenBonusLines(IReadOnlyList<LevelBonus> bonuses) => bonuses
        .SelectMany(b => b.BonusText.Split("<br>", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        .ToList();
}
