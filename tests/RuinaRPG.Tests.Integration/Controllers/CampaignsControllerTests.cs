using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignsControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest("Nome", "Desc"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_the_campaign()
    {
        var token = await RegisterGmAndGetTokenAsync("CampGm1", "camp1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", token, new CreateCampaignRequest("A Ruína Aguarda", "Descrição")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignResponse>();
        body!.Nome.Should().Be("A Ruína Aguarda");
    }

    [Fact]
    public async Task List_returns_only_campaigns_owned_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("CampGmA", "campgma@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("CampGmB", "campgmb@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenA, new CreateCampaignRequest("Campanha A", "")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenB, new CreateCampaignRequest("Campanha B", "")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/campaigns", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>();
        body!.Should().ContainSingle(c => c.Nome == "Campanha A");
    }
}
