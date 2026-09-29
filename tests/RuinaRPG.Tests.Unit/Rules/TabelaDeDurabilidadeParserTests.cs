using FluentAssertions;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class TabelaDeDurabilidadeParserTests
{
    [Fact]
    public void Parses_the_real_tabela()
    {
        var markdown = File.ReadAllText(Path.Combine(RepoRoot(), "Docs", "Sistema RPG", "Tabela de Durabilidade por Rank.md"));

        TabelaDeDurabilidadeParser.Parse(markdown).Should().Equal(
            new DurabilidadeDeRank(RankDeItem.F, 20, false),
            new DurabilidadeDeRank(RankDeItem.E, 45, false),
            new DurabilidadeDeRank(RankDeItem.D, 80, false),
            new DurabilidadeDeRank(RankDeItem.C, 125, false),
            new DurabilidadeDeRank(RankDeItem.B, 180, false),
            new DurabilidadeDeRank(RankDeItem.A, 245, false),
            new DurabilidadeDeRank(RankDeItem.S, null, true),
            new DurabilidadeDeRank(RankDeItem.SS, null, true));
    }

    [Fact]
    public void Throws_on_an_unknown_rank_or_value() =>
        FluentActions.Invoking(() => TabelaDeDurabilidadeParser.Parse("| Rank | Durabilidade |\n|---|---|\n| Z | 10 |"))
            .Should().Throw<FormatException>();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RuinaRPG.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
