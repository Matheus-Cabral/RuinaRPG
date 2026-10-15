using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Condicoes;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CondicaoIconeTests
{
    [Fact]
    public void Every_key_the_API_accepts_has_a_MudBlazor_icon_and_a_display_name()
    {
        foreach (var chave in CondicaoIcones.Chaves)
        {
            CondicaoIcone.Icone(chave).Should().NotBeNullOrWhiteSpace($"a chave {chave} precisa de um ícone");
            CondicaoIcone.Nome(chave).Should().NotBeNullOrWhiteSpace($"a chave {chave} precisa de um nome");
        }
    }

    [Fact]
    public void The_mapping_has_no_key_the_API_would_refuse()
    {
        CondicaoIcone.Chaves.Should().BeEquivalentTo(CondicaoIcones.Chaves);
    }

    // As 20 primeiras já estão guardadas em Condições do catálogo: não mudam, nem de ordem; as novas vêm depois.
    private static readonly string[] Originais =
    [
        "Veneno", "Fogo", "Gelo", "Atordoado", "Sangramento", "Escudo", "Lentidao", "Cegueira", "Medo", "Bencao",
        "Maldicao", "Sono", "Enraizado", "Enfraquecido", "Fortalecido", "Queimadura", "Eletrificado", "Silenciado",
        "Invisivel", "Marcado"
    ];

    [Fact]
    public void The_set_is_large_keeps_the_original_keys_first_and_matches_the_API_list_in_order()
    {
        CondicaoIcones.Chaves.Count.Should().BeGreaterThanOrEqualTo(80);
        CondicaoIcones.Chaves.Take(Originais.Length).Should().Equal(Originais);
        CondicaoIcone.Chaves.Should().Equal(CondicaoIcones.Chaves);
    }

    [Fact]
    public void Keys_are_ascii_identifiers_and_names_and_icons_are_all_distinct()
    {
        CondicaoIcones.Chaves.Should().OnlyHaveUniqueItems()
            .And.OnlyContain(c => System.Text.RegularExpressions.Regex.IsMatch(c, "^[A-Z][A-Za-z]*$"));
        CondicaoIcones.Chaves.Select(CondicaoIcone.Nome).Should().OnlyHaveUniqueItems();
        CondicaoIcones.Chaves.Select(CondicaoIcone.Icone).Should().OnlyHaveUniqueItems()
            .And.NotContain(MudBlazor.Icons.Material.Filled.Coronavirus, "o genérico é o da chave desconhecida");
    }

    [Fact]
    public void An_unknown_key_falls_back_to_a_generic_icon_instead_of_throwing()
    {
        CondicaoIcone.Icone("Inexistente").Should().NotBeNullOrWhiteSpace();
        CondicaoIcone.Nome("Inexistente").Should().Be("Inexistente");
    }
}
