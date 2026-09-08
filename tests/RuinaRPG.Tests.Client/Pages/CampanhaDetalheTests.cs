using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
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

    private IRenderedComponent<CampanhaDetalhe> RenderDetalhesTab(Func<HttpRequestMessage, HttpResponseMessage> respond)
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
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null) })
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
}
