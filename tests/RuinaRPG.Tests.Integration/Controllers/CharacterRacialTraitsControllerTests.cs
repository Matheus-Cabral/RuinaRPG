using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterRacialTraitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterRacialTraitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest ValidUpdate(string linhagem, string variante) => new(
        null, "Teste", linhagem, variante, "Campeao", "Duelista", "Fogo", null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null);

    private async Task<string> SetVarianteAsync(string gmToken, string playerToken, string sheetId, string linhagem, string variante)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate(linhagem, variante)));
        response.EnsureSuccessStatusCode();
        return sheetId;
    }

    [Fact]
    public async Task PendingRacialTraitChoice_is_false_when_Variante_is_still_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm1", "charracial1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer1", "charracialplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));

        var body = await response.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>();
        body!.Pending.Should().BeFalse();
    }

    [Fact]
    public async Task PendingRacialTraitChoice_is_true_with_the_right_options_once_a_Variante_with_no_Obrigatoria_is_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm2", "charracial2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer2", "charracialplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));

        var body = await response.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>();
        body!.Pending.Should().BeTrue();
        body.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Alfabetizado", "Sedutor", "Aparência Inofensiva (2 pontos)");
        body.Obrigatoria.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_for_a_Variante_with_no_Obrigatoria_grants_only_the_Gratuita_pick_at_zero_cost()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm3", "charracial3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer3", "charracialplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");

        var resolveResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Alfabetizado", null)));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pendingResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));
        (await pendingResponse.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>())!.Pending.Should().BeFalse();

        var traitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<CharacterTraitsListResponse>();
        var granted = traits!.Positivas.Should().ContainSingle(t => t.Nome == "Alfabetizado").Subject;
        granted.Custo.Should().Be(0); // real catalog cost is 1 — racial grants are free
        granted.IsRacial.Should().BeTrue();
        traits.TotalPositivas.Should().Be(0); // must not count toward the budget total either
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_for_a_Variante_with_a_real_Obrigatoria_choice_grants_both_picks()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm4", "charracial4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer4", "charracialplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Phylauc", "PhylacTai");

        var resolveResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Imunidade de Venenos (2 pontos)", "Crédulo")));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var traitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<CharacterTraitsListResponse>();
        traits!.Positivas.Should().ContainSingle(t => t.Nome == "Imunidade de Venenos (2 pontos)" && t.Custo == 0 && t.IsRacial);
        traits.Negativas.Should().ContainSingle(t => t.Nome == "Crédulo" && t.Custo == 0 && t.IsRacial);
        traits.TotalPositivas.Should().Be(0);
        traits.TotalNegativas.Should().Be(0);
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_for_Alora_auto_grants_the_fixed_Obrigatoria_with_its_Especificacao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm5", "charracial5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer5", "charracialplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Econos", "Alora");

        var resolveResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Detectar Magia", null)));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var traitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<CharacterTraitsListResponse>();
        traits!.Positivas.Should().ContainSingle(t => t.Nome == "Detectar Magia" && t.IsRacial);
        traits.Negativas.Should().ContainSingle(t => t.Nome == "Desvantagem Elemental" && t.Especificacao == "Fogo" && t.IsRacial);
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_with_a_Gratuita_pick_outside_the_allowed_options_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm6", "charracial6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer6", "charracialplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Coragem", null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Changing_Variante_removes_the_stale_racial_grants_and_reopens_the_pending_choice()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm7", "charracial7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer7", "charracialplayer7@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Alfabetizado", null)));

        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Laonir");

        var traitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<CharacterTraitsListResponse>();
        traits!.Positivas.Should().NotContain(t => t.Nome == "Alfabetizado");

        var pendingResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));
        (await pendingResponse.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>())!.Pending.Should().BeTrue();
    }
}
