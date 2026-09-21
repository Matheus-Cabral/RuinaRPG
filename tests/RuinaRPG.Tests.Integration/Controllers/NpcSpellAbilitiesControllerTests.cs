using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcSpellAbilitiesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcSpellAbilitiesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static AddNpcSpellAbilityRequest BolaDeFogoFromScratch() => new(
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
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasGm1", "npcmagias1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<NpcSpellAbilityResponse>();
        body!.Nome.Should().Be("Bola de Fogo");
        body.Tipo.Should().Be("Magia");
        body.Grau.Should().Be(3);
        body.GastoEmPI.Should().Be(14); // 8 + 6
        body.Custo.Should().Be(18); // ceil(14 * 1.25) = 18
        body.Efeitos.Should().HaveCount(2);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<NpcSpellAbilityResponse>>();
        list!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.Tipo == "Magia" && e.Grau == 3 && e.GastoEmPI == 14 && e.Custo == 18);
    }

    [Fact]
    public async Task AddFromBankEntry_copies_every_field_and_still_creates_a_second_independent_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasGm2", "npcmagias2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var bankCreateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest("Cura Leve", "Habilidade", 1, "Restaura um pouco de vida.",
                [new SpellAbilityEffectRequest("Dano", 2, 4), new SpellAbilityEffectRequest("Cura", null, 2)])));
        var bankEntry = await bankCreateResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken,
            new AddNpcSpellAbilityRequest(bankEntry!.Id, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sheetCopy = await response.Content.ReadFromJsonAsync<NpcSpellAbilityResponse>();
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
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasGm3", "npcmagias3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        var entryId = (await addResponse.Content.ReadFromJsonAsync<NpcSpellAbilityResponse>())!.Id;

        (await GetBankAsync(gmToken)).Should().ContainSingle();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}/spell-abilities/{entryId}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<NpcSpellAbilityResponse>>();
        list!.Should().NotContain(e => e.Id == entryId);

        (await GetBankAsync(gmToken)).Should().ContainSingle(); // untouched — independent copies
    }

    [Fact]
    public async Task Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasGm4", "npcmagias4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken,
            new AddNpcSpellAbilityRequest(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcMagiasGmOwner5", "npcmagiasowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcMagiasGmOther5", "npcmagiasother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmTokenOther, BolaDeFogoFromScratch()));

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

    private async Task<string> GrantBlankNpcAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new RuinaRPG.Contracts.Campaigns.GrantSheetRequest(playerId, "Npc", null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.GrantSheetResponse>())!.SheetId;
    }

    [Fact]
    public async Task AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasAutoGm1", "npcmagiasauto1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcMagiasAutoPlayer1", "npcmagiasautoplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha NPC Magia Auto");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", playerToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSpellEfeitoGm", "npcspellefeito@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken,
            new AddNpcSpellAbilityRequest(null, "Cura Sem Dano", "Magia", 1, "Descrição.",
                new List<SpellAbilityEffectRequest> { new("Cura", null, 2) })));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddFromScratch_by_the_gm_on_their_own_npc_does_not_auto_attach()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasAutoGm2", "npcmagiasauto2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo"); // the bank copy still happens (R0001) — just no CampaignAttachment
    }

    private async Task<string> CreateBankEntryAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Magia", 1, "Descrição.", [])));
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task PublishBankEntryAsync(string gmToken, string campaignId, string entryId)
    {
        var attach = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new RuinaRPG.Contracts.Campaigns.AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    // Segurança: uma entrada do Banco de Magias é copiada para a ficha só se for do GM da ficha e, para um
    // jogador, só se o GM a anexou como pública à campanha da ficha (Requisitos - Ficha de Personagem R0003).
    [Fact]
    public async Task AddFromBankEntry_by_a_player_requires_the_entry_to_be_public_in_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SecMagNpcPGm", "SecMagNpcPgm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SecMagNpcPPl", "SecMagNpcPpl@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Seg Magia NPC");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);
        var entryId = await CreateBankEntryAsync(gmToken, "Magia Reservada");

        var antes = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", playerToken,
            new AddNpcSpellAbilityRequest(entryId, null, null, null, null, null)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishBankEntryAsync(gmToken, campaignId, entryId);

        var depois = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", playerToken,
            new AddNpcSpellAbilityRequest(entryId, null, null, null, null, null)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddFromBankEntry_never_accepts_another_gms_entry_not_even_for_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SecMagNpcGGm", "SecMagNpcGgm@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var otherGmToken = await RegisterGmAndGetTokenAsync("SecMagNpcOtherGm", "secmagnpcothergm@teste.com");
        var foreignEntryId = await CreateBankEntryAsync(otherGmToken, "Magia do Outro GM");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken,
            new AddNpcSpellAbilityRequest(foreignEntryId, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
