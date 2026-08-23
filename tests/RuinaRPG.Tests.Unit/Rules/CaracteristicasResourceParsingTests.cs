using FluentAssertions;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class CaracteristicasResourceParsingTests
{
    [Fact]
    public void RealCaracteristicasResource_parses_into_at_least_one_trait_per_polaridade()
    {
        var markdown = RulesDataProvider.ReadResource("Caracteristicas.md");
        var parsed = TraitSeedParser.Parse(markdown);

        parsed.Should().NotBeEmpty();
        parsed.Should().Contain(t => t.Polaridade == "Positiva");
        parsed.Should().Contain(t => t.Polaridade == "Negativa");
    }
}
