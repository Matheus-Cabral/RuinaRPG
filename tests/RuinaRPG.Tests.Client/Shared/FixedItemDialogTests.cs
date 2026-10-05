using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using static RuinaRPG.Tests.Client.Shared.FixedItemTestData;

namespace RuinaRPG.Tests.Client.Shared;

public class FixedItemDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();
    private CreateItemRequest? _corpo;

    private void RegisterHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(Rota(request));
            if (request.Method != HttpMethod.Get && request.Content is not null)
                _corpo = request.Content.ReadFromJsonAsync<CreateItemRequest>().GetAwaiter().GetResult();
            if (request.RequestUri!.AbsolutePath.EndsWith("pericias") || request.RequestUri.AbsolutePath.EndsWith("subcategoria-options"))
                return Json(HttpStatusCode.OK, new List<object>());
            return respond(request);
        }));

    private async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> AbrirAsync(string tipo, string nomeInicial, EquipmentKitFixedItemResponse? existente)
    {
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        IDialogReference? reference = null;
        await cut.InvokeAsync(async () => reference = await dialogs.ShowAsync<FixedItemDialog>("Item fixo",
            new DialogParameters<FixedItemDialog> { { x => x.Tipo, tipo }, { x => x.NomeInicial, nomeInicial }, { x => x.Existente, existente } }));
        return (cut, reference!);
    }

    private static void ClicarSalvar(IRenderedComponent<MudDialogProvider> cut) => cut.Find("button:contains('Salvar')").Click();

    [Fact]
    public async Task A_new_item_starts_with_the_tipo_locked_and_the_searched_name_and_posts_on_Salvar()
    {
        var criado = Item("fi-9", "Espada Nova", "Arma");
        RegisterHttp(r => r.Method == HttpMethod.Post ? Json(HttpStatusCode.Created, criado) : new HttpResponseMessage(HttpStatusCode.NotFound));

        var (cut, dialog) = await AbrirAsync("Arma", "Espada Nova", null);

        var tipo = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");
        tipo.Instance.Disabled.Should().BeTrue();
        cut.Markup.Should().Contain("Espada Nova");

        ClicarSalvar(cut);
        var resultado = await dialog.Result;

        _requests.Should().Contain("POST /api/equipment-kit-fixed-items");
        _corpo!.Tipo.Should().Be("Arma");
        _corpo.Nome.Should().Be("Espada Nova");
        resultado!.Canceled.Should().BeFalse();
        resultado.Data.Should().BeEquivalentTo(criado);
    }

    [Fact]
    public async Task An_existing_item_loads_its_data_and_puts_on_Salvar()
    {
        var existente = Item("fi-1", "Espada Longa", "Arma");
        RegisterHttp(r => r.Method == HttpMethod.Put ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Json(HttpStatusCode.OK, new List<EquipmentKitFixedItemResponse> { existente }));

        var (cut, dialog) = await AbrirAsync("Arma", "", existente);
        cut.Markup.Should().Contain("Espada Longa");

        ClicarSalvar(cut);
        var resultado = await dialog.Result;

        _requests.Should().Contain("PUT /api/equipment-kit-fixed-items/fi-1");
        _requests.Should().Contain(r => r.StartsWith("GET /api/equipment-kit-fixed-items?tipo=Arma&q=Espada"));
        _corpo!.Nome.Should().Be("Espada Longa");
        ((EquipmentKitFixedItemResponse)resultado!.Data!).Id.Should().Be("fi-1");
    }

    [Fact]
    public async Task An_existing_item_used_by_kits_warns_that_the_change_applies_to_all_of_them()
    {
        var existente = Item("fi-1", "Corda", "ItemGeral", false, "Viajante", "Explorador");
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var (cut, _) = await AbrirAsync("ItemGeral", "", existente);

        cut.Markup.Should().Contain("vale para todos os kits que usam este item").And.Contain("Viajante").And.Contain("Explorador");
    }

    [Fact]
    public async Task A_409_from_the_server_is_shown_and_the_dialog_stays_open()
    {
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("Ja existe um item fixo com esse nome e tipo.") });

        var (cut, dialog) = await AbrirAsync("Arma", "Espada", null);

        ClicarSalvar(cut);
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Ja existe um item fixo com esse nome e tipo."));

        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task The_image_field_is_not_shown()
    {
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var (cut, _) = await AbrirAsync("Arma", "Espada", null);

        cut.FindComponents<ImageAttachmentField>().Should().BeEmpty();
    }
}
