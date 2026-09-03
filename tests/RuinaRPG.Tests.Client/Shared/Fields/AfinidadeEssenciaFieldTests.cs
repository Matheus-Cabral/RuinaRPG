using Bunit;
using FluentAssertions;
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
    public void SubElementos_has_the_14_sub_elements_of_the_Matriz_Elemental()
    {
        AfinidadeEssenciaField.SubElementos.Should().HaveCount(14);
        AfinidadeEssenciaField.SubElementos.Select(o => o.Valor).Should().Contain("Invocacao");
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
            .Add(x => x.Label, "Sub-Elemento")
            .Add(x => x.Opcoes, AfinidadeEssenciaField.SubElementos)
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
}
