using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class LevelUpNoticeCalculatorTests
{
    private static readonly IReadOnlyList<LevelBonus> Niveis =
    [
        new(1, "+9 Pontos de Atributo"),
        new(2, "+4 Pontos de Ignição"),
        new(3, "+Status de Vocação de Vida/Foco")
    ];

    [Fact]
    public void PendingBonuses_with_no_prior_dismissal_returns_every_level_up_to_current()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: null, currentLevel: 2, Niveis);

        result.Select(b => b.Nivel).Should().Equal(1, 2);
    }

    [Fact]
    public void PendingBonuses_excludes_levels_already_dismissed()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: 1, currentLevel: 2, Niveis);

        result.Select(b => b.Nivel).Should().Equal(2);
    }

    [Fact]
    public void PendingBonuses_lists_every_level_gained_at_once_in_ascending_order()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: 1, currentLevel: 3, Niveis);

        result.Select(b => b.Nivel).Should().Equal(2, 3);
    }

    [Fact]
    public void PendingBonuses_is_empty_when_fully_caught_up()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: 3, currentLevel: 3, Niveis);

        result.Should().BeEmpty();
    }
}
