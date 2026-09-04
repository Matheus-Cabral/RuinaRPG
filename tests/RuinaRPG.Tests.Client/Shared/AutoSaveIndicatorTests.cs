using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class AutoSaveIndicatorTests : MudBunitContext
{
    [Fact]
    public void Idle_renders_nothing()
    {
        var cut = Render<AutoSaveIndicator>(p => p.Add(x => x.State, AutoSaveState.Idle));

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public void Saving_shows_the_in_progress_label()
    {
        var cut = Render<AutoSaveIndicator>(p => p.Add(x => x.State, AutoSaveState.Saving));

        cut.Markup.Should().Contain("Salvando...");
    }

    [Fact]
    public void Saved_shows_the_last_saved_time()
    {
        var cut = Render<AutoSaveIndicator>(p => p
            .Add(x => x.State, AutoSaveState.Saved)
            .Add(x => x.LastSavedAt, new DateTime(2026, 1, 1, 14, 32, 0)));

        cut.Markup.Should().Contain("Salvo às 14:32");
    }

    [Fact]
    public void Error_shows_the_failure_label()
    {
        var cut = Render<AutoSaveIndicator>(p => p.Add(x => x.State, AutoSaveState.Error));

        cut.Markup.Should().Contain("Erro ao salvar");
    }
}
