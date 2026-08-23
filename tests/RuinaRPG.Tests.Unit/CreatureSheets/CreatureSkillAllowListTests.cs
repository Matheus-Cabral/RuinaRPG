using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Tests.Unit.CreatureSheets;

public class CreatureSkillAllowListTests
{
    [Theory]
    [InlineData(Pericia.Acrobacia, true)]
    [InlineData(Pericia.Atletismo, true)]
    [InlineData(Pericia.Sobrevivencia, true)]
    [InlineData(Pericia.Alquimia, false)] // not in the R0005 list
    [InlineData(Pericia.Biblioteca, false)]
    [InlineData(Pericia.Linguistica, false)]
    public void IsAllowed_matches_the_R0005_subset(Pericia pericia, bool expected)
    {
        CreatureSkillAllowList.IsAllowed(pericia).Should().Be(expected);
    }

    [Fact]
    public void IsAllowed_permits_exactly_20_pericias()
    {
        var allowedCount = Enum.GetValues<Pericia>().Count(CreatureSkillAllowList.IsAllowed);

        allowedCount.Should().Be(20);
    }
}
