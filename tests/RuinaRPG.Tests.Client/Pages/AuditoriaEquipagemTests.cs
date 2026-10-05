using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// Task 11: "Construtor de Subcategoria" section (per-Tipo Categoria/Família vocabulary CRUD) and
/// the widened choice-slot authoring form (Tipo Arma/Armadura/Escudo/Artefato, ArmorSlot picker,
/// Família multi-select) on the existing /auditoria/equipagem page.
/// </summary>
public class AuditoriaEquipagemTests : MudBunitContext
{
    // Info popups' inline <MudDialog> only renders its content through a MudDialogProvider present
    // elsewhere in the render tree (the real app has one in MainLayout) — same idiom as
    // ChangelogDialogTests/CatalogoItemPickerTests/BancoDeMagiasFormTests. Only the two info-popup
    // tests below need this; every pre-existing test in this file keeps using plain Render<...>().
    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider(HttpClient http)
    {
        Services.AddScoped(_ => http);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaEquipagem>(1);
            builder.CloseComponent();
        });
    }

    private static readonly object EmptyKits = new List<object>();

    private static object KitWithArmaduraSlot => new
    {
        Id = "kit-1",
        Nome = "Caçador",
        Descricao = "Kit inicial do Caçador",
        Ciclos = 3,
        Items = new List<object>(),
        ChoiceSlots = new object[]
        {
            new
            {
                Id = "slot-1", Label = "Armadura inicial", Tipo = "Armadura",
                Subcategorias = (List<string>?)null, Rank = (string?)null, Qtd = 1,
                BonusSubcategoria = (string?)null, BonusNome = (string?)null, BonusFixedItemId = (string?)null, BonusQtd = (int?)null,
                ArmorSlot = "Superior",
            },
        },
    };

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };

    private static bool HasExactText(IRenderedComponent<MudButton> c, string text) =>
        Regex.IsMatch(c.Markup, $@">\s*{Regex.Escape(text)}\s*<");

    private static List<object> FamiliaOptions(params string[] valores) =>
        valores.Select((v, i) => (object)new { Id = $"familia-{i}", Tipo = "Arma", Facet = "Familia", Valor = v }).ToList();

    [Fact]
    public async Task Vocabulary_section_loads_Categorias_and_Familias_for_the_default_Tipo_and_reloads_on_switch()
    {
        var requests = new List<string>();
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}{request.RequestUri!.Query}");
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, EmptyKits);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        requests.Should().Contain(r => r.Contains("subcategoria-options") && r.Contains("tipo=Arma") && r.Contains("facet=Categoria"));
        requests.Should().Contain(r => r.Contains("subcategoria-options") && r.Contains("tipo=Arma") && r.Contains("facet=Familia"));

        var subcatTipo = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");
        await cut.InvokeAsync(() => subcatTipo.Instance.ValueChanged.InvokeAsync("Armadura"));
        await Task.Delay(50);

        requests.Should().Contain(r => r.Contains("subcategoria-options") && r.Contains("tipo=Armadura") && r.Contains("facet=Categoria"));
        requests.Should().Contain(r => r.Contains("subcategoria-options") && r.Contains("tipo=Armadura") && r.Contains("facet=Familia"));
    }

    [Fact]
    public async Task Adicionar_Categoria_POSTs_the_right_request()
    {
        CreateSubcategoriaOptionRequestCapture? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, EmptyKits);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
            {
                captured = request.Content!.ReadFromJsonAsync<CreateSubcategoriaOptionRequestCapture>().GetAwaiter().GetResult();
                return Json(HttpStatusCode.Created, new { Id = "opt-1", Tipo = "Arma", Facet = "Categoria", Valor = "Distância" });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        var novaCategoria = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nova Categoria");
        await cut.InvokeAsync(() => novaCategoria.Instance.ValueChanged.InvokeAsync("Distância"));

        var addButton = cut.FindComponents<MudButton>().Where(c => HasExactText(c, "Adicionar")).ToList()[0]; // [0]=categoria, [1]=familia, [2]=kit ("Construtor de Subcategoria" now renders first on the page)
        await cut.InvokeAsync(() => addButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        captured.Should().NotBeNull();
        captured!.Tipo.Should().Be("Arma");
        captured.Facet.Should().Be("Categoria");
        captured.Valor.Should().Be("Distância");
    }

    [Fact]
    public async Task Delete_button_calls_DELETE_on_the_option()
    {
        var deleteCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, EmptyKits);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options") && request.RequestUri!.Query.Contains("Categoria"))
                return Json(HttpStatusCode.OK, new object[] { new { Id = "opt-1", Tipo = "Arma", Facet = "Categoria", Valor = "Distância" } });
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Delete && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options/opt-1"))
            {
                deleteCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        // Scoped to the Delete icon specifically: the page also renders two InfoPopup ⓘ
        // MudIconButtons ("Construtor de Subcategoria"/"Kits" section headers) unrelated to this option row.
        var deleteButton = cut.FindComponents<MudIconButton>().Single(c => c.Instance.Icon == Icons.Material.Filled.Delete);
        await cut.InvokeAsync(() => deleteButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        deleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Adding_a_slot_with_Tipo_Armadura_sends_Tipo_and_ArmorSlot()
    {
        UpdateEquipmentKitRequestCapture? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, new[] { KitWithArmaduraSlot });
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options") && request.RequestUri!.Query.Contains("Familia"))
                return Json(HttpStatusCode.OK, FamiliaOptions("Arcos", "Bestas"));
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Put)
            {
                captured = request.Content!.ReadFromJsonAsync<UpdateEquipmentKitRequestCapture>().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        // With 1 kit rendered: the "Construtor de Subcategoria" section's "Tipo" select renders
        // first (it's the top section on the page), then the add-item picker's "Tipo" select, then
        // slot-form's "Tipo" select — in that document order.
        var tipoSelects = cut.FindComponents<MudSelect<string>>().Where(c => c.Instance.Label == "Tipo").ToList();
        tipoSelects.Should().HaveCount(3);
        var slotTipo = tipoSelects[2];
        await cut.InvokeAsync(() => slotTipo.Instance.ValueChanged.InvokeAsync("Armadura"));
        await Task.Delay(50);

        var slotLabel = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Label");
        await cut.InvokeAsync(() => slotLabel.Instance.ValueChanged.InvokeAsync("Elmo inicial"));

        var armorSlot = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Slot de Armadura");
        await cut.InvokeAsync(() => armorSlot.Instance.ValueChanged.InvokeAsync("Capacete"));

        var addSlotButton = cut.FindComponents<MudButton>().Single(c => HasExactText(c, "Adicionar slot"));
        await cut.InvokeAsync(() => addSlotButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        captured.Should().NotBeNull();
        var newSlot = captured!.ChoiceSlots.Single(s => s.Label == "Elmo inicial");
        newSlot.Tipo.Should().Be("Armadura");
        newSlot.ArmorSlot.Should().Be("Capacete");
    }

    [Fact]
    public async Task Adding_a_slot_with_a_non_Armadura_Tipo_sends_ArmorSlot_null()
    {
        UpdateEquipmentKitRequestCapture? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, new[] { KitWithArmaduraSlot });
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Put)
            {
                captured = request.Content!.ReadFromJsonAsync<UpdateEquipmentKitRequestCapture>().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        var slotLabel = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Label");
        await cut.InvokeAsync(() => slotLabel.Instance.ValueChanged.InvokeAsync("Arco inicial"));

        var addSlotButton = cut.FindComponents<MudButton>().Single(c => HasExactText(c, "Adicionar slot"));
        await cut.InvokeAsync(() => addSlotButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        captured.Should().NotBeNull();
        var newSlot = captured!.ChoiceSlots.Single(s => s.Label == "Arco inicial");
        newSlot.Tipo.Should().Be("Arma"); // default Tipo of a fresh slot form
        newSlot.ArmorSlot.Should().BeNull();
    }

    // Finding 5 of the final review: Rank only ever means anything for a Tipo=Arma choice slot
    // (EquipmentKitGrantService.ResolveEligibleOptionsAsync only applies the Rank filter in the
    // Arma branch) — the API now rejects a non-empty Rank on any other Tipo, so the form must not
    // offer the field at all once Armadura/Escudo/Artefato is picked.
    [Fact]
    public async Task Rank_field_is_shown_for_Tipo_Arma_and_hidden_after_switching_away()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, new[] { KitWithArmaduraSlot });
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        cut.FindComponents<MudSelect<string>>().Should().Contain(c => c.Instance.Label == "Rank (vazio = qualquer)");

        var tipoSelects = cut.FindComponents<MudSelect<string>>().Where(c => c.Instance.Label == "Tipo").ToList();
        var slotTipo = tipoSelects[2];
        await cut.InvokeAsync(() => slotTipo.Instance.ValueChanged.InvokeAsync("Armadura"));
        await Task.Delay(50);

        cut.FindComponents<MudSelect<string>>().Should().NotContain(c => c.Instance.Label == "Rank (vazio = qualquer)");
    }

    [Fact]
    public void Rank_select_offers_F_through_SS()
    {
        AuditoriaEquipagem.RankOptions.Should().BeEquivalentTo(
            new[] { "F", "E", "D", "C", "B", "A", "S", "SS" }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Adding_a_non_Arma_slot_sends_Rank_null_even_if_it_was_chosen_while_Tipo_was_Arma()
    {
        UpdateEquipmentKitRequestCapture? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, new[] { KitWithArmaduraSlot });
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Put)
            {
                captured = request.Content!.ReadFromJsonAsync<UpdateEquipmentKitRequestCapture>().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        var slotRank = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Rank (vazio = qualquer)");
        await cut.InvokeAsync(() => slotRank.Instance.ValueChanged.InvokeAsync("F"));

        var tipoSelects = cut.FindComponents<MudSelect<string>>().Where(c => c.Instance.Label == "Tipo").ToList();
        var slotTipo = tipoSelects[2];
        await cut.InvokeAsync(() => slotTipo.Instance.ValueChanged.InvokeAsync("Escudo"));
        await Task.Delay(50);

        var slotLabel = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Label");
        await cut.InvokeAsync(() => slotLabel.Instance.ValueChanged.InvokeAsync("Rodela inicial"));

        var addSlotButton = cut.FindComponents<MudButton>().Single(c => HasExactText(c, "Adicionar slot"));
        await cut.InvokeAsync(() => addSlotButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        captured.Should().NotBeNull();
        var newSlot = captured!.ChoiceSlots.Single(s => s.Label == "Rodela inicial");
        newSlot.Tipo.Should().Be("Escudo");
        newSlot.Rank.Should().BeNull();
    }

    [Fact]
    public async Task The_Familia_multi_select_sends_the_selected_values()
    {
        UpdateEquipmentKitRequestCapture? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, new[] { KitWithArmaduraSlot });
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options") && request.RequestUri!.Query.Contains("Familia"))
                return Json(HttpStatusCode.OK, FamiliaOptions("Arcos", "Bestas"));
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Put)
            {
                captured = request.Content!.ReadFromJsonAsync<UpdateEquipmentKitRequestCapture>().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        var slotLabel = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Label");
        await cut.InvokeAsync(() => slotLabel.Instance.ValueChanged.InvokeAsync("Arco inicial"));

        var familiaSelect = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label != null && c.Instance.Label.Contains("Famílias"));
        await cut.InvokeAsync(() => familiaSelect.Instance.SelectedValuesChanged.InvokeAsync(new List<string> { "Arcos" }));

        var addSlotButton = cut.FindComponents<MudButton>().Single(c => HasExactText(c, "Adicionar slot"));
        await cut.InvokeAsync(() => addSlotButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        captured.Should().NotBeNull();
        var newSlot = captured!.ChoiceSlots.Single(s => s.Label == "Arco inicial");
        newSlot.Subcategorias.Should().BeEquivalentTo(new[] { "Arcos" });
    }

    [Fact]
    public async Task An_unrelated_kit_update_re_PUTs_the_existing_Armadura_slot_with_ArmorSlot_intact()
    {
        UpdateEquipmentKitRequestCapture? captured = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, new[] { KitWithArmaduraSlot });
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Put)
            {
                captured = request.Content!.ReadFromJsonAsync<UpdateEquipmentKitRequestCapture>().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        // Index 1: the "Adicionar Kit" section's own "Nome" field (for _newForm) renders first, then
        // this kit's "Nome" field inside the expansion panel — in that document order.
        var nome = cut.FindComponents<MudTextField<string>>().Where(c => c.Instance.Label == "Nome").ToList()[1];
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Caçador Renomeado"));
        await Task.Delay(50);

        captured.Should().NotBeNull();
        var existingSlot = captured!.ChoiceSlots.Single(s => s.Label == "Armadura inicial");
        existingSlot.Tipo.Should().Be("Armadura");
        existingSlot.ArmorSlot.Should().Be("Superior");
    }

    [Fact]
    public async Task A_400_body_from_adding_a_Categoria_surfaces_as_the_error_message()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, EmptyKits);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
                // ASCII-only on purpose: System.Text.Json's default encoder escapes non-ASCII
                // characters (accents), and ReadAsStringAsync surfaces the raw (still-JSON-escaped)
                // wire text verbatim — a pre-existing quirk of this page's error surfacing shared by
                // AddAsync/AuditoriaHistoricos, not something to re-verify here.
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create("Valor invalido para essa Categoria.") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        var novaCategoria = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nova Categoria");
        await cut.InvokeAsync(() => novaCategoria.Instance.ValueChanged.InvokeAsync("bad - valor"));

        var addButton = cut.FindComponents<MudButton>().Where(c => HasExactText(c, "Adicionar")).ToList()[0]; // categoria's "Adicionar" — "Construtor de Subcategoria" now renders first
        await cut.InvokeAsync(() => addButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Valor invalido para essa Categoria.");
    }

    // Item 1 of the "ajustes-ui-historico" UI-tweaks brief: "Construtor de Subcategoria" is used to
    // set up the vocabulary that "Adicionar Kit"/"Kits" then consume, so it belongs above them in
    // reading order — pure reorder, no behavior change.
    [Fact]
    public async Task Construtor_de_Subcategoria_section_renders_before_Adicionar_Kit_and_Kits()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, EmptyKits);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaEquipagem>();
        await Task.Delay(50);

        var construtorIndex = cut.Markup.IndexOf("Construtor de Subcategoria", StringComparison.Ordinal);
        var adicionarKitIndex = cut.Markup.IndexOf("Adicionar Kit", StringComparison.Ordinal);
        var kitsIndex = cut.Markup.IndexOf(">Kits<", StringComparison.Ordinal);

        construtorIndex.Should().BeGreaterThan(-1);
        adicionarKitIndex.Should().BeGreaterThan(-1);
        kitsIndex.Should().BeGreaterThan(-1);
        construtorIndex.Should().BeLessThan(adicionarKitIndex);
        construtorIndex.Should().BeLessThan(kitsIndex);
    }

    [Fact]
    public async Task Construtor_de_Subcategoria_section_has_an_info_popup_with_the_exact_help_text()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, EmptyKits);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        var infoButton = cut.Find("button[title='Como funciona o Construtor']");
        infoButton.GetAttribute("aria-label").Should().Be("Como funciona o Construtor");

        infoButton.Click();

        var content = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        content.Should().Be(TextNormalization.Collapse(
            "Aqui você mantém, para cada Tipo (Arma, Armadura, Escudo, Artefato), as listas de Categorias e Famílias. No Catálogo de Itens, quando o GM marca \"Item Inicial\", ele escolhe uma Categoria e uma Família dessas listas. A Subcategoria do item vira então Equipamento inicial - {Tipo} - {Categoria} - {Família}. Os slots de escolha dos kits usam as Famílias para decidir quais itens o jogador pode escolher. Os valores não podem conter \" - \" nem vírgula. Remover um valor não altera os itens que já o usam."));
    }

    // 5b of the info-popups brief: the "Como montar um Kit" popup belongs to the "Kits" section only
    // — "Adicionar Kit" (which renders earlier in document order, per the reorder test above) must
    // have no ⓘ of its own.
    [Fact]
    public async Task Kits_section_has_an_info_popup_and_Adicionar_Kit_does_not()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("equipment-kits"))
                return Json(HttpStatusCode.OK, EmptyKits);
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        var kitPopupButtons = cut.FindAll("button[title='Como montar um Kit']");
        kitPopupButtons.Should().HaveCount(1);
        kitPopupButtons[0].GetAttribute("aria-label").Should().Be("Como montar um Kit");

        var adicionarKitIndex = cut.Markup.IndexOf("Adicionar Kit", StringComparison.Ordinal);
        var kitsIndex = cut.Markup.IndexOf(">Kits<", StringComparison.Ordinal);
        var kitPopupIndex = cut.Markup.IndexOf("Como montar um Kit", StringComparison.Ordinal);
        adicionarKitIndex.Should().BeGreaterThan(-1);
        kitsIndex.Should().BeGreaterThan(-1);
        kitPopupIndex.Should().BeGreaterThan(kitsIndex, "the ⓘ belongs to the 'Kits' section, not 'Adicionar Kit'");

        kitPopupButtons[0].Click();

        var content = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        content.Should().Be(TextNormalization.Collapse(string.Join(" ",
            "Um kit é o equipamento inicial que o jogador escolhe uma única vez na aba Posses da ficha. Os Ciclos são somados ao dinheiro da ficha.",
            "Itens fixos: todo mundo que escolhe o kit recebe esses itens. Eles vêm da base de itens fixos, igual para todas as campanhas: pesquise pelo nome ou cadastre um novo. Ao aplicar o kit, o item que o GM ainda não tem é copiado completo para o catálogo dele.",
            "Slots de escolha: o jogador escolhe um item. Defina o Tipo e as Famílias permitidas; vazio significa qualquer uma. Para Arma, defina também o Rank. Para Armadura, defina em qual posição (Capacete, Superior ou Inferior) ela será equipada, substituindo o que estiver lá.",
            "Excluir um kit já escolhido em alguma ficha é bloqueado.")));
    }

    // ---- Task 15: itens fixos dos kits escolhidos da base global ----

    private static object ItemRow(string id, string fixedItemId, string nome, string tipo, int qtd = 1, string? armorSlot = null, bool incompleto = false) =>
        new { Id = id, FixedItemId = fixedItemId, Nome = nome, Tipo = tipo, Qtd = qtd, ArmorSlot = armorSlot, DetalhesIncompletos = incompleto };

    private static object KitComItens(params object[] itens) => new
    {
        Id = "kit-1", Nome = "Viajante", Descricao = "Kit do Viajante", Ciclos = 3,
        Items = itens.ToList(), ChoiceSlots = new List<object>(),
    };

    private class Escrivaninha
    {
        public List<string> Requests { get; } = new();
        public UpdateEquipmentKitRequestCapture? KitPut { get; set; }
        public List<object> Kits { get; set; } = new();
        public List<EquipmentKitFixedItemResponse> Base { get; set; } = new();
        public HttpResponseMessage? RespostaDelete { get; set; }
        public int KitGets => Requests.Count(r => r == "GET /api/equipment-kits");
    }

    private Escrivaninha RegisterFixedItemHttp(Action<Escrivaninha>? configure = null)
    {
        var e = new Escrivaninha();
        configure?.Invoke(e);
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var rota = FixedItemTestData.Rota(request);
            e.Requests.Add(rota);
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/equipment-kits"))
                return Json(HttpStatusCode.OK, e.Kits);
            if (request.Method == HttpMethod.Get && (path.EndsWith("subcategoria-options") || path.EndsWith("pericias")))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Get && path.EndsWith("equipment-kit-fixed-items"))
                return Json(HttpStatusCode.OK, e.Base);
            if (request.Method == HttpMethod.Put && path.Contains("/equipment-kits/"))
            {
                e.KitPut = request.Content!.ReadFromJsonAsync<UpdateEquipmentKitRequestCapture>().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Put && path.Contains("/equipment-kit-fixed-items/"))
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            if (request.Method == HttpMethod.Delete && path.Contains("/equipment-kit-fixed-items/"))
                return e.RespostaDelete ?? new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        return e;
    }

    private IRenderedComponent<ContainerFragment> RenderFull() =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaEquipagem>(2);
            builder.CloseComponent();
        });

    private static async Task PickAsync(IRenderedComponent<ContainerFragment> cut, int pickerIndex, EquipmentKitFixedItemResponse item)
    {
        var autocomplete = cut.FindComponents<FixedItemPicker>()[pickerIndex].FindComponent<MudAutocomplete<EquipmentKitFixedItemResponse>>();
        await cut.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(item));
    }

    private static IRenderedComponent<MudButton> ButtonWithText(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.FindComponents<MudButton>().Single(b => HasExactText(b, text));

    private static Task ClickAsync(IRenderedComponent<ContainerFragment> cut, IRenderedComponent<MudButton> button) =>
        cut.InvokeAsync(() => button.Instance.OnClick.InvokeAsync(new MouseEventArgs()));

    private static async Task SetNumberAsync(IRenderedComponent<ContainerFragment> cut, string label, int value)
    {
        var field = cut.FindComponents<MudNumericField<int>>().Single(c => c.Instance.Label == label);
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(value));
    }

    [Fact]
    public async Task The_add_item_row_uses_the_picker_with_all_five_tipos_including_Armadura()
    {
        RegisterFixedItemHttp(e => e.Kits = new() { KitComItens() });
        var cut = RenderFull();
        await Task.Delay(50);

        var picker = cut.FindComponents<FixedItemPicker>().First();
        picker.Instance.TiposPermitidos.Should().BeEquivalentTo(new[] { "ItemGeral", "Arma", "Armadura", "Escudo", "Artefato" });
    }

    [Fact]
    public async Task Adding_an_item_puts_the_kit_with_the_picked_FixedItemId_and_qtd()
    {
        var e = RegisterFixedItemHttp(x => x.Kits = new() { KitComItens(ItemRow("row-1", "fi-corda", "Corda", "ItemGeral", 1)) });
        var cut = RenderFull();
        await Task.Delay(50);

        await PickAsync(cut, 0, FixedItemTestData.Item("fi-tocha", "Tocha", "ItemGeral"));
        await SetNumberAsync(cut, "Quantidade do item", 3);
        await ClickAsync(cut, ButtonWithText(cut, "Adicionar item"));
        await Task.Delay(50);

        e.KitPut.Should().NotBeNull();
        e.KitPut!.Items.Should().HaveCount(2);
        e.KitPut.Items[0].FixedItemId.Should().Be("fi-corda");
        e.KitPut.Items[1].Should().Be(new EquipmentKitItemInputCapture("fi-tocha", 3, null));
    }

    [Fact]
    public async Task Adding_an_armadura_requires_and_sends_the_slot()
    {
        var e = RegisterFixedItemHttp(x => x.Kits = new() { KitComItens() });
        var cut = RenderFull();
        await Task.Delay(50);

        await PickAsync(cut, 0, FixedItemTestData.Item("fi-elmo", "Elmo", "Armadura"));
        ButtonWithText(cut, "Adicionar item").Instance.Disabled.Should().BeTrue();

        var slot = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Slot de Armadura");
        await cut.InvokeAsync(() => slot.Instance.ValueChanged.InvokeAsync("Capacete"));
        ButtonWithText(cut, "Adicionar item").Instance.Disabled.Should().BeFalse();
        await ClickAsync(cut, ButtonWithText(cut, "Adicionar item"));
        await Task.Delay(50);

        e.KitPut!.Items.Single().Should().Be(new EquipmentKitItemInputCapture("fi-elmo", 1, "Capacete"));
    }

    [Fact]
    public async Task Changing_a_rows_qtd_puts_the_kit_with_the_new_value()
    {
        var e = RegisterFixedItemHttp(x => x.Kits = new() { KitComItens(ItemRow("row-1", "fi-corda", "Corda", "ItemGeral", 1)) });
        var cut = RenderFull();
        await Task.Delay(50);

        await SetNumberAsync(cut, "Qtd do item", 5);
        await Task.Delay(50);

        e.KitPut!.Items.Single().Should().Be(new EquipmentKitItemInputCapture("fi-corda", 5, null));
    }

    [Fact]
    public async Task A_row_whose_fixed_item_is_incomplete_is_flagged_Detalhes_incompletos()
    {
        RegisterFixedItemHttp(x => x.Kits = new() { KitComItens(
            ItemRow("row-1", "fi-a", "Corda", "ItemGeral", 1, null, true), ItemRow("row-2", "fi-b", "Tocha", "ItemGeral")) });
        var cut = RenderFull();
        await Task.Delay(50);

        var chips = cut.FindComponents<MudChip<string>>().Where(c => c.Markup.Contains("Detalhes incompletos")).ToList();
        chips.Should().HaveCount(1);
        chips[0].Instance.Color.Should().Be(Color.Warning);
    }

    [Fact]
    public async Task The_edit_button_opens_the_dialog_and_a_replacement_puts_the_kit_with_the_new_item_keeping_the_qtd()
    {
        var e = RegisterFixedItemHttp(x => x.Kits = new() { KitComItens(ItemRow("row-1", "fi-corda", "Corda", "ItemGeral", 4)) });
        var cut = RenderFull();
        await Task.Delay(50);

        _ = cut.Find("button[title='Editar item fixo']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Substituir item"));
        await cut.InvokeAsync(() => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Substituir item")).Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        var autocompleteDoDialogo = cut.FindComponent<EditarItemFixoDoKitDialog>().FindComponent<MudAutocomplete<EquipmentKitFixedItemResponse>>();
        await cut.InvokeAsync(() => autocompleteDoDialogo.Instance.ValueChanged.InvokeAsync(FixedItemTestData.Item("fi-tocha", "Tocha", "ItemGeral")));
        await cut.InvokeAsync(() => cut.FindComponents<MudButton>().Single(b => HasExactText(b, "Confirmar")).Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => e.KitPut.Should().NotBeNull());

        e.KitPut!.Items.Single().Should().Be(new EquipmentKitItemInputCapture("fi-tocha", 4, null));
    }

    [Fact]
    public async Task After_Editar_detalhes_the_kits_are_reloaded()
    {
        var corda = FixedItemTestData.Item("fi-corda", "Corda", "ItemGeral");
        var e = RegisterFixedItemHttp(x =>
        {
            x.Kits = new() { KitComItens(ItemRow("row-1", "fi-corda", "Corda", "ItemGeral", 1)) };
            x.Base = new() { corda };
        });
        var cut = RenderFull();
        await Task.Delay(50);
        var antes = e.KitGets;

        _ = cut.Find("button[title='Editar item fixo']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Editar detalhes"));
        _ = cut.InvokeAsync(() => cut.FindComponents<MudButton>().Single(b => b.Markup.Contains("Editar detalhes")).Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => cut.Find("button:contains('Salvar')"));
        cut.Find("button:contains('Salvar')").Click();

        cut.WaitForAssertion(() => e.KitGets.Should().BeGreaterThan(antes));
        e.KitPut.Should().BeNull("editar detalhes altera o item da base, não o kit");
    }

    [Fact]
    public async Task The_slot_bonus_is_picked_from_the_base_restricted_to_Item_Geral()
    {
        var e = RegisterFixedItemHttp(x => x.Kits = new() { KitComItens() });
        var cut = RenderFull();
        await Task.Delay(50);

        var pickers = cut.FindComponents<FixedItemPicker>();
        pickers.Should().HaveCount(2); // [0] = adicionar item, [1] = bônus do slot
        pickers[1].Instance.TiposPermitidos.Should().BeEquivalentTo(new[] { "ItemGeral" });

        var label = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Label");
        await cut.InvokeAsync(() => label.Instance.ValueChanged.InvokeAsync("Arco inicial"));
        await PickAsync(cut, 1, FixedItemTestData.Item("fi-flecha", "Flecha", "ItemGeral"));
        var sub = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Subcategoria do bônus");
        await cut.InvokeAsync(() => sub.Instance.ValueChanged.InvokeAsync("Arcos"));
        await SetNumberAsync(cut, "Qtd do bônus", 20);
        await ClickAsync(cut, ButtonWithText(cut, "Adicionar slot"));
        await Task.Delay(50);

        var novo = e.KitPut!.ChoiceSlots.Single(s => s.Label == "Arco inicial");
        novo.BonusFixedItemId.Should().Be("fi-flecha");
        novo.BonusSubcategoria.Should().Be("Arcos");
        novo.BonusQtd.Should().Be(20);
    }

    [Fact]
    public async Task The_Itens_fixos_section_lists_the_base_filters_by_tipo_and_name_and_can_edit_and_delete()
    {
        var e = RegisterFixedItemHttp(x => x.Base = new()
        {
            FixedItemTestData.Item("fi-1", "Corda", "ItemGeral", false, "Viajante"),
            FixedItemTestData.Item("fi-2", "Espada", "Arma", true),
        });
        var cut = RenderFull();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Itens fixos").And.Contain("Corda").And.Contain("Espada").And.Contain("Viajante");
        cut.FindComponents<MudChip<string>>().Should().Contain(c => c.Markup.Contains("Detalhes incompletos"));
        ButtonWithText(cut, "Novo item fixo").Should().NotBeNull();

        var tipo = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Filtrar por Tipo");
        await cut.InvokeAsync(() => tipo.Instance.ValueChanged.InvokeAsync("Arma"));
        await Task.Delay(50);
        e.Requests.Should().Contain("GET /api/equipment-kit-fixed-items?tipo=Arma&q=");

        var busca = cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Buscar por nome");
        await cut.InvokeAsync(() => busca.Instance.ValueChanged.InvokeAsync("esp"));
        await Task.Delay(50);
        e.Requests.Should().Contain("GET /api/equipment-kit-fixed-items?tipo=Arma&q=esp");

        var editar = cut.FindAll("button[title='Editar item da base']");
        editar.Should().NotBeEmpty();
        _ = editar[0].ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => cut.Find("button:contains('Salvar')"));
        cut.Find("button:contains('Cancelar')").Click();

        await cut.InvokeAsync(() => cut.FindComponents<MudIconButton>().First(b => b.Markup.Contains("Excluir item fixo")).Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);
        e.Requests.Should().Contain(r => r.StartsWith("DELETE /api/equipment-kit-fixed-items/fi-"));
    }

    [Fact]
    public async Task Deleting_a_fixed_item_in_use_shows_the_servers_conflict_message()
    {
        RegisterFixedItemHttp(x =>
        {
            x.Base = new() { FixedItemTestData.Item("fi-1", "Corda", "ItemGeral", false, "Viajante") };
            x.RespostaDelete = new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("Item usado nos kits: Viajante.") };
        });
        var cut = RenderFull();
        await Task.Delay(50);

        await cut.InvokeAsync(() => cut.FindComponents<MudIconButton>().First(b => b.Markup.Contains("Excluir item fixo")).Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Item usado nos kits: Viajante.");
    }

    private record CreateSubcategoriaOptionRequestCapture(string Tipo, string Facet, string Valor);

    private record UpdateEquipmentKitRequestCapture(string Nome, string Descricao, int Ciclos,
        List<EquipmentKitItemInputCapture> Items, List<EquipmentKitChoiceSlotInputCapture> ChoiceSlots);

    private record EquipmentKitItemInputCapture(string FixedItemId, int Qtd, string? ArmorSlot);

    private record EquipmentKitChoiceSlotInputCapture(string Label, string Tipo, List<string>? Subcategorias,
        string? Rank, int Qtd, string? BonusSubcategoria, string? BonusFixedItemId, int? BonusQtd, string? ArmorSlot);
}
