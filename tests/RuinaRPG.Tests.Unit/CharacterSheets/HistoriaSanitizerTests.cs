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

        result.Should().Contain("color: rgba(255, 0, 0, 1)")
            .And.Contain("background-color: rgba(255, 255, 0, 1)")
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
            .And.Contain("color: rgba(0, 0, 255, 1)")
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

    [Fact]
    public void Player_prose_with_rgba_values_is_untouched()
    {
        var result = HistoriaSanitizer.Sanitize("<p>Minha cor favorita é rgba(255, 0, 0, 1) no jogo.</p>");

        result.Should().Contain("rgba(255, 0, 0, 1) no jogo");
    }

    [Fact]
    public void Unwraps_a_disallowed_pre_block_keeping_its_text()
    {
        var result = HistoriaSanitizer.Sanitize("<pre>codigo importante</pre>");

        result.Should().NotBeNull();
        result.Should().Contain("codigo importante").And.NotContain("<pre");
    }

    [Fact]
    public void Unwraps_inline_code_keeping_the_surrounding_text()
    {
        var result = HistoriaSanitizer.Sanitize("<p>a <code>x()</code> b</p>");

        result.Should().Contain("x()").And.Contain("a ").And.Contain(" b").And.NotContain("<code");
    }

    [Fact]
    public void Unwraps_a_section_keeping_its_allowed_children()
    {
        HistoriaSanitizer.Sanitize("<section><p>Texto</p></section>").Should().Contain("<p>Texto</p>").And.NotContain("section");
    }

    [Fact]
    public void Unwraps_the_google_sheets_paste_wrapper_keeping_the_table()
    {
        var result = HistoriaSanitizer.Sanitize("<google-sheets-html-origin><table><tbody><tr><td>Célula</td></tr></tbody></table></google-sheets-html-origin>");

        result.Should().Contain("<td>Célula</td>").And.NotContain("google-sheets");
    }

    [Fact]
    public void Keeps_list_style_type()
    {
        HistoriaSanitizer.Sanitize("<ul style=\"list-style-type: lower-alpha\"><li>x</li></ul>").Should().Contain("list-style-type: lower-alpha");
    }

    [Fact]
    public void Keeps_table_width_border_collapse_and_vertical_align()
    {
        var result = HistoriaSanitizer.Sanitize("<table style=\"width: 100%; border-collapse: collapse\"><tbody><tr><td style=\"vertical-align: top; width: 50%\">x</td></tr></tbody></table>");

        result.Should().Contain("width").And.Contain("border-collapse").And.Contain("vertical-align");
    }

    [Theory]
    [InlineData("<noscript><p>escondido</p></noscript><p>visivel</p>", "visivel", "escondido")]
    [InlineData("<svg><text>desenho</text></svg><p>ok</p>", "ok", "desenho")]
    [InlineData("<template><p>molde</p></template><p>ok</p>", "ok", "molde")]
    [InlineData("<textarea>rascunho</textarea><p>ok</p>", "ok", "rascunho")]
    [InlineData("<select><option>opcao</option></select><p>ok</p>", "ok", "opcao")]
    [InlineData("<math><mi>equacao</mi></math><p>ok</p>", "ok", "equacao")]
    [InlineData("<object><p>fallback</p></object><p>ok</p>", "ok", "fallback")]
    [InlineData("<p>oi</p><script>alert(1)</script>", "oi", "alert")]
    [InlineData("<style>p{color:red}</style><p>oi</p>", "oi", "p{color")]
    [InlineData("<pre>antes<script>alert(1)</script>depois</pre>", "antesdepois", "alert")]
    public void Drops_the_contents_of_non_text_elements_entirely(string input, string kept, string dropped)
    {
        var result = HistoriaSanitizer.Sanitize(input);

        result.Should().Contain(kept).And.NotContain(dropped);
    }
}
