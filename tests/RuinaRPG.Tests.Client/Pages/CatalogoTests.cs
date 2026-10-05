using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class CatalogoTests : MudBunitContext
{
    [Fact]
    public void FiltrarSubcategorias_returns_everything_when_the_query_is_blank()
    {
        var disponiveis = new List<string> { "Poção", "Munição", "Ferramenta" };

        Catalogo.FiltrarSubcategorias(disponiveis, "").Should().BeEquivalentTo(disponiveis);
    }

    [Fact]
    public void FiltrarSubcategorias_matches_a_substring_case_insensitively()
    {
        var disponiveis = new List<string> { "Poção", "Munição", "Ferramenta" };

        Catalogo.FiltrarSubcategorias(disponiveis, "ÇÃO").Should().BeEquivalentTo(new[] { "Poção", "Munição" });
    }

    [Fact]
    public void BuildQueryString_includes_nome_when_set()
    {
        Catalogo.BuildQueryString(nome: "Espada", tipo: "", subcategoria: null, rank: null, categoria: null, tipoDeDano: null)
            .Should().Be("?nome=Espada");
    }

    [Fact]
    public void BuildQueryString_omits_nome_when_blank()
    {
        Catalogo.BuildQueryString(nome: "  ", tipo: "", subcategoria: null, rank: null, categoria: null, tipoDeDano: null)
            .Should().Be("");
    }

    [Fact]
    public void BuildQueryString_combines_every_filter()
    {
        Catalogo.BuildQueryString(nome: "Espada", tipo: "Arma", subcategoria: "Lâmina", rank: "F", categoria: "Corte", tipoDeDano: "Cortante")
            .Should().Be("?nome=Espada&tipo=Arma&subcategoria=L%C3%A2mina&rank=F&categoria=Corte&tipoDeDano=Cortante");
    }

    [Fact]
    public async Task The_Rank_filter_is_a_select_of_Todos_plus_F_through_SS_that_filters_by_the_exact_Rank()
    {
        var requested = new List<string>();
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            requested.Add(request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
        });
        Services.AddScoped(_ => http);

        var cut = Render<Catalogo>();
        await Task.Delay(50);

        cut.FindComponents<MudBlazor.MudTextField<string>>().Should().NotContain(c => c.Instance.Label == "Rank");
        var rankSelect = cut.FindComponents<MudBlazor.MudSelect<string>>().Single(c => c.Instance.Label == "Rank");
        Catalogo.RankFiltroOptions.Should().BeEquivalentTo(
            new[] { ("", "Todos"), ("F", "F"), ("E", "E"), ("D", "D"), ("C", "C"), ("B", "B"), ("A", "A"), ("S", "S"), ("SS", "SS") },
            o => o.WithStrictOrdering());

        await cut.InvokeAsync(() => rankSelect.Instance.ValueChanged.InvokeAsync("SS"));

        requested.Should().Contain(q => q.EndsWith("items?rank=SS"));
    }

    private static RuinaRPG.Contracts.Items.ItemResponse Item(string id, string nome, List<string>? requisitos, List<string>? penalidade) => new(
        id, "Arma", nome, 1m, 10, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, false, null, null, requisitos, penalidade);

    [Fact]
    public async Task An_item_with_spelled_out_requirements_and_penalty_shows_both_and_one_without_shows_neither()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    Item("a", "Espada Pesada", ["Vigor ≥ 8"], ["Força −2"]),
                    Item("b", "Adaga", null, null),
                }),
            });
        Services.AddScoped(_ => http);

        var cut = Render<Catalogo>();
        await Task.Delay(50);

        var linhas = cut.FindAll("tbody tr");
        linhas.Should().HaveCount(2);
        linhas[0].TextContent.Should().Contain("Requisitos: Vigor ≥ 8").And.Contain("Penalidade: Força −2");
        linhas[1].TextContent.Should().NotContain("Requisitos:").And.NotContain("Penalidade:");
    }
}
