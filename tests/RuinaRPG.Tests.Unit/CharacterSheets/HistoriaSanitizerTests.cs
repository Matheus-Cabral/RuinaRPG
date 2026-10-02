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

    // ---- Imagens dentro do texto: só as hospedadas pelo app ("/images/{guid}.{ext}") e liberadas pelo chamador ----

    private const string AppImage = "/images/0f8fad5b-d9cb-469f-a165-70867728950e.png";
    private const string AppImageFile = "0f8fad5b-d9cb-469f-a165-70867728950e.png";
    private static readonly HashSet<string> Permitidas = [AppImageFile];

    [Fact]
    public void Keeps_an_app_image_with_only_the_allowed_attributes()
    {
        var result = HistoriaSanitizer.Sanitize(
            $"<p>oi</p><img src=\"{AppImage}\" alt=\"Retrato\" width=\"300\" height=\"200\" class=\"x\" id=\"y\" title=\"t\" loading=\"lazy\" data-x=\"1\">", Permitidas);

        result.Should().Contain($"<img src=\"{AppImage}\" alt=\"Retrato\" width=\"300\" height=\"200\">");
    }

    [Fact]
    public void Keeps_the_allowed_css_of_an_app_image_and_drops_the_rest()
    {
        var result = HistoriaSanitizer.Sanitize($"<p><img src=\"{AppImage}\" style=\"width: 300px; height: 200px; position: fixed; background-image: url(https://mal.com/p.gif)\"></p>", Permitidas);

        result.Should().Contain("width: 300px").And.Contain("height: 200px").And.NotContain("position").And.NotContain("mal.com");
    }

    [Theory]
    [InlineData("http://mal.com/p.png")]
    [InlineData("https://mal.com/images/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("//mal.com/images/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("HTTPS://MAL.COM/p.png")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("DaTa:image/svg+xml,<svg onload=alert(1)>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("blob:https://app/0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("/api/images/mine")]
    [InlineData("/api/auth/logout")]
    [InlineData("/images/")]
    [InlineData("/images/qualquer.png")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.svg")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.html")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("/images/../api/auth/logout")]
    [InlineData("/images/../images/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/images/..%2F0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/images/%2e%2e/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/images/sub/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/images\\0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("\\\\mal.com\\images\\0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("images/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("./images/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/IMAGES/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/images/0F8FAD5B-D9CB-469F-A165-70867728950E.png")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.PNG")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.png?x=1")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.png#x")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.png ")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.png\n")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.png/../../api/x")]
    [InlineData("")]
    public void Drops_an_image_whose_src_is_not_the_canonical_app_image_url(string src)
    {
        var result = HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{System.Net.WebUtility.HtmlEncode(src)}\" alt=\"a\">", Permitidas);

        result.Should().Contain("oi").And.NotContain("<img").And.NotContain("mal.com").And.NotContain("javascript").And.NotContain("data:");
    }

    [Theory]
    [InlineData("<IMG SRC=\"http://mal.com/p.png\">")]
    [InlineData("<ImG sRc=\"JAVASCRIPT:alert(1)\">")]
    [InlineData("<img src=x onerror=\"alert(1)\">")]
    [InlineData("<img/src=\"http://mal.com/p.png\"/onerror=alert(1)>")]
    [InlineData("<img>")]
    [InlineData("<img alt=\"sem src\">")]
    [InlineData("<picture><source srcset=\"http://mal.com/p.png\"><img src=\"http://mal.com/p.png\"></picture>")]
    [InlineData("<input type=\"image\" src=\"http://mal.com/p.png\">")]
    [InlineData("<video poster=\"http://mal.com/p.png\"></video>")]
    [InlineData("<p style=\"background-image: url(http://mal.com/p.png)\">x</p>")]
    [InlineData("<p style=\"background: url('/images/0f8fad5b-d9cb-469f-a165-70867728950e.png')\">x</p>")]
    [InlineData("<a href=\"javascript:alert(1)\"><img src=\"http://mal.com/p.png\" onerror=\"alert(1)\"></a>")]
    [InlineData("<svg><image href=\"http://mal.com/p.png\"/></svg>")]
    public void Drops_every_other_way_of_embedding_an_image(string markup)
    {
        var result = HistoriaSanitizer.Sanitize("<p>oi</p>" + markup, Permitidas);

        result.Should().Contain("oi").And.NotContain("<img").And.NotContain("mal.com").And.NotContain("onerror")
            .And.NotContain("javascript", "nem em maiúsculas").And.NotContain("JAVASCRIPT").And.NotContain("url(").And.NotContain("srcset").And.NotContain("poster");
    }

    [Theory]
    [InlineData("onerror=\"alert(1)\"", "onerror")]
    [InlineData("ONLOAD=\"alert(1)\"", "onload")]
    [InlineData("onclick=alert(1)", "onclick")]
    [InlineData("srcset=\"http://mal.com/p.png 2x\"", "srcset")]
    [InlineData("SrcSet=\"http://mal.com/p.png 2x\"", "mal.com")]
    [InlineData("sizes=\"100vw\"", "sizes")]
    [InlineData("usemap=\"#m\"", "usemap")]
    [InlineData("longdesc=\"http://mal.com\"", "longdesc")]
    [InlineData("lowsrc=\"http://mal.com/p.png\"", "lowsrc")]
    [InlineData("dynsrc=\"http://mal.com/p.png\"", "dynsrc")]
    [InlineData("crossorigin=\"use-credentials\"", "crossorigin")]
    [InlineData("referrerpolicy=\"unsafe-url\"", "referrerpolicy")]
    [InlineData("width=\"100%\"", "width")]
    [InlineData("width=\"expression(alert(1))\"", "width")]
    [InlineData("height=\"99999\"", "height")]
    [InlineData("height=\"-1\"", "height")]
    public void An_app_image_survives_but_loses_event_handlers_srcset_and_other_attributes(string attribute, string forbidden)
    {
        var result = HistoriaSanitizer.Sanitize($"<p><img src=\"{AppImage}\" {attribute}></p>", Permitidas);

        result.Should().Contain($"<img src=\"{AppImage}\">").And.NotContainEquivalentOf(forbidden);
    }

    [Fact]
    public void An_app_image_inside_a_link_keeps_the_link_rules()
    {
        var seguro = HistoriaSanitizer.Sanitize($"<p><a href=\"https://exemplo.com\"><img src=\"{AppImage}\"></a></p>", Permitidas);
        var perigoso = HistoriaSanitizer.Sanitize($"<p><a href=\"javascript:alert(1)\"><img src=\"{AppImage}\"></a></p>", Permitidas);

        seguro.Should().Contain($"<img src=\"{AppImage}\">").And.Contain("rel=\"noopener noreferrer\"").And.Contain("target=\"_blank\"");
        perigoso.Should().Contain($"<img src=\"{AppImage}\">").And.NotContain("javascript");
    }

    [Fact]
    public void Drops_a_canonical_app_image_the_caller_was_not_allowed_to_use()
    {
        const string alheia = "/images/11111111-2222-3333-4444-555555555555.png";

        var result = HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{AppImage}\"><img src=\"{alheia}\">", Permitidas);

        result.Should().Contain(AppImage).And.NotContain(alheia);
    }

    [Fact]
    public void Without_a_set_of_allowed_images_no_image_survives()
    {
        HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{AppImage}\">").Should().Contain("oi").And.NotContain("<img");
    }

    [Fact]
    public void A_lone_app_image_counts_as_content()
    {
        HistoriaSanitizer.Sanitize($"<p><img src=\"{AppImage}\"></p>", Permitidas).Should().Contain("<img");
        HistoriaSanitizer.Sanitize("<p><img src=\"http://mal.com/p.png\"></p>", Permitidas).Should().BeNull();
    }

    [Theory]
    [InlineData("<p src=\"/images/0f8fad5b-d9cb-469f-a165-70867728950e.png\" alt=\"a\" width=\"10\" height=\"10\">oi</p>")]
    [InlineData("<a href=\"https://exemplo.com\" src=\"http://mal.com/p.png\">oi</a>")]
    [InlineData("<td width=\"10\" height=\"10\" alt=\"a\">oi</td>")]
    public void The_image_attributes_are_only_kept_on_img(string markup)
    {
        var result = HistoriaSanitizer.Sanitize(markup, Permitidas);

        result.Should().Contain("oi").And.NotContain("src=").And.NotContain("alt=").And.NotContain("width=").And.NotContain("height=");
    }

    [Fact]
    public void ImageFiles_lists_only_the_canonical_app_images_referenced_by_img_tags()
    {
        var files = HistoriaSanitizer.ImageFiles(
            $"<p>texto {AppImage} /images/11111111-2222-3333-4444-555555555555.png</p><img src=\"{AppImage}\"><img src=\"http://mal.com/p.png\">" +
            "<a href=\"/images/22222222-2222-3333-4444-555555555555.png\">x</a><img src=\"/images/../x.png\">");

        files.Should().BeEquivalentTo([AppImageFile]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p>sem imagem</p>")]
    public void ImageFiles_is_empty_when_there_is_no_app_image(string? html)
    {
        HistoriaSanitizer.ImageFiles(html).Should().BeEmpty();
    }

    [Theory]
    [InlineData("//evil.com/x")]
    [InlineData("/api/auth/logout")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("../x")]
    [InlineData("relativo.html")]
    [InlineData("?q=1")]
    [InlineData("#ancora")]
    [InlineData("\\\\evil.com\\x")]
    [InlineData("http:evil.com")]
    [InlineData("ftp://evil.com/x")]
    public void A_link_without_an_absolute_http_https_or_mailto_href_loses_the_href_but_keeps_its_text(string href)
    {
        var result = HistoriaSanitizer.Sanitize($"<p>antes <a href=\"{System.Net.WebUtility.HtmlEncode(href)}\">o texto</a> depois</p>");

        result.Should().Contain("o texto").And.NotContain("href").And.NotContain("evil").And.NotContain("/api");
    }

    [Theory]
    [InlineData("http://exemplo.com/a")]
    [InlineData("https://exemplo.com/a?b=1#c")]
    [InlineData("HTTPS://EXEMPLO.COM")]
    [InlineData("mailto:mestre@exemplo.com")]
    public void A_link_with_an_absolute_http_https_or_mailto_href_is_kept(string href)
    {
        HistoriaSanitizer.Sanitize($"<p><a href=\"{href}\">x</a></p>").Should().Contain("href=").And.Contain("rel=\"noopener noreferrer\"");
    }

    [Fact]
    public void An_app_image_inside_a_link_with_a_relative_href_stays_and_the_href_goes()
    {
        var result = HistoriaSanitizer.Sanitize($"<p><a href=\"/api/auth/logout\"><img src=\"{AppImage}\"></a></p>", Permitidas);

        result.Should().Contain($"<img src=\"{AppImage}\">").And.NotContain("href").And.NotContain("logout");
    }

    // --- Pins: casos verificados à mão na revisão de segurança ---

    [Theory]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e&#46;png")]
    [InlineData("&#47;images&#47;0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("&sol;images&sol;0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    public void An_entity_encoded_src_is_kept_only_in_its_canonical_decoded_form(string rawSrc)
    {
        var result = HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{rawSrc}\">", Permitidas);

        result.Should().Contain($"<img src=\"{AppImage}\">").And.NotContain("&");
    }

    [Theory]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.p&#110;g&#47;..&#47;x")]
    [InlineData("&#106;avascript:alert(1)")]
    [InlineData("&#x2F;&#x2F;mal.com/p.png")]
    public void An_entity_encoded_src_that_decodes_to_something_else_is_dropped(string rawSrc)
    {
        HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{rawSrc}\">", Permitidas).Should().Contain("oi").And.NotContain("<img");
    }

    [Fact]
    public void With_duplicate_src_attributes_the_first_wins_and_is_validated()
    {
        const string alheia = "/images/11111111-2222-3333-4444-555555555555.png";

        var bom = HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{AppImage}\" src=\"http://mal.com/p.png\">", Permitidas);
        var ruim = HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"http://mal.com/p.png\" src=\"{AppImage}\">", Permitidas);
        var alheiaPrimeiro = HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{alheia}\" src=\"{AppImage}\">", Permitidas);

        bom.Should().Contain($"<img src=\"{AppImage}\">").And.NotContain("mal.com");
        ruim.Should().NotContain("<img").And.NotContain("mal.com");
        alheiaPrimeiro.Should().NotContain("<img");
    }

    [Fact]
    public void With_duplicate_width_attributes_the_first_wins_and_is_validated()
    {
        var bom = HistoriaSanitizer.Sanitize($"<p><img src=\"{AppImage}\" width=\"120\" width=\"expression(1)\"></p>", Permitidas);
        var ruim = HistoriaSanitizer.Sanitize($"<p><img src=\"{AppImage}\" width=\"100%\" width=\"120\"></p>", Permitidas);

        bom.Should().Contain("width=\"120\"").And.NotContain("expression");
        ruim.Should().Contain($"<img src=\"{AppImage}\">").And.NotContain("width").And.NotContain("%");
    }

    [Theory]
    [InlineData("\t")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\0")]
    [InlineData("&#9;")]
    [InlineData("&#10;")]
    [InlineData("&#13;")]
    [InlineData("&#0;")]
    public void Control_characters_around_or_inside_the_src_drop_the_image(string control)
    {
        var meio = AppImage.Insert(10, control);

        foreach (var src in new[] { control + AppImage, AppImage + control, control + AppImage + control, meio })
            HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{src}\">", Permitidas).Should().Contain("oi").And.NotContain("<img", $"src com {control.Replace("\0", "NUL")}");
    }

    [Theory]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-7086772895\u0665e.png")] // 5 arábico-índico
    [InlineData("/images/\uff10f8fad5b-d9cb-469f-a165-70867728950e.png")] // 0 de largura total
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950\uff45.png")] // e de largura total
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e.p\u0578g")]
    [InlineData("/\u0456mages/0f8fad5b-d9cb-469f-a165-70867728950e.png")] // i cirílico
    public void Unicode_lookalike_digits_and_letters_drop_the_image(string src)
    {
        HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{src}\">", Permitidas).Should().Contain("oi").And.NotContain("<img");
    }

    [Theory]
    [InlineData("/images%2f0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("%2fimages/0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("/images/0f8fad5b-d9cb-469f-a165-70867728950e%2epng")]
    public void Percent_encoded_src_is_dropped(string src)
    {
        HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{src}\">", Permitidas).Should().Contain("oi").And.NotContain("<img");
    }

    [Fact]
    public void A_100_kb_src_is_dropped()
    {
        var gigante = AppImage + new string('a', 100 * 1024);

        HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"{gigante}\">", Permitidas).Should().Contain("oi").And.NotContain("<img");
        HistoriaSanitizer.Sanitize($"<p>oi</p><img src=\"/images/{new string('a', 100 * 1024)}.png\">", Permitidas).Should().Contain("oi").And.NotContain("<img");
    }

    [Fact]
    public void The_image_alias_tag_never_survives_as_image_and_is_validated_like_img()
    {
        // O parser HTML (como o do navegador) trata <image> como <img>: o que sai é um <img> normal, validado
        // pela mesma regra de src. Nunca sai a tag <image>, e um src externo não passa.
        var result = HistoriaSanitizer.Sanitize($"<p>oi</p><image src=\"{AppImage}\"><image src=\"http://mal.com/p.png\">", Permitidas);

        result.Should().Be($"<p>oi</p><img src=\"{AppImage}\">");
        HistoriaSanitizer.Sanitize("<p>oi</p><image src=\"/images/11111111-2222-3333-4444-555555555555.png\">", Permitidas).Should().NotContain("<img").And.NotContain("<image");
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("math")]
    [InlineData("noscript")]
    [InlineData("template")]
    public void An_img_inside_a_non_html_or_inert_container_leaves_nothing_dangerous(string container)
    {
        var markup = $"<p>oi</p><{container}><img src=\"http://mal.com/p.png\" onerror=\"alert(1)\"><img src=\"javascript:alert(1)\"><a href=\"javascript:alert(1)\">x</a></{container}>";

        var result = HistoriaSanitizer.Sanitize(markup, Permitidas);

        result.Should().Contain("oi").And.NotContain("mal.com").And.NotContain("onerror").And.NotContain("javascript").And.NotContain("<img").And.NotContain("href");
    }

    [Theory]
    [InlineData("<table background=\"http://mal.com/p.png\"><tr><td>x</td></tr></table>")]
    [InlineData("<table><tr><td background=\"/images/0f8fad5b-d9cb-469f-a165-70867728950e.png\">x</td></tr></table>")]
    [InlineData("<table><tr><th background=\"http://mal.com/p.png\">x</th></tr></table>")]
    [InlineData("<body background=\"http://mal.com/p.png\"><p>x</p></body>")]
    public void The_background_attribute_is_dropped(string markup)
    {
        var result = HistoriaSanitizer.Sanitize(markup, Permitidas);

        result.Should().Contain("x").And.NotContain("background=").And.NotContain("mal.com");
    }

    [Fact]
    public void Layout_escaping_and_url_css_are_stripped_from_an_img_style()
    {
        var result = HistoriaSanitizer.Sanitize(
            $"<p><img src=\"{AppImage}\" style=\"position: fixed; z-index: 9999; top: 0; left: 0; background: url(http://mal.com/p.png); width: 100%\"></p>", Permitidas);

        result.Should().Contain("<img").And.NotContain("position").And.NotContain("z-index").And.NotContain("url(").And.NotContain("mal.com").And.NotContain("top").And.NotContain("left");
    }

    [Fact]
    public void Sanitizing_a_valid_document_twice_gives_the_same_output()
    {
        var html = "<h2>Origem</h2><p style=\"color: red; text-align: center\"><strong>Nasceu</strong> em <em>Alkeria</em> &amp; <a href=\"https://exemplo.com/a?b=1&amp;c=2\">site</a> " +
                   $"<a href=\"mailto:a@b.com\">mail</a></p><p><img src=\"{AppImage}\" width=\"120\" height=\"80\" alt=\"Retrato\"></p>" +
                   "<table><tbody><tr><td colspan=\"2\" style=\"background-color: yellow\">x</td></tr></tbody></table><ul><li>um</li><li>dois</li></ul><hr><p>fim</p>";

        var uma = HistoriaSanitizer.Sanitize(html, Permitidas);
        var duas = HistoriaSanitizer.Sanitize(uma, Permitidas);

        uma.Should().Contain($"<img src=\"{AppImage}\"").And.Contain("href=\"https://exemplo.com/a?b=1&amp;c=2\"");
        duas.Should().Be(uma);
    }
}
