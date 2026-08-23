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
}
