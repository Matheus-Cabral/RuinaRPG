using System.Net;
using FluentAssertions;

namespace RuinaRPG.Tests.Integration;

public class ApiTestBaseTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Helpers_register_a_gm_a_linked_player_and_a_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("BaseGm", "basegm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "BasePlayer", "baseplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Base");
        await GrantRulesAuditorAsync("basegm@teste.com");

        playerId.Should().NotBeNullOrEmpty();
        campaignId.Should().NotBeNullOrEmpty();
        var me = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", playerToken));
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
