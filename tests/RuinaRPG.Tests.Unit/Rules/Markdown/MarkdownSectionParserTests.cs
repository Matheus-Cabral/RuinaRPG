using FluentAssertions;
using RuinaRPG.Domain.Rules.Markdown;

namespace RuinaRPG.Tests.Unit.Rules.Markdown;

public class MarkdownSectionParserTests
{
    [Fact]
    public void Parse_extracts_top_level_sections_with_their_body_text()
    {
        const string markdown = """
            # Primeira Seção

            Corpo da primeira seção.

            # Segunda Seção

            Corpo da segunda seção.
            """;

        var sections = MarkdownSectionParser.Parse(markdown);

        sections.Should().HaveCount(2);
        sections[0].Title.Should().Be("Primeira Seção");
        sections[0].Body.Should().Contain("Corpo da primeira seção.");
        sections[1].Title.Should().Be("Segunda Seção");
    }

    [Fact]
    public void Parse_nests_child_sections_under_their_parent()
    {
        const string markdown = """
            # 1º GRAU / CÍRCULO I

            ## Aumentar Armadura

            Gasto: 2 PI por Ponto de Redução.

            ## Cura

            Gasto: 2 PI

            # 2º GRAU / CÍRCULO II

            ## Aceleração

            Gasto: 3 PI
            """;

        var sections = MarkdownSectionParser.Parse(markdown);

        sections.Should().HaveCount(2);
        sections[0].Title.Should().Be("1º GRAU / CÍRCULO I");
        sections[0].Children.Should().HaveCount(2);
        sections[0].Children[0].Title.Should().Be("Aumentar Armadura");
        sections[0].Children[0].Body.Should().Contain("Gasto: 2 PI por Ponto de Redução.");
        sections[0].Children[1].Title.Should().Be("Cura");
        sections[1].Children.Should().ContainSingle(c => c.Title == "Aceleração");
    }

    [Fact]
    public void Parse_returns_empty_for_markdown_with_no_headings()
    {
        var sections = MarkdownSectionParser.Parse("Just prose, no headings.");

        sections.Should().BeEmpty();
    }
}
