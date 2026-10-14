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

    [Fact]
    public void An_unknown_key_falls_back_to_a_generic_icon_instead_of_throwing()
    {
        CondicaoIcone.Icone("Inexistente").Should().NotBeNullOrWhiteSpace();
        CondicaoIcone.Nome("Inexistente").Should().Be("Inexistente");
    }
}
