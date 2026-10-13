using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CaracteristicaDeCriaturaDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();
    private CreateCreatureExclusiveTraitRequest? _corpo;

    private void RegisterHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            if (request.Method == HttpMethod.Post)
                _corpo = request.Content!.ReadFromJsonAsync<CreateCreatureExclusiveTraitRequest>().GetAwaiter().GetResult();
            return respond(request);
        }));

    private async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> AbrirAsync()
    {
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        IDialogReference? reference = null;
        await cut.InvokeAsync(async () => reference = await dialogs.ShowAsync<CaracteristicaDeCriaturaDialog>("Nova característica de criatura"));
        return (cut, reference!);
    }

    [Fact]
    public async Task Salvar_posts_the_form_and_closes_with_the_created_characteristic()
    {
        var criada = new CreatureExclusiveTraitResponse("ct-1", "Regeneração Bestial", "Recupera Vitalidade.", 3, "Positiva", false);
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(criada) });
        var (cut, dialog) = await AbrirAsync();

        cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nome").Find("input").Change("Regeneração Bestial");
        cut.FindComponents<MudNumericField<int>>().Single(c => c.Instance.Label == "Custo").Find("input").Change("3");
        cut.Find("button:contains('Salvar')").Click();
        var resultado = await dialog.Result;

        _requests.Should().Contain("POST /api/creature-exclusive-traits");
        _corpo!.Nome.Should().Be("Regeneração Bestial");
        _corpo.Custo.Should().Be(3);
        _corpo.Polaridade.Should().Be("Positiva");
        resultado!.Canceled.Should().BeFalse();
        resultado.Data.Should().BeEquivalentTo(criada);
    }

    [Fact]
    public async Task A_refused_save_shows_the_api_message_and_keeps_the_dialog_open()
    {
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Já existe uma característica com esse Nome, Custo e Polaridade.") });
        var (cut, _) = await AbrirAsync();

        cut.Find("button:contains('Salvar')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Já existe uma característica com esse Nome, Custo e Polaridade."));
        cut.FindAll("button:contains('Salvar')").Should().NotBeEmpty();
    }
}
