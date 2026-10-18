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
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CreatureSheets;

using RuinaRPG.Tests.Integration.Controllers.Fichas;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureSheetsControllerTests(ApiFixture fixture) : FichaTestes<TipoDeTesteCriatura>(fixture)
{
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
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    private async Task AddMemberAsync(string gmToken, string campaignId, string playerId) =>
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

    /// <summary>Grants a blank Creature sheet to playerId, via the real grants endpoint, and returns its Id.</summary>
    private async Task<string> GrantBlankCreatureAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Creature", null)));
        return (await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    internal static UpdateCreatureSheetRequest ValidUpdate() => new(
        null, "Lobo das Ruínas", "Lobo", "Fisico", "Predador", "Terra",
        "F", 3, 200, 5, 12, 8, 10, "Parcial");

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await Client.PostAsync("/api/creature-sheets", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm2", "criatura2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "CreatureJogador2", "criaturajogador2@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", jogadorToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_by_the_owning_gm_returns_204_and_persists_every_field()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGm5", "criatura5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, ValidUpdate()));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Nome.Should().Be("Lobo das Ruínas");
        body.Raca.Should().Be("Lobo");
        body.Arquetipo.Should().Be("Fisico");
        body.Rank.Should().Be("F");
        body.Nivel.Should().Be(3);
        body.VitalidadeAtual.Should().Be(12);
    }

    private async Task<CreatureSheetResponse> GetCreatureAsync(string token, string sheetId) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", token)))
            .Content.ReadFromJsonAsync<CreatureSheetResponse>())!;

    private Task<HttpResponseMessage> PutPontosDeIgnicaoAsync(string token, string sheetId, int pontos) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/pontos-de-ignicao", token, pontos));

    [Fact]
    public async Task Update_does_not_change_PontosDeIgnicao()
    {
        // O saldo tem endpoint próprio: o formulário da ficha (ValidUpdate() manda 5) não o grava.
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmPi1", "criaturapi1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        (await PutPontosDeIgnicaoAsync(gmToken, sheetId, 11)).EnsureSuccessStatusCode();

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, ValidUpdate()))).EnsureSuccessStatusCode();

        (await GetCreatureAsync(gmToken, sheetId)).PontosDeIgnicao.Should().Be(11);
    }

    [Fact]
    public async Task UpdatePontosDeIgnicao_sets_the_balance_for_the_gm_and_the_granted_player()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmPi2", "criaturapi2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureJogadorPi2", "creaturejogadorpi2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Creature PI");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var sheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);

        (await PutPontosDeIgnicaoAsync(playerToken, sheetId, 23)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetCreatureAsync(gmToken, sheetId)).PontosDeIgnicao.Should().Be(23);

        (await PutPontosDeIgnicaoAsync(gmToken, sheetId, 0)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetCreatureAsync(gmToken, sheetId)).PontosDeIgnicao.Should().Be(0);
    }

    [Fact]
    public async Task UpdatePontosDeIgnicao_refuses_a_negative_value()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmPi3", "criaturapi3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        (await PutPontosDeIgnicaoAsync(gmToken, sheetId, 4)).EnsureSuccessStatusCode();

        var response = await PutPontosDeIgnicaoAsync(gmToken, sheetId, -1);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Os Pontos de Ignição não podem ser negativos.");
        (await GetCreatureAsync(gmToken, sheetId)).PontosDeIgnicao.Should().Be(4);
    }

    [Fact]
    public async Task UpdatePontosDeIgnicao_is_404_for_a_stranger_and_for_an_unknown_sheet()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureGmPiOwner4", "criaturapiowner4@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureGmPiOther4", "criaturapiother4@teste.com");
        var (_, strangerToken) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "CreatureJogadorPi4", "creaturejogadorpi4@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        (await PutPontosDeIgnicaoAsync(gmTokenOther, sheetId, 50)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutPontosDeIgnicaoAsync(strangerToken, sheetId, 50)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PutPontosDeIgnicaoAsync(gmTokenOwner, Guid.NewGuid().ToString(), 50)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetCreatureAsync(gmTokenOwner, sheetId)).PontosDeIgnicao.Should().Be(0);
    }

    [Fact]
    public async Task Create_seeds_6_zeroed_attributes_20_zeroed_skills_and_3_armor_slots()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSeed1", "creatureseed1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var attributes = await db.CreatureAttributes.Where(a => a.SheetId == Guid.Parse(sheetId)).ToListAsync();
        var skills = await db.CreatureSkills.Where(s => s.SheetId == Guid.Parse(sheetId)).ToListAsync();
        var armorSlots = await db.CreatureArmorSlots.Where(a => a.SheetId == Guid.Parse(sheetId)).ToListAsync();

        // AtributoCriatura has 6 members (not Atributo's 8).
        attributes.Should().HaveCount(6);
        attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
        // The R0005 allow-list has 20 members (not all 39 Pericia values, unlike Ficha de NPCs).
        skills.Should().HaveCount(20);
        skills.Should().OnlyContain(s => s.Gasto == 0);
        skills.Select(s => s.PericiaId).Should().BeEquivalentTo(RuinaRPG.Domain.CharacterSheets.PericiasIniciais.Todas.Where(p => p.DisponivelParaCriaturas).Select(p => p.Id));
        // ArmorSlotType has 3 members (Capacete, Superior, Inferior) — not 6.
        armorSlots.Should().HaveCount(3);
        armorSlots.Should().OnlyContain(a => a.ItemId == null);
    }

    [Fact]
    public async Task Get_computes_Abate_and_Assistencia_from_ExperienciaAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmXp1", "criaturaxp1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { ExperienciaAtual = 100 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.Abate.Should().Be(15);
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
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(8);
        body.FocoMaximo.Should().Be(4);
        body.AdrenalinaMaximo.Should().Be(10); // 10 + Artefato bonus (nenhum equipado aqui → 0; ver o teste dedicado abaixo)
    }

    [Fact]
    public async Task Get_computes_Vitalidade_and_Foco_Maximo_for_Arcano_Arquetipo()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmMax2", "criaturamax2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Arquetipo = "Arcano", Nivel = 1 };
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
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
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken, update));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var sheetGuid = Guid.Parse(sheetId);
            var vigor = await db.CreatureAttributes.SingleAsync(a => a.SheetId == sheetGuid && a.Atributo == AtributoCriatura.Vigor);
            vigor.Gasto = 5; // Vigor total = 5 (sem maestria/bonus) -> Vitalidade = 5*2 + 8 = 18
            var astucia = await db.CreatureAttributes.SingleAsync(a => a.SheetId == sheetGuid && a.Atributo == AtributoCriatura.Astucia);
            astucia.Gasto = 3; // Astúcia total = 3 -> Foco = 3*2 + 4 = 10
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}", gmToken));
        var body = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        body!.VitalidadeMaximo.Should().Be(18);
        body.FocoMaximo.Should().Be(10);
    }

    // Sub-Atributos — R0005 §2.b: mesmos campos/fórmulas do Personagem/NPC, exceto Resistência
    // Física/Arcana e Dano Cortante (pendente/sem fórmula ainda). Mirrors
    // NpcSheetsControllerTests.SubAttributes_computes_from_attributes_and_arsenal.
    [Fact]
    public async Task SubAttributes_computes_from_attributes_and_arsenal_without_Peso()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSub1", "creaturesub1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Agilidade Gasto 4, no bônus/maestria/artefato → Total 4. Vigor same → Total 4.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Agilidade", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Vigor", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Movimentacao.Should().Be(8); // (4*2) + 0 artefato - 0 sobrepeso (nothing carried yet)
        body.PesoAtual.Should().BeNull(); // Espólios is loot, not carried inventory — see docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md
        body.PesoMaximo.Should().BeNull();
    }

    [Fact]
    public async Task SubAttributes_EficienciaElemental_and_DanoElemental_are_null_for_Criatura()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSubElemental", "creaturesubelemental@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.EficienciaElemental.Should().BeNull();
        body.DanoElemental.Should().BeNull();
    }

    [Fact]
    public async Task List_returns_only_the_callers_own_creatures()
    {
        var gmTokenA = await RegisterGmAndGetTokenAsync("CreatureGmList1", "creaturelist1@teste.com");
        var gmTokenB = await RegisterGmAndGetTokenAsync("CreatureGmList2", "creaturelist2@teste.com");

        var sheetA = await CreateSheetAsync(gmTokenA);
        var sheetB = await CreateSheetAsync(gmTokenB);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetA}", gmTokenA,
            ValidUpdate() with { Nome = "Creature A" }));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetB}", gmTokenB,
            ValidUpdate() with { Nome = "Creature B" }));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-sheets", gmTokenA));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CreatureSheetSummaryResponse>>();
        body.Should().HaveCount(1);
        body![0].Nome.Should().Be("Creature A");
    }

    [Fact]
    public async Task List_can_filter_by_partial_Nome_Raca_Arquetipo_and_Rank_together()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmListFilter", "creaturelistfilter@teste.com");

        var sheet1 = await CreateSheetAsync(gmToken);
        var sheet2 = await CreateSheetAsync(gmToken);

        // Both creatures share Rank=F, but differ on Nome/Raca/Arquetipo
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheet1}", gmToken,
            ValidUpdate() with { Nome = "Lobo das Ruínas", Raca = "Lobo", Arquetipo = "Fisico", Rank = "F", Nivel = 3 }));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheet2}", gmToken,
            ValidUpdate() with { Nome = "Dragão de Fogo", Raca = "Dragão", Arquetipo = "Arcano", Rank = "F", Nivel = 5 }));

        // Filter by Rank alone (shared field) should return both
        var responseRankOnly = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/creature-sheets?rank=F", gmToken));
        var bodyRankOnly = await responseRankOnly.Content.ReadFromJsonAsync<List<CreatureSheetSummaryResponse>>();
        bodyRankOnly.Should().HaveCount(2);

        // Filter by all fields (Rank + Nome + Raca + Arquetipo) should return exactly 1
        var responseAll = await Client.SendAsync(AuthedRequest(HttpMethod.Get,
            "/api/creature-sheets?nome=Lobo&raca=Lobo&arquetipo=Fisico&rank=F", gmToken));
        var bodyAll = await responseAll.Content.ReadFromJsonAsync<List<CreatureSheetSummaryResponse>>();
        bodyAll.Should().HaveCount(1);
        bodyAll![0].Nome.Should().Be("Lobo das Ruínas");
        bodyAll[0].Raca.Should().Be("Lobo");
        bodyAll[0].Arquetipo.Should().Be("Fisico");
        bodyAll[0].Rank.Should().Be("F");
    }

    // Alma e Vida são Caminhos, não Afinidades — Requisitos - Ficha de Personagem 1.a.
    [Theory]
    [InlineData("Alma")]
    [InlineData("Vida")]
    public async Task Update_rejects_a_new_Afinidade_of_Alma_or_Vida(string afinidade)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"CaminhoAfinGmACreatureSheets{afinidade}", $"caminhoafinACreatureSheets{afinidade}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Afinidade = afinidade }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_a_legacy_Afinidade_of_Vida_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CaminhoAfinGmBCreatureSheets", "caminhoafinBCreatureSheets@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var entity = await db.CreatureSheets.SingleAsync(s => s.Id == Guid.Parse(sheetId));
            entity.Afinidade = RuinaRPG.Domain.CharacterSheets.AfinidadeElemental.Vida;
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Afinidade = "Vida" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
