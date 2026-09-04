using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CopyableCodeTests : MudBunitContext
{
    [Fact]
    public void Renders_the_value_as_the_chips_visible_text()
    {
        var cut = Render<CopyableCode>(p => p.Add(x => x.Value, "ABCD1234"));

        cut.Markup.Should().Contain("ABCD1234");
    }

    [Fact]
    public async Task A_successful_copy_shows_a_success_snackbar()
    {
        // navigator.clipboard requires a secure context (HTTPS or localhost) — plain-HTTP LAN
        // access has no such API, so the component goes through ruinaClipboard.copy
        // (wwwroot/js/clipboard.js), which falls back to document.execCommand('copy') there.
        JSInterop.Setup<bool>("ruinaClipboard.copy", "ABCD1234").SetResult(true);
        var cut = Render<CopyableCode>(p => p.Add(x => x.Value, "ABCD1234"));

        await cut.InvokeAsync(() => cut.Instance.CopyForTests());

        var snackbar = Services.GetRequiredService<ISnackbar>();
        snackbar.ShownSnackbars.Should().ContainSingle(s => s.Severity == Severity.Success && s.Message == "Código copiado!");
    }

    [Fact]
    public async Task A_failed_copy_shows_a_warning_snackbar_with_a_manual_fallback_hint()
    {
        JSInterop.Setup<bool>("ruinaClipboard.copy", "ABCD1234").SetResult(false);
        var cut = Render<CopyableCode>(p => p.Add(x => x.Value, "ABCD1234"));

        await cut.InvokeAsync(() => cut.Instance.CopyForTests());

        var snackbar = Services.GetRequiredService<ISnackbar>();
        snackbar.ShownSnackbars.Should().ContainSingle(s => s.Severity == Severity.Warning);
    }
}
