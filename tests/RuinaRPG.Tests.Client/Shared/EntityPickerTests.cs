using Bunit;
using FluentAssertions;
using MudBlazor.Services;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class EntityPickerTests : BunitContext, IAsyncLifetime
{
    public EntityPickerTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // MudBlazor registers at least one DI service (PointerEventsNoneService) that only implements
    // IAsyncDisposable. xUnit's default synchronous IDisposable.Dispose() teardown can't dispose
    // that cleanly, so route teardown through IAsyncLifetime.DisposeAsync() -> BunitContext's own
    // async-safe disposal instead.
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task SearchFunc_delegates_to_the_caller_supplied_SearchItems_for_a_non_empty_query()
    {
        // MudAutocomplete owns its own popover/debounce internally — not something bUnit's
        // synchronous TestContext can drive through real elapsed time or a real dropdown click.
        // This test (and the two below) exercise EntityPicker's own logic directly through its
        // test-only seams instead of trying to reproduce MudAutocomplete's internal rendering.
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, query => Task.FromResult(new List<PickerOption>
            {
                new("id-1", $"Espada Longa (matched '{query}')"),
            })));

        var results = await cut.Instance.SearchAsyncForTests("esp");

        results.Should().ContainSingle(o => o.Id == "id-1" && o.Label.Contains("'esp'"));
    }

    [Fact]
    public async Task SearchFunc_returns_no_results_for_a_blank_query_without_calling_SearchItems()
    {
        var searchItemsCalled = false;
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ =>
            {
                searchItemsCalled = true;
                return Task.FromResult(new List<PickerOption>());
            }));

        var results = await cut.Instance.SearchAsyncForTests("   ");

        results.Should().BeEmpty();
        searchItemsCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Selecting_a_result_sets_Value_and_switches_off_the_search_input()
    {
        string? boundValue = null;
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.ValueChanged, v => boundValue = v)
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>())));

        await cut.InvokeAsync(() => cut.Instance.SelectForTests(new PickerOption("id-1", "Espada Longa")));

        boundValue.Should().Be("id-1");
        cut.Markup.Should().Contain("Espada Longa");
        cut.Markup.Should().NotContain("<input");
    }

    [Fact]
    public void Caller_resetting_Value_to_empty_clears_the_confirmed_selection_display()
    {
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "id-1")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>())));
        cut.Render(p => p.Add(x => x.Value, "id-1"));
        cut.Instance.SetSelectedLabelForTests("Espada Longa"); // simulate a prior confirmed selection

        cut.Render(p => p.Add(x => x.Value, ""));

        cut.Markup.Should().Contain("<input");
    }
}
