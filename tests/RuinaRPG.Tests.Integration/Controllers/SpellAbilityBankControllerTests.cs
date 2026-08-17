using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SpellAbilityBankControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SpellAbilityBankControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static CreateSpellAbilityEntryRequest BolaDeFogo() => new(
        "Bola de Fogo", "Magia", 3, "Uma explosão de fogo.",
        [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)]);

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/spell-ability-bank", BolaDeFogo());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_computes_GastoEmPI_and_Custo_from_the_effects_ignoring_any_client_supplied_totals()
    {
        var token = await RegisterGmAndGetTokenAsync("BankGm1", "bank1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", token, BolaDeFogo()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>();
        body!.GastoEmPI.Should().Be(14); // 8 + 6
        body.Custo.Should().Be(18); // ceil(14 * 1.25) = ceil(17.5) = 18
        body.Efeitos.Should().HaveCount(2);
    }
}
