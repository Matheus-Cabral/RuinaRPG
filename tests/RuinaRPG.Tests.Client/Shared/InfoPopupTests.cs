using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class InfoPopupTests : MudBunitContext
{
    // InfoPopup's inline <MudDialog> only renders its content through a MudDialogProvider present
    // elsewhere in the render tree (the real app has one in MainLayout) — bUnit's TestContext starts
    // with none, so every test renders one alongside InfoPopup via this composite fragment (same
    // idiom as ChangelogDialogTests/CatalogoItemPickerTests).
    private IRenderedComponent<ContainerFragment> RenderPopup(string title, string content) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<InfoPopup>(1);
            builder.AddAttribute(2, nameof(InfoPopup.Title), title);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, content)));
            builder.CloseComponent();
        });

    [Fact]
    public void Renders_the_info_button_with_the_title_as_title_and_aria_label()
    {
        var cut = RenderPopup("Como criar uma Magia/Habilidade", "conteúdo qualquer");

        var button = cut.Find("button");
        button.GetAttribute("title").Should().Be("Como criar uma Magia/Habilidade");
        button.GetAttribute("aria-label").Should().Be("Como criar uma Magia/Habilidade");
    }

    [Fact]
    public void Dialog_is_closed_initially()
    {
        var cut = RenderPopup("Título do popup", "Texto que só deve aparecer quando aberto");

        cut.Markup.Should().NotContain("Texto que só deve aparecer quando aberto");
    }

    [Fact]
    public void Clicking_the_info_button_opens_the_dialog_showing_title_and_content()
    {
        var cut = RenderPopup("Título do popup", "Texto que só deve aparecer quando aberto");

        cut.Find("button").Click();

        cut.Markup.Should().Contain("Título do popup");
        cut.Markup.Should().Contain("Texto que só deve aparecer quando aberto");
    }

    [Fact]
    public void Clicking_Fechar_closes_the_dialog()
    {
        var cut = RenderPopup("Título do popup", "Texto que só deve aparecer quando aberto");
        cut.Find("button").Click();

        cut.Find("button:contains('Fechar')").Click();

        cut.Markup.Should().NotContain("Texto que só deve aparecer quando aberto");
    }
}
