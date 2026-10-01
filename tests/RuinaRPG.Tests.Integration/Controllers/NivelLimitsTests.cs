using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Auditoria da Tabela de Níveis: o último nível e os saldos por nível vêm da Tabela de Níveis do
/// banco, não mais de um "50" fixo nem de regex sobre o texto de bônus.
/// </summary>
public class NivelLimitsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NivelLimitsTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    [Fact]
    public async Task Npc_level_above_the_last_level_of_the_table_is_rejected()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NivelLimGm1", "nivellim1@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmToken);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 51))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 50))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Creature_level_above_the_last_level_of_the_table_is_rejected()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NivelLimGm2", "nivellim2@teste.com");
        var sheetId = await CreateCreatureSheetAsync(gmToken);

        var acima = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/nivel", gmToken, 51));
        acima.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await acima.Content.ReadAsStringAsync()).Should().Contain("O nível deve estar entre 1 e 50.");
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/nivel", gmToken, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/nivel", gmToken, 50))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // Expected values = what the regex calculators (deleted since) produced over the Markdown; the
    // full 50-level snapshot lives in the unit suite's NivelBonusExtractorTests.
    [Theory]
    [InlineData(1, 9, 5)]
    [InlineData(10, 13, 7)]
    [InlineData(25, 23, 8)]
    [InlineData(50, 56, 11)]
    public async Task Budgets_served_by_the_api_match_the_pre_refactor_snapshot(int nivel, int pontosDeAtributo, int espacosDeCaracteristica)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NivelSnapGm{nivel}", $"nivelsnap{nivel}@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmToken);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, nivel))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var atributos = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", gmToken)))
            .Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        var caracteristicas = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/traits", gmToken)))
            .Content.ReadFromJsonAsync<NpcTraitsListResponse>();

        atributos!.PontosDisponiveis.Should().Be(pontosDeAtributo);
        caracteristicas!.PontosDisponiveis.Should().Be(espacosDeCaracteristica);
    }
}
