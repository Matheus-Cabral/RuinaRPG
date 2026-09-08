using FluentAssertions;
using RuinaRPG.Infrastructure.Rules;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class RulebookRendererTests
{
    [Fact]
    public void Returns_the_four_documents_in_a_fixed_order()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var documents = renderer.GetDocuments();

        documents.Select(d => d.Slug).Should().Equal(
            "caracteristicas", "sistema-basico", "graus-e-circulos", "tabela-de-niveis");
    }

    [Fact]
    public void SistemaBasico_is_split_into_one_section_per_level_2_heading()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        // Ruína RPG - Sistema Básico.md's own "## 1. Atributos" / "## 2. Perícias e Progressão" /
        // etc. — 7 top-level numbered sections, no "##" headings besides those 7.
        var sistemaBasico = renderer.GetDocuments().Single(d => d.Slug == "sistema-basico");

        sistemaBasico.Sections.Should().HaveCount(7);
        sistemaBasico.Sections.Select(s => s.Titulo).Should().Equal(
            "1. Atributos", "2. Perícias e Progressão", "3. Pontos de Adrenalina (PA)",
            "4. Sistema de Ações em Combate", "5. Iniciativa", "6. Sistema de Viagem", "7. Linhagens e Variantes");
    }

    [Fact]
    public void SistemaBasico_sections_carry_a_stable_non_empty_id_and_no_group()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var sistemaBasico = renderer.GetDocuments().Single(d => d.Slug == "sistema-basico");

        sistemaBasico.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Id));
        sistemaBasico.Sections.Select(s => s.Id).Should().OnlyHaveUniqueItems();
        sistemaBasico.Sections.Should().OnlyContain(s => s.Grupo == null);
    }

    [Fact]
    public void SistemaBasico_section_Html_contains_its_own_subheadings_but_not_the_section_title_itself()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var sistemaBasico = renderer.GetDocuments().Single(d => d.Slug == "sistema-basico");

        // "4. Sistema de Ações em Combate" has "### Usos das Ações" etc. nested under it — those
        // stay inside the section's own Html (only the level-2 split boundary gets pulled out into
        // Titulo), and the section's own "<h2" for its own title must not be duplicated in the body.
        var combate = sistemaBasico.Sections.Single(s => s.Titulo == "4. Sistema de Ações em Combate");
        combate.Html.Should().Contain("<h3").And.Contain("Usos das Ações");
        combate.Html.Should().NotContain("Sistema de Ações em Combate");
    }

    [Fact]
    public void SistemaBasico_has_no_IntroHtml_since_the_document_starts_with_its_first_heading()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var sistemaBasico = renderer.GetDocuments().Single(d => d.Slug == "sistema-basico");

        sistemaBasico.IntroHtml.Should().BeNull();
    }

    [Fact]
    public void GrausECirculos_is_split_into_one_section_per_Grau_with_effects_nested_inside()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var grausECirculos = renderer.GetDocuments().Single(d => d.Slug == "graus-e-circulos");

        grausECirculos.Sections.Should().HaveCount(9); // 1º through 9º Grau/Círculo — the source doc has no 10th
        var primeiroGrau = grausECirculos.Sections.First();
        primeiroGrau.Titulo.Should().Contain("1º GRAU");
        // "## Cura" etc. are one level deeper than the "#" split boundary — they stay nested as
        // real sub-headings inside the Grau's own Html, not pulled into their own top-level section.
        primeiroGrau.Html.Should().Contain("<h2").And.Contain("Cura");
    }

    [Fact]
    public void GrausECirculos_IntroHtml_has_the_cost_table_and_the_two_reference_images_before_the_first_Grau()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var grausECirculos = renderer.GetDocuments().Single(d => d.Slug == "graus-e-circulos");

        grausECirculos.IntroHtml.Should().NotBeNull();
        grausECirculos.IntroHtml!.Should().Contain("<table").And.Contain("<td");
        grausECirculos.IntroHtml.Should().Contain("/rulebook/Escolas_de_Magia.png");
        grausECirculos.IntroHtml.Should().Contain("/rulebook/Matriz_Elemental.png");
        // Images come after the cost table (the table is the document's own lead-in content, the
        // images are appended reference material — same relative order R0002 always specified).
        var tableIndex = grausECirculos.IntroHtml!.IndexOf("<table", StringComparison.Ordinal);
        var imageIndex = grausECirculos.IntroHtml!.IndexOf("/rulebook/Escolas_de_Magia.png", StringComparison.Ordinal);
        tableIndex.Should().BeLessThan(imageIndex);
    }

    [Fact]
    public void Caracteristicas_is_split_per_trait_and_tagged_with_its_Positivas_or_Negativas_group()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var caracteristicas = renderer.GetDocuments().Single(d => d.Slug == "caracteristicas");

        caracteristicas.Sections.Should().HaveCountGreaterThan(50); // 58 traits at the time of writing
        var alfabetizado = caracteristicas.Sections.Single(s => s.Titulo == "Alfabetizado");
        alfabetizado.Grupo.Should().Be("Positivas");
        alfabetizado.Html.Should().Contain("Saber ler e escrever");
    }

    [Fact]
    public void Caracteristicas_IntroHtml_has_the_documents_lead_in_paragraphs()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        var caracteristicas = renderer.GetDocuments().Single(d => d.Slug == "caracteristicas");

        caracteristicas.IntroHtml.Should().NotBeNull();
        caracteristicas.IntroHtml!.Should().Contain("cada característica");
    }

    [Fact]
    public void TabelaDeNiveis_has_no_headings_so_everything_lands_in_IntroHtml_as_a_real_table()
    {
        IRulebookRenderer renderer = new RulebookRenderer();

        // Tabela de Níveis.md is one big GFM pipe table with no Markdown headings at all — splitting
        // at any heading level finds nothing to split on, so it stays a single block, same as before
        // this change (only now surfaced via IntroHtml instead of the old flat Html field).
        var tabelaDeNiveis = renderer.GetDocuments().Single(d => d.Slug == "tabela-de-niveis");

        tabelaDeNiveis.Sections.Should().BeEmpty();
        tabelaDeNiveis.IntroHtml.Should().NotBeNull();
        tabelaDeNiveis.IntroHtml!.Should().Contain("<table").And.Contain("<td");
        tabelaDeNiveis.IntroHtml.Should().NotContain("| NÍVEL |");
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
