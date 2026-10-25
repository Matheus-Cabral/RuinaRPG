using System.Collections.Concurrent;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Hubs;

/// <summary>Cemitério de fichas: o dono recebe FichaNoCemiterioAlterada só quando a ficha entra ou sai do cemitério.</summary>
public class CemiterioHubTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static readonly TimeSpan Arrives = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaysSilent = TimeSpan.FromSeconds(1);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private sealed record Cenario(string GmToken, string PlayerId, string PlayerToken, string CampaignId, string SheetId);

    private async Task<Cenario> MontarAsync()
    {
        var gmToken = await RegisterGmAndGetTokenAsync(Unique("Gm"), Unique("gm") + "@t.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, Unique("Jg"), Unique("jg") + "@t.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Camp");
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId))))
            .EnsureSuccessStatusCode();
        var created = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        created.EnsureSuccessStatusCode();
        return new Cenario(gmToken, playerId, playerToken, campaignId, (await created.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id);
    }

    private Task<HttpResponseMessage> DefinirMorteAsync(Cenario c, bool morto) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/morte", c.GmToken, new DefinirMorteRequest(morto)));

    private async Task<(HubConnection Connection, ConcurrentQueue<string> Recebidas, SemaphoreSlim Chegou)> OuvirAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();
        var recebidas = new ConcurrentQueue<string>();
        var chegou = new SemaphoreSlim(0);
        connection.On<string>(NotificationEvents.FichaNoCemiterioAlterada, id => { recebidas.Enqueue(id); chegou.Release(); });
        await connection.StartAsync();
        return (connection, recebidas, chegou);
    }

    [Fact]
    public async Task Dono_e_avisado_quando_a_ficha_entra_e_sai_do_cemiterio()
    {
        var c = await MontarAsync();
        var (connection, recebidas, chegou) = await OuvirAsync(c.PlayerToken);
        await using var _ = connection;

        (await DefinirMorteAsync(c, true)).EnsureSuccessStatusCode();
        (await chegou.WaitAsync(Arrives)).Should().BeTrue();
        (await DefinirMorteAsync(c, false)).EnsureSuccessStatusCode();
        (await chegou.WaitAsync(Arrives)).Should().BeTrue();

        await Task.Delay(StaysSilent);
        recebidas.Should().Equal(c.SheetId, c.SheetId);
    }

    [Fact]
    public async Task Desmarcar_morto_com_dono_fora_da_campanha_nao_avisa()
    {
        var c = await MontarAsync();
        (await DefinirMorteAsync(c, true)).EnsureSuccessStatusCode();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            await db.CampaignMembers.Where(m => m.CampaignId == Guid.Parse(c.CampaignId) && m.UserId == Guid.Parse(c.PlayerId)).ExecuteDeleteAsync();
        }
        var (connection, recebidas, chegou) = await OuvirAsync(c.PlayerToken);
        await using var _ = connection;

        (await DefinirMorteAsync(c, false)).EnsureSuccessStatusCode();

        (await chegou.WaitAsync(StaysSilent)).Should().BeFalse("o estado continua no cemitério (RemovidoDaCampanha)");
        recebidas.Should().BeEmpty();
    }
}
