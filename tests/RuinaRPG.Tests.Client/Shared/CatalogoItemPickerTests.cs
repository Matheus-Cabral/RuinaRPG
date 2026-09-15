using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Items;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CatalogoItemPickerTests : MudBunitContext
{
    private static HttpClient EmptyListClient() => FakeHttpMessageHandler.CreateClient(request =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) });

    // CatalogoItemPicker's inline <MudDialog> only renders its content through a MudDialogProvider
    // present elsewhere in the render tree (the real app has one in MainLayout) — bUnit's TestContext
    // starts with none, so every test renders one alongside the picker via this host.
    private IRenderedComponent<Host> RenderPicker(Action<ComponentParameterCollectionBuilder<Host>> parameterBuilder) =>
        Render<Host>(parameterBuilder);

    // EntityPicker's own MudAutocomplete always renders an internal MudIconButton for its dropdown
    // arrow, unrelated to our "+" create button — so these assertions scope to the Add icon
    // specifically instead of asserting on every MudIconButton in the render tree.
    private static IEnumerable<IRenderedComponent<MudIconButton>> CreateButtons(IRenderedComponent<Host> cut) =>
        cut.FindComponents<MudIconButton>().Where(b => b.Instance.Icon == Icons.Material.Filled.Add);

    [Fact]
    public void PodeCriar_false_hides_the_create_button()
    {
        var cut = RenderPicker(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, false));

        CreateButtons(cut).Should().BeEmpty();
    }

    [Fact]
    public void PodeCriar_true_shows_the_create_button_and_opening_the_dialog_passes_the_Tipo_through()
    {
        Services.AddScoped(_ => EmptyListClient());

        var cut = RenderPicker(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, true)
            .Add(x => x.Tipo, "Arma"));

        CreateButtons(cut).Should().ContainSingle();

        cut.Instance.Picker.OpenDialogForTests();
        cut.Render();

        cut.FindComponent<CatalogoItemForm>().Instance.FixedTipo.Should().Be("Arma");
    }

    [Fact]
    public async Task Creating_an_item_closes_the_dialog_and_selects_it_in_the_picker()
    {
        Services.AddScoped(_ => EmptyListClient());
        string? boundValue = null;

        var cut = RenderPicker(p => p
            .Add(x => x.Value, "")
            .Add(x => x.ValueChanged, v => boundValue = v)
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, true)
            .Add(x => x.Tipo, "ItemGeral"));

        cut.Instance.Picker.OpenDialogForTests();
        cut.Render();

        var created = new ItemResponse("item-new", "ItemGeral", "Poção Nova", 1m, 5, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        await cut.InvokeAsync(() => cut.Instance.Picker.HandleCreatedForTestsAsync(created));

        cut.Instance.Picker.DialogOpenForTests.Should().BeFalse();
        boundValue.Should().Be("item-new");
        cut.Markup.Should().Contain("Poção Nova");
    }

    [Fact]
    public void Cancelar_closes_the_dialog_without_creating_anything()
    {
        Services.AddScoped(_ => EmptyListClient());

        var cut = RenderPicker(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>()))
            .Add(x => x.PodeCriar, true)
            .Add(x => x.Tipo, "Arma"));

        cut.Instance.Picker.OpenDialogForTests();
        cut.Instance.Picker.CancelForTests();

        cut.Instance.Picker.DialogOpenForTests.Should().BeFalse();
    }

    // Thin host putting a MudDialogProvider and the CatalogoItemPicker under test side by side in
    // the same render tree, forwarding CatalogoItemPicker's own parameters and exposing the
    // rendered instance for the test-only seams (OpenDialogForTests, etc.) — see RenderPicker above.
    private sealed class Host : ComponentBase
    {
        [Parameter] public string? Value { get; set; }
        [Parameter] public EventCallback<string?> ValueChanged { get; set; }
        [Parameter] public Func<string, Task<List<PickerOption>>> SearchItems { get; set; } = null!;
        [Parameter] public string? Placeholder { get; set; }
        [Parameter] public string? Tipo { get; set; }
        [Parameter] public bool PodeCriar { get; set; }

        public CatalogoItemPicker Picker { get; private set; } = null!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();

            builder.OpenComponent<CatalogoItemPicker>(1);
            builder.AddAttribute(2, nameof(CatalogoItemPicker.Value), Value);
            builder.AddAttribute(3, nameof(CatalogoItemPicker.ValueChanged), ValueChanged);
            builder.AddAttribute(4, nameof(CatalogoItemPicker.SearchItems), SearchItems);
            if (Placeholder is not null)
                builder.AddAttribute(5, nameof(CatalogoItemPicker.Placeholder), Placeholder);
            builder.AddAttribute(6, nameof(CatalogoItemPicker.Tipo), Tipo);
            builder.AddAttribute(7, nameof(CatalogoItemPicker.PodeCriar), PodeCriar);
            builder.AddComponentReferenceCapture(8, instance => Picker = (CatalogoItemPicker)instance);
            builder.CloseComponent();
        }
    }
}
