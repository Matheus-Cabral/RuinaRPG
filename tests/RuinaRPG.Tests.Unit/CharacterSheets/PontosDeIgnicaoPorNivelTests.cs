using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using static RuinaRPG.Tests.Unit.Rules.TabelaDeNiveisDeTeste;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PontosDeIgnicaoPorNivelTests
{
    private static readonly ProgressaoDeNivel Niveis = Criar(
        Nivel(1, (ChavesDeNivel.PontosDeIgnicao, 10)),
        Nivel(2, (ChavesDeNivel.PontosDeIgnicao, 4)),
        Nivel(3),
        Nivel(4, (ChavesDeNivel.PontosDeIgnicao, 8)));

    [Fact]
    public void Delta_is_zero_when_the_level_does_not_change() =>
        PontosDeIgnicaoPorNivel.Delta(2, 2, Niveis).Should().Be(0);

    [Fact]
    public void Delta_going_up_one_level_is_that_levels_grant() =>
        PontosDeIgnicaoPorNivel.Delta(1, 2, Niveis).Should().Be(4);

    [Fact]
    public void Delta_going_up_several_levels_sums_them_and_an_empty_cell_counts_zero() =>
        PontosDeIgnicaoPorNivel.Delta(1, 4, Niveis).Should().Be(12); // 4 + 0 + 8

    [Fact]
    public void Delta_going_down_is_the_negative_of_going_up() =>
        PontosDeIgnicaoPorNivel.Delta(4, 1, Niveis).Should().Be(-12);

    [Fact]
    public void Aplicar_adds_the_delta() =>
        PontosDeIgnicaoPorNivel.Aplicar(atual: 5, delta: 4).Should().Be(9);

    [Fact]
    public void Aplicar_never_goes_below_zero() =>
        PontosDeIgnicaoPorNivel.Aplicar(atual: 5, delta: -12).Should().Be(0);

    [Fact]
    public void Inicial_is_everything_granted_up_to_the_starting_level() =>
        PontosDeIgnicaoPorNivel.Inicial(2, Niveis).Should().Be(14);
}
