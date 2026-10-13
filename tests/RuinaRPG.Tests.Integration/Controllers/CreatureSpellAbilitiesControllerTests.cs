using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

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
        await SetPiAsync(sheetId, 1000);

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
    public async Task AddFromBankEntry_copies_every_field_and_reuses_the_bank_entry_without_duplicating_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasGm2", "creaturemagias2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 1000);

        var bankCreateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest("Cura Leve", "Habilidade", 1, "Restaura um pouco de vida.",
                [new SpellAbilityEffectRequest("Dano", 2, 4), new SpellAbilityEffectRequest("Cura", null, 2)])));
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
        bank.Should().ContainSingle().Which.Id.Should().Be(bankEntry.Id); // escolher do banco não gera uma segunda entrada
        (await SourceBankEntryIdAsync(sheetCopy.Id)).Should().Be(Guid.Parse(bankEntry.Id));
    }

    [Fact]
    public async Task Delete_removes_only_the_sheet_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasGm3", "creaturemagias3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 1000);
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
        await SetPiAsync(sheetId, 1000);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", playerToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSpellEfeitoGm", "creaturespellefeito@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCreatureSpellAbilityRequest(null, "Cura Sem Dano", "Magia", 1, "Descrição.",
                new List<SpellAbilityEffectRequest> { new("Cura", null, 2) })));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddFromScratch_by_the_gm_on_their_own_creature_does_not_auto_attach()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasAutoGm2", "creaturemagiasauto2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 1000);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
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
        var gmToken = await RegisterGmAndGetTokenAsync("SecMagCrePGm", "SecMagCrePgm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SecMagCrePPl", "SecMagCrePpl@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Seg Magia Criatura");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);
        var entryId = await CreateBankEntryAsync(gmToken, "Magia Reservada");

        var antes = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCreatureSpellAbilityRequest(entryId, null, null, null, null, null)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishBankEntryAsync(gmToken, campaignId, entryId);

        var depois = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", playerToken,
            new AddCreatureSpellAbilityRequest(entryId, null, null, null, null, null)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddFromBankEntry_never_accepts_another_gms_entry_not_even_for_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SecMagCreGGm", "SecMagCreGgm@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var otherGmToken = await RegisterGmAndGetTokenAsync("SecMagCreOtherGm", "secmagcreothergm@teste.com");
        var foreignEntryId = await CreateBankEntryAsync(otherGmToken, "Magia do Outro GM");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCreatureSpellAbilityRequest(foreignEntryId, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid?> SourceBankEntryIdAsync(string sheetSpellAbilityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        return await db.CreatureSpellAbilities.Where(e => e.Id == Guid.Parse(sheetSpellAbilityId)).Select(e => e.SourceBankEntryId).SingleAsync();
    }

    [Fact]
    public async Task AddFromScratch_links_the_sheet_entry_to_the_bank_entry_it_created()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasGmLink", "creaturemagiasgmlink@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 1000);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        var sheetCopy = await response.Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>();

        var bankEntry = (await GetBankAsync(gmToken)).Should().ContainSingle().Subject;
        (await SourceBankEntryIdAsync(sheetCopy!.Id)).Should().Be(Guid.Parse(bankEntry.Id));
        bankEntry.DeCriatura.Should().Be(true);
    }

    [Fact]
    public async Task An_entry_built_from_scratch_is_flagged_CriadaNaFicha_and_one_from_the_bank_is_not()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasFlag1", "creaturemagiasflag1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 100);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCreatureSpellAbilityRequest(bankId, null, null, null, null, null)));

        var list = (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken)))
            .Content.ReadFromJsonAsync<List<CreatureSpellAbilityResponse>>())!;
        list.Should().HaveCount(2);
        list.Count(e => e.CriadaNaFicha).Should().Be(1);
    }

    private async Task SetPiAsync(string sheetId, int pi)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.CreatureSheets.FindAsync(Guid.Parse(sheetId)))!.PontosDeIgnicao = pi;
        await db.SaveChangesAsync();
    }

    private static UpdateSheetSpellAbilityRequest BolaDeFogoEditada() => new(
        "Bola de Fogo Maior", "Magia", 3, "Explosão ampliada.",
        [new SpellAbilityEffectRequest("Dano", 4, 8)]);

    private async Task<CreatureSpellAbilityResponse> AddAsync(string token, string sheetId, AddCreatureSpellAbilityRequest request) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", token, request)))
            .Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>())!;

    [Fact]
    public async Task Update_by_the_owner_changes_a_from_scratch_entry_and_leaves_the_bank_copy_alone()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreSpEdit1", "crespedit1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreSpEditP1", "crespeditp1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha CreSp Edit");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(playerToken, sheetId, BolaDeFogoFromScratch());

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/spell-abilities/{criada.Id}", playerToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreatureSpellAbilityResponse>();
        body!.Nome.Should().Be("Bola de Fogo Maior");
        body.Descricao.Should().Be("Explosão ampliada.");
        body.GastoEmPI.Should().Be(8);
        body.Custo.Should().Be(10); // ceil(8 * 1.25)
        body.Efeitos.Should().ContainSingle(e => e.EfeitoNome == "Dano");
        body.CriadaNaFicha.Should().BeTrue();
        (await GetBankAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.GastoEmPI == 14);
    }

    [Fact]
    public async Task Update_refuses_an_entry_that_came_from_the_bank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreSpEdit2", "crespedit2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 100);
        await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        var doBanco = await AddAsync(gmToken, sheetId, new AddCreatureSpellAbilityRequest(bankId, null, null, null, null, null));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/spell-abilities/{doBanco.Id}", gmToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Só Magias e Habilidades montadas na ficha podem ser editadas.");
    }

    [Fact]
    public async Task Update_validates_like_a_from_scratch_add()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreSpEdit3", "crespedit3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        var url = $"/api/creature-sheets/{sheetId}/spell-abilities/{criada.Id}";

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada() with { Tipo = "Passiva" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada() with { Tipo = "Inexistente" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            BolaDeFogoEditada() with { Efeitos = [new SpellAbilityEffectRequest("Efeito Que Não Existe", 1, 1)] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_by_someone_who_cannot_edit_the_sheet_is_404_and_an_unknown_id_is_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreSpEdit4", "crespedit4@teste.com");
        var outroGm = await RegisterGmAndGetTokenAsync("CreSpEdit4b", "crespedit4b@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/spell-abilities/{criada.Id}", outroGm, BolaDeFogoEditada())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/spell-abilities/{Guid.NewGuid()}", gmToken, BolaDeFogoEditada())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> GetPiAsync(string sheetId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return (await db.CreatureSheets.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(sheetId))).PontosDeIgnicao;
    }

    [Fact]
    public async Task Add_is_refused_when_the_Gasto_em_PI_exceeds_the_balance_even_for_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreSpPi1", "cresppi1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 13);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 14 e a ficha tem 13.");
        (await GetPiAsync(sheetId)).Should().Be(13);
        (await GetBankAsync(gmToken)).Should().NotContain(e => e.Nome == "Bola de Fogo", "uma criação recusada não deixa cópia no banco");
    }

    [Fact]
    public async Task Add_with_exactly_enough_debits_the_balance_to_zero_and_Delete_refunds_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreSpPi2", "cresppi2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreSpPiP2", "cresppip2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha CreSp Pi");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);
        await SetPiAsync(sheetId, 14);

        var criada = await AddAsync(playerToken, sheetId, BolaDeFogoFromScratch());
        (await GetPiAsync(sheetId)).Should().Be(0);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}/spell-abilities/{criada.Id}", playerToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPiAsync(sheetId)).Should().Be(14);
    }

    [Fact]
    public async Task Update_settles_only_the_difference_in_both_directions()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreSpPi4", "cresppi4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 14);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch()); // gasto 14, saldo 0
        var url = $"/api/creature-sheets/{sheetId}/spell-abilities/{criada.Id}";

        // Só o nome muda (diferença 0): aceito mesmo com saldo zero.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Outro Nome", "Magia", 3, "Uma explosão de fogo.",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(0);

        // Custo cai de 14 para 8: devolve 6.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada()))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(6);

        // Custo sobe de 8 para 14: precisa de 6, tem 6.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(0);

        // Voltar a 8 e tentar subir para 14 com saldo 5: recusado, nada muda.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada()))).EnsureSuccessStatusCode();
        await SetPiAsync(sheetId, 5);
        var recusada = await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await recusada.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5.");
        (await GetPiAsync(sheetId)).Should().Be(5);
    }

    [Fact]
    public async Task Update_refuses_a_from_scratch_entry_whose_stored_Tipo_does_not_consume_PI()
    {
        // Uma Racial montada na ficha é CriadaNaFicha mas nunca foi debitada: convertê-la em Magia por
        // aqui custaria quase nada e a remoção devolveria o gasto inteiro.
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasFix1", "creaturemagiasfix1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 100);
        var racial = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch() with { Tipo = "Racial" });
        var saldo = await GetPiAsync(sheetId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/spell-abilities/{racial.Id}", gmToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Só Magias e Habilidades montadas na ficha podem ser editadas.");
        var list = (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken)))
            .Content.ReadFromJsonAsync<List<CreatureSpellAbilityResponse>>())!;
        list.Should().ContainSingle(e => e.Id == racial.Id && e.Nome == "Bola de Fogo" && e.Tipo == "Racial" && e.GastoEmPI == 14 && e.Efeitos.Count == 2);
        (await GetPiAsync(sheetId)).Should().Be(saldo);
    }

    [Fact]
    public async Task A_stale_sheet_form_cannot_erase_a_debit()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasFix2", "creaturemagiasfix2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await SetPiAsync(sheetId, 14);
        await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        (await GetPiAsync(sheetId)).Should().Be(0);

        // O formulário da ficha, aberto antes do débito, ainda traz 14 no campo de PI.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken,
            new UpdateCreatureSheetRequest(null, "Lobo das Ruínas", "Lobo", "Fisico", "Predador", "Terra", "F", 1, 0, 14, 0, 0, 0, "Nenhuma")))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetPiAsync(sheetId)).Should().Be(0);
    }
}
