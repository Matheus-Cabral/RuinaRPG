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

public class MinhaCampanhaTests : MudBunitContext
{
    /// <summary>
    /// Campanha R0009: o jogador vê os anexos públicos com a mesma organização do GM — agrupados por
    /// tipo e filtráveis —, mas sem filtro de visibilidade (tudo o que ele recebe é público) e sem
    /// controles de edição.
    /// </summary>
    [Fact]
    public async Task Anexos_publicos_tab_shows_the_public_attachments_grouped_with_their_facets_and_no_visibility_filter()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("jogador");
        authContext.SetRoles("Jogador");
        var facets = new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Espadas");
        var view = new PlayerCampaignViewResponse(new(), new(), new()
        {
            new PublicAttachmentSummary("a1", "Item", "Espada", null, facets),
            new PublicAttachmentSummary("a2", "Image", null, "/images/mapa.png"),
        });
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith("player-view")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(view) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) }));

        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        await Task.Delay(100);
        var tab = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Anexos Públicos");
        await cut.InvokeAsync(() => tab.Click());
        await Task.Delay(50);

        var lista = cut.FindComponent<AnexosAgrupados>().Instance;
        lista.FiltrarPorVisibilidade.Should().BeFalse();
        lista.Visibilidade.Should().BeNull();
        lista.Acoes.Should().BeNull();
        lista.Anexos.Should().BeEquivalentTo(new[]
        {
            new AnexoView("a1", "Item", "Espada", null, facets, null),
            new AnexoView("a2", "Image", null, "/images/mapa.png", null, null),
        });
    }
}
