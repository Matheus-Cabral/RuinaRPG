using Bunit;
using Bunit.TestDoubles;
using AngleSharp.Dom;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class MinhaCampanhaTests : MudBunitContext
{
    private readonly List<HttpRequestMessage> _requests = new();
    private List<SecretNoteResponse> _notes = new();
    private List<UnreadSecretNotesResponse> _unread = new();

    private void RegisterJogadorBackend()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("jogador");
        authContext.SetRoles("Jogador");
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("player-view"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PlayerCampaignViewResponse(new(), new(), new())) };
            if (path.EndsWith("secret-notes/unread"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_unread) };
            if (path.EndsWith("secret-notes/mark-read"))
            {
                _unread = new();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_notes) };
        }));
    }

    private int MarkReadCalls => _requests.Count(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("campaigns/campaign-1/secret-notes/mark-read"));

    private static string ActiveTab(IRenderedComponent<MinhaCampanha> cut) => cut.Find("div.mud-tab-active").TextContent;

    [Fact]
    public void Opens_on_the_Notas_Secretas_tab_and_marks_the_campaign_read_when_aba_is_notas()
    {
        RegisterJogadorBackend();
        Services.GetRequiredService<NavigationManager>().NavigateTo("campanhas/campaign-1/jogador?aba=notas");

        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));

        cut.WaitForAssertion(() =>
        {
            ActiveTab(cut).Should().Contain("Notas Secretas");
            MarkReadCalls.Should().Be(1);
        });
    }

    [Fact]
    public async Task Without_the_aba_parameter_it_opens_on_the_first_tab_and_marks_nothing_read()
    {
        RegisterJogadorBackend();

        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        await Task.Delay(100);

        ActiveTab(cut).Should().Contain("Minhas Fichas");
        MarkReadCalls.Should().Be(0);
    }

    [Fact]
    public async Task Notas_Secretas_tab_shows_the_unread_count_and_clears_it_when_opened()
    {
        RegisterJogadorBackend();
        _unread = new() { new("campaign-1", "Ruína", 2) };
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync();
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        IElement NotasTab() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Contains("Notas Secretas"));
        cut.WaitForAssertion(() => NotasTab().TextContent.Should().Contain("2"));

        await cut.InvokeAsync(() => NotasTab().Click());

        cut.WaitForAssertion(() =>
        {
            MarkReadCalls.Should().Be(1);
            NotasTab().TextContent.Trim().Should().Be("Notas Secretas");
        });
    }

    [Fact]
    public async Task A_note_arriving_while_on_the_Notas_Secretas_tab_reloads_the_list_and_ends_read()
    {
        RegisterJogadorBackend();
        Services.GetRequiredService<NavigationManager>().NavigateTo("campanhas/campaign-1/jogador?aba=notas");
        var notifier = Services.GetRequiredService<SecretNoteNotifier>();
        await notifier.StartAsync();
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        cut.WaitForAssertion(() => MarkReadCalls.Should().Be(1));
        _notes = new() { new SecretNoteResponse("n1", "Você percebe algo estranho.", DateTime.UtcNow, new(), new()) };

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("campaign-1", "Ruína"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Você percebe algo estranho.");
            MarkReadCalls.Should().Be(2);
            notifier.UnreadFor("campaign-1").Should().Be(0);
        });
    }

    [Fact]
    public async Task A_note_for_another_campaign_does_not_mark_this_one_read()
    {
        RegisterJogadorBackend();
        Services.GetRequiredService<NavigationManager>().NavigateTo("campanhas/campaign-1/jogador?aba=notas");
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync();
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        cut.WaitForAssertion(() => MarkReadCalls.Should().Be(1));

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("other", "Outra"));
        await Task.Delay(100);

        MarkReadCalls.Should().Be(1);
    }

    [Fact]
    public void Navigating_to_aba_notas_while_already_on_the_page_switches_to_the_tab()
    {
        RegisterJogadorBackend();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("campanhas/campaign-1/jogador");
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        cut.WaitForAssertion(() => ActiveTab(cut).Should().Contain("Minhas Fichas"));

        cut.InvokeAsync(() => navigation.NavigateTo("campanhas/campaign-1/jogador?aba=notas"));

        cut.WaitForAssertion(() =>
        {
            ActiveTab(cut).Should().Contain("Notas Secretas");
            MarkReadCalls.Should().Be(1);
        });
    }

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
