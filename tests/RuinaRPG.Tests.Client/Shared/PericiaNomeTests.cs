using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class PericiaNomeTests : MudBunitContext
{
    private IRenderedComponent<ContainerFragment> RenderNome(string nome, string? descricao) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<PericiaNome>(2);
            builder.AddAttribute(3, nameof(PericiaNome.Nome), nome);
            builder.AddAttribute(4, nameof(PericiaNome.Descricao), descricao);
            builder.CloseComponent();
        });

    [Fact]
    public void Without_description_renders_plain_text_and_no_tooltip()
    {
        var cut = RenderNome("Atletismo", null);

        cut.Markup.Should().Contain("Atletismo");
        cut.FindComponents<MudTooltip>().Should().BeEmpty();
        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void With_description_has_a_tooltip_with_the_text()
    {
        var cut = RenderNome("Atletismo", "Correr e saltar.");

        cut.FindComponent<MudTooltip>().Instance.Text.Should().Be("Correr e saltar.");
    }

    [Fact]
    public void Clicking_the_name_opens_a_popup_with_the_description()
    {
        var cut = RenderNome("Atletismo", "Correr e saltar.");

        cut.Find("button[aria-label='Descrição de Atletismo']").Click();

        cut.FindAll(".mud-dialog").Should().ContainSingle();
        cut.Find(".mud-dialog").TextContent.Should().Contain("Correr e saltar.");
    }
}
