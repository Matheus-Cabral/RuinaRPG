using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcSheetsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcSheetsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
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

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private async Task AddMemberAsync(string gmToken, string campaignId, string playerId) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

    /// <summary>Grants a blank Npc sheet to playerId, via the real grants endpoint, and returns its Id.</summary>
    private async Task<string> GrantBlankNpcAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Npc", null)));
        return (await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    private static UpdateNpcSheetRequest ValidUpdate() => new(
        null, "Sentinela da Ruína", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Guardiã do Portal",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100);

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/npc-sheets", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_Nivel_1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm1", "npc1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Nivel.Should().Be(1);
        body.Nome.Should().BeNull();
        body.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm2", "npc2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "NpcJogador2", "npcjogador2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", jogadorToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_by_the_owning_gm_returns_200()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm3", "npc3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwner4", "npcowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOther4", "npcother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_and_Update_by_the_player_the_sheet_was_granted_to_both_succeed()
    {
        // Requisitos - Campanha R0010: a granted NPC/Criatura "usa o mesmo modelo de edição" as
        // the player's own Ficha de Personagem — the owning player, not just the GM, can edit it.
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmGrant1", "npcgmgrant1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcJogadorGrant1", "npcjogadorgrant1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha NPC Grant");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var sheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", playerToken));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = ValidUpdate() with { Nome = "Companheiro do Jogador" };
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", playerToken, update));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", playerToken));
        (await getAfter.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Nome.Should().Be("Companheiro do Jogador");
    }

    [Fact]
    public async Task Get_by_a_jogador_the_sheet_was_never_granted_to_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmGrant2", "npcgmgrant2@teste.com");
        var (_, strangerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcJogadorGrant2", "npcjogadorgrant2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // OwnerId stays null — never granted to anyone

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", strangerToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_the_owning_gm_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm5", "npc5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Nome.Should().Be("Sentinela da Ruína");
        body.Linhagem.Should().Be("Humano");
        body.Variante.Should().Be("Sinir");
        body.Nivel.Should().Be(5);
        body.VitalidadeAtual.Should().Be(30);
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwner6", "npcowner6@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOther6", "npcother6@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmTokenOther, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_by_the_owning_gm_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm7", "npc7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwner8", "npcowner8@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOther8", "npcother8@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/npc-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm9", "npc9@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{Guid.NewGuid()}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_a_Variante_that_does_not_belong_to_the_Linhagem_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm10", "npc10@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var invalid = ValidUpdate() with { Linhagem = "Humano", Variante = "Yavos" }; // Yavos belongs to Nephrytes
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_seeds_8_zeroed_attributes_39_zeroed_skills_and_3_armor_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSeed1", "npcseed1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var attributes = await db.NpcAttributes.Where(a => a.NpcSheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.NpcSkills.Where(s => s.NpcSheetId == Guid.Parse(sheetId)).ToListAsync();
        var armorSlots = await db.NpcArmorSlots.Where(a => a.NpcSheetId == Guid.Parse(sheetId)).ToListAsync();

        attributes.Should().HaveCount(8);
        attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
        skills.Should().HaveCount(39);
        skills.Should().OnlyContain(s => s.Gasto == 0);
        // ArmorSlotType has 3 members (Capacete, Superior, Inferior) — not 6.
        armorSlots.Should().HaveCount(3);
        armorSlots.Should().OnlyContain(a => a.ItemId == null);
    }

    [Fact]
    public async Task Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm11", "npc11@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Vocacao = "Campeao", EAPAtual = 150 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Grau");
        body.Graduacao.Should().Be(1); // 150 EAP >= the real Tabela's Grau 1 threshold (100)
    }

    [Fact]
    public async Task List_returns_only_the_callers_own_npcs()
    {
        var gmTokenA = await RegisterGmAndGetTokenAsync("NpcGmListA", "npcgmlista@teste.com");
        var gmTokenB = await RegisterGmAndGetTokenAsync("NpcGmListB", "npcgmlistb@teste.com");

        var sheetIdA = await CreateSheetAsync(gmTokenA);
        var sheetIdB = await CreateSheetAsync(gmTokenB);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets", gmTokenA));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        body.Should().HaveCount(1);
        body![0].Id.Should().Be(sheetIdA);
    }

    [Fact]
    public async Task List_shows_OwnerNickname_for_a_granted_sheet_and_null_for_the_GMs_own()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmListOwner", "npcgmlistowner@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "NpcGmListOwnerPlayer", "npcgmlistownerplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha List Owner");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var ownSheetId = await CreateSheetAsync(gmToken);
        var grantedSheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        body!.Single(s => s.Id == ownSheetId).OwnerNickname.Should().BeNull();
        body!.Single(s => s.Id == grantedSheetId).OwnerNickname.Should().Be("NpcGmListOwnerPlayer");
    }

    [Fact]
    public async Task List_can_filter_by_Variante()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmListFilterVariante", "npcgmlistfiltervariante@teste.com");
        var sheetId1 = await CreateSheetAsync(gmToken);
        var sheetId2 = await CreateSheetAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId1}", gmToken, ValidUpdate() with { Variante = "Sinir" }));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId2}", gmToken, ValidUpdate() with { Variante = "Laonir" }));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets?variante=Laonir", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        body!.Should().ContainSingle(s => s.Id == sheetId2);
    }

    [Fact]
    public async Task List_can_filter_by_partial_Nome_Linhagem_and_Nivel_together()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmListFilter", "npcgmlistfilter@teste.com");

        var sheetId1 = await CreateSheetAsync(gmToken);
        var sheetId2 = await CreateSheetAsync(gmToken);

        // Both sheets have same Linhagem (Humano) but differ on Nome and Nivel
        // This ensures filtering by Linhagem alone returns both; only adding the other filters narrows to one
        var update1 = ValidUpdate() with { Nome = "Sentinela da Ruína", Linhagem = "Humano", Nivel = 5 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId1}", gmToken, update1));

        var update2 = ValidUpdate() with { Nome = "Assassino Silencioso", Linhagem = "Humano", Nivel = 7 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId2}", gmToken, update2));

        // Verify that filtering by Linhagem alone returns both sheets (proving it's not being ignored)
        var filterByLinhagemAlone = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets?linhagem=Humano", gmToken));
        filterByLinhagemAlone.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyLinhagemOnly = await filterByLinhagemAlone.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        bodyLinhagemOnly.Should().HaveCount(2);

        // Now filter by all three fields together and verify only the matching one is returned
        // This proves the three filters combine with AND, not OR
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets?nome=Sentinela&linhagem=Humano&nivel=5", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        body.Should().HaveCount(1);
        body![0].Id.Should().Be(sheetId1);
        body![0].Nome.Should().Be("Sentinela da Ruína");
        body![0].Linhagem.Should().Be("Humano");
        body![0].Nivel.Should().Be(5);
    }

    [Fact]
    public async Task RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmRacial1", "npcracial1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var beforeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken));
        (await beforeResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().BeNull();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Nephrytes", Variante = "Yavos" }));

        var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken));
        (await afterResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().Be("Racial (Sobre Voo)");
    }

    [Fact]
    public async Task RacialAbility_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwnerRacial2", "npcownerracial2@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOtherRacial2", "npcotherracial2@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubAttributes_computes_from_attributes_and_arsenal()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSub1", "npcsub1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Agilidade Gasto 4, no bônus/maestria/artefato → Total 4. Vigor same → Total 4.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Agilidade", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Movimentacao.Should().Be(8); // (4*2) + 0 artefato - 0 sobrepeso (nothing carried yet)
    }

    [Fact]
    public async Task SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSub4", "npcsub4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Agilidade", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));

        // Prontidao Gasto 9 -> Modificador 3, Reflexos Gasto 6 -> Modificador 2, Fortitude Gasto 3 -> Modificador 1.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Prontidao", gmToken,
            new UpdateNpcSkillRequest(9, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Reflexos", gmToken,
            new UpdateNpcSkillRequest(6, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Fortitude", gmToken,
            new UpdateNpcSkillRequest(3, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(7); // agilidade 4 + brutoProntidao 3 + 0 artefato
        body.EsquivaNatural.Should().Be(6); // agilidade 4 + brutoReflexos 2 + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 + 0 escudo + 0 artefatos + 0 cobertura
    }

    private async Task<string> CreateArmaduraItemAsync(string gmToken, int rf, int rm)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Armadura", "Peitoral de Testes", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, 12, "Medio", 5, rf, rm, "-1 Furtividade", 2, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    [Fact]
    public async Task SubAttributes_includes_equipped_armor_RF_and_RM()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSub2", "npcsub2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, rf: 3, rm: 2);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete", gmToken,
            new UpdateNpcArmorSlotRequest(armorItemId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.ReducaoFisica.Should().Be(3);
        body.ReducaoMagica.Should().Be(2);
    }

    [Fact]
    public async Task SubAttributes_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwnerSub3", "npcownersub3@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOtherSub3", "npcothersub3@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_computes_vitalidade_and_foco_maximo_from_vigor_astucia_and_vocacao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmMax1", "npcmax1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Campeão Nível 1 → Vida 8, Arcana 4 (real Tabela de Vocação excerpt, same as
        // VocacaoProgressaoParserTests). This vocação is specifically chosen because it
        // exercises the accented-name lookup bug (Campeão/Caçador) fixed in this task.
        var update = ValidUpdate() with { Vocacao = "Campeao", Nivel = 1 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        // Vigor total = Gasto(5) + Bonus(0)/2 (sem maestria) = 5 → Vitalidade = 5*2 + 8 = 18.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateNpcAttributeRequest(5, 0, false)));
        // Astúcia total = Gasto(3) + Bonus(0)/2 (sem maestria) = 3 → Foco = 3*2 + 4 = 10.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Astucia", gmToken,
            new UpdateNpcAttributeRequest(3, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));

        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(18);
        body.FocoMaximo.Should().Be(10);
        body.AdrenalinaMaximo.Should().Be(10); // 10 + Artefato bonus (não modelado ainda → 0)
        body.EstresseMaximo.Should().Be(10); // flat
    }

    [Fact]
    public async Task CampaignId_is_populated_for_a_granted_sheet_and_null_for_an_ungranted_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcCampaignIdGm1", "npccampaignid1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcCampaignIdPlayer1", "npccampaignidplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha NPC CampaignId");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var grantedSheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);
        var ungrantedSheetId = await CreateSheetAsync(gmToken); // read helper: POST npc-sheets, no update needed

        var grantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{grantedSheetId}", playerToken));
        var granted = await grantedResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        granted!.CampaignId.Should().Be(campaignId);

        var ungrantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{ungrantedSheetId}", gmToken));
        var ungranted = await ungrantedResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        ungranted!.CampaignId.Should().BeNull();
    }
}
