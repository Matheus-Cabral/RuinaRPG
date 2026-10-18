using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

// Base for API test classes: the class's ApiFixture plus the helpers that used to be copied into
// dozens of classes. Helpers used by only a few classes stay in those classes.
public abstract class ApiTestBase(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    protected ApiFixture Fixture { get; } = fixture;
    protected ApiFactory Factory => Fixture.Factory;
    protected HttpClient Client => Fixture.Client;

    protected static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    protected async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    protected async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await Client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var meResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens!.AccessToken));
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    protected async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    protected async Task<string> GrantRulesAuditorAndReturnEmailAsync(string email)
    {
        await GrantRulesAuditorAsync(email);
        return email;
    }

    protected async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }
}
