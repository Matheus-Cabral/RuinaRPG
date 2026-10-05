using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class PassivasDoLivroOrganizadorTests
{
    private static PassivaDoLivroResponse P(string nome, string categoria, string? vocacao = null, string? classe = null) =>
        new(Guid.NewGuid().ToString(), nome, categoria, "", [], vocacao, classe);

    private static readonly FiltroDePassivas SemFiltro = new();

    [Fact]
    public void Groups_are_always_Livres_Vocacionais_De_Classe_in_that_order_with_these_titles()
    {
        var grupos = PassivasDoLivroOrganizador.Agrupar([P("C", "DeClasse"), P("V", "Vocacional"), P("L", "Livre")], SemFiltro);

        grupos.Select(g => g.Titulo).Should().Equal("Passivas Livres", "Passivas Vocacionais", "Passivas de Classe");
    }

    [Fact]
    public void Inside_a_group_the_order_is_alphabetical_ignoring_case_and_accents()
    {
        var grupos = PassivasDoLivroOrganizador.Agrupar([P("zelo", "Livre"), P("Ímpeto", "Livre"), P("ardor", "Livre"), P("Brio", "Livre"), P("Água", "Livre")], SemFiltro);

        grupos.Single().Passivas.Select(p => p.Nome).Should().Equal("Água", "ardor", "Brio", "Ímpeto", "zelo");
    }

    [Fact]
    public void A_group_left_empty_by_the_filters_disappears()
    {
        var grupos = PassivasDoLivroOrganizador.Agrupar([P("A", "Livre"), P("B", "Vocacional")], new(Categoria: "Vocacional"));

        grupos.Select(g => g.Titulo).Should().Equal("Passivas Vocacionais");
    }

    [Fact]
    public void Name_vocacao_and_classe_filters_combine()
    {
        var passivas = new[]
        {
            P("Golpe Firme", "Vocacional", "Campeão"), P("Golpe Rápido", "Vocacional", "Caçador"),
            P("Golpe Duplo", "DeClasse", "Campeão", "Duelista"), P("Passo Leve", "DeClasse", "Campeão", "Duelista"),
        };

        var grupos = PassivasDoLivroOrganizador.Agrupar(passivas, new(Nome: "golpe", Vocacao: "Campeão", Classe: "Duelista"));

        grupos.SelectMany(g => g.Passivas).Select(p => p.Nome).Should().Equal("Golpe Duplo");
    }

    [Fact]
    public void The_name_filter_ignores_case_and_surrounding_spaces_and_a_blank_one_filters_nothing()
    {
        var passivas = new[] { P("Ardor", "Livre"), P("Brio", "Livre") };

        PassivasDoLivroOrganizador.Agrupar(passivas, new(Nome: "  ARD ")).Single().Passivas.Should().ContainSingle();
        PassivasDoLivroOrganizador.Agrupar(passivas, new(Nome: "   ")).Single().Passivas.Should().HaveCount(2);
    }

    [Fact]
    public void A_vocacao_filter_leaves_out_passivas_without_that_requirement()
    {
        var grupos = PassivasDoLivroOrganizador.Agrupar([P("Sem", "Livre"), P("Com", "Livre", "Campeão")], new(Vocacao: "Campeão"));

        grupos.Single().Passivas.Select(p => p.Nome).Should().Equal("Com");
    }

    [Fact]
    public void An_unknown_category_is_not_listed_and_nothing_fails() =>
        PassivasDoLivroOrganizador.Agrupar([P("X", "Inventada")], SemFiltro).Should().BeEmpty();

    [Fact]
    public void Vocacoes_and_Classes_are_the_distinct_values_present_sorted()
    {
        var passivas = new[] { P("a", "Livre", "Caçador", "Batedor"), P("b", "Livre", "Campeão", "Duelista"), P("c", "Livre", "Campeão", "Batedor"), P("d", "Livre") };

        PassivasDoLivroOrganizador.Vocacoes(passivas).Should().Equal("Caçador", "Campeão");
        PassivasDoLivroOrganizador.Classes(passivas).Should().Equal("Batedor", "Duelista");
    }
}
