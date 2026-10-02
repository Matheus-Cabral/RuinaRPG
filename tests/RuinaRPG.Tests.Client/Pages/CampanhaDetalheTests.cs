using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// Covers Finding 3 of the autosave-on-blur final review: the "Detalhes" tab's SaveIfValidAsync
/// never validated, so clearing "Nome" and blurring auto-saved an empty campaign name.
/// </summary>
public class CampanhaDetalheTests : MudBunitContext
{
    private const string CampaignId = "campaign-1";

    private IRenderedComponent<CampanhaDetalhe> RenderDetalhesTab(Func<HttpRequestMessage, HttpResponseMessage> respond, decimal bonusDeCarga = 0m)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm-user");
        authContext.SetRoles("GM");

        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("campaigns"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null, bonusDeCarga) })
                };
            }

            if (request.Method == HttpMethod.Put)
                return respond(request);

            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CampanhaDetalhe>(p => p.Add(x => x.CampaignId, CampaignId));
        return cut;
    }

    private static void GoToDetalhesTab(IRenderedComponent<CampanhaDetalhe> cut)
    {
        var tabHeader = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Detalhes");
        cut.InvokeAsync(() => tabHeader.Click());
    }

    private static void GoToAnexosTab(IRenderedComponent<CampanhaDetalhe> cut)
    {
        var tabHeader = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Anexos");
        cut.InvokeAsync(() => tabHeader.Click());
    }

    /// <summary>
    /// Finding 7 of the final whole-branch review: the "Entrada do Banco de Magias" picker on the
    /// Anexos tab (used to publish a Banco entry, Passiva included, to the campaign) formatted every
    /// result as "Nome (Tipo, Grau N)" — for a Passiva (always Grau 0) that read as the meaningless
    /// "(Passiva, Grau 0)". It must show the Categoria instead, like the rest of the app does for
    /// Passivas (BancoDeMagias list, HabilidadesPassivasSection).
    /// </summary>
    [Fact]
    public async Task Anexos_tab_banco_picker_labels_a_passiva_by_categoria_not_grau()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm-user");
        authContext.SetRoles("GM");

        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("campaigns"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null) })
                };

            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("spell-ability-bank"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new { Id = "p1", Nome = "Pele de Pedra", Tipo = "Passiva", Grau = 0, GastoEmPI = 0, Custo = 0, Descricao = "d", Efeitos = new List<object>(), DeCriatura = false, Categoria = (string?)"Vocacional" },
                        new { Id = "m1", Nome = "Bola de Fogo", Tipo = "Magia", Grau = 2, GastoEmPI = 3, Custo = 4, Descricao = "d", Efeitos = new List<object>(), DeCriatura = false, Categoria = (string?)null },
                    })
                };

            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CampanhaDetalhe>(p => p.Add(x => x.CampaignId, CampaignId));
        await Task.Delay(100);

        GoToAnexosTab(cut);
        await Task.Delay(50);

        var picker = cut.FindComponents<EntityPicker>().Single(c => c.Instance.Placeholder == "Buscar magia/habilidade...");
        var results = await picker.Instance.SearchAsyncForTests("");

        results.Should().Contain(o => o.Id == "p1" && o.Label == "Pele de Pedra (Passiva Vocacional)");
        results.Should().Contain(o => o.Id == "m1" && o.Label == "Bola de Fogo (Magia, Grau 2)");
    }

    [Fact]
    public async Task Clearing_Nome_and_blurring_does_not_call_PUT()
    {
        var putCalled = false;
        var cut = RenderDetalhesTab(request =>
        {
            putCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await Task.Delay(100); // let OnInitializedAsync finish populating _detailsForm

        GoToDetalhesTab(cut);
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync(""));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("an empty Nome violates [Required(AllowEmptyStrings = false)] and must not reach the server");
    }

    [Fact]
    public async Task Setting_a_valid_Nome_and_blurring_calls_PUT()
    {
        var putCalled = false;
        var cut = RenderDetalhesTab(request =>
        {
            putCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await Task.Delay(100);

        GoToDetalhesTab(cut);
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Nova Campanha"));

        await Task.Delay(700);

        putCalled.Should().BeTrue("a non-empty Nome passes validation and the field blur must trigger the auto-save PUT");
    }

    [Fact]
    public async Task Detalhes_tab_shows_the_loaded_BonusDeCarga_with_its_info_popup()
    {
        var cut = RenderDetalhesTab(_ => new HttpResponseMessage(HttpStatusCode.OK), bonusDeCarga: -4m);
        await Task.Delay(100);

        GoToDetalhesTab(cut);
        await Task.Delay(50);

        cut.Markup.Should().Contain("Bônus de carga dos personagens");
        cut.FindAll("input").Select(i => i.GetAttribute("value")).Should().Contain("-4");
        cut.FindAll("button[title='Bônus de carga']").Should().HaveCount(1);
    }

    [Fact]
    public async Task Editing_the_BonusDeCarga_autosaves_with_the_new_value()
    {
        string? body = null;
        var cut = RenderDetalhesTab(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await Task.Delay(100);
        GoToDetalhesTab(cut);
        await Task.Delay(50);

        var field = cut.FindComponents<MudBlazor.MudNumericField<decimal>>().Single(c => c.Instance.Label == "Bônus de carga dos personagens");
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(12.5m));
        await Task.Delay(700);

        body.Should().NotBeNull("changing the bonus must trigger the autosave PUT");
        body.Should().Contain("\"bonusDeCarga\":12.5");
    }
}
