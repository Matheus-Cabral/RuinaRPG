using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
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

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
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

    [Fact]
    public async Task Create_as_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("BankGm2", "bank2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "BankJogador1", "bankjogador1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", jogadorToken, BolaDeFogo()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> CreateAsync(string token, CreateSpellAbilityEntryRequest request) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", token, request));

    [Fact]
    public async Task List_returns_only_entries_created_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("BankGmA", "bankgma@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("BankGmB", "bankgmb@teste.com");
        await CreateAsync(tokenA, BolaDeFogo());
        await CreateAsync(tokenB, BolaDeFogo());

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        body!.Should().ContainSingle();
    }

    [Fact]
    public async Task List_can_filter_by_Tipo_and_Grau_together()
    {
        var token = await RegisterGmAndGetTokenAsync("BankGmFilter1", "bankfilter1@teste.com");
        await CreateAsync(token, BolaDeFogo()); // Magia, Grau 3
        await CreateAsync(token, new CreateSpellAbilityEntryRequest("Fúria", "Habilidade", 1, "Aumenta o dano.", []));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?tipo=Magia&grau=3", token));

        var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        body!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task List_can_filter_by_partial_Nome_case_insensitively()
    {
        var token = await RegisterGmAndGetTokenAsync("BankGmFilter2", "bankfilter2@teste.com");
        await CreateAsync(token, BolaDeFogo());

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?nome=bola", token));

        var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        body!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }
}
