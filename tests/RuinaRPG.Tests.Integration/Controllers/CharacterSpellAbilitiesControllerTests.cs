using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterSpellAbilitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterSpellAbilitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static AddCharacterSpellAbilityRequest BolaDeFogoFromScratch() => new(
        null, "Bola de Fogo", "Magia", 3, "Uma explosão de fogo.",
        [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)]);

    private async Task<List<SpellAbilityEntryResponse>> GetBankAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>())!;
    }

    [Fact]
    public async Task AddFromScratch_creates_the_sheet_entry_and_an_independent_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm1", "magias1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer1", "magiasplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>();
        body!.Nome.Should().Be("Bola de Fogo");
        body.Tipo.Should().Be("Magia");
        body.Grau.Should().Be(3);
        body.GastoEmPI.Should().Be(14); // 8 + 6
        body.Custo.Should().Be(18); // ceil(14 * 1.25) = 18
        body.Efeitos.Should().HaveCount(2);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>();
        list!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.Tipo == "Magia" && e.Grau == 3 && e.GastoEmPI == 14 && e.Custo == 18);
    }

    // AddFromScratch_editing_the_sheet_copy_afterward_does_not_change_the_bank_copy: this plan
    // doesn't build a PUT for spell abilities — Ficha de Personagem's 4.b describes add/remove,
    // not edit-in-place — so there is no endpoint to exercise for this case; intentionally omitted.

    [Fact]
    public async Task AddFromBankEntry_copies_every_field_and_still_creates_a_second_independent_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm2", "magias2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer2", "magiasplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var bankCreateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest("Cura Leve", "Habilidade", 1, "Restaura um pouco de vida.",
                [new SpellAbilityEffectRequest("Cura", 2, 4)])));
        var bankEntry = await bankCreateResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCharacterSpellAbilityRequest(bankEntry!.Id, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sheetCopy = await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>();
        sheetCopy!.Nome.Should().Be(bankEntry.Nome);
        sheetCopy.Tipo.Should().Be(bankEntry.Tipo);
        sheetCopy.Grau.Should().Be(bankEntry.Grau);
        sheetCopy.Descricao.Should().Be(bankEntry.Descricao);
        sheetCopy.GastoEmPI.Should().Be(bankEntry.GastoEmPI);
        sheetCopy.Custo.Should().Be(bankEntry.Custo);
        sheetCopy.Efeitos.Should().BeEquivalentTo(bankEntry.Efeitos);

        var bank = await GetBankAsync(gmToken);
        bank.Should().HaveCount(2); // the original + the auto-copy this creation also landed (R0001/R0003)
        bank.Where(e => e.Nome == "Cura Leve").Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_removes_only_the_sheet_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm3", "magias3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer3", "magiasplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        var entryId = (await addResponse.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!.Id;

        (await GetBankAsync(gmToken)).Should().ContainSingle();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/spell-abilities/{entryId}", playerToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>();
        list!.Should().NotContain(e => e.Id == entryId);

        (await GetBankAsync(gmToken)).Should().ContainSingle(); // untouched — independent copies
    }

    [Fact]
    public async Task Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm4", "magias4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer4", "magiasplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCharacterSpellAbilityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasGm5", "magias5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer5", "magiasplayer5@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPlayer5b", "magiasplayer5b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", otherToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
