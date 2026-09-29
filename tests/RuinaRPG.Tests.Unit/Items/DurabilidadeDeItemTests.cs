using FluentAssertions;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class DurabilidadeDeItemTests
{
    private static readonly Dictionary<RankDeItem, DurabilidadeDeRank> Tabela = new()
    {
        [RankDeItem.F] = new(RankDeItem.F, 20, false),
        [RankDeItem.S] = new(RankDeItem.S, null, true),
    };

    [Fact]
    public void No_rank_means_no_durability() =>
        DurabilidadeDeItem.Resolver(null, Tabela).Should().Be(((int?)null, false));

    [Fact]
    public void A_numeric_rank_resolves_its_value() =>
        DurabilidadeDeItem.Resolver(RankDeItem.F, Tabela).Should().Be(((int?)20, false));

    [Fact]
    public void An_unbreakable_rank_has_no_max_and_is_inquebravel() =>
        DurabilidadeDeItem.Resolver(RankDeItem.S, Tabela).Should().Be(((int?)null, true));

    [Fact]
    public void A_rank_missing_from_the_table_means_no_durability() =>
        DurabilidadeDeItem.Resolver(RankDeItem.C, Tabela).Should().Be(((int?)null, false));

    [Theory]
    [InlineData(10, 20, 10)]
    [InlineData(30, 20, 20)]
    [InlineData(-5, 20, 0)]
    [InlineData(7, null, 0)]
    public void LimitarAtual_clamps_between_zero_and_the_max(int atual, int? maxima, int esperado) =>
        DurabilidadeDeItem.LimitarAtual(atual, maxima).Should().Be(esperado);
}
