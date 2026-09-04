using FluentAssertions;
using RuinaRPG.Infrastructure.Rules;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class RulebookRendererTests
{
    [Fact]
    public void Returns_the_four_documents_in_a_fixed_order_with_non_empty_Html()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var documents = renderer.GetDocuments();

        documents.Select(d => d.Slug).Should().Equal(
            "caracteristicas", "sistema-basico", "graus-e-circulos", "tabela-de-niveis");
        documents.Should().OnlyContain(d => !string.IsNullOrWhiteSpace(d.Html));
    }

    [Fact]
    public void Renders_a_pipe_table_as_a_real_html_table_not_literal_pipe_text()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        // Tabela de Níveis.md is one big "| NÍVEL | BÔNUS |" GFM pipe table — the bare Markdig
        // pipeline leaves that as literal "|"-delimited text inside a <p>, not a <table>; only
        // UseAdvancedExtensions() turns it into real markup.
        var tabelaDeNiveis = renderer.GetDocuments().Single(d => d.Slug == "tabela-de-niveis");

        tabelaDeNiveis.Html.Should().Contain("<table").And.Contain("<td");
        tabelaDeNiveis.Html.Should().NotContain("| NÍVEL |");
    }

    [Fact]
    public void Renders_markdown_to_real_html_not_just_the_escaped_source_text()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var sistemaBasico = renderer.GetDocuments().Single(d => d.Slug == "sistema-basico");

        // Ruína RPG - Sistema Básico.md has "## " headings and "- " bullet lists — real HTML
        // output turns those into tags, not literal "##"/"- " characters.
        sistemaBasico.Html.Should().Contain("<h2").And.Contain("<li>");
        sistemaBasico.Html.Should().NotContain("##");
    }

    [Fact]
    public void GrausECirculos_gets_the_two_reference_images_inserted_before_its_first_heading()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var grausECirculos = renderer.GetDocuments().Single(d => d.Slug == "graus-e-circulos");

        grausECirculos.Html.Should().Contain("/rulebook/Escolas_de_Magia.png");
        grausECirculos.Html.Should().Contain("/rulebook/Matriz_Elemental.png");
        var firstImageIndex = grausECirculos.Html.IndexOf("/rulebook/Escolas_de_Magia.png", StringComparison.Ordinal);
        var firstHeadingIndex = grausECirculos.Html.IndexOf("<h1", StringComparison.Ordinal);
        firstImageIndex.Should().BeLessThan(firstHeadingIndex);
    }

    [Fact]
    public void The_other_three_documents_do_not_get_the_reference_images()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var others = renderer.GetDocuments().Where(d => d.Slug != "graus-e-circulos");

        others.Should().OnlyContain(d => !d.Html.Contains("/rulebook/"));
    }

    [Fact]
    public void Repeated_access_reuses_the_cached_render_result()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var firstCall = renderer.GetDocuments();
        var secondCall = renderer.GetDocuments();

        secondCall.Should().BeSameAs(firstCall);
    }
}
