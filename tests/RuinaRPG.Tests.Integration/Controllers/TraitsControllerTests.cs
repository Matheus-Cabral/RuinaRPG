using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class TraitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public TraitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/traits");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_with_no_filter_returns_a_real_Id_for_a_known_trait()
    {
        // TraitId is what AddCharacterTraitRequest (and the Npc/Creature equivalents) actually
        // needs — this is the one endpoint that lets a caller resolve it, unlike
        // CompendioController's search, which only ever returns Nome/Descricao text.
        var token = await RegisterGmAndGetTokenAsync("TraitGm1", "trait1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<TraitResponse>>();
        var alfabetizado = body!.Should().ContainSingle(t => t.Nome == "Alfabetizado").Subject;
        Guid.TryParse(alfabetizado.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task List_can_filter_by_partial_Nome_case_insensitively()
    {
        var token = await RegisterGmAndGetTokenAsync("TraitGm2", "trait2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits?nome=alfabet", token));

        var body = await response.Content.ReadFromJsonAsync<List<TraitResponse>>();
        body!.Should().ContainSingle(t => t.Nome == "Alfabetizado");
    }
}
