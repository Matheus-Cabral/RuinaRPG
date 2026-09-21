using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.CharacterSheets;
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

    private static readonly VarianteLiberadaResponse[] SolarLiberada = [new("AloraSolar", "Alóra Solar")];

    [Fact]
    public void The_solar_Alora_is_not_offered_until_the_GM_names_it()
    {
        var cut = Render<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, "Econos")
            .Add(x => x.Variante, (string?)null));

        cut.Instance.VarianteOptions().Select(o => o.Valor).Should().Equal("Alora");
    }

    [Fact]
    public void Once_named_the_solar_Alora_is_offered_under_the_GMs_name()
    {
        var cut = Render<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, "Econos")
            .Add(x => x.Variante, (string?)null)
            .Add(x => x.VariantesLiberadas, SolarLiberada));

        cut.Instance.VarianteOptions().Should().Contain(o => o.Valor == "AloraSolar" && o.Rotulo == "Alóra Solar" && o.Polaridade == "Sol");
    }

    [Fact]
    public void A_sheet_already_on_the_solar_Alora_keeps_seeing_it_even_if_the_GM_removed_the_name()
    {
        var cut = Render<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, "Econos")
            .Add(x => x.Variante, "AloraSolar"));

        cut.Instance.VarianteOptions().Should().Contain(o => o.Valor == "AloraSolar" && o.Rotulo == "Alóra (Sol)");
    }

    [Fact]
    public void A_named_variant_never_leaks_into_another_Linhagem()
    {
        var cut = Render<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, "Humano")
            .Add(x => x.Variante, (string?)null)
            .Add(x => x.VariantesLiberadas, SolarLiberada));

        cut.Instance.VarianteOptions().Select(o => o.Valor).Should().BeEquivalentTo("Sinir", "Laonir");
    }
}

