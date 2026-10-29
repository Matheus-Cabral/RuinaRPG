using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using RuinaRPG.Contracts.Campaigns;
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
}
