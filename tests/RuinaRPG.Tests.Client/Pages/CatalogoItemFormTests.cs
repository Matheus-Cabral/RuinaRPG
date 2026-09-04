using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class CatalogoItemFormTests : MudBunitContext
{
    [Fact]
    public async Task An_out_of_range_Preco_blocks_the_save_call_in_edit_mode()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("items"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "item-1", Tipo = "ItemGeral", Nome = "Poção", ImageUrl = (string?)null, Peso = 1m, Preco = 10,
                          Subcategoria = (string?)null, Descricao = (string?)null, Tier = (string?)null, Empunhadura = (string?)null,
                          Dados = (string?)null, Dano = (int?)null, Critico = (string?)null, Alcance = (int?)null, TipoDeDano = (string?)null,
                          RequisitoAtributo = (string?)null, DurabilidadeMaxima = (int?)null, Categoria = (string?)null, Defesa = (int?)null,
                          RF = (int?)null, RM = (int?)null, Penalidade = (string?)null, RequisitoVigor = (int?)null, BonusDefesa = (int?)null,
                          TipoDeAlvo = (string?)null, Alvo = (string?)null, Valor = (int?)null }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>(p => p.Add(x => x.ItemId, "item-1"));
        await Task.Delay(50); // let OnInitializedAsync finish populating _form

        var preco = cut.FindComponents<MudBlazor.MudNumericField<int>>().Single(c => c.Instance.Label == "Preço (Ciclos)");
        await cut.InvokeAsync(() => preco.Instance.ValueChanged.InvokeAsync(-5));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("a negative Preço violates [Range(0, int.MaxValue)] and must not reach the server");
    }

    [Fact]
    public async Task Blurring_a_field_in_create_mode_never_calls_PUT()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CatalogoItemForm>();
        await Task.Delay(50); // let OnInitializedAsync finish (create mode has nothing to load)

        var preco = cut.FindComponents<MudBlazor.MudNumericField<int>>().Single(c => c.Instance.Label == "Preço (Ciclos)");
        await cut.InvokeAsync(() => preco.Instance.ValueChanged.InvokeAsync(5));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("create mode has no ItemId to PUT against — field blur must not trigger auto-save there");
    }

    [Fact]
    public async Task Editing_a_missing_item_shows_an_error_instead_of_crashing()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("items"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) }; // no items at all
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        // Regression test: with _editContext assigned late inside OnInitializedAsync (only after
        // an existing item was found and its fields copied), the "item not found" early-return
        // left _editContext null, and <EditForm EditContext="_editContext"> threw
        // InvalidOperationException on render. _editContext must now be built synchronously
        // (constructor / field initializer) so this path renders safely.
        var act = () => Render<CatalogoItemForm>(p => p.Add(x => x.ItemId, "does-not-exist"));
        act.Should().NotThrow();

        var cut = act();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Item não encontrado.");
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
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("images/mine"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("items"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "item-1", Tipo = "ItemGeral", Nome = "Poção", ImageUrl = (string?)null, Peso = 1m, Preco = 10,
                          Subcategoria = (string?)null, Descricao = (string?)null, Tier = (string?)null, Empunhadura = (string?)null,
                          Dados = (string?)null, Dano = (int?)null, Critico = (string?)null, Alcance = (int?)null, TipoDeDano = (string?)null,
                          RequisitoAtributo = (string?)null, DurabilidadeMaxima = (int?)null, Categoria = (string?)null, Defesa = (int?)null,
                          RF = (int?)null, RM = (int?)null, Penalidade = (string?)null, RequisitoVigor = (int?)null, BonusDefesa = (int?)null,
                          TipoDeAlvo = (string?)null, Alvo = (string?)null, Valor = (int?)null }
                }) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var act = () => Render<CatalogoItemForm>(p => p.Add(x => x.ItemId, "item-1"));
        act.Should().NotThrow();

        var cut = act();
        await Task.Delay(200); // let the async round trips finish populating _form

        cut.Markup.Should().Contain("Poção");
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
