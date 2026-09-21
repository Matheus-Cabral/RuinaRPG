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

    private static string[] Disponiveis(string? vocacao, string? elemento, string? caminho, string? atual = null) =>
        AfinidadeEssenciaField.SubElementosDisponiveis(vocacao, elemento, caminho, atual).Select(o => o.Valor).ToArray();

    [Fact]
    public void SubElementosDisponiveis_offers_Curar_only_with_Fogo_and_the_Vida_Caminho()
    {
        Disponiveis("Adepto", "Fogo", "Vida").Should().Contain("Curar");
        Disponiveis("Adepto", "Fogo", "Mundano").Should().NotContain("Curar");
        Disponiveis("Adepto", "Fogo", null).Should().NotContain("Curar");
        Disponiveis("Adepto", null, "Vida").Should().NotContain("Curar");
        Disponiveis("Adepto", "Ar", "Vida").Should().NotContain("Curar");
    }

    [Fact]
    public void SubElementosDisponiveis_offers_only_the_gated_SubElementos_that_match_the_Elemento_and_Caminho()
    {
        Disponiveis("Adepto", "Ar", "Alma").Should().Contain("Prever").And.NotContain("Purificar");
        Disponiveis("Adepto", "Agua", "Alma").Should().Contain("Purificar").And.NotContain("Prever");
        Disponiveis("Adepto", "Terra", "Vida").Should().Contain("Aprimorar").And.NotContain("Curar");
    }

    [Fact]
    public void SubElementosDisponiveis_keeps_the_ungated_SubElementos_the_Vocacao_allows_regardless_of_Elemento_and_Caminho()
    {
        Disponiveis("Adepto", null, null).Should().BeEquivalentTo(["Alma", "Vida"]);
        Disponiveis("Feiticeiro", null, null).Should().BeEquivalentTo(["Gelo", "Flora", "Ferro", "Raio"]);
    }

    [Fact]
    public void SubElementosDisponiveis_still_filters_by_Vocacao()
    {
        // Necromancia é de Maculação: Bruxo libera, Adepto não — mesmo com Fogo + Mundano.
        Disponiveis("Bruxo", "Fogo", "Mundano").Should().Contain("Necromancia");
        Disponiveis("Adepto", "Fogo", "Mundano").Should().NotContain("Necromancia");
        Disponiveis("Campeao", "Fogo", "Vida").Should().BeEmpty();
        Disponiveis(null, "Fogo", "Vida").Should().BeEmpty();
    }

    [Fact]
    public void SubElementosDisponiveis_keeps_the_currently_saved_value_so_the_select_never_shows_blank()
    {
        Disponiveis("Adepto", "Fogo", "Mundano", atual: "Curar").Should().Contain("Curar");
    }
}
