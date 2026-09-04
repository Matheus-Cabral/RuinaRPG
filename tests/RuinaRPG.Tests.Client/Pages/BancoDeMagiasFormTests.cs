using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class BancoDeMagiasFormTests : MudBunitContext
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
                    new { Id = "entry-1", Nome = "Bola de Fogo", Tipo = "Magia", Grau = 1, Descricao = "",
                          Efeitos = new List<object>() }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeMagiasForm>(p => p.Add(x => x.EntryId, "entry-1"));
        await Task.Delay(50); // let OnInitializedAsync finish populating _form

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync(""));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("an empty Nome violates [Required] and must not reach the server");
    }

    [Fact]
    public async Task Editing_a_missing_entry_shows_an_error_instead_of_crashing()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) }; // no entries at all
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        // Regression test: with _editContext assigned late inside OnInitializedAsync (only after
        // an existing entry was found and its fields copied), the "entry not found" early-return
        // left _editContext null, and <EditForm EditContext="_editContext"> threw
        // InvalidOperationException on render. _editContext must now be built synchronously
        // (constructor / field initializer) so this path renders safely.
        var act = () => Render<BancoDeMagiasForm>(p => p.Add(x => x.EntryId, "does-not-exist"));
        act.Should().NotThrow();

        var cut = act();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Entrada não encontrada.");
    }

    [Fact]
    public async Task A_real_non_synchronous_HTTP_round_trip_does_not_crash_first_render()
    {
        // Regression test: FakeHttpMessageHandler completes its Task synchronously, so the whole
        // OnInitializedAsync await-chain used to run to completion before Blazor's first render —
        // masking the fact that _editContext was assigned only after the last await. A handler
        // that genuinely yields forces Blazor to render once while OnInitializedAsync's task is
        // still pending, which is what a real HTTP call over the network would also do.
        var http = AsyncFakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "entry-1", Nome = "Bola de Fogo", Tipo = "Magia", Grau = 1, Descricao = "",
                          Efeitos = new List<object>() }
                }) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var act = () => Render<BancoDeMagiasForm>(p => p.Add(x => x.EntryId, "entry-1"));
        act.Should().NotThrow();

        var cut = act();
        await Task.Delay(200); // let the async round trip finish populating _form

        cut.Markup.Should().Contain("Bola de Fogo");
    }

    /// <summary>
    /// Unlike <see cref="FakeHttpMessageHandler"/>, actually yields before responding, so awaits
    /// on it do not resolve synchronously — reproducing the timing of a real HTTP round trip.
    /// </summary>
    private sealed class AsyncFakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        private AsyncFakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return _respond(request);
        }

        public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) => new(new AsyncFakeHttpMessageHandler(respond))
        {
            BaseAddress = new Uri("http://localhost/api/"),
        };
    }
}
