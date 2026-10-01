using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PericiasIniciaisTests
{
    [Fact]
    public void IdPorNome_resolves_the_display_name()
    {
        PericiasIniciais.IdPorNome("Empatia c/ Animais").Should().Be(14);
    }

    [Fact]
    public void Protected_ids_are_Fortitude_Prontidao_Reflexos()
    {
        PericiasDeSistema.Todas.Select(id => PericiasIniciais.Todas.Single(p => p.Id == id).Chave)
            .Should().BeEquivalentTo("Fortitude", "Prontidao", "Reflexos");
        PericiasDeSistema.IsProtegida(7).Should().BeFalse();
    }
}
