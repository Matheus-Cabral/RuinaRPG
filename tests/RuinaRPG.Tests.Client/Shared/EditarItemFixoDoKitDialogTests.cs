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

public class EditarItemFixoDoKitDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();

    private void RegisterHttp(List<EquipmentKitFixedItemResponse> base_) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(Rota(request));
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("pericias") || path.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            if (request.Method == HttpMethod.Get && path.EndsWith("equipment-kit-fixed-items"))
                return Json(HttpStatusCode.OK, base_);
            if (request.Method == HttpMethod.Put)
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

    private static EquipmentKitItemResponse LinhaDe(string tipo, string armorSlot = null!) =>
        new("row-1", "fi-atual", "Item atual", tipo, 2, armorSlot, false);

    private async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> AbrirAsync(EquipmentKitItemResponse linha)
    {
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        IDialogReference? reference = null;
        await cut.InvokeAsync(async () => reference = await dialogs.ShowAsync<EditarItemFixoDoKitDialog>("Editar item fixo",
            new DialogParameters<EditarItemFixoDoKitDialog> { { x => x.Linha, linha } }));
        return (cut, reference!);
    }

    private static IRenderedComponent<MudButton> Botao(IRenderedComponent<MudDialogProvider> cut, string texto) =>
        cut.FindComponents<MudButton>().Single(b => b.Markup.Contains(texto));

    private static async Task EscolherAsync(IRenderedComponent<MudDialogProvider> cut, EquipmentKitFixedItemResponse item) =>
        await cut.InvokeAsync(() => cut.FindComponent<MudAutocomplete<EquipmentKitFixedItemResponse>>().Instance.ValueChanged.InvokeAsync(item));

    [Fact]
    public async Task Offers_Editar_detalhes_and_Substituir_item()
    {
        RegisterHttp(new());

        var (cut, _) = await AbrirAsync(LinhaDe("Arma"));

        cut.Markup.Should().Contain("Editar detalhes").And.Contain("Substituir item");
    }

    [Fact]
    public async Task Substituir_by_an_item_of_another_type_returns_the_new_fixed_item_id()
    {
        RegisterHttp(new());
        var (cut, dialog) = await AbrirAsync(LinhaDe("Arma"));

        await cut.InvokeAsync(() => Botao(cut, "Substituir item").Instance.OnClick.InvokeAsync());
        await EscolherAsync(cut, Item("fi-novo", "Corda", "ItemGeral"));
        Botao(cut, "Confirmar").Instance.Disabled.Should().BeFalse();
        cut.Find("button:contains('Confirmar')").Click();

        var resultado = await dialog.Result;
        resultado!.Data.Should().Be(new ItemFixoEditado("fi-novo", null, false));
    }

    [Fact]
    public async Task Substituir_by_an_armadura_asks_for_the_slot_before_confirming()
    {
        RegisterHttp(new());
        var (cut, dialog) = await AbrirAsync(LinhaDe("Arma"));

        await cut.InvokeAsync(() => Botao(cut, "Substituir item").Instance.OnClick.InvokeAsync());
        await EscolherAsync(cut, Item("fi-elmo", "Elmo", "Armadura"));

        Botao(cut, "Confirmar").Instance.Disabled.Should().BeTrue();
        var slot = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Slot de Armadura");
        await cut.InvokeAsync(() => slot.Instance.ValueChanged.InvokeAsync("Capacete"));
        Botao(cut, "Confirmar").Instance.Disabled.Should().BeFalse();
        cut.Find("button:contains('Confirmar')").Click();

        (await dialog.Result)!.Data.Should().Be(new ItemFixoEditado("fi-elmo", "Capacete", false));
    }

    [Fact]
    public async Task Substituir_away_from_an_armadura_returns_a_null_ArmorSlot()
    {
        RegisterHttp(new());
        var (cut, dialog) = await AbrirAsync(LinhaDe("Armadura", "Superior"));

        await cut.InvokeAsync(() => Botao(cut, "Substituir item").Instance.OnClick.InvokeAsync());
        await EscolherAsync(cut, Item("fi-escudo", "Escudo de Madeira", "Escudo"));
        cut.Find("button:contains('Confirmar')").Click();

        (await dialog.Result)!.Data.Should().Be(new ItemFixoEditado("fi-escudo", null, false));
    }

    [Fact]
    public async Task Editar_detalhes_returns_DetalhesAlterados_true_and_the_same_fixed_item_id()
    {
        var atual = Item("fi-atual", "Item atual", "Arma");
        RegisterHttp(new() { atual });
        var (cut, dialog) = await AbrirAsync(LinhaDe("Arma", null!));

        // o handler só termina quando o diálogo aberto por cima fecha: não aguardar o clique
        _ = cut.InvokeAsync(() => Botao(cut, "Editar detalhes").Instance.OnClick.InvokeAsync());
        cut.WaitForAssertion(() => cut.Find("button:contains('Salvar')"));
        _requests.Should().Contain("GET /api/equipment-kit-fixed-items?tipo=Arma&q=Item%20atual");

        // o FixedItemDialog aberto por cima salva com PUT e re-lê o item
        cut.Find("button:contains('Salvar')").Click();

        var resultado = await dialog.Result;
        resultado!.Data.Should().Be(new ItemFixoEditado("fi-atual", null, true));
    }
}
