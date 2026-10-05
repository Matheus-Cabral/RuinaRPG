using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using System.Net;
using Xunit;
using static RuinaRPG.Tests.Client.Shared.FixedItemTestData;

namespace RuinaRPG.Tests.Client.Shared;

public class FixedItemPickerTests : MudBunitContext
{
    private static readonly string[] Todos = ["ItemGeral", "Arma", "Armadura", "Escudo", "Artefato"];

    private List<string> _requests = new();

    private void RegisterHttp(List<EquipmentKitFixedItemResponse>? resultado = null)
    {
        resultado ??= new();
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(Rota(request));
            return Json(HttpStatusCode.OK, resultado);
        }));
    }

    private IRenderedComponent<FixedItemPicker> RenderPicker(string[] tipos, string tipo = "Arma",
        EquipmentKitFixedItemResponse? value = null, Action<EquipmentKitFixedItemResponse?>? onValue = null, Action<string>? onTipo = null) =>
        Render<FixedItemPicker>(p =>
        {
            p.Add(x => x.TiposPermitidos, tipos);
            p.Add(x => x.Tipo, tipo);
            p.Add(x => x.Value, value);
            if (onValue is not null) p.Add(x => x.ValueChanged, onValue);
            if (onTipo is not null) p.Add(x => x.TipoChanged, onTipo);
        });

    [Fact]
    public async Task Typing_searches_the_base_with_the_selected_tipo_and_the_text()
    {
        RegisterHttp();
        var cut = RenderPicker(Todos);

        await cut.InvokeAsync(() => cut.Instance.BuscarParaTestesAsync("esp"));

        _requests.Should().Contain("GET /api/equipment-kit-fixed-items?tipo=Arma&q=esp");
    }

    [Fact]
    public async Task Choosing_a_result_raises_ValueChanged_with_it()
    {
        RegisterHttp();
        EquipmentKitFixedItemResponse? escolhido = null;
        var cut = RenderPicker(Todos, onValue: v => escolhido = v);
        var espada = Item("fi-1", "Espada", "Arma");

        await cut.InvokeAsync(() => cut.FindComponent<MudAutocomplete<EquipmentKitFixedItemResponse>>().Instance.ValueChanged.InvokeAsync(espada));

        escolhido.Should().Be(espada);
    }

    [Fact]
    public async Task A_search_with_no_match_offers_Cadastrar_novo_item()
    {
        RegisterHttp(new());
        var cut = RenderPicker(Todos);

        await cut.InvokeAsync(() => cut.Instance.BuscarParaTestesAsync("espada nova"));

        cut.Markup.Should().Contain("Cadastrar novo item");
    }

    [Fact]
    public async Task A_blank_search_does_not_offer_Cadastrar_novo_item()
    {
        RegisterHttp(new());
        var cut = RenderPicker(Todos);

        await cut.InvokeAsync(() => cut.Instance.BuscarParaTestesAsync("   "));

        cut.Markup.Should().NotContain("Cadastrar novo item");
    }

    [Fact]
    public async Task A_search_with_matches_does_not_offer_Cadastrar_novo_item()
    {
        RegisterHttp(new() { Item("fi-1", "Espada", "Arma") });
        var cut = RenderPicker(Todos);

        await cut.InvokeAsync(() => cut.Instance.BuscarParaTestesAsync("esp"));

        cut.Markup.Should().NotContain("Cadastrar novo item");
    }

    [Fact]
    public async Task Changing_the_tipo_clears_the_selected_item()
    {
        RegisterHttp();
        var tipoEscolhido = "";
        var limpou = false;
        var cut = RenderPicker(Todos, value: Item("fi-1", "Espada", "Arma"),
            onValue: v => limpou = v is null, onTipo: t => tipoEscolhido = t);

        var select = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync("Escudo"));

        tipoEscolhido.Should().Be("Escudo");
        limpou.Should().BeTrue();
    }

    [Fact]
    public void Only_the_allowed_tipos_are_offered()
    {
        RegisterHttp();
        var cut = RenderWithPopover<FixedItemPicker>(("TiposPermitidos", new[] { "ItemGeral" }), ("Tipo", "ItemGeral"));

        OpenSelect(cut, "Tipo").Should().Equal("Item Geral");
    }
}
