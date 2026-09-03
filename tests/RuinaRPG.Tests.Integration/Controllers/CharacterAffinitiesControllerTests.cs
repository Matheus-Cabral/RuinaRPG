using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterAffinitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterAffinitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    [Fact]
    public async Task Add_a_valid_combination_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm1", "aff1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer1", "affplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Elemento == "Fogo" && a.ElementoValor == 3 && a.SubElemento == "Vida" && a.SubElementoValor == 2
            && a.CaminhoNome == "Caminho da Fênix" && a.Experiencia == 10);
    }

    [Fact]
    public async Task Add_an_invalid_Elemento_SubElemento_combination_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm2", "aff2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer2", "affplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Ar", 0, "Ferro", 0, "Caminho Inválido", 0)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm3", "aff3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer3", "affplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Terra", 1, "Vida", 1, "Caminho da Terra", 5)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>();
        body!.Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm4", "aff4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer4", "affplayer4@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer4b", "affplayer4b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", otherToken,
            new AddCharacterAffinityRequest("Terra", 1, "Vida", 1, "Caminho da Terra", 5)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm5", "aff5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer5", "affplayer5@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer5b", "affplayer5b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Add_with_every_field_null_returns_201_and_a_blank_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm6", "aff6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer6", "affplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        added!.Elemento.Should().BeNull();
        added.SubElemento.Should().BeNull();
        added.CaminhoNome.Should().BeNull();
        added.Experiencia.Should().BeNull();
    }

    [Fact]
    public async Task Add_with_only_Elemento_given_returns_201_without_checking_a_combination()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm7", "aff7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer7", "affplayer7@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        added!.Elemento.Should().Be("Fogo");
        added.SubElemento.Should().BeNull();
    }

    [Fact]
    public async Task Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm8", "aff8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer8", "affplayer8@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Terra", 5, "Aprimorar", 1, "Caminho da Terra", 8)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        updated!.Elemento.Should().Be("Terra");
        updated.ElementoValor.Should().Be(5);
        updated.SubElemento.Should().Be("Aprimorar");
        updated.SubElementoValor.Should().Be(1);
        updated.CaminhoNome.Should().Be("Caminho da Terra");
        updated.Experiencia.Should().Be(8);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>();
        body!.Should().ContainSingle(a => a.Id == added.Id && a.Elemento == "Terra" && a.SubElemento == "Aprimorar");
    }

    [Fact]
    public async Task Update_can_clear_fields_back_to_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm9", "aff9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer9", "affplayer9@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest(null, null, null, null, null, null)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();
        updated!.Elemento.Should().BeNull();
        updated.SubElemento.Should().BeNull();
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm10", "aff10@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer10", "affplayer10@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer10b", "affplayer10b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", otherToken,
            new UpdateCharacterAffinityRequest("Terra", 1, "Vida", 1, "Outro", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_a_nonexistent_affinity_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm11", "aff11@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer11", "affplayer11@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{Guid.NewGuid()}", playerToken,
            new UpdateCharacterAffinityRequest("Terra", 1, "Vida", 1, "Outro", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
