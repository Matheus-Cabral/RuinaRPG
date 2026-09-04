using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureSpellAbilitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureSpellAbilitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    private static AddCreatureSpellAbilityRequest BolaDeFogoFromScratch() => new(
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
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasGm1", "creaturemagias1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>();
        body!.Nome.Should().Be("Bola de Fogo");
        body.Tipo.Should().Be("Magia");
        body.Grau.Should().Be(3);
        body.GastoEmPI.Should().Be(14); // 8 + 6
        body.Custo.Should().Be(18); // ceil(14 * 1.25) = 18
        body.Efeitos.Should().HaveCount(2);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CreatureSpellAbilityResponse>>();
        list!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.Tipo == "Magia" && e.Grau == 3 && e.GastoEmPI == 14 && e.Custo == 18);
    }

    [Fact]
    public async Task AddFromBankEntry_copies_every_field_and_still_creates_a_second_independent_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasGm2", "creaturemagias2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var bankCreateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest("Cura Leve", "Habilidade", 1, "Restaura um pouco de vida.",
                [new SpellAbilityEffectRequest("Cura", 2, 4)])));
        var bankEntry = await bankCreateResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCreatureSpellAbilityRequest(bankEntry!.Id, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sheetCopy = await response.Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>();
        sheetCopy!.Nome.Should().Be(bankEntry.Nome);
        sheetCopy.Tipo.Should().Be(bankEntry.Tipo);
        sheetCopy.Grau.Should().Be(bankEntry.Grau);
        sheetCopy.Descricao.Should().Be(bankEntry.Descricao);
        sheetCopy.GastoEmPI.Should().Be(bankEntry.GastoEmPI);
        sheetCopy.Custo.Should().Be(bankEntry.Custo);
        sheetCopy.Efeitos.Should().BeEquivalentTo(bankEntry.Efeitos);

        var bank = await GetBankAsync(gmToken);
        bank.Should().HaveCount(2); // the original + the auto-copy this creation also landed (R0001)
        bank.Where(e => e.Nome == "Cura Leve").Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_removes_only_the_sheet_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasGm3", "creaturemagias3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        var entryId = (await addResponse.Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>())!.Id;

        (await GetBankAsync(gmToken)).Should().ContainSingle();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/spell-abilities/{entryId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<CreatureSpellAbilityResponse>>();
        list!.Should().NotContain(e => e.Id == entryId);

        (await GetBankAsync(gmToken)).Should().ContainSingle(); // untouched — independent copies
    }

    [Fact]
    public async Task Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasGm4", "creaturemagias4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCreatureSpellAbilityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureMagiasGmOwner5", "creaturemagiasowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureMagiasGmOther5", "creaturemagiasother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmTokenOther, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        return ((await meResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Auth.MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new RuinaRPG.Contracts.Campaigns.CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignResponse>())!.Id;
    }

    private async Task<string> GrantBlankCreatureAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new RuinaRPG.Contracts.Campaigns.GrantSheetRequest(playerId, "Creature", null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.GrantSheetResponse>())!.SheetId;
    }

    [Fact]
    public async Task AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasAutoGm1", "creaturemagiasauto1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureMagiasAutoPlayer1", "creaturemagiasautoplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Creature Magia Auto");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", playerToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task AddFromScratch_by_the_gm_on_their_own_creature_does_not_auto_attach()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasAutoGm2", "creaturemagiasauto2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo"); // the bank copy still happens (R0001) — just no CampaignAttachment
    }
}
