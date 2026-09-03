using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterRunesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterRunesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
    public async Task Add_a_valid_rune_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm1", "rune1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer1", "runeplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa do Fogo", "Queima o alvo.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>();
        body!.Should().ContainSingle(r => r.Nome == "Runa do Fogo" && r.Descricao == "Queima o alvo." && r.Grau == 1);
    }

    [Fact]
    public async Task List_returns_the_runes_on_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm2", "rune2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer2", "runeplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa da Terra", "Endurece a pele.", 2)));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", playerToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>();
        body!.Should().ContainSingle(r => r.Nome == "Runa da Terra" && r.Descricao == "Endurece a pele." && r.Grau == 2);
    }

    [Fact]
    public async Task Delete_an_existing_rune_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm3", "rune3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer3", "runeplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa da Água", "Cura ferimentos leves.", 3)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterRuneResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/runes/{added!.Id}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>();
        body!.Should().NotContain(r => r.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm4", "rune4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer4", "runeplayer4@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer4b", "runeplayer4b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", otherToken,
            new AddCharacterRuneRequest("Runa do Vento", "Aumenta velocidade.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_an_existing_rune_returns_200_and_the_list_reflects_the_change()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm5", "rune5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer5", "runeplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa do Fogo", "Queima o alvo.", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterRuneResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/runes/{added!.Id}", playerToken,
            new UpdateCharacterRuneRequest("Runa do Gelo", "Congela o alvo.", 2)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterRuneResponse>();
        updated!.Nome.Should().Be("Runa do Gelo");
        updated.Descricao.Should().Be("Congela o alvo.");
        updated.Grau.Should().Be(2);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/runes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterRuneResponse>>();
        body!.Should().ContainSingle(r => r.Id == added.Id && r.Nome == "Runa do Gelo" && r.Descricao == "Congela o alvo." && r.Grau == 2);
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm6", "rune6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer6", "runeplayer6@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer6b", "runeplayer6b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa da Luz", "Ilumina a área.", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterRuneResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/runes/{added!.Id}", otherToken,
            new UpdateCharacterRuneRequest("Runa da Sombra", "Escurece a área.", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_a_nonexistent_rune_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneGm7", "rune7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RunePlayer7", "runeplayer7@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/runes/{Guid.NewGuid()}", playerToken,
            new UpdateCharacterRuneRequest("Runa Inexistente", "N/A", 1)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
