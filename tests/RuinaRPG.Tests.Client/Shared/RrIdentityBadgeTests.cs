using Bunit;
using FluentAssertions;
using MudBlazor.Services;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class RrIdentityBadgeTests : BunitContext, IAsyncLifetime
{
    public RrIdentityBadgeTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // Same MudBlazor-DI-teardown fix as Task 7's EntityPickerTests and Task 8's field tests.
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Theory]
    [InlineData("Humano", "Sinir", "Humano", "Sol")]
    [InlineData("Humano", "Laonir", "Humano", "Lua")]
    [InlineData("Phylauc", "EsPhylauc", "Phylauc", "Lua")]
    [InlineData("Nephrytes", "Koroanos", "Nephrytes", "Sol")]
    [InlineData("Econos", "Alora", "Econos", "Lua")]
    public void Renders_the_right_glyph_and_polaridade_for_a_known_pair(
        string linhagem, string variante, string expectedGlyphKey, string expectedPolaridade)
    {
        var cut = Render<RrIdentityBadge>(p => p
            .Add(x => x.Linhagem, linhagem)
            .Add(x => x.Variante, variante));

        cut.Find(".rr-identity-badge").ClassList.Should().Contain($"rr-glyph-{expectedGlyphKey.ToLowerInvariant()}");
        cut.Find(".rr-identity-badge").ClassList.Should().Contain($"rr-polaridade-{expectedPolaridade.ToLowerInvariant()}");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Humano", null)]
    [InlineData(null, "Sinir")]
    [InlineData("Humano", "NaoExiste")]
    public void Renders_a_neutral_placeholder_for_an_unknown_or_incomplete_pair(string? linhagem, string? variante)
    {
        var cut = Render<RrIdentityBadge>(p => p
            .Add(x => x.Linhagem, linhagem)
            .Add(x => x.Variante, variante));

        cut.Find(".rr-identity-badge").ClassList.Should().Contain("rr-glyph-none");
    }
}
