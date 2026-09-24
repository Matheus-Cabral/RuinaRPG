using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class BancoDeMagiasFormTests : MudBunitContext
{
    // Info popups' inline <MudDialog> only renders its content through a MudDialogProvider present
    // elsewhere in the render tree (the real app has one in MainLayout) — same idiom as
    // ChangelogDialogTests/CatalogoItemPickerTests.
    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider(HttpClient http)
    {
        Services.AddScoped(_ => http);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<BancoDeMagiasForm>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task Geral_section_has_an_info_popup_with_the_exact_help_text()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("efeitos"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        var infoButton = cut.Find("button[title='Como criar uma Magia/Habilidade']");
        infoButton.GetAttribute("aria-label").Should().Be("Como criar uma Magia/Habilidade");

        infoButton.Click();

        var content = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        content.Should().Be(TextNormalization.Collapse(
            "Preencha Nome, Tipo (Magia, Habilidade ou Racial), Grau e Descrição. O Grau define quais Efeitos você pode comprar: estão disponíveis os Efeitos do Grau escolhido e de todos os Graus abaixo dele. Na seção Efeitos, escolha cada Efeito da lista. Efeitos com pré-requisito só aparecem depois que o pré-requisito já estiver na Magia. O Custo em PI de cada Efeito é calculado automaticamente, exceto nos Efeitos de custo Manual, em que o Mestre digita o valor. O Gasto em PI é a soma dos Efeitos, e o Custo em Foco é o Gasto em PI × 1,25, arredondado para cima. Ao salvar, a entrada fica no seu banco e pode ser usada como ponto de partida em qualquer ficha."));
    }

    [Fact]
    public async Task Clearing_the_required_Nome_field_blocks_the_save_call_in_edit_mode()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            // AddEfeitoForm (nested under this page) fetches the Efeito catalog on its own — answer
            // it separately from the spell-ability-bank entries GET below.
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("efeitos"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
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
    public async Task Create_with_a_400_from_the_server_shows_its_specific_message()
    {
        // R0007: "a tela guia, o servidor garante" — a server-side validation rejection (e.g.
        // EfeitoValidator) must reach the screen verbatim instead of a generic fallback.
        const string serverMessage = "Efeito \"Dano\" excede o teto de 3 para Grau/Círculo 1.";
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("efeitos"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
            if (request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(serverMessage) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeMagiasForm>();
        await Task.Delay(50); // let OnInitializedAsync finish (no EntryId, so it's a no-op)

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Bola de Fogo"));

        var salvar = cut.FindAll("button").Single(b => b.TextContent.Contains("Salvar"));
        await cut.InvokeAsync(() => salvar.Click());

        cut.Markup.Should().Contain(serverMessage);
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
            // AddEfeitoForm (nested under this page) fetches the Efeito catalog on its own — answer
            // it separately from the spell-ability-bank entries GET below.
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("efeitos"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
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
