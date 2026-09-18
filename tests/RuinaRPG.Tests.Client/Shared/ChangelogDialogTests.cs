using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ChangelogDialogTests : MudBunitContext
{
    // ChangelogDialog's inline <MudDialog> only renders its content through a MudDialogProvider
    // present elsewhere in the render tree (the real app has one in MainLayout) — bUnit's TestContext
    // starts with none, so every test renders one alongside the dialog via this composite fragment.
    private IRenderedComponent<ContainerFragment> RenderDialog(string version, EventCallback onDismissed) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChangelogDialog>(1);
            builder.AddAttribute(2, nameof(ChangelogDialog.Version), version);
            builder.AddAttribute(3, nameof(ChangelogDialog.OnDismissed), onDismissed);
            builder.CloseComponent();
        });

    [Fact]
    public void Rendering_the_dialog_does_not_invoke_OnDismissed()
    {
        var dismissedCount = 0;

        RenderDialog("1.2.0", EventCallback.Factory.Create(this, () => dismissedCount++));

        dismissedCount.Should().Be(0);
    }

    [Fact]
    public void Clicking_Fechar_invokes_OnDismissed_exactly_once()
    {
        var dismissedCount = 0;
        var cut = RenderDialog("1.2.0", EventCallback.Factory.Create(this, () => dismissedCount++));

        cut.Find("button:contains('Fechar')").Click();

        dismissedCount.Should().Be(1);
    }

    [Fact]
    public void Renders_the_Version_parameter_in_the_title_not_a_hardcoded_literal()
    {
        var cut = RenderDialog("9.9.9", EventCallback.Factory.Create(this, () => { }));

        cut.Markup.Should().Contain("Novidades da Versão 9.9.9");
        cut.Markup.Should().NotContain("1.2.0");
    }
}
