using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ArcaEvolucoesButtonTests : MudBunitContext
{
    private IRenderedComponent<ContainerFragment> RenderButton(List<ArcaEvolucaoResponse> evolucoes) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ArcaEvolucoesButton>(1);
            builder.AddAttribute(2, nameof(ArcaEvolucoesButton.Evolucoes), evolucoes);
            builder.CloseComponent();
        });

    private static AngleSharp.Dom.IElement MainButton(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Contains("Evoluções da Arca"));

    [Fact]
    public void Shows_the_count_and_is_disabled_when_there_are_none()
    {
        var cut = RenderButton(new());

        MainButton(cut).TextContent.Should().Contain("Evoluções da Arca (0)");
        MainButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Clicking_opens_a_dialog_listing_each_level_and_text()
    {
        var cut = RenderButton(new() { new(Guid.NewGuid(), 2, "dois"), new(Guid.NewGuid(), 5, "cinco") });

        MainButton(cut).TextContent.Should().Contain("Evoluções da Arca (2)");
        MainButton(cut).Click();

        cut.Markup.Should().Contain("Nível 2").And.Contain("dois").And.Contain("Nível 5").And.Contain("cinco");
    }
}
