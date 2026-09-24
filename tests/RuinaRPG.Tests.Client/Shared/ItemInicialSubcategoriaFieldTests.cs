using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Domain.Items;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ItemInicialSubcategoriaFieldTests : MudBunitContext
{
    // Matches the vocabulary GET the real component hits — used to seed Categoria/Família options.
    private static HttpClient VocabularyClient(List<string> categorias, List<string> familias) =>
        FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath + request.RequestUri.Query;
            if (path.Contains("facet=Categoria"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(categorias.Select(v => new { Id = Guid.NewGuid().ToString(), Tipo = "Arma", Facet = "Categoria", Valor = v }).ToList()) };
            if (path.Contains("facet=Familia"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(familias.Select(v => new { Id = Guid.NewGuid().ToString(), Tipo = "Arma", Facet = "Familia", Valor = v }).ToList()) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    private static MudCheckBox<bool> Checkbox(IRenderedComponent<ItemInicialSubcategoriaField> cut) =>
        cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Item Inicial").Instance;

    private static MudSelect<string> CategoriaSelect(IRenderedComponent<ItemInicialSubcategoriaField> cut) =>
        cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Categoria (Item Inicial)").Instance;

    private static MudSelect<string> FamiliaSelect(IRenderedComponent<ItemInicialSubcategoriaField> cut) =>
        cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Família (Item Inicial)").Instance;

    [Fact]
    public void Free_text_mode_by_default_for_a_non_constructor_value()
    {
        Services.AddScoped(_ => VocabularyClient(new(), new()));

        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Value, "Espada longa"));

        cut.Instance.IsCheckedForTests.Should().BeFalse();
        cut.FindComponents<MudTextField<string>>().Should().ContainSingle(c => c.Instance.Label == "Subcategoria");
        cut.FindComponents<MudSelect<string>>().Should().BeEmpty();
    }

    [Fact]
    public void A_composed_value_for_the_matching_Tipo_opens_in_constructor_mode_with_both_selects_pre_set()
    {
        Services.AddScoped(_ => VocabularyClient(new() { "Cortante" }, new() { "Espadas" }));
        var composed = SubcategoriaBuilder.Compose(ItemTipo.Arma, "Cortante", "Espadas");

        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Value, composed));

        cut.Instance.IsCheckedForTests.Should().BeTrue();
        var selects = cut.FindComponents<MudSelect<string>>();
        selects.Should().Contain(c => c.Instance.Label == "Categoria (Item Inicial)" && c.Instance.Value == "Cortante");
        selects.Should().Contain(c => c.Instance.Label == "Família (Item Inicial)" && c.Instance.Value == "Espadas");
    }

    [Fact]
    public void A_composed_value_for_a_different_Tipo_stays_in_free_text_mode()
    {
        Services.AddScoped(_ => VocabularyClient(new(), new()));
        var composedForArmadura = SubcategoriaBuilder.Compose(ItemTipo.Armadura, "Pesada", "Placas");

        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Value, composedForArmadura));

        cut.Instance.IsCheckedForTests.Should().BeFalse();
    }

    [Fact]
    public async Task Selecting_Categoria_then_Familia_emits_the_composed_string_via_ValueChanged()
    {
        Services.AddScoped(_ => VocabularyClient(new() { "Cortante" }, new() { "Espadas" }));
        string? emitted = null;
        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.ValueChanged, v => emitted = v));

        await cut.InvokeAsync(() => Checkbox(cut).ValueChanged.InvokeAsync(true));
        emitted.Should().BeNull("nothing is emitted until both Categoria and Família are chosen");

        await cut.InvokeAsync(() => CategoriaSelect(cut).ValueChanged.InvokeAsync("Cortante"));
        emitted.Should().BeNull("Família has not been chosen yet");

        await cut.InvokeAsync(() => FamiliaSelect(cut).ValueChanged.InvokeAsync("Espadas"));

        emitted.Should().Be(SubcategoriaBuilder.Compose(ItemTipo.Arma, "Cortante", "Espadas"));
    }

    [Fact]
    public void A_value_arriving_after_first_render_still_switches_to_constructor_mode()
    {
        // Regression for the controller ruling on the brief: CatalogoItemForm loads the existing
        // item's Subcategoria after an await, so this field can first render with Value=null and
        // must still pick up the real value once it shows up on a later parameter set.
        Services.AddScoped(_ => VocabularyClient(new() { "Cortante" }, new() { "Espadas" }));
        var composed = SubcategoriaBuilder.Compose(ItemTipo.Arma, "Cortante", "Espadas");

        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Value, (string?)null));
        cut.Instance.IsCheckedForTests.Should().BeFalse();

        cut.Render(p => p.Add(x => x.Value, composed));

        cut.Instance.IsCheckedForTests.Should().BeTrue();
        var selects = cut.FindComponents<MudSelect<string>>();
        selects.Should().Contain(c => c.Instance.Label == "Categoria (Item Inicial)" && c.Instance.Value == "Cortante");
        selects.Should().Contain(c => c.Instance.Label == "Família (Item Inicial)" && c.Instance.Value == "Espadas");
    }

    [Fact]
    public async Task Unchecking_keeps_the_current_Value_and_does_not_emit()
    {
        Services.AddScoped(_ => VocabularyClient(new() { "Cortante" }, new() { "Espadas" }));
        var composed = SubcategoriaBuilder.Compose(ItemTipo.Arma, "Cortante", "Espadas");
        var emittedCount = 0;
        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Value, composed)
            .Add(x => x.ValueChanged, _ => emittedCount++));

        cut.Instance.IsCheckedForTests.Should().BeTrue();

        await cut.InvokeAsync(() => Checkbox(cut).ValueChanged.InvokeAsync(false));

        emittedCount.Should().Be(0, "unchecking is pure UI state and must not emit a new Value");
        cut.Instance.IsCheckedForTests.Should().BeFalse();
        // The free-text field must still show the current (composed) Value for continued editing.
        cut.FindComponents<MudTextField<string>>().Should().ContainSingle(c => c.Instance.Label == "Subcategoria" && c.Instance.Value == composed);
    }

    [Fact]
    public void Switching_Tipo_to_a_sibling_clears_a_composed_value_that_belonged_to_the_old_Tipo()
    {
        // Regression: CatalogoItemForm's Armadura/Escudo branch renders a single
        // ItemInicialSubcategoriaField instance shared by both Tipos — switching the top-level
        // Tipo select reuses this same component instance with a new Tipo but an unchanged Value.
        // A composed Subcategoria for the OLD Tipo (here Armadura) is never valid once Tipo becomes
        // Escudo, so it must be cleared (emitted as null) rather than silently kept and persisted
        // mismatched.
        Services.AddScoped(_ => VocabularyClient(new() { "Pesada" }, new() { "Placas" }));
        var composedForArmadura = SubcategoriaBuilder.Compose(ItemTipo.Armadura, "Pesada", "Placas");
        string? emitted = "not called";
        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Armadura")
            .Add(x => x.Value, composedForArmadura)
            .Add(x => x.ValueChanged, v => emitted = v));
        cut.Instance.IsCheckedForTests.Should().BeTrue();

        cut.Render(p => p.Add(x => x.Tipo, "Escudo"));

        emitted.Should().BeNull("a composed Subcategoria for Armadura is never valid once Tipo becomes Escudo — it must be cleared");
        cut.Instance.IsCheckedForTests.Should().BeFalse();
    }

    [Fact]
    public void Switching_Tipo_keeps_a_free_text_value_that_is_not_a_constructor_string()
    {
        Services.AddScoped(_ => VocabularyClient(new(), new()));
        string? emitted = "not called";
        var cut = Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Armadura")
            .Add(x => x.Value, "Texto livre qualquer")
            .Add(x => x.ValueChanged, v => emitted = v));
        cut.Instance.IsCheckedForTests.Should().BeFalse();

        cut.Render(p => p.Add(x => x.Tipo, "Escudo"));

        emitted.Should().Be("not called", "free text is not a constructor string for any Tipo and must survive a Tipo change untouched");
        cut.Instance.IsCheckedForTests.Should().BeFalse();
        cut.FindComponents<MudTextField<string>>().Should().ContainSingle(c => c.Instance.Label == "Subcategoria" && c.Instance.Value == "Texto livre qualquer");
    }

    [Fact]
    public void Empty_option_lists_do_not_throw()
    {
        Services.AddScoped(_ => VocabularyClient(new(), new()));

        var act = () => Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Escudo")
            .Add(x => x.Value, (string?)null));

        act.Should().NotThrow();
    }

    [Fact]
    public void A_404_from_the_vocabulary_endpoint_does_not_throw()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Services.AddScoped(_ => http);

        var act = () => Render<ItemInicialSubcategoriaField>(p => p
            .Add(x => x.Tipo, "Artefato")
            .Add(x => x.Value, (string?)null));

        act.Should().NotThrow();
    }
}
