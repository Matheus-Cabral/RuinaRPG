using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class ArcaEvolucaoRulesTests
{
    private sealed record Ev(string Id, int Nivel, DateTimeOffset CriadaEm);

    private static readonly DateTimeOffset T0 = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Desbloqueadas_keeps_only_levels_up_to_the_sheet_level_ordered_by_level_then_creation()
    {
        var evolucoes = new[]
        {
            new Ev("c", 10, T0.AddMinutes(1)),
            new Ev("a", 5, T0.AddMinutes(2)),
            new Ev("b", 5, T0.AddMinutes(1)),
            new Ev("d", 11, T0),
        };

        var result = ArcaEvolucaoRules.Desbloqueadas(evolucoes, e => e.Nivel, e => e.CriadaEm, nivelDaFicha: 10);

        result.Select(e => e.Id).Should().Equal("b", "a", "c");
    }

    [Fact]
    public void Desbloqueadas_of_an_empty_list_is_empty()
    {
        ArcaEvolucaoRules.Desbloqueadas(Array.Empty<Ev>(), e => e.Nivel, e => e.CriadaEm, 50).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void NivelValido_accepts_1_to_the_last_level_of_the_table(int nivel, bool esperado)
    {
        ArcaEvolucaoRules.NivelValido(nivel, ultimoNivel: 50).Should().Be(esperado);
    }

    [Fact]
    public void NivelValido_follows_the_last_level_it_is_given()
    {
        ArcaEvolucaoRules.NivelValido(51, ultimoNivel: 60).Should().BeTrue();
        ArcaEvolucaoRules.NivelValido(31, ultimoNivel: 30).Should().BeFalse();
    }
}
