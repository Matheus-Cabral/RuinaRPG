using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class HistoricoBonusCalculatorTests
{
    [Fact]
    public void For_returns_6_when_the_Pericia_matches_PericiaMaisSeis()
    {
        HistoricoBonusCalculator.For(Pericia.Arcano, Pericia.Arcano, Pericia.Biblioteca).Should().Be(6);
    }

    [Fact]
    public void For_returns_3_when_the_Pericia_matches_PericiaMaisTres()
    {
        HistoricoBonusCalculator.For(Pericia.Biblioteca, Pericia.Arcano, Pericia.Biblioteca).Should().Be(3);
    }

    [Fact]
    public void For_returns_0_when_the_Pericia_matches_neither()
    {
        HistoricoBonusCalculator.For(Pericia.Fortitude, Pericia.Arcano, Pericia.Biblioteca).Should().Be(0);
    }

    [Fact]
    public void For_returns_0_when_there_is_no_Historico_chosen()
    {
        HistoricoBonusCalculator.For(Pericia.Arcano, null, null).Should().Be(0);
    }
}
