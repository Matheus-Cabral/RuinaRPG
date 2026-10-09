using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class TabelaDeAfinidadesParserTests
{
    [Fact]
    public void Parse_reads_every_row_of_the_table()
    {
        const string markdown = "Texto de introdução.\n\n| Afinidade | Eficiência | Dano |\n| --------- | ---------- | ---- |\n| 0         | 0          | 0    |\n| 1         | 1          | 0    |\n| 21        | 11         | 10   |\n";

        TabelaDeAfinidadesParser.Parse(markdown).Should().Equal(
            new LinhaDaTabelaDeAfinidades(0, 0, 0), new LinhaDaTabelaDeAfinidades(1, 1, 0), new LinhaDaTabelaDeAfinidades(21, 11, 10));
    }

    [Theory]
    [InlineData("| x | 1 | 1 |")]
    [InlineData("| 1 | -1 | 1 |")]
    [InlineData("| 1 | 1 |")]
    public void Parse_rejects_a_malformed_row(string linha)
    {
        var act = () => TabelaDeAfinidadesParser.Parse("| Afinidade | Eficiência | Dano |\n| --- | --- | --- |\n" + linha);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_rejects_a_repeated_Afinidade()
    {
        var act = () => TabelaDeAfinidadesParser.Parse("| Afinidade | Eficiência | Dano |\n| --- | --- | --- |\n| 1 | 1 | 0 |\n| 1 | 2 | 1 |");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_reads_the_embedded_Tabela_de_Afinidades()
    {
        var linhas = TabelaDeAfinidadesParser.Parse(RulesDataProvider.ReadResource("Tabela de Afinidades.md"));

        linhas.Should().HaveCount(22);
        linhas[0].Should().Be(new LinhaDaTabelaDeAfinidades(0, 0, 0));
        linhas[^1].Should().Be(new LinhaDaTabelaDeAfinidades(21, 11, 10));
    }
}
