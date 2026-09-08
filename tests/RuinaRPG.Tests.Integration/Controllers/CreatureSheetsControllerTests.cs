using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureSheetsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureSheetsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
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

    /// <summary>Grants a blank Creature sheet to playerId, via the real grants endpoint, and returns its Id.</summary>
    private async Task<string> GrantBlankCreatureAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Creature", null)));
        return (await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    private static UpdateCreatureSheetRequest ValidUpdate() => new(
        null, "Lobo das Ruínas", "Lobo", "Fisico", "Predador", "Terra",
        "F", 3, 200, 5, 12, 8, 10, "Parcial");

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/creature-sheets", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_Nivel_1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm1", "criatura1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Nivel.Should().Be(1);
        body.Nome.Should().BeNull();
        body.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm2", "criatura2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "CreatureJogador2", "criaturajogador2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", jogadorToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_by_the_owning_gm_returns_200()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm3", "criatura3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmOwner4", "criaturaowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmOther4", "criaturaother4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_and_Update_by_the_player_the_sheet_was_granted_to_both_succeed()
    {
        // Requisitos - Campanha R0010: a granted NPC/Criatura "usa o mesmo modelo de edição" as
        // the player's own Ficha de Personagem — the owning player, not just the GM, can edit it.
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmGrant1", "creaturegmgrant1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureJogadorGrant1", "creaturejogadorgrant1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Creature Grant");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var sheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", playerToken));
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var update = ValidUpdate() with { Nome = "Companheira do Jogador" };
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", playerToken, update));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", playerToken));
        (await getAfter.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Nome.Should().Be("Companheira do Jogador");
    }

    [Fact]
    public async Task Get_by_a_jogador_the_sheet_was_never_granted_to_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmGrant2", "creaturegmgrant2@teste.com");
        var (_, strangerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureJogadorGrant2", "creaturejogadorgrant2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // OwnerId stays null — never granted to anyone

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", strangerToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_the_owning_gm_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm5", "criatura5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Nome.Should().Be("Lobo das Ruínas");
        body.Raca.Should().Be("Lobo");
        body.Arquetipo.Should().Be("Fisico");
        body.Rank.Should().Be("F");
        body.Nivel.Should().Be(3);
        body.VitalidadeAtual.Should().Be(12);
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmOwner6", "criaturaowner6@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmOther6", "criaturaother6@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmTokenOther, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_by_the_owning_gm_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm7", "criatura7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmOwner8", "criaturaowner8@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmOther8", "criaturaother8@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/creature-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm9", "criatura9@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{Guid.NewGuid()}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_seeds_6_zeroed_attributes_20_zeroed_skills_and_3_armor_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSeed1", "creatureseed1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var attributes = await db.CreatureAttributes.Where(a => a.CreatureSheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.CreatureSkills.Where(s => s.CreatureSheetId == Guid.Parse(sheetId)).ToListAsync();
        var armorSlots = await db.CreatureArmorSlots.Where(a => a.CreatureSheetId == Guid.Parse(sheetId)).ToListAsync();

        // AtributoCriatura has 6 members (not Atributo's 8).
        attributes.Should().HaveCount(6);
        attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
        // The R0005 allow-list has 20 members (not all 39 Pericia values, unlike Ficha de NPCs).
        skills.Should().HaveCount(20);
        skills.Should().OnlyContain(s => s.Gasto == 0);
        skills.Select(s => s.Pericia).Should().BeEquivalentTo(CreatureSkillAllowList.AllowedPericias);
        // ArmorSlotType has 3 members (Capacete, Superior, Inferior) — not 6.
        armorSlots.Should().HaveCount(3);
        armorSlots.Should().OnlyContain(a => a.ItemId == null);
    }

    [Fact]
    public async Task Get_computes_Kill_and_Assistencia_from_ExperienciaAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmXp1", "criaturaxp1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { ExperienciaAtual = 100 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Kill.Should().Be(15);
        body.Assistencia.Should().Be(12);
    }

    // vigorTotal/astuciaTotal now compute for real from the seeded CreatureAttribute rows (Task 3),
    // which start zeroed — so VitalidadeMaximo/FocoMaximo reduce to statusVida/statusFoco alone
    // (0*2 + status) here. Real excerpt from Docs/Sistema RPG/Tabela de Arquetipos.md, Nível 1:
    // Fisico -> Vida 8, Arcana 4; Arcano -> Vida 4, Arcana 8. Exercising both proves the plain
    // Arquetipo.ToString() lookup (no accent-mapping helper needed, unlike Vocacao) works for
    // both enum members. See Get_computes_Vitalidade_and_Foco_Maximo_from_real_Vigor_and_Astucia
    // below for the non-zero case.
    [Fact]
    public async Task Get_computes_Vitalidade_Foco_and_Adrenalina_Maximo_for_Fisico_Arquetipo()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmMax1", "criaturamax1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Arquetipo = "Fisico", Nivel = 1 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(8);
        body.FocoMaximo.Should().Be(4);
        body.AdrenalinaMaximo.Should().Be(10); // 10 + Artefato bonus (não modelado ainda → 0)
    }

    [Fact]
    public async Task Get_computes_Vitalidade_and_Foco_Maximo_for_Arcano_Arquetipo()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmMax2", "criaturamax2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Arquetipo = "Arcano", Nivel = 1 };
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(4);
        body.FocoMaximo.Should().Be(8);
    }

    // CreatureAttributesController doesn't exist until Task 4, so Vigor/Astúcia are set directly
    // via the DbContext — same pattern other migration/seed tests in this codebase already use —
    // rather than through an API route that doesn't exist yet. Proves ToResponseAsync's
    // GetAttributeTotalAsync(AtributoCriatura.Vigor/Astucia) genuinely reads real, non-default
    // CreatureAttribute rows, not just the seeded zeroes exercised above.
    [Fact]
    public async Task Get_computes_Vitalidade_and_Foco_Maximo_from_real_Vigor_and_Astucia()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmMax3", "criaturamax3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Arquetipo = "Fisico", Nivel = 1 }; // Fisico Nível 1 -> Vida 8, Arcana 4
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var sheetGuid = Guid.Parse(sheetId);
            var vigor = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetGuid && a.Atributo == AtributoCriatura.Vigor);
            vigor.Gasto = 5; // Vigor total = 5 (sem maestria/bonus) -> Vitalidade = 5*2 + 8 = 18
            var astucia = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetGuid && a.Atributo == AtributoCriatura.Astucia);
            astucia.Gasto = 3; // Astúcia total = 3 -> Foco = 3*2 + 4 = 10
            await db.SaveChangesAsync();
        }

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(18);
        body.FocoMaximo.Should().Be(10);
    }

    // Sub-Atributos — R0005 §2.b: mesmos campos/fórmulas do Personagem/NPC, exceto Resistência
    // Física/Arcana e Dano Cortante (pendente/sem fórmula ainda). Mirrors
    // NpcSheetsControllerTests.SubAttributes_computes_from_attributes_and_arsenal.
    [Fact]
    public async Task SubAttributes_computes_from_attributes_and_arsenal()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSub1", "creaturesub1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Agilidade Gasto 4, no bônus/maestria/artefato → Total 4. Vigor same → Total 4.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Agilidade", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Movimentacao.Should().Be(8); // (4*2) + 0 artefato - 0 sobrepeso (nothing carried yet)
    }

    [Fact]
    public async Task SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSub2", "creaturesub2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Agilidade", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));

        // Prontidao Gasto 9 -> Modificador 3, Reflexos Gasto 6 -> Modificador 2, Fortitude Gasto 3 -> Modificador 1.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Prontidao", gmToken,
            new UpdateCreatureSkillRequest(9, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Reflexos", gmToken,
            new UpdateCreatureSkillRequest(6, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Fortitude", gmToken,
            new UpdateCreatureSkillRequest(3, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(7); // agilidade 4 + brutoProntidao 3 + 0 artefato
        body.EsquivaNatural.Should().Be(6); // agilidade 4 + brutoReflexos 2 + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 + 0 escudo + 0 artefatos + 0 cobertura
    }

    private async Task<string> CreateArmaduraItemAsync(string gmToken, int rf, int rm)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Armadura", "Peitoral de Testes", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, 12, "Medio", 5, rf, rm, "-1 Furtividade", 2, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task SubAttributes_includes_equipped_armor_RF_and_RM()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSub3", "creaturesub3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, rf: 3, rm: 2);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/armor-slots/Capacete", gmToken,
            new UpdateCreatureArmorSlotRequest(armorItemId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.ReducaoFisica.Should().Be(3);
        body.ReducaoMagica.Should().Be(2);
    }

    [Fact]
    public async Task SubAttributes_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmOwnerSub4", "creatureownersub4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmOtherSub4", "creatureothersub4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/sub-attributes", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_returns_only_the_callers_own_creatures()
    {
        var gmTokenA = await RegisterGmAndGetTokenAsync("CreatureGmList1", "creaturelist1@teste.com");
        var gmTokenB = await RegisterGmAndGetTokenAsync("CreatureGmList2", "creaturelist2@teste.com");

        var sheetA = await CreateSheetAsync(gmTokenA);
        var sheetB = await CreateSheetAsync(gmTokenB);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetA}", gmTokenA,
            ValidUpdate() with { Nome = "Creature A" }));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetB}", gmTokenB,
            ValidUpdate() with { Nome = "Creature B" }));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-sheets", gmTokenA));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CreatureSheetSummaryResponse>>();
        body.Should().HaveCount(1);
        body![0].Nome.Should().Be("Creature A");
    }

    [Fact]
    public async Task List_shows_OwnerNickname_for_a_granted_sheet_and_null_for_the_GMs_own()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmListOwner", "creaturelistowner@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureGmListOwnerPlayer", "creaturelistownerplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Creature List Owner");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var ownSheetId = await CreateSheetAsync(gmToken);
        var grantedSheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-sheets", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<CreatureSheetSummaryResponse>>();
        body!.Single(s => s.Id == ownSheetId).OwnerNickname.Should().BeNull();
        body!.Single(s => s.Id == grantedSheetId).OwnerNickname.Should().Be("CreatureGmListOwnerPlayer");
    }

    [Fact]
    public async Task List_can_filter_by_partial_Nome_Raca_Arquetipo_and_Rank_together()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmListFilter", "creaturelistfilter@teste.com");

        var sheet1 = await CreateSheetAsync(gmToken);
        var sheet2 = await CreateSheetAsync(gmToken);

        // Both creatures share Rank=F, but differ on Nome/Raca/Arquetipo
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheet1}", gmToken,
            ValidUpdate() with { Nome = "Lobo das Ruínas", Raca = "Lobo", Arquetipo = "Fisico", Rank = "F", Nivel = 3 }));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheet2}", gmToken,
            ValidUpdate() with { Nome = "Dragão de Fogo", Raca = "Dragão", Arquetipo = "Arcano", Rank = "F", Nivel = 5 }));

        // Filter by Rank alone (shared field) should return both
        var responseRankOnly = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-sheets?rank=F", gmToken));
        var bodyRankOnly = await responseRankOnly.Content.ReadFromJsonAsync<List<CreatureSheetSummaryResponse>>();
        bodyRankOnly.Should().HaveCount(2);

        // Filter by all fields (Rank + Nome + Raca + Arquetipo) should return exactly 1
        var responseAll = await _client.SendAsync(AuthedRequest(HttpMethod.Get,
            "/api/creature-sheets?nome=Lobo&raca=Lobo&arquetipo=Fisico&rank=F", gmToken));
        var bodyAll = await responseAll.Content.ReadFromJsonAsync<List<CreatureSheetSummaryResponse>>();
        bodyAll.Should().HaveCount(1);
        bodyAll![0].Nome.Should().Be("Lobo das Ruínas");
        bodyAll[0].Raca.Should().Be("Lobo");
        bodyAll[0].Arquetipo.Should().Be("Fisico");
        bodyAll[0].Rank.Should().Be("F");
    }

    [Fact]
    public async Task CampaignId_is_populated_for_a_granted_sheet_and_null_for_an_ungranted_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureCampaignIdGm1", "creaturecampaignid1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureCampaignIdPlayer1", "creaturecampaignidplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Creature CampaignId");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var grantedSheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);
        var ungrantedSheetId = await CreateSheetAsync(gmToken);

        var grantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{grantedSheetId}", playerToken));
        var granted = await grantedResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        granted!.CampaignId.Should().Be(campaignId);

        var ungrantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{ungrantedSheetId}", gmToken));
        var ungranted = await ungrantedResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        ungranted!.CampaignId.Should().BeNull();
    }
}
