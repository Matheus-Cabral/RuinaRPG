using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class CoberturaRatingTests : MudBunitContext
{
    private (IRenderedComponent<CoberturaRating> Cut, List<string> Emitted) Render(string? value)
    {
        var emitted = new List<string>();
        var cut = Render<CoberturaRating>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<string>(this, v => emitted.Add(v))));
        return (cut, emitted);
    }

    private static IReadOnlyList<AngleSharp.Dom.IElement> Escudos(IRenderedComponent<CoberturaRating> cut) => cut.FindAll("button");

    private static int Acesos(IRenderedComponent<CoberturaRating> cut) => Escudos(cut).Count(b => b.GetAttribute("aria-pressed") == "true");

    [Theory]
    [InlineData(null, 0, "Sem cobertura")]
    [InlineData("Nenhuma", 0, "Sem cobertura")]
    [InlineData("Parcial", 1, "Parcial (+5)")]
    [InlineData("Completa", 2, "Completa (+10)")]
    public void Renders_the_label_the_state_text_and_the_lit_shields(string? value, int acesos, string estado)
    {
        var (cut, _) = Render(value);

        cut.Markup.Should().Contain("Cobertura");
        cut.Find(".cobertura-rating-estado").TextContent.Should().Be(estado);
        Escudos(cut).Should().HaveCount(2);
        Acesos(cut).Should().Be(acesos);
        // Os acesos são sempre os primeiros: 1 = [aceso, apagado], 2 = [aceso, aceso].
        Escudos(cut).Take(acesos).All(b => b.GetAttribute("aria-pressed") == "true").Should().BeTrue();
    }

    [Fact]
    public void Each_shield_has_an_accessible_name()
    {
        var (cut, _) = Render(null);

        var nomes = Escudos(cut).Select(b => b.GetAttribute("aria-label")).ToList();
        nomes.Should().Equal("Cobertura parcial (+5)", "Cobertura completa (+10)");
    }

    [Theory]
    [InlineData("Nenhuma", 0, "Parcial")]
    [InlineData("Nenhuma", 1, "Completa")]
    [InlineData(null, 0, "Parcial")]
    [InlineData("Parcial", 1, "Completa")]
    [InlineData("Parcial", 0, "Nenhuma")]   // reclicar o valor atual limpa
    [InlineData("Completa", 1, "Nenhuma")]  // reclicar o valor atual limpa
    [InlineData("Completa", 0, "Parcial")]
    public void Clicking_a_shield_emits_the_matching_value(string? atual, int escudo, string esperado)
    {
        var (cut, emitted) = Render(atual);

        Escudos(cut)[escudo].Click();

        emitted.Should().Equal(esperado);
    }
}
