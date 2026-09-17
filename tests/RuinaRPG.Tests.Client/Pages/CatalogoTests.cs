using FluentAssertions;
using RuinaRPG.Client.Pages;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class CatalogoTests
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
        Catalogo.BuildQueryString(nome: "Espada", tipo: "", subcategoria: null, tier: null, categoria: null, tipoDeDano: null)
            .Should().Be("?nome=Espada");
    }

    [Fact]
    public void BuildQueryString_omits_nome_when_blank()
    {
        Catalogo.BuildQueryString(nome: "  ", tipo: "", subcategoria: null, tier: null, categoria: null, tipoDeDano: null)
            .Should().Be("");
    }

    [Fact]
    public void BuildQueryString_combines_every_filter()
    {
        Catalogo.BuildQueryString(nome: "Espada", tipo: "Arma", subcategoria: "Lâmina", tier: "1", categoria: "Corte", tipoDeDano: "Cortante")
            .Should().Be("?nome=Espada&tipo=Arma&subcategoria=L%C3%A2mina&tier=1&categoria=Corte&tipoDeDano=Cortante");
    }
}
