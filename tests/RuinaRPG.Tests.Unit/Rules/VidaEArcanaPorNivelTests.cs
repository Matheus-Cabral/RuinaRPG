using FluentAssertions;
using RuinaRPG.Domain.Rules.ReferenceData;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class VidaEArcanaPorNivelTests
{
    private static readonly IReadOnlyList<VocacaoProgressao> Vocacoes = new[]
    {
        new VocacaoProgressao("Campeão", 2, 20, 2),
        new VocacaoProgressao("Campeão", 1, 10, 1),
        new VocacaoProgressao("Mago", 1, 5, 9),
    };

    [Fact]
    public void Exact_level_uses_that_row()
        => VidaEArcanaPorNivel.Vocacao(Vocacoes, "Campeão", 1).Should().Be((10, 1));

    [Fact]
    public void Level_above_the_table_uses_the_last_row()
        => VidaEArcanaPorNivel.Vocacao(Vocacoes, "Campeão", 51).Should().Be((20, 2));

    [Fact]
    public void Unknown_vocacao_is_zero()
        => VidaEArcanaPorNivel.Vocacao(Vocacoes, "Nada", 3).Should().Be((0, 0));

    [Fact]
    public void Level_below_the_first_row_is_zero()
        => VidaEArcanaPorNivel.Vocacao(Vocacoes, "Mago", 0).Should().Be((0, 0));

    [Fact]
    public void Arquetipo_falls_back_the_same_way()
    {
        VidaEArcanaPorNivel.Arquetipo(new[] { new ArquetipoProgressao("Y", 1, 5, 6) }, "Y", 60).Should().Be((5, 6));
    }
}
