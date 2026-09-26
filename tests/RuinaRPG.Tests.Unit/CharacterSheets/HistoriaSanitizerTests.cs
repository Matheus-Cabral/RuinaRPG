using FluentAssertions;
using RuinaRPG.Infrastructure.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class HistoriaSanitizerTests
{
    [Fact]
    public void Keeps_headings_paragraphs_and_inline_formatting()
    {
        var result = HistoriaSanitizer.Sanitize("<h2>Origem</h2><p><strong>Nasceu</strong> em <em>Alkeria</em>, <u>filho</u> de <s>ninguém</s> H<sub>2</sub>O x<sup>2</sup></p>");

        result.Should().Contain("<h2>Origem</h2>")
            .And.Contain("<strong>Nasceu</strong>")
            .And.Contain("<em>Alkeria</em>")
            .And.Contain("<u>filho</u>")
            .And.Contain("<s>ninguém</s>")
            .And.Contain("<sub>2</sub>")
            .And.Contain("<sup>2</sup>");
    }

    [Fact]
    public void Keeps_allowed_css_properties_and_drops_the_rest()
    {
        var result = HistoriaSanitizer.Sanitize("<p style=\"color: red; background-color: yellow; text-align: center; position: fixed; font-size: 20px\">Texto</p>");

        result.Should().Contain("color: red")
            .And.Contain("background-color: yellow")
            .And.Contain("text-align: center")
            .And.Contain("font-size: 20px")
            .And.NotContain("position");
    }

    [Fact]
    public void Keeps_tables_with_colspan_and_rowspan()
    {
        var result = HistoriaSanitizer.Sanitize("<table><thead><tr><th colspan=\"2\">Aliados</th></tr></thead><tbody><tr><td rowspan=\"2\">Lira</td><td>Irmã</td></tr></tbody></table>");

        result.Should().Contain("<table>").And.Contain("colspan=\"2\"").And.Contain("rowspan=\"2\"").And.Contain("<td");
    }

    [Fact]
    public void Keeps_http_and_mailto_links_and_forces_target_blank_with_noopener()
    {
        var result = HistoriaSanitizer.Sanitize("<p><a href=\"https://exemplo.com\" target=\"_self\">site</a> <a href=\"mailto:mestre@exemplo.com\">email</a></p>");

        result.Should().Contain("href=\"https://exemplo.com\"")
            .And.Contain("href=\"mailto:mestre@exemplo.com\"")
            .And.Contain("target=\"_blank\"")
            .And.Contain("rel=\"noopener noreferrer\"")
            .And.NotContain("_self");
    }

    [Theory]
    [InlineData("<p>oi</p><script>alert(1)</script>", "script")]
    [InlineData("<p onclick=\"alert(1)\">oi</p>", "onclick")]
    [InlineData("<p><a href=\"javascript:alert(1)\">oi</a></p>", "javascript")]
    [InlineData("<p>oi</p><img src=\"x\" onerror=\"alert(1)\">", "img")]
    [InlineData("<p>oi</p><iframe src=\"https://mal.com\"></iframe>", "iframe")]
    [InlineData("<style>p{color:red}</style><p>oi</p>", "style>")]
    [InlineData("<p class=\"perigo\" id=\"x\">oi</p>", "class")]
    [InlineData("<p><a href=\"data:text/html;base64,PHNjcmlwdD4=\">oi</a></p>", "data:")]
    public void Strips_dangerous_or_disallowed_markup_but_keeps_the_text(string input, string forbidden)
    {
        var result = HistoriaSanitizer.Sanitize(input);

        result.Should().Contain("oi").And.NotContain(forbidden);
    }

    [Fact]
    public void Cleans_markup_pasted_from_word()
    {
        var result = HistoriaSanitizer.Sanitize("<p class=\"MsoNormal\" style=\"mso-line-height-alt: 12pt; color: blue\"><b>Capítulo 1</b><o:p></o:p></p>");

        result.Should().Contain("<b>Capítulo 1</b>")
            .And.Contain("color: blue")
            .And.NotContain("Mso")
            .And.NotContain("mso-")
            .And.NotContain("o:p");
    }

    [Fact]
    public void Preserves_accents_and_emoji()
    {
        var result = HistoriaSanitizer.Sanitize("<p>Coração 🌿 e ação</p>");

        result.Should().Contain("Coração 🌿 e ação");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p><br></p>")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("<script>alert(1)</script>")]
    public void Returns_null_when_there_is_no_visible_content(string? input)
    {
        HistoriaSanitizer.Sanitize(input).Should().BeNull();
    }

    [Fact]
    public void A_lone_horizontal_rule_counts_as_content()
    {
        HistoriaSanitizer.Sanitize("<hr>").Should().NotBeNull();
    }
}
