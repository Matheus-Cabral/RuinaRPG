using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcRacialTraitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcRacialTraitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private static UpdateNpcSheetRequest ValidUpdate(string linhagem, string variante) => new(
        null, "Teste", linhagem, variante, "Campeao", "Duelista", "Fogo", null,
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, null);

    private async Task SetVarianteAsync(string gmToken, string sheetId, string linhagem, string variante)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate(linhagem, variante)));
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PendingRacialTraitChoice_is_false_when_Variante_is_still_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRacialGm1", "npcracialtrait1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-traits/pending", gmToken));

        var body = await response.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>();
        body!.Pending.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_for_a_Variante_with_a_real_Obrigatoria_choice_grants_both_picks_at_zero_cost()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRacialGm2", "npcracialtrait2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetVarianteAsync(gmToken, sheetId, "Phylauc", "PhylacTai");

        var resolveResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/racial-traits/resolve", gmToken,
            new ResolveRacialTraitChoiceRequest("Imunidade de Venenos (2 pontos)", "Crédulo")));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var traitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/traits", gmToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<NpcTraitsListResponse>();
        traits!.Positivas.Should().ContainSingle(t => t.Nome == "Imunidade de Venenos (2 pontos)" && t.Custo == 0 && t.IsRacial);
        traits.Negativas.Should().ContainSingle(t => t.Nome == "Crédulo" && t.Custo == 0 && t.IsRacial);
        traits.TotalPositivas.Should().Be(0);
        traits.TotalNegativas.Should().Be(0);

        var pendingResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-traits/pending", gmToken));
        (await pendingResponse.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>())!.Pending.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_with_an_Obrigatoria_pick_outside_the_allowed_options_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRacialGm3", "npcracialtrait3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetVarianteAsync(gmToken, sheetId, "Phylauc", "PhylacTai");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/racial-traits/resolve", gmToken,
            new ResolveRacialTraitChoiceRequest("Coragem", "Curioso")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Changing_Variante_removes_the_stale_racial_grants_and_reopens_the_pending_choice()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRacialGm4", "npcracialtrait4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetVarianteAsync(gmToken, sheetId, "Humano", "Sinir");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/racial-traits/resolve", gmToken,
            new ResolveRacialTraitChoiceRequest("Alfabetizado", null)));

        await SetVarianteAsync(gmToken, sheetId, "Humano", "Laonir");

        var traitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/traits", gmToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<NpcTraitsListResponse>();
        traits!.Positivas.Should().NotContain(t => t.Nome == "Alfabetizado");

        var pendingResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-traits/pending", gmToken));
        (await pendingResponse.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>())!.Pending.Should().BeTrue();
    }

    [Fact]
    public async Task PendingRacialTraitChoice_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcRacialGmOwner5", "npcracialtraitowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcRacialGmOther5", "npcracialtraitother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-traits/pending", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
