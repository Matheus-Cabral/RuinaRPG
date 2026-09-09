using FluentAssertions;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class CompendioSearchServiceTests
{
    private static readonly IRulesDataProvider Rules = new RulesDataProvider();

    private static readonly IReadOnlyList<TraitSeed> Traits = new[]
    {
        new TraitSeed("Ambidestria", "Manuseia armas com as duas mãos.", 2, "Positiva", RequerEspecificacao: false),
        new TraitSeed("Alergia", "É alérgico a alguma coisa.", -1, "Negativa", RequerEspecificacao: true)
    };

    [Fact]
    public void Search_with_no_query_and_no_category_filter_returns_results_from_all_four_categories()
    {
        var results = CompendioSearchService.Search(query: null, categorias: null, Traits, Rules);

        results.Select(r => r.Categoria).Distinct().Should()
            .BeEquivalentTo(new[] { CompendioCategoria.Caracteristica, CompendioCategoria.Efeito, CompendioCategoria.Tabela, CompendioCategoria.Regra });
    }

    [Fact]
    public void Search_by_text_matches_case_insensitively_across_title_and_content()
    {
        var results = CompendioSearchService.Search("ambidestria", categorias: null, Traits, Rules);

        results.Should().ContainSingle(r => r.Categoria == CompendioCategoria.Caracteristica && r.Titulo == "Ambidestria");
    }

    [Fact]
    public void Search_can_be_restricted_to_a_single_category()
    {
        var results = CompendioSearchService.Search(query: null, categorias: [CompendioCategoria.Caracteristica], Traits, Rules);

        results.Should().OnlyContain(r => r.Categoria == CompendioCategoria.Caracteristica);
        results.Should().HaveCount(2);
    }

    [Fact]
    public void Search_result_for_an_effect_names_its_grade_in_Origem()
    {
        var results = CompendioSearchService.Search("Aumentar Armadura", categorias: [CompendioCategoria.Efeito], Traits, Rules);

        results.Should().ContainSingle().Which.Origem.Should().Be("Efeito — 1º Grau/Círculo I");
    }

    [Fact]
    public void Search_result_for_a_table_row_names_the_table_and_level_in_Origem()
    {
        var results = CompendioSearchService.Search("Pontos de Atributo", categorias: [CompendioCategoria.Tabela], Traits, Rules);

        results.Should().Contain(r => r.Origem == "Tabela de Níveis — Nível 1");
    }

    [Fact]
    public void Search_includes_the_Circulo_e_Grau_por_EAP_table()
    {
        // Épico 6 item 1 of the gap audit: this table was already parsed by IRulesDataProvider but
        // never reached CompendioSearchService.
        var results = CompendioSearchService.Search(query: null, categorias: [CompendioCategoria.Tabela], Traits, Rules);

        results.Should().Contain(r => r.Origem.StartsWith("Tabela de Círculo e Grau por VIS"));
    }

    [Fact]
    public void Search_includes_the_Xp_e_EAP_por_Nivel_tables()
    {
        var results = CompendioSearchService.Search(query: null, categorias: [CompendioCategoria.Tabela], Traits, Rules);

        results.Should().Contain(r => r.Origem.StartsWith("Tabela de XP — Nível"));
        results.Should().Contain(r => r.Origem.StartsWith("Tabela de VIS por Nível — Nível"));
    }

    [Fact]
    public void Search_includes_the_Tabela_de_Classes()
    {
        // Épico 6 item 2 of the gap audit: Tabela de Classes.md wasn't even embedded, parsed, or
        // indexed at all — unlike item 1's tables, which were only missing from the search wiring.
        var results = CompendioSearchService.Search(query: null, categorias: [CompendioCategoria.Tabela], Traits, Rules);

        results.Should().Contain(r => r.Origem.StartsWith("Tabela de Classes — Duelista"));
    }
}
