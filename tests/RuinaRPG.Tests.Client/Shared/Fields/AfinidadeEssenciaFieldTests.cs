using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class AfinidadeEssenciaFieldTests : MudBunitContext
{
    [Fact]
    public void Elementos_has_the_4_elements_of_the_Matriz_Elemental()
    {
        AfinidadeEssenciaField.Elementos.Select(o => o.Valor).Should().BeEquivalentTo(["Ar", "Agua", "Fogo", "Terra"]);
    }

    [Fact]
    public async Task Changing_the_name_raises_NomeChanged_with_the_new_value()
    {
        string? newNome = null;
        var cut = Render<AfinidadeEssenciaField>(p => p
            .Add(x => x.Label, "Elemento")
            .Add(x => x.Opcoes, AfinidadeEssenciaField.Elementos)
            .Add(x => x.Nome, (string?)null)
            .Add(x => x.NomeChanged, v => newNome = v));

        await cut.InvokeAsync(() => cut.Instance.SetNomeForTests("Fogo"));

        newNome.Should().Be("Fogo");
    }

    [Fact]
    public async Task Changing_the_value_raises_ValorChanged_independently_of_the_name()
    {
        int? newValor = -1;
        var cut = Render<AfinidadeEssenciaField>(p => p
            .Add(x => x.Label, "Essência 2")
            .Add(x => x.Opcoes, AfinidadeEssenciaField.EssenciasBasicas)
            .Add(x => x.Nome, "Vida")
            .Add(x => x.Valor, (int?)null)
            .Add(x => x.ValorChanged, v => newValor = v));

        await cut.InvokeAsync(() => cut.Instance.SetValorForTests(7));

        newValor.Should().Be(7);
    }

    [Fact]
    public async Task The_value_can_be_cleared_back_to_null()
    {
        int? newValor = 3;
        var cut = Render<AfinidadeEssenciaField>(p => p
            .Add(x => x.Label, "Elemento")
            .Add(x => x.Opcoes, AfinidadeEssenciaField.Elementos)
            .Add(x => x.Valor, 3)
            .Add(x => x.ValorChanged, v => newValor = v));

        await cut.InvokeAsync(() => cut.Instance.SetValorForTests(null));

        newValor.Should().BeNull();
    }

    [Theory]
    [InlineData("Fogo", new[] { "Ar", "Terra", "Vida", "Mundano" })]
    [InlineData("Agua", new[] { "Ar", "Terra", "Alma", "Mundano" })]
    public void OpcoesSegundaEssencia_follow_the_Matriz_for_the_chosen_Essencia1(string elemento, string[] esperado)
    {
        AfinidadeEssenciaField.OpcoesSegundaEssencia(elemento, atual: null).Select(o => o.Valor).Should().Equal(esperado);
    }

    [Fact]
    public void OpcoesSegundaEssencia_is_empty_without_Essencia1()
    {
        AfinidadeEssenciaField.OpcoesSegundaEssencia(null, atual: null).Should().BeEmpty();
    }

    [Fact]
    public void OpcoesSegundaEssencia_labels_Agua_with_its_accent()
    {
        AfinidadeEssenciaField.OpcoesSegundaEssencia("Ar", atual: null).Should().Contain(("Agua", "Água"));
    }

    [Fact]
    public void Disabled_disables_the_name_select()
    {
        var cut = Render<AfinidadeEssenciaField>(p => p
            .Add(x => x.Label, "Essência 2")
            .Add(x => x.Opcoes, Array.Empty<(string, string)>())
            .Add(x => x.Disabled, true));

        cut.FindComponent<MudSelect<string>>().Instance.Disabled.Should().BeTrue();
    }

    [Fact]
    public void Clearable_is_passed_to_the_name_select()
    {
        var cut = Render<AfinidadeEssenciaField>(p => p
            .Add(x => x.Label, "Essência 2")
            .Add(x => x.Opcoes, AfinidadeEssenciaField.EssenciasBasicas)
            .Add(x => x.Clearable, true));

        cut.FindComponent<MudSelect<string>>().Instance.Clearable.Should().BeTrue();
    }

    [Fact]
    public void The_name_select_is_not_clearable_by_default()
    {
        var cut = Render<AfinidadeEssenciaField>(p => p
            .Add(x => x.Label, "Elemento")
            .Add(x => x.Opcoes, AfinidadeEssenciaField.Elementos));

        cut.FindComponent<MudSelect<string>>().Instance.Clearable.Should().BeFalse();
    }
}
