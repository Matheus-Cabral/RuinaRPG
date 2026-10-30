using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Tests.Integration.Hubs;

/// <summary>Campanha R0018: o jogador removido, com o sistema aberto, é avisado para o painel se atualizar.</summary>
public class RemoverMembroHubTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static readonly TimeSpan Arrives = TimeSpan.FromSeconds(5);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task Jogador_removido_recebe_CampanhasDoJogadorAlteradas()
    {
        var gm = await RegisterGmAndGetTokenAsync(Unique("Gm"), Unique("gm") + "@t.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, Unique("Jg"), Unique("jg") + "@t.com");
        var campaignId = await CreateCampaignAsync(gm, "Camp");
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId))))
            .EnsureSuccessStatusCode();

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(playerToken);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();
        var chegou = new SemaphoreSlim(0);
        connection.On(NotificationEvents.CampanhasDoJogadorAlteradas, () => chegou.Release());
        await connection.StartAsync();

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campaignId}/members/{playerId}", gm))).EnsureSuccessStatusCode();

        (await chegou.WaitAsync(Arrives)).Should().BeTrue();
    }

    private sealed record Cenario(string GmToken, string PlayerId, string PlayerToken, string CampaignId, string SheetId);

    private async Task<Cenario> MontarComFichaAsync()
    {
        var gm = await RegisterGmAndGetTokenAsync(Unique("Gm"), Unique("gm") + "@t.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, Unique("Jg"), Unique("jg") + "@t.com");
        var campaignId = await CreateCampaignAsync(gm, "Camp");
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId))))
            .EnsureSuccessStatusCode();
        var created = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gm, new CreateCharacterSheetRequest(playerId)));
        created.EnsureSuccessStatusCode();
        return new Cenario(gm, playerId, playerToken, campaignId, (await created.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id);
    }

    private async Task<(HubConnection Connection, ConcurrentQueue<string> Fichas, SemaphoreSlim FichaChegou, SemaphoreSlim PainelChegou)> OuvirAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();
        var fichas = new ConcurrentQueue<string>();
        var fichaChegou = new SemaphoreSlim(0);
        var painelChegou = new SemaphoreSlim(0);
        connection.On<string>(NotificationEvents.FichaNoCemiterioAlterada, id => { fichas.Enqueue(id); fichaChegou.Release(); });
        connection.On(NotificationEvents.CampanhasDoJogadorAlteradas, () => painelChegou.Release());
        await connection.StartAsync();
        return (connection, fichas, fichaChegou, painelChegou);
    }

    [Fact]
    public async Task Jogador_removido_recebe_FichaNoCemiterioAlterada_de_cada_ficha_sua_na_campanha()
    {
        var c = await MontarComFichaAsync();
        var (connection, fichas, fichaChegou, _) = await OuvirAsync(c.PlayerToken);
        await using var _ = connection;

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{c.CampaignId}/members/{c.PlayerId}", c.GmToken))).EnsureSuccessStatusCode();

        (await fichaChegou.WaitAsync(Arrives)).Should().BeTrue();
        fichas.Should().Equal(c.SheetId);
    }

    [Fact]
    public async Task Jogador_readicionado_recebe_FichaNoCemiterioAlterada_e_CampanhasDoJogadorAlteradas()
    {
        var c = await MontarComFichaAsync();
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{c.CampaignId}/members/{c.PlayerId}", c.GmToken))).EnsureSuccessStatusCode();
        var (connection, fichas, fichaChegou, painelChegou) = await OuvirAsync(c.PlayerToken);
        await using var _ = connection;

        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/members", c.GmToken, new AddCampaignMemberRequest(c.PlayerId))))
            .EnsureSuccessStatusCode();

        (await fichaChegou.WaitAsync(Arrives)).Should().BeTrue();
        (await painelChegou.WaitAsync(Arrives)).Should().BeTrue();
        fichas.Should().Equal(c.SheetId);
    }

    [Fact]
    public async Task Readicionar_membro_que_ja_e_membro_nao_avisa()
    {
        var c = await MontarComFichaAsync();
        var (connection, _, fichaChegou, painelChegou) = await OuvirAsync(c.PlayerToken);
        await using var _ = connection;

        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/members", c.GmToken, new AddCampaignMemberRequest(c.PlayerId))))
            .EnsureSuccessStatusCode();

        (await fichaChegou.WaitAsync(TimeSpan.FromSeconds(1))).Should().BeFalse();
        (await painelChegou.WaitAsync(TimeSpan.FromMilliseconds(100))).Should().BeFalse();
    }
}
