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

    [Fact]
    public void FlattenBonusLines_splits_a_multi_bonus_cell_on_br_into_separate_trimmed_lines()
    {
        var bonuses = new List<LevelBonus> { new(1, "+9 Pontos de Atributo  <br>+Status de Vida Aprimorado  <br>+1 Espaço de Maestria") };

        var result = LevelUpNoticeCalculator.FlattenBonusLines(bonuses);

        result.Should().Equal("+9 Pontos de Atributo", "+Status de Vida Aprimorado", "+1 Espaço de Maestria");
    }

    [Fact]
    public void FlattenBonusLines_leaves_a_single_bonus_cell_with_no_br_untouched()
    {
        var bonuses = new List<LevelBonus> { new(3, "+Status de Vocação de Vida/Foco") };

        var result = LevelUpNoticeCalculator.FlattenBonusLines(bonuses);

        result.Should().Equal("+Status de Vocação de Vida/Foco");
    }

    [Fact]
    public void FlattenBonusLines_concatenates_lines_across_multiple_levels_in_order()
    {
        var bonuses = new List<LevelBonus>
        {
            new(1, "+9 Pontos de Atributo  <br>+4 Pontos de Perícia"),
            new(2, "+4 Pontos de Ignição")
        };

        var result = LevelUpNoticeCalculator.FlattenBonusLines(bonuses);

        result.Should().Equal("+9 Pontos de Atributo", "+4 Pontos de Perícia", "+4 Pontos de Ignição");
    }
}
