using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class BancoDeRunasFormTests : MudBunitContext
{
    [Fact]
    public async Task Clearing_the_required_Nome_field_blocks_the_save_call_in_edit_mode()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "entry-1", Nome = "Runa do Fogo", Descricao = "Queima.", Grau = 1 }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>(p => p.Add(x => x.EntryId, "entry-1"));
        await Task.Delay(50); // let OnInitializedAsync populate the form

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync(""));

        await Task.Delay(700); // past the autosave debounce

        putCalled.Should().BeFalse("an empty Nome violates [Required] and must not reach the server");
    }

    [Fact]
    public async Task Editing_saves_the_changed_fields_with_a_PUT_to_the_rune_bank()
    {
        string? putPath = null;
        string? putBody = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "entry-1", Nome = "Runa do Fogo", Descricao = "Queima.", Grau = 1 }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putPath = request.RequestUri!.AbsolutePath;
                putBody = request.Content!.ReadAsStringAsync().Result;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>(p => p.Add(x => x.EntryId, "entry-1"));
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Runa do Fogo Maior"));

        await Task.Delay(700);

        putPath.Should().EndWith("rune-bank/entry-1");
        putBody.Should().Contain("Runa do Fogo Maior");
    }

    [Fact]
    public async Task Create_with_a_400_from_the_server_shows_its_specific_message()
    {
        const string serverMessage = "Nome da runa inválido.";
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(serverMessage) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>();
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Runa do Fogo"));

        var salvar = cut.FindAll("button").Single(b => b.TextContent.Contains("Salvar"));
        await cut.InvokeAsync(() => salvar.Click());

        cut.Markup.Should().Contain(serverMessage);
    }

    [Fact]
    public async Task Editing_a_missing_entry_shows_an_error_instead_of_crashing()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>(p => p.Add(x => x.EntryId, "does-not-exist"));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Entrada não encontrada.");
    }
}
