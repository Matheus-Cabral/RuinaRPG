using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterMasteriesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterMasteriesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
    public async Task Add_a_valid_mastery_returns_201()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MasteryGm1", "mastery1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryPlayer1", "masteryplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", playerToken,
            new AddCharacterMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 3)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/masteries", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Pontaria" && m.Pericia == "Pontaria" && m.Atributo == "Destreza" && m.GastoMaestria == 3);
    }

    [Fact]
    public async Task List_returns_the_masteries_on_the_sheet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MasteryGm2", "mastery2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryPlayer2", "masteryplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", playerToken,
            new AddCharacterMasteryRequest("Maestria em Furtividade", "Furtividade", "Agilidade", 2)));

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/masteries", playerToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Nome == "Maestria em Furtividade" && m.Pericia == "Furtividade" && m.Atributo == "Agilidade" && m.GastoMaestria == 2);
    }

    [Fact]
    public async Task Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MasteryGm3", "mastery3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryPlayer3", "masteryplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", playerToken,
            new AddCharacterMasteryRequest("Maestria em Investigação", "Investigacao", "Astucia", 1)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterMasteryResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/masteries/{added!.Id}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/masteries", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterMasteryResponse>>();
        body!.Should().NotContain(m => m.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MasteryGm4", "mastery4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryPlayer4", "masteryplayer4@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryPlayer4b", "masteryplayer4b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", otherToken,
            new AddCharacterMasteryRequest("Maestria em Atletismo", "Atletismo", "Vigor", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Add_computes_Total_from_the_matching_skill_and_attribute_rows()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MasteryGm5", "mastery5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryPlayer5", "masteryplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        // Bruto[Pontaria] = Modificador(9) = 3
        var skillUpdate = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Pontaria", gmToken,
            new UpdateCharacterSkillRequest(9, null)));
        skillUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // AtributoTotal[Destreza] = 4 (Gasto 4, Bonus 0, sem maestria)
        var attributeUpdate = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Destreza", gmToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));
        attributeUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", playerToken,
            new AddCharacterMasteryRequest("Maestria em Pontaria", "Pontaria", "Destreza", 5)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await response.Content.ReadFromJsonAsync<CharacterMasteryResponse>();

        // Total = GastoMaestria (5) + Bruto[Pontaria] (3) + AtributoTotal[Destreza] (4) = 12
        added!.Total.Should().Be(12);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/masteries", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterMasteryResponse>>();
        body!.Should().ContainSingle(m => m.Id == added.Id && m.Total == 12);
    }

    [Fact]
    public async Task Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list()
    {
        // A numeric string satisfies Enum.TryParse but isn't a real Pericia/Atributo value - without
        // an Enum.IsDefined check, the row would be saved before ComputeTotalAsync runs and then every
        // later GET .../masteries would 500 forever trying to look up a skill/attribute row that
        // doesn't exist for that undefined enum value.
        var gmToken = await RegisterGmAndGetTokenAsync("MasteryGm6", "mastery6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MasteryPlayer6", "masteryplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", playerToken,
            new AddCharacterMasteryRequest("Maestria Inválida", "999", "Destreza", 3)));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/masteries", playerToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterMasteryResponse>>();
        body!.Should().BeEmpty();
    }
}
