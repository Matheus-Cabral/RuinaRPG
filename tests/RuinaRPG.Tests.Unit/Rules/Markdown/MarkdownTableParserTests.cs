using FluentAssertions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Tests.Unit.Rules.Markdown;

public class MarkdownTableParserTests
{
    [Fact]
    public void ParseTables_parses_a_single_simple_table()
    {
        const string markdown = """
            | Nível | Bônus |
            | :---: | ----- |
            |   1   | +9 Pontos de Atributo |
            |   2   | +4 Pontos de Ignição |
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables.Should().HaveCount(1);
        tables[0].Headers.Should().Equal("Nível", "Bônus");
        tables[0].Rows.Should().HaveCount(2);
        tables[0].Rows[0].Should().Equal("1", "+9 Pontos de Atributo");
        tables[0].Rows[1].Should().Equal("2", "+4 Pontos de Ignição");
    }

    [Fact]
    public void ParseTables_parses_multiple_tables_separated_by_blank_lines()
    {
        const string markdown = """
            | A | B |
            | - | - |
            | 1 | 2 |

            | C | D |
            | - | - |
            | 3 | 4 |
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables.Should().HaveCount(2);
        tables[0].Headers.Should().Equal("A", "B");
        tables[1].Headers.Should().Equal("C", "D");
    }

    [Fact]
    public void ParseTables_ignores_prose_outside_tables()
    {
        const string markdown = """
            Some introductory prose that is not a table.

            | X | Y |
            | - | - |
            | 1 | 2 |

            More prose after the table.
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables.Should().HaveCount(1);
        tables[0].Headers.Should().Equal("X", "Y");
    }

    [Fact]
    public void ParseTables_handles_a_row_with_an_empty_first_cell()
    {
        // Mirrors the real shape of Tabela de Vocação's sub-header row: "|       |  Vida   | Arcana  | ..."
        const string markdown = """
            | Nível | Campeão | Campeão |
            | :---: | :-----: | :-----: |
            |       |  Vida   | Arcana  |
            |   1   |    8    |    4    |
            """;

        var tables = MarkdownTableParser.ParseTables(markdown);

        tables[0].Rows[0].Should().Equal("", "Vida", "Arcana");
        tables[0].Rows[1].Should().Equal("1", "8", "4");
    }

    [Fact]
    public void ParseTables_returns_empty_when_there_are_no_tables()
    {
        var tables = MarkdownTableParser.ParseTables("Just some prose, no pipe tables here.");

        tables.Should().BeEmpty();
    }
}
