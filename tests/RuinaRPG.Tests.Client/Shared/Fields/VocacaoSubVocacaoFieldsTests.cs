using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class VocacaoSubVocacaoFieldsTests : MudBunitContext
{
    [Theory]
    [InlineData("Campeao", new[] { "Cavalheiro", "Duelista", "Paladino", "Lamina Holística", "Guardião" })]
    [InlineData("Cacador", new[] { "Arqueiro", "Domador", "Assassino" })]
    [InlineData("Feiticeiro", new[] { "Aeromante", "Biomante", "Fluxomante", "Piromante", "Sábio" })]
    [InlineData("Adepto", new[] { "Paladino", "Arauto", "Trovador", "Eremita" })]
    [InlineData("Bruxo", new[] { "Ecomante", "Hemomante", "Osteomante", "Nexomante", "Cultista" })]
    public void SubVocacao_options_are_scoped_to_the_selected_Vocacao(string vocacao, string[] expected)
    {
        var cut = Render<VocacaoSubVocacaoFields>(p => p
            .Add(x => x.Vocacao, vocacao)
            .Add(x => x.SubVocacao, (string?)null));

        cut.Instance.SubVocacaoOptions().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Changing_Vocacao_clears_a_now_invalid_SubVocacao()
    {
        string? newSubVocacao = "not-cleared-yet";
        var cut = Render<VocacaoSubVocacaoFields>(p => p
            .Add(x => x.Vocacao, "Campeao")
            .Add(x => x.SubVocacao, "Duelista")
            .Add(x => x.SubVocacaoChanged, v => newSubVocacao = v));

        await cut.InvokeAsync(() => cut.Instance.SetVocacaoForTests("Bruxo"));

        newSubVocacao.Should().BeNull();
    }
}
