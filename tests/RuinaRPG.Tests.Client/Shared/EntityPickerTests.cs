using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class EntityPickerTests : MudBunitContext
{
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
    public async Task A_blank_query_still_calls_SearchItems_so_opening_the_field_lists_everything()
    {
        // MudAutocomplete's defaults (MinCharacters=0, OpenOnFocus=true) already search with the
        // empty string as soon as the field opens — SearchItems must see that blank query (every
        // backend "nome" filter already treats it as "no filter", i.e. return everything for this
        // GM) instead of the component swallowing it and showing an empty dropdown until typed in.
        var searchItemsCalled = false;
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, query =>
            {
                searchItemsCalled = true;
                return Task.FromResult(new List<PickerOption> { new("id-1", $"matched '{query}'") });
            }));

        var results = await cut.Instance.SearchAsyncForTests("   ");

        searchItemsCalled.Should().BeTrue();
        results.Should().ContainSingle(o => o.Id == "id-1");
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
    public void Does_not_cap_the_number_of_results_shown()
    {
        // MudAutocomplete's MaxItems defaults to 10, silently hiding any match past the 10th —
        // every search field in the app goes through this one component, so it must ask for the
        // full list (MaxItems = null) rather than let a common query truncate results.
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>())));

        cut.FindComponent<MudAutocomplete<PickerOption>>().Instance.MaxItems.Should().BeNull();
    }

    [Fact]
    public void Caller_resetting_Value_to_empty_clears_the_confirmed_selection_display()
    {
        var cut = Render<EntityPicker>(p => p
            .Add(x => x.Value, "id-1")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>())));
        cut.Instance.SetSelectedLabelForTests("Espada Longa"); // simulate a prior confirmed selection

        cut.Render(p => p.Add(x => x.Value, ""));
        cut.Render(p => p.Add(x => x.Value, "id-2")); // a different selection, made externally without going through SelectAsync

        cut.Markup.Should().NotContain("Espada Longa"); // proves _selectedLabel was actually cleared, not just that Value=="" alone hid it
    }
}
