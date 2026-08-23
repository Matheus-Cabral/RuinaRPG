using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterAffectionsAndTraitsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterAffectionsAndTraitsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Traits are seeded at app startup by TraitSeeder from Características.md (imported ahead of
    // schedule from the Compêndio de Regras plan). Pull one positive and one negative real Trait
    // straight out of the running app's database rather than inserting synthetic ones.
    private async Task<(string PositiveTraitId, string NegativeTraitId)> GetSeededTraitIdsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var positive = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Positiva);
        var negative = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Negativa);
        return (positive.Id.ToString(), negative.Id.ToString());
    }

    [Fact]
    public async Task AddAffection_then_ListAffections_returns_it_then_DeleteAffection_removes_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm1", "afftraitgm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer1", "afftraitplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affections", playerToken,
            new AddCharacterAffectionRequest("Amigo de infância", 5)));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffectionResponse>();
        added!.Nome.Should().Be("Amigo de infância");
        added.Favorabilidade.Should().Be(5);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affections", playerToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffectionResponse>>();
        list!.Should().ContainSingle(a => a.Id == added.Id);

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/affections/{added.Id}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDelete = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affections", playerToken));
        var listAfter = await listAfterDelete.Content.ReadFromJsonAsync<List<CharacterAffectionResponse>>();
        listAfter!.Should().NotContain(a => a.Id == added.Id);
    }

    [Fact]
    public async Task AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm2", "afftraitgm2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer2", "afftraitplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var (positiveTraitId, negativeTraitId) = await GetSeededTraitIdsAsync();

        var addPositiveResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(positiveTraitId)));
        addPositiveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addNegativeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(negativeTraitId)));
        addNegativeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addedNegative = await addNegativeResponse.Content.ReadFromJsonAsync<CharacterTraitResponse>();

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<CharacterTraitsListResponse>();
        list!.Positivas.Should().ContainSingle(t => t.TraitId == positiveTraitId);
        list.Negativas.Should().ContainSingle(t => t.TraitId == negativeTraitId);
        list.TotalPositivas.Should().Be(list.Positivas.Sum(t => t.Custo));
        list.TotalNegativas.Should().Be(list.Negativas.Sum(t => t.Custo));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/traits/{addedNegative!.Id}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDelete = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var listAfter = await listAfterDelete.Content.ReadFromJsonAsync<CharacterTraitsListResponse>();
        listAfter!.Negativas.Should().NotContain(t => t.Id == addedNegative.Id);
    }

    [Fact]
    public async Task AddTrait_with_a_nonexistent_TraitId_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm3", "afftraitgm3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer3", "afftraitplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(Guid.NewGuid().ToString())));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Affection_and_trait_actions_by_an_unrelated_jogador_return_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm4", "afftraitgm4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer4", "afftraitplayer4@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer4b", "afftraitplayer4b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var (positiveTraitId, _) = await GetSeededTraitIdsAsync();

        var addAffection = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affections", playerToken, new AddCharacterAffectionRequest("Rival", -2)));
        var affectionId = (await addAffection.Content.ReadFromJsonAsync<CharacterAffectionResponse>())!.Id;
        var addTrait = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken, new AddCharacterTraitRequest(positiveTraitId)));
        var traitId = (await addTrait.Content.ReadFromJsonAsync<CharacterTraitResponse>())!.Id;

        var addAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affections", otherToken, new AddCharacterAffectionRequest("Rival", -2)));
        addAffectionResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var listAffectionsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affections", otherToken));
        listAffectionsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/affections/{affectionId}", otherToken));
        deleteAffectionResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var addTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", otherToken, new AddCharacterTraitRequest(positiveTraitId)));
        addTraitResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var listTraitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", otherToken));
        listTraitsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/traits/{traitId}", otherToken));
        deleteTraitResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
