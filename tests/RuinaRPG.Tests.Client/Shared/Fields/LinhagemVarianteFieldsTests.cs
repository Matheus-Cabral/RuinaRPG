using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class LinhagemVarianteFieldsTests : MudBunitContext
{
    [Theory]
    [InlineData("Humano", new[] { "Sinir", "Laonir" })]
    [InlineData("Phylauc", new[] { "PhylacTai", "EsPhylauc" })]
    [InlineData("Nephrytes", new[] { "Koroanos", "Yavos" })]
    [InlineData("Econos", new[] { "Alora" })]
    public void Variante_options_are_scoped_to_the_selected_Linhagem(string linhagem, string[] expectedVariantes)
    {
        var cut = Render<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, linhagem)
            .Add(x => x.Variante, (string?)null));

        var options = cut.Instance.VarianteOptions().Select(o => o.Valor);

        options.Should().BeEquivalentTo(expectedVariantes);
    }

    [Fact]
    public async Task Changing_Linhagem_clears_a_now_invalid_Variante()
    {
        string? newVariante = "not-cleared-yet";
        var cut = Render<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, "Humano")
            .Add(x => x.Variante, "Sinir")
            .Add(x => x.VarianteChanged, v => newVariante = v));

        await cut.InvokeAsync(() => cut.Instance.SetLinhagemForTests("Nephrytes"));

        newVariante.Should().BeNull();
    }

    [Fact]
    public void No_Linhagem_selected_yields_no_Variante_options()
    {
        var cut = Render<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, (string?)null)
            .Add(x => x.Variante, (string?)null));

        cut.Instance.VarianteOptions().Should().BeEmpty();
    }
}
