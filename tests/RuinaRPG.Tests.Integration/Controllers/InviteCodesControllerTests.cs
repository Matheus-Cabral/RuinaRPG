using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;

namespace RuinaRPG.Tests.Integration.Controllers;

public class InviteCodesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public InviteCodesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    [Fact]
    public async Task Generate_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/invite-codes", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Generate_returns_201_with_an_8_char_code_in_Ativo_status()
    {
        var token = await RegisterGmAndGetTokenAsync("ConviteGm1", "convite1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", token));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InviteCodeResponse>();
        body!.Code.Should().HaveLength(8);
        body.Status.Should().Be("Ativo");
        body.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));
        body.RedeemedByNickname.Should().BeNull();
        body.RedeemedAt.Should().BeNull();
    }

    [Fact]
    public async Task List_returns_only_codes_generated_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("ConviteGmA", "convitea@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("ConviteGmB", "conviteb@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", tokenA));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", tokenB));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/invite-codes", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<InviteCodeResponse>>();
        body!.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/invite-codes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
