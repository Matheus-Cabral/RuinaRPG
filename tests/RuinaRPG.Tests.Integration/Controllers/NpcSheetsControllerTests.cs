using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Rules.Niveis;

using RuinaRPG.Tests.Integration.Controllers.Fichas;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcSheetsControllerTests(ApiFixture fixture) : FichaDePersonagemOuNpcTestes<TipoDeTesteNpc>(fixture)
{
    protected override object AtualizacaoValida() => ValidUpdate();

    protected override Task DefinirXpAsync(FichaDeTeste f, int xp) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}/experiencia-atual", f.GmToken, xp));

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await Client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await Client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task AddMemberAsync(string gmToken, string campaignId, string playerId) =>
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

    /// <summary>Grants a blank Npc sheet to playerId, via the real grants endpoint, and returns its Id.</summary>
    private async Task<string> GrantBlankNpcAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Npc", null)));
        return (await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    internal static UpdateNpcSheetRequest ValidUpdate() => new(
        null, "Sentinela da Ruína", "Humano", "Sinir", "Campeao", "Duelista", null, "Guardiã do Portal",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, null, null, 0, null, null, 0);

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await Client.PostAsync("/api/npc-sheets", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm2", "npc2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "NpcJogador2", "npcjogador2@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", jogadorToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_by_the_owning_gm_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGm5", "npc5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Nome.Should().Be("Sentinela da Ruína");
        body.Linhagem.Should().Be("Humano");
        body.Variante.Should().Be("Sinir");
        body.Nivel.Should().Be(5);
        body.VitalidadeAtual.Should().Be(30);
    }

    [Fact]
    public async Task Update_persists_Estrela_and_SinaAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmEst1", "npcest1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Estrela = "Sadir", SinaAtual = 3 };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Estrela.Should().Be("Sadir");
        body.SinaAtual.Should().Be(3);
    }

    [Fact]
    public async Task GetSheet_surfaces_the_sheet_s_real_EquipmentKitId_once_one_is_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitRealGm", "equipkitreal@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        Guid kitId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            kitId = (await db.EquipmentKits.FirstAsync()).Id;
            var sheet = await db.NpcSheets.FindAsync(Guid.Parse(sheetId));
            sheet!.EquipmentKitId = kitId;
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.EquipmentKitId.Should().Be(kitId.ToString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task Update_with_SinaAtual_outside_0_to_3_returns_400(int sinaAtual)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NpcGmSina{sinaAtual}", $"npcsina{sinaAtual}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var invalid = ValidUpdate() with { SinaAtual = sinaAtual };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(1000, 150, 120)]
    [InlineData(149, 22, 17)]
    public async Task Get_by_the_gm_computes_Abate_and_Assistencia_from_ExperienciaAtual(int xp, int abate, int assistencia)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NpcGmXp{xp}", $"npcgmxp{xp}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/experiencia-atual", gmToken, xp));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>();

        body!.Abate.Should().Be(abate);
        body.Assistencia.Should().Be(assistencia);
    }

    [Fact]
    public async Task Abate_and_Assistencia_follow_ExperienciaAtual_and_Nivel_changes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmXpChange", "npcgmxpchange@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/experiencia-atual", gmToken, 1000));
        var before = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/experiencia-atual", gmToken, 2000));
        var after = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 5));
        var viaNivel = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>();

        before!.Abate.Should().Be(150);
        after!.Abate.Should().Be(300);
        after.Assistencia.Should().Be(240);
        viaNivel!.ExperienciaAtual.Should().Be(500);
        viaNivel.Abate.Should().Be(75);
        viaNivel.Assistencia.Should().Be(60);
    }

    [Fact]
    public async Task Get_by_the_player_the_sheet_was_granted_to_hides_Abate_and_Assistencia()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmXpHide", "npcgmxphide@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcJogadorXpHide", "npcjogadorxphide@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha NPC Xp");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var sheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/experiencia-atual", playerToken, 1000));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", playerToken)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>();

        body!.ExperienciaAtual.Should().Be(1000);
        body.Abate.Should().BeNull();
        body.Assistencia.Should().BeNull();
    }

    [Fact]
    public async Task Update_allows_an_Afinidade_liberada_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmAfin2", "npcafin2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Vocacao = "Bruxo", Afinidade = "Necromancia" };
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_keeps_an_old_Afinidade_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmAfin3", "npcafin3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Vocacao = "Bruxo", Afinidade = "Necromancia" }));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Necromancia" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Afinidade.Should().Be("Necromancia");
    }

    [Fact]
    public async Task Create_seeds_8_zeroed_attributes_39_zeroed_skills_and_3_armor_slots()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSeed1", "npcseed1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var attributes = await db.NpcAttributes.Where(a => a.SheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.NpcSkills.Where(s => s.SheetId == Guid.Parse(sheetId)).ToListAsync();
        var armorSlots = await db.NpcArmorSlots.Where(a => a.SheetId == Guid.Parse(sheetId)).ToListAsync();

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

        var update = ValidUpdate() with { Vocacao = "Campeao" }; // Nível 5 = 120 EAP
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.GraduacaoLabel.Should().Be("Grau");
        body.Graduacao.Should().Be(1); // 120 EAP (Nível 5) >= the real Tabela's Grau 1 threshold (100)
    }

    [Fact]
    public async Task Graduacao_of_a_Cacador_at_Nivel_1_is_Grau_1_and_a_Feiticeiro_without_coracao_stays_0()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmGrau1", "npcgrau1@teste.com");
        var cacadorId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{cacadorId}", gmToken, ValidUpdate() with { Vocacao = "Cacador", Nivel = 1 }));
        var cacador = await GetNpcAsync(gmToken, cacadorId);
        cacador.EAPAtual.Should().Be(0);
        cacador.Graduacao.Should().Be(1);

        var feiticeiroId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{feiticeiroId}", gmToken, ValidUpdate() with { Vocacao = "Feiticeiro", Nivel = 1, PossuiCoracaoDeMana = false }));
        (await GetNpcAsync(gmToken, feiticeiroId)).Graduacao.Should().Be(0);
    }

    [Fact]
    public async Task EAPAtual_is_the_level_base_ignoring_the_EAPAtual_sent()
    {
        // Mesma regra da Ficha de Personagem: base do Nível (Nível 5 = 120 na tabela de EAP) + Âmbares.
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmEap1", "npceap1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Nivel = 5, EAPAtual = 99999 }));

        (await GetNpcAsync(gmToken, sheetId)).EAPAtual.Should().Be(120);
    }

    [Fact]
    public async Task EAPAtual_adds_the_Ambares_by_Rank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmEap2", "npceap2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Nivel = 5, NucleosRankF = 2 }));

        (await GetNpcAsync(gmToken, sheetId)).EAPAtual.Should().Be(130); // 120 + 2*5
    }

    [Fact]
    public async Task UpdateNivel_changes_the_computed_EAPAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmEap3", "npceap3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 6));

        (await GetNpcAsync(gmToken, sheetId)).EAPAtual.Should().Be(150);
    }

    [Fact]
    public async Task Graduacao_follows_the_computed_EAP_not_the_EAPAtual_sent()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmEap4", "npceap4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Nível 2 = 30 EAP, abaixo do limiar do Grau 1 (100): o 99999 enviado não pode subir o Grau.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Vocacao = "Campeao", Nivel = 2, EAPAtual = 99999 }));
        var abaixo = await GetNpcAsync(gmToken, sheetId);
        abaixo.EAPAtual.Should().Be(30);
        abaixo.Graduacao.Should().Be(1); // piso das vocações marciais, não o Grau do 99999

        // Âmbares Rank C (120) levam o VIS calculado a 150 >= 100: Grau 1.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Vocacao = "Campeao", Nivel = 2, NucleosRankC = 1 }));
        var acima = await GetNpcAsync(gmToken, sheetId);
        acima.EAPAtual.Should().Be(150);
        acima.Graduacao.Should().Be(1);
    }

    [Fact]
    public async Task List_returns_only_the_callers_own_npcs()
    {
        var gmTokenA = await RegisterGmAndGetTokenAsync("NpcGmListA", "npcgmlista@teste.com");
        var gmTokenB = await RegisterGmAndGetTokenAsync("NpcGmListB", "npcgmlistb@teste.com");

        var sheetIdA = await CreateSheetAsync(gmTokenA);
        var sheetIdB = await CreateSheetAsync(gmTokenB);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets", gmTokenA));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        body.Should().HaveCount(1);
        body![0].Id.Should().Be(sheetIdA);
    }

    [Fact]
    public async Task List_can_filter_by_Variante()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmListFilterVariante", "npcgmlistfiltervariante@teste.com");
        var sheetId1 = await CreateSheetAsync(gmToken);
        var sheetId2 = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId1}", gmToken, ValidUpdate() with { Variante = "Sinir" }));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId2}", gmToken, ValidUpdate() with { Variante = "Laonir" }));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets?variante=Laonir", gmToken));

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
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId1}", gmToken, update1));

        var update2 = ValidUpdate() with { Nome = "Assassino Silencioso", Linhagem = "Humano", Nivel = 7 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId2}", gmToken, update2));

        // Verify that filtering by Linhagem alone returns both sheets (proving it's not being ignored)
        var filterByLinhagemAlone = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets?linhagem=Humano", gmToken));
        filterByLinhagemAlone.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyLinhagemOnly = await filterByLinhagemAlone.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        bodyLinhagemOnly.Should().HaveCount(2);

        // Now filter by all three fields together and verify only the matching one is returned
        // This proves the three filters combine with AND, not OR
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/npc-sheets?nome=Sentinela&linhagem=Humano&nivel=5", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NpcSheetSummaryResponse>>();
        body.Should().HaveCount(1);
        body![0].Id.Should().Be(sheetId1);
        body![0].Nome.Should().Be("Sentinela da Ruína");
        body![0].Linhagem.Should().Be("Humano");
        body![0].Nivel.Should().Be(5);
    }

    [Fact]
    public async Task RacialAbility_resolves_the_Arca_for_a_Humano_sheet_that_has_rolled()
    {
        // NOTE: the plan brief's literal test text reused "NpcGmRacial1"/"npcracial1@teste.com" —
        // the exact same credentials as the RacialAbility_is_null_before_a_Variante... test above,
        // which shares this class's Postgres container/fixture. That would collide (duplicate
        // email) and fail registration for an unrelated reason. Renumbered to Racial2 here,
        // matching this file's own established naming convention.
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmRacial2", "npcracial2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/3", gmToken, new RuinaRPG.Contracts.CharacterSheets.UpdateArcaEntryRequest("Sombra Fugaz", "Some por 1 turno.")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 3 }));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken));

        var body = await response.Content.ReadFromJsonAsync<RacialAbilityResponse>();
        body!.ArcaRolada.Should().Be(3);
        body.ArcaNome.Should().Be("Sombra Fugaz");
    }

    private Task<HttpResponseMessage> PutNpcArcaAsync(string token, string sheetId, int arca, int vitalidadeAtual = 0) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", token,
            ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = arca, VitalidadeAtual = vitalidadeAtual }));

    [Fact]
    public async Task Update_accepts_ArcaRolada_20_and_rejects_21_under_the_default_die()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaDado1", "npcarcadado1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        (await PutNpcArcaAsync(gmToken, sheetId, 20)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rejected = await PutNpcArcaAsync(gmToken, sheetId, 21);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("ArcaRolada deve estar entre 1 e 20.");
    }

    [Fact]
    public async Task Update_rejects_ArcaRolada_7_under_D6()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaDado2", "npcarcadado2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", gmToken, new RuinaRPG.Contracts.CharacterSheets.ArcaDadoRequest(6)));

        (await PutNpcArcaAsync(gmToken, sheetId, 7)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_stored_ArcaRolada_above_a_shrunken_die_is_reported_out_of_the_table_and_does_not_block_saving_other_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaDado3", "npcarcadado3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/15", gmToken, new RuinaRPG.Contracts.CharacterSheets.UpdateArcaEntryRequest("Arca Alta", "Descrição.")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/15/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "evo")));
        (await PutNpcArcaAsync(gmToken, sheetId, 15)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/dado", gmToken, new RuinaRPG.Contracts.CharacterSheets.ArcaDadoRequest(12)));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();
        body!.ArcaDado.Should().Be(12);
        body.ArcaForaDaTabela.Should().BeTrue();
        body.ArcaNome.Should().BeNull();
        body.ArcaDescricao.Should().BeNull();
        body.ArcaEvolucoes.Should().BeEmpty();

        (await PutNpcArcaAsync(gmToken, sheetId, 15, vitalidadeAtual: 1)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PutNpcArcaAsync(gmToken, sheetId, 14)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PutNpcArcaAsync(gmToken, sheetId, 12)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RacialAbility_lists_only_Arca_evolucoes_unlocked_by_the_npc_level()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaEvo1", "npcarcaevo1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/3", gmToken, new UpdateArcaEntryRequest("Sombra Fugaz", "Some por 1 turno.")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(5, "cinco")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "dois")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(6, "seis")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 3, Nivel = 5 }));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaEvolucoes.Select(e => e.Descricao).Should().Equal("dois", "cinco");
    }

    [Fact]
    public async Task RacialAbility_has_no_Arca_evolucoes_when_the_npc_is_not_Humano()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaEvo2", "npcarcaevo2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "um")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 3, Nivel = 5 }));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Nephrytes", Variante = "Yavos", ArcaRolada = 3, Nivel = 5 }));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaEvolucoes.Should().BeEmpty();
    }

    [Fact]
    public async Task RacialAbility_treats_an_Arca_created_only_by_an_evolucao_as_not_registered_but_lists_the_evolucao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaEvo3", "npcarcaevo3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/8/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "um")));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 8, Nivel = 1 }));

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaNome.Should().BeNull();
        body.ArcaDescricao.Should().BeNull();
        body.ArcaEvolucoes.Select(e => e.Descricao).Should().Equal("um");
    }

    [Fact]
    public async Task RacialAbility_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcGmOwnerRacial2", "npcownerracial2@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcGmOtherRacial2", "npcotherracial2@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubAttributes_includes_the_Historico_bonus_on_Prontidao_and_Reflexos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmSubHist1", "npcsubhist1@teste.com");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "NPCSUBHIST1@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var historicoResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus Sub-Atributo NPC", "Descrição de teste.", "Prontidao", "Reflexos")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var sheetId = await CreateSheetAsync(gmToken);

        // Cobertura is pinned to "Nenhuma" (ValidUpdate()'s default is "Parcial", which would add
        // +5 to DefesaNatural via SubAttributes' cobertura term and break the "0 cobertura" assertion below).
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { HistoricoId = historico!.Id, Cobertura = "Nenhuma" }));

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Agilidade", gmToken,
            new UpdateAttributeRequest(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateAttributeRequest(4, 0, false)));

        // Prontidao Gasto 9, Histórico bonus 6 counts as Pericia points -> Modificador (9+6)/3 = 5.
        // Reflexos Gasto 6, Histórico bonus 3 -> Modificador (6+3)/3 = 3.
        // Fortitude Gasto 3 -> Modificador 1, untouched by this Histórico.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Prontidao", gmToken,
            new UpdateSkillRequest(9, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Reflexos", gmToken,
            new UpdateSkillRequest(6, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Fortitude", gmToken,
            new UpdateSkillRequest(3, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(9); // agilidade 4 + brutoProntidao ((9+6)/3=5) + 0 artefato
        body.EsquivaNatural.Should().Be(7); // agilidade 4 + brutoReflexos ((6+3)/3=3) + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 (no bonus) + 0 escudo + 0 artefatos + 0 cobertura
    }

    [Fact]
    public async Task SubAttributes_PesoMaximo_ignores_the_BonusDeCarga_of_the_gms_campaigns()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmPesoBc", "npcpesobc@teste.com");
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new RuinaRPG.Contracts.Campaigns.CreateCampaignRequest("Campanha Bônus", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignResponse>())!.Id;
        var update = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}", gmToken, new RuinaRPG.Contracts.Campaigns.UpdateCampaignRequest("Campanha Bônus", "", null, 25m)));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken, new UpdateAttributeRequest(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken, new UpdateAttributeRequest(4, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoMaximo.Should().Be(4m);
        body.BonusDeCargaDaCampanha.Should().Be(0m);
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
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        // Vigor total = Gasto(5) + Bonus(0)/2 (sem maestria) = 5 → Vitalidade = 5*2 + 8 = 18.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateAttributeRequest(5, 0, false)));
        // Astúcia total = Gasto(3) + Bonus(0)/2 (sem maestria) = 3 → Foco = 3*2 + 4 = 10.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Astucia", gmToken,
            new UpdateAttributeRequest(3, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));

        var body = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(18);
        body.FocoMaximo.Should().Be(10);
        body.AdrenalinaMaximo.Should().Be(10); // 10 + Artefato bonus (nenhum equipado aqui → 0; ver o teste dedicado abaixo)
        body.EstresseMaximo.Should().Be(10); // flat
    }

    // Alma e Vida são Caminhos, não Afinidades — Requisitos - Ficha de Personagem 1.a.
    [Theory]
    [InlineData("Alma")]
    [InlineData("Vida")]
    public async Task Update_rejects_a_new_Afinidade_of_Alma_or_Vida(string afinidade)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"CaminhoAfinGmANpcSheets{afinidade}", $"caminhoafinANpcSheets{afinidade}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = afinidade }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_a_legacy_Afinidade_of_Vida_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CaminhoAfinGmBNpcSheets", "caminhoafinBNpcSheets@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var entity = await db.NpcSheets.SingleAsync(s => s.Id == Guid.Parse(sheetId));
            entity.Afinidade = RuinaRPG.Domain.CharacterSheets.AfinidadeElemental.Vida;
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Vida" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<NpcSheetResponse> GetNpcAsync(string token, string sheetId) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", token)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>())!;

    private async Task<int> PiAcumuladoAsync(int nivel)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var tabela = await scope.ServiceProvider.GetRequiredService<ITabelaDeNiveis>().ObterAsync();
        return tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivel);
    }

    [Fact]
    public async Task A_new_npc_starts_with_the_level_1_Pontos_de_Ignicao()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiNovo", "npcpinovo@teste.com");
        var id = await CreateSheetAsync(gm);

        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(await PiAcumuladoAsync(1));
    }

    [Fact]
    public async Task Changing_the_npc_level_through_the_nivel_endpoint_moves_Atual()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiNivel", "npcpinivel@teste.com");
        var id = await CreateSheetAsync(gm);
        var inicial = (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual;

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}/nivel", gm, 4))).EnsureSuccessStatusCode();
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(inicial + await PiAcumuladoAsync(4) - await PiAcumuladoAsync(1));

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}/nivel", gm, 1))).EnsureSuccessStatusCode();
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(inicial);
    }

    [Fact]
    public async Task Changing_the_npc_experience_moves_Atual_with_the_level_it_computes()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiXp", "npcpixp@teste.com");
        var id = await CreateSheetAsync(gm);
        var inicial = (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual;

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}/experiencia-atual", gm, 749))).EnsureSuccessStatusCode();

        var npc = await GetNpcAsync(gm, id);
        npc.PontosDeIgnicaoAtual.Should().Be(inicial + await PiAcumuladoAsync(npc.Nivel) - await PiAcumuladoAsync(1));
    }

    private Task<HttpResponseMessage> PutPontosDeIgnicaoAsync(string token, string sheetId, int pontos) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/pontos-de-ignicao", token, pontos));

    [Fact]
    public async Task The_full_npc_update_applies_the_level_delta_and_ignores_PontosDeIgnicaoTotal()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiPut", "npcpiput@teste.com");
        var id = await CreateSheetAsync(gm);
        var antes = await GetNpcAsync(gm, id);
        (await PutPontosDeIgnicaoAsync(gm, id, 6)).EnsureSuccessStatusCode();

        // O saldo do formulário (500) é ignorado: a diferença de nível entra sobre o valor guardado (6).
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}", gm,
            ValidUpdate() with { Nivel = 3, PontosDeIgnicaoAtual = 500, PontosDeIgnicaoTotal = 999 }))).EnsureSuccessStatusCode();

        var npc = await GetNpcAsync(gm, id);
        npc.PontosDeIgnicaoAtual.Should().Be(6 + await PiAcumuladoAsync(3) - await PiAcumuladoAsync(antes.Nivel));
        npc.PontosDeIgnicaoTotal.Should().Be(antes.PontosDeIgnicaoTotal);
    }

    [Fact]
    public async Task The_full_npc_update_ignores_the_PontosDeIgnicaoAtual_of_the_request()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiIgnora", "npcpiignora@teste.com");
        var id = await CreateSheetAsync(gm);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}", gm, ValidUpdate()))).EnsureSuccessStatusCode();
        var guardado = (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual;

        // Mesmo nível: só o saldo do formulário difere.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}", gm,
            ValidUpdate() with { PontosDeIgnicaoAtual = guardado + 123 }))).EnsureSuccessStatusCode();

        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(guardado);
    }

    [Fact]
    public async Task The_pontos_de_ignicao_endpoint_sets_the_balance_for_the_gm_and_the_granted_player()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiCampo", "npcpicampo@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPiCampoP", "npcpicampop@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha PI Campo");
        await AddMemberAsync(gm, campaignId, playerId);
        var id = await GrantBlankNpcAsync(gm, campaignId, playerId);

        (await PutPontosDeIgnicaoAsync(playerToken, id, 23)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(23);

        (await PutPontosDeIgnicaoAsync(gm, id, 0)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(0);
    }

    [Fact]
    public async Task The_pontos_de_ignicao_endpoint_refuses_a_negative_value()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiNegativo", "npcpinegativo@teste.com");
        var id = await CreateSheetAsync(gm);
        var antes = (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual;

        var response = await PutPontosDeIgnicaoAsync(gm, id, -1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Os Pontos de Ignição não podem ser negativos.");
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(antes);
    }

    [Fact]
    public async Task The_pontos_de_ignicao_endpoint_is_404_for_a_stranger_and_for_an_unknown_sheet()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiEstranho", "npcpiestranho@teste.com");
        var outroGm = await RegisterGmAndGetTokenAsync("NpcPiEstranhoB", "npcpiestranhob@teste.com");
        var (_, strangerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPiEstranhoP", "npcpiestranhop@teste.com");
        var id = await CreateSheetAsync(gm);
        var antes = (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual;

        (await PutPontosDeIgnicaoAsync(outroGm, id, 50)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutPontosDeIgnicaoAsync(strangerToken, id, 50)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutPontosDeIgnicaoAsync(gm, Guid.NewGuid().ToString(), 50)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(antes);
    }

    [Fact]
    public async Task A_blank_npc_granted_to_a_player_also_starts_with_the_level_1_points()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiGrant", "npcpigrant@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPiGrantP", "npcpigrantp@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha PI");
        await AddMemberAsync(gm, campaignId, playerId);
        var id = await GrantBlankNpcAsync(gm, campaignId, playerId);

        (await GetNpcAsync(playerToken, id)).PontosDeIgnicaoAtual.Should().Be(await PiAcumuladoAsync(1));
    }

    [Fact]
    public async Task UpdateHistoria_by_the_gm_persists_sanitized_html()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm1", "npchistgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", gmToken,
            new UpdateHistoriaRequest("<p>Guardiã do portal</p><img src=x onerror=alert(1)>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcAsync(gmToken, sheetId)).Historia.Should().Contain("Guardiã do portal").And.NotContain("img");
    }

    [Fact]
    public async Task UpdateHistoria_by_a_different_gm_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm2", "npchistgm2@teste.com");
        var otherGmToken = await RegisterGmAndGetTokenAsync("NpcHistGm2b", "npchistgm2b@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", otherGmToken,
            new UpdateHistoriaRequest("<p>x</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateHistoria_by_the_player_the_npc_was_granted_to_is_allowed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm3", "npchistgm3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcHistPlayer3", "npchistplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha História NPC");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var sheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", playerToken,
            new UpdateHistoriaRequest("<p>Meu companheiro</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateHistoria_longer_than_200000_characters_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm4", "npchistgm4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", gmToken,
            new UpdateHistoriaRequest(new string('a', 200_001))));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_general_npc_Update_leaves_Historia_untouched()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm5", "npchistgm5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", gmToken, new UpdateHistoriaRequest("<p>Passado sombrio</p>")));

        var update = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate()));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcAsync(gmToken, sheetId)).Historia.Should().Contain("Passado sombrio");
    }
}
