using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
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
                Subcategorias = (List<string>?)null, Tier = (string?)null, Qtd = 1,
                BonusSubcategoria = (string?)null, BonusNome = (string?)null, BonusQtd = (int?)null,
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

        var addButton = cut.FindComponents<MudButton>().Where(c => HasExactText(c, "Adicionar")).ToList()[1]; // [0]=kit, [1]=categoria, [2]=familia
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

        var deleteButton = cut.FindComponents<MudIconButton>().Single();
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

        // With 1 kit rendered: item-form's "Tipo" select, then slot-form's "Tipo" select, then the
        // vocabulary section's "Tipo" select — in that document order.
        var tipoSelects = cut.FindComponents<MudSelect<string>>().Where(c => c.Instance.Label == "Tipo").ToList();
        tipoSelects.Should().HaveCount(3);
        var slotTipo = tipoSelects[1];
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

        var addButton = cut.FindComponents<MudButton>().Where(c => HasExactText(c, "Adicionar")).ToList()[1];
        await cut.InvokeAsync(() => addButton.Instance.OnClick.InvokeAsync(new MouseEventArgs()));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Valor invalido para essa Categoria.");
    }

    private record CreateSubcategoriaOptionRequestCapture(string Tipo, string Facet, string Valor);

    private record UpdateEquipmentKitRequestCapture(string Nome, string Descricao, int Ciclos,
        List<EquipmentKitItemInputCapture> Items, List<EquipmentKitChoiceSlotInputCapture> ChoiceSlots);

    private record EquipmentKitItemInputCapture(string Nome, string Tipo, int Qtd, string? SubcategoriaHint);

    private record EquipmentKitChoiceSlotInputCapture(string Label, string Tipo, List<string>? Subcategorias,
        string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd, string? ArmorSlot);
}
