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
using RuinaRPG.Infrastructure.Rules;

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
    // straight out of the running app's database rather than inserting synthetic ones. Restricted to
    // !RequerEspecificacao so callers that don't care about that field can add the trait with a bare
    // TraitId, no Especificacao required.
    private async Task<(string PositiveTraitId, string NegativeTraitId)> GetSeededTraitIdsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var positive = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Positiva && !t.RequerEspecificacao && t.Custo <= 3);
        var negative = await db.Traits.FirstAsync(t => t.Polaridade == RuinaRPG.Domain.Enums.Polaridade.Negativa && !t.RequerEspecificacao && t.Custo >= -3);
        return (positive.Id.ToString(), negative.Id.ToString());
    }

    // "Alergia" is a real, stable single-tier Negativa (-1 ponto) that RequerEspecificacao — used
    // directly by name, mirroring TraitSeedParserTests' own convention of referencing it verbatim.
    private async Task<Trait> GetTraitAsync(string nome)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return await db.Traits.SingleAsync(t => t.Nome == nome);
    }

    // The two highest-cost traits on a side (excluding ones needing a specification, to isolate the
    // budget check from the specification check) — real Características.md data, at least 5 points
    // apart when combined, safely exceeding the level-1 budget of 5.
    private async Task<List<(string Id, int Custo)>> GetTopCostTraitsAsync(RuinaRPG.Domain.Enums.Polaridade polaridade, int take)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        // Each candidate's own |Custo| must stay within the level-1 budget (5) on its own — the test
        // using this helper adds them one at a time and expects the FIRST to succeed, so a trait that
        // alone already exceeds the budget (e.g. a -10 magnitude Negativa) would be a false failure.
        var ordered = polaridade == RuinaRPG.Domain.Enums.Polaridade.Positiva
            ? await db.Traits.Where(t => t.Polaridade == polaridade && !t.RequerEspecificacao && t.Custo <= 5).OrderByDescending(t => t.Custo).Take(take).ToListAsync()
            : await db.Traits.Where(t => t.Polaridade == polaridade && !t.RequerEspecificacao && t.Custo >= -5).OrderBy(t => t.Custo).Take(take).ToListAsync();
        return ordered.Select(t => (t.Id.ToString(), t.Custo)).ToList();
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
    public async Task UpdateAffection_returns_200_and_the_list_reflects_the_change()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm6", "afftraitgm6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer6", "afftraitplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affections", playerToken,
            new AddCharacterAffectionRequest("Amigo de infância", 5)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffectionResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affections/{added!.Id}", playerToken,
            new UpdateCharacterAffectionRequest("Rival de infância", -3)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CharacterAffectionResponse>();
        updated!.Nome.Should().Be("Rival de infância");
        updated.Favorabilidade.Should().Be(-3);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affections", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAffectionResponse>>();
        body!.Should().ContainSingle(a => a.Id == added.Id && a.Nome == "Rival de infância" && a.Favorabilidade == -3);
    }

    [Fact]
    public async Task UpdateAffection_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm7", "afftraitgm7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer7", "afftraitplayer7@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer7b", "afftraitplayer7b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affections", playerToken,
            new AddCharacterAffectionRequest("Mentor", 8)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffectionResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affections/{added!.Id}", otherToken,
            new UpdateCharacterAffectionRequest("Impostor", 0)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAffection_for_a_nonexistent_id_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm5", "afftraitgm5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer5", "afftraitplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affections/{Guid.NewGuid()}", playerToken,
            new UpdateCharacterAffectionRequest("Ninguém", 0)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm2", "afftraitgm2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer2", "afftraitplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var (positiveTraitId, negativeTraitId) = await GetSeededTraitIdsAsync();

        var addPositiveResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(positiveTraitId, null)));
        addPositiveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var addNegativeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(negativeTraitId, null)));
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
            new AddCharacterTraitRequest(Guid.NewGuid().ToString(), null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_rejects_a_Positiva_that_would_push_the_side_total_past_the_budget()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm8", "afftraitgm8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer8", "afftraitplayer8@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var top = await GetTopCostTraitsAsync(RuinaRPG.Domain.Enums.Polaridade.Positiva, 2);
        top.Sum(t => t.Custo).Should().BeGreaterThan(5); // level-1 creation budget

        var first = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(top[0].Id, null)));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(top[1].Id, null)));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_rejects_a_Negativa_that_would_push_the_side_total_past_the_budget()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm9", "afftraitgm9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer9", "afftraitplayer9@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var top = await GetTopCostTraitsAsync(RuinaRPG.Domain.Enums.Polaridade.Negativa, 2);
        Math.Abs(top.Sum(t => t.Custo)).Should().BeGreaterThan(5); // level-1 creation budget

        var first = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(top[0].Id, null)));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(top[1].Id, null)));
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm10", "afftraitgm10@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer10", "afftraitplayer10@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var alergia = await GetTraitAsync("Alergia");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(alergia.Id.ToString(), null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTrait_persists_and_returns_the_Especificacao_for_a_RequerEspecificacao_trait()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm11", "afftraitgm11@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer11", "afftraitplayer11@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var alergia = await GetTraitAsync("Alergia");

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken,
            new AddCharacterTraitRequest(alergia.Id.ToString(), "Poeira")));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterTraitResponse>();
        added!.Especificacao.Should().Be("Poeira");
        added.RequerEspecificacao.Should().BeTrue();

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var list = await listResponse.Content.ReadFromJsonAsync<CharacterTraitsListResponse>();
        list!.Negativas.Should().ContainSingle(t => t.Id == added.Id && t.Especificacao == "Poeira");
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
        var addTrait = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken, new AddCharacterTraitRequest(positiveTraitId, null)));
        var traitId = (await addTrait.Content.ReadFromJsonAsync<CharacterTraitResponse>())!.Id;

        var addAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affections", otherToken, new AddCharacterAffectionRequest("Rival", -2)));
        addAffectionResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var listAffectionsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affections", otherToken));
        listAffectionsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteAffectionResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/affections/{affectionId}", otherToken));
        deleteAffectionResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var addTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", otherToken, new AddCharacterTraitRequest(positiveTraitId, null)));
        addTraitResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var listTraitsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", otherToken));
        listTraitsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteTraitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/traits/{traitId}", otherToken));
        deleteTraitResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
