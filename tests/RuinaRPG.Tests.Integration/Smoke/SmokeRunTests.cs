using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Smoke;

namespace RuinaRPG.Tests.Integration.Smoke;

public class SmokeRunTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Every_planned_request_succeeds_against_a_populated_api()
    {
        var gm = await RegisterGmAndGetTokenAsync("SmokeGm", "smokegm@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Smoke");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "SmokePlayer", "smokeplayer@teste.com");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gm));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gm));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gm, new CreateCharacterSheetRequest(playerId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gm, new CreateEncounterRequest("Smoke")));

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var plan = await SmokePlan.BuildAsync(db);

        var failures = await SmokeRunner.RunAsync(Client, jwt, db, plan);

        plan.Should().Contain(r => r.Path == $"/api/campaigns/{campaignId}/members");
        plan.Should().Contain(r => r.Path.StartsWith("/api/npc-sheets/"));
        plan.Should().Contain(r => r.Path.StartsWith("/api/creature-sheets/"));
        plan.Should().Contain(r => r.Path.StartsWith("/api/character-sheets/"));
        plan.Should().Contain(r => r.Path.EndsWith("/log"));
        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task A_broken_route_is_reported()
    {
        await RegisterGmAndGetTokenAsync("SmokeGm2", "smokegm2@teste.com");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var gmId = (await db.Users.SingleAsync(u => u.Email == "smokegm2@teste.com")).Id;

        var failures = await SmokeRunner.RunAsync(Client, jwt, db, [new SmokeRequest(gmId, "/api/does-not-exist")]);

        failures.Should().ContainSingle().Which.Should().Be("404 /api/does-not-exist");
    }
}
