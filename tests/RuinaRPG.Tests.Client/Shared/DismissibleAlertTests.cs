using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class DismissibleAlertTests : MudBunitContext
{
    [Fact]
    public void Renders_nothing_when_Message_is_null()
    {
        var cut = Render<DismissibleAlert>(p => p.Add(x => x.Message, (string?)null));

        cut.FindAll(".mud-alert").Should().BeEmpty();
    }

    [Fact]
    public void Renders_nothing_when_Message_is_empty()
    {
        var cut = Render<DismissibleAlert>(p => p.Add(x => x.Message, ""));

        cut.FindAll(".mud-alert").Should().BeEmpty();
    }

    [Fact]
    public void Renders_the_message_as_a_Filled_alert_for_good_contrast()
    {
        // Variant.Filled (not MudAlert's own Variant.Text default) is the whole point of this
        // component — a bare MudAlert defaults to mud-alert-text-error, a thin colored-text style
        // with poor contrast in both themes; Filled gives a solid background + the themed
        // --mud-palette-error-text white/high-contrast text this app already bridges.
        var cut = Render<DismissibleAlert>(p => p.Add(x => x.Message, "Algo deu errado."));

        cut.Markup.Should().Contain("mud-alert-filled-error");
        cut.Markup.Should().Contain("Algo deu errado.");
    }

    [Fact]
    public void Honors_a_non_default_Severity()
    {
        var cut = Render<DismissibleAlert>(p => p
            .Add(x => x.Message, "Aviso.")
            .Add(x => x.Severity, Severity.Warning));

        cut.Markup.Should().Contain("mud-alert-filled-warning");
    }

    [Fact]
    public void Clicking_the_close_icon_clears_the_bound_Message()
    {
        string? message = "Falhou.";
        var cut = Render<DismissibleAlert>(p => p
            .Add(x => x.Message, message)
            .Add(x => x.MessageChanged, v => message = v));

        cut.Find(".mud-alert-close-button").Click();

        message.Should().BeNull();
    }
}
