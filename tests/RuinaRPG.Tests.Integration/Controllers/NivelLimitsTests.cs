using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Auditoria da Tabela de Níveis: o último nível e os saldos por nível vêm da Tabela de Níveis do
/// banco, não mais de um "50" fixo nem de regex sobre o texto de bônus.
/// </summary>
public class NivelLimitsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NivelLimitsTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    [Fact]
    public async Task Npc_level_above_the_last_level_of_the_table_is_rejected()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NivelLimGm1", "nivellim1@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmToken);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 51))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, 50))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Creature_level_above_the_last_level_of_the_table_is_rejected()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NivelLimGm2", "nivellim2@teste.com");
        var sheetId = await CreateCreatureSheetAsync(gmToken);

        var acima = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/nivel", gmToken, 51));
        acima.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await acima.Content.ReadAsStringAsync()).Should().Contain("O nível deve estar entre 1 e 50.");
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/nivel", gmToken, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/nivel", gmToken, 50))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // Expected values = what the regex calculators (deleted since) produced over the Markdown; the
    // full 50-level snapshot lives in the unit suite's NivelBonusExtractorTests.
    [Theory]
    [InlineData(1, 9, 5)]
    [InlineData(10, 13, 7)]
    [InlineData(25, 23, 8)]
    [InlineData(50, 56, 11)]
    public async Task Budgets_served_by_the_api_match_the_pre_refactor_snapshot(int nivel, int pontosDeAtributo, int espacosDeCaracteristica)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NivelSnapGm{nivel}", $"nivelsnap{nivel}@teste.com");
        var sheetId = await CreateNpcSheetAsync(gmToken);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", gmToken, nivel))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var atributos = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", gmToken)))
            .Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
        var caracteristicas = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/traits", gmToken)))
            .Content.ReadFromJsonAsync<NpcTraitsListResponse>();

        atributos!.PontosDisponiveis.Should().Be(pontosDeAtributo);
        caracteristicas!.PontosDisponiveis.Should().Be(espacosDeCaracteristica);
    }

    // ---- Tetos por nível (Máx. de Atributo/Perícia/Passivas) ----
    // A Tabela de Níveis é global ao banco da classe: cada teste grava o teto no nível 1 (herdado por
    // todos os níveis) e o remove no finally.

    private async Task<int?> SetLimiteAsync(string chave, int? valor)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var colunaId = await db.ColunasDeNivel.Where(c => c.ChaveDeSistema == chave).Select(c => c.Id).SingleAsync();
        var celula = await db.ValoresDeNivel.SingleOrDefaultAsync(v => v.ColunaId == colunaId && v.Nivel == 1);
        var anterior = celula?.Valor;
        if (celula is null)
            db.ValoresDeNivel.Add(new RuinaRPG.Infrastructure.Rules.Niveis.ValorDeNivel { ColunaId = colunaId, Nivel = 1, Valor = valor });
        else
            celula.Valor = valor;
        await db.SaveChangesAsync();
        return anterior;
    }

    private async Task<string> ReadBodyAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    private async Task<(string GmToken, string SheetId, string PlayerToken)> SetUpCharacterAsync(string tag)
    {
        var gm = await RegisterGmAndGetTokenAsync($"LimGm{tag}", $"limgm{tag}@teste.com");
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gm));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var reg = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"LimPl{tag}", $"limpl{tag}@teste.com", "Senha!123", "Senha!123", code));
        var tokens = (await reg.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken));
        var playerId = (await me.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        var campaign = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(playerId)));
        var sheet = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gm, new CreateCharacterSheetRequest(playerId)));
        return (gm, (await sheet.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id, tokens.AccessToken);
    }

    private Task<HttpResponseMessage> PutCharacterForcaAsync(string token, string sheetId, int gasto) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", token, new UpdateCharacterAttributeRequest(gasto, 0, false)));

    [Fact]
    public async Task Character_attribute_gasto_above_MaxAtributo_is_rejected_but_lowering_is_allowed()
    {
        var (gm, sheetId, _) = await SetUpCharacterAsync("A1");
        var anterior = await SetLimiteAsync("MaxAtributo", 3);
        try
        {
            var acima = await PutCharacterForcaAsync(gm, sheetId, 4);
            acima.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadBodyAsync(acima)).Should().Contain("não pode passar de 3");
            (await PutCharacterForcaAsync(gm, sheetId, 3)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            await SetLimiteAsync("MaxAtributo", anterior);
            (await PutCharacterForcaAsync(gm, sheetId, 5)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            await SetLimiteAsync("MaxAtributo", 3);
            (await PutCharacterForcaAsync(gm, sheetId, 4)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await PutCharacterForcaAsync(gm, sheetId, 6)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally { await SetLimiteAsync("MaxAtributo", anterior); }
    }

    [Fact]
    public async Task Shrinking_the_attribute_budget_below_what_was_spent_still_allows_lowering_and_editing_bonus()
    {
        var (gm, sheetId, _) = await SetUpCharacterAsync("A2");
        (await PutCharacterForcaAsync(gm, sheetId, 5)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var anterior = await SetLimiteAsync("PontosDeAtributo", 3); // orçamento do nível 1 (3) < gasto (5)
        try
        {
            // Subir é recusado; manter, baixar e editar Bônus/Maestria seguem permitidos.
            (await PutCharacterForcaAsync(gm, sheetId, 6)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var bonus = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", gm, new UpdateCharacterAttributeRequest(5, 2, true)));
            bonus.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await PutCharacterForcaAsync(gm, sheetId, 4)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally { await SetLimiteAsync("PontosDeAtributo", anterior); }
    }

    [Fact]
    public async Task Npc_attribute_gasto_above_MaxAtributo_is_rejected()
    {
        var gm = await RegisterGmAndGetTokenAsync("LimGmN1", "limgmn1@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);
        var anterior = await SetLimiteAsync("MaxAtributo", 3);
        try
        {
            var acima = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gm, new UpdateNpcAttributeRequest(4, 0, false)));
            acima.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadBodyAsync(acima)).Should().Contain("não pode passar de 3");
            (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gm, new UpdateNpcAttributeRequest(3, 0, false)))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally { await SetLimiteAsync("MaxAtributo", anterior); }
    }

    [Fact]
    public async Task Character_skill_gasto_above_MaxPericia_is_rejected()
    {
        var (gm, sheetId, _) = await SetUpCharacterAsync("S1");
        var anterior = await SetLimiteAsync("MaxPericia", 3);
        try
        {
            var acima = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", gm, new UpdateCharacterSkillRequest(4, null)));
            acima.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadBodyAsync(acima)).Should().Contain("Atletismo não pode passar de 3 pontos");
            (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", gm, new UpdateCharacterSkillRequest(3, null)))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally { await SetLimiteAsync("MaxPericia", anterior); }
    }

    [Fact]
    public async Task Npc_skill_gasto_above_MaxPericia_is_rejected()
    {
        var gm = await RegisterGmAndGetTokenAsync("LimGmN2", "limgmn2@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);
        var anterior = await SetLimiteAsync("MaxPericia", 3);
        try
        {
            var acima = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gm, new UpdateNpcSkillRequest(4, null)));
            acima.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadBodyAsync(acima)).Should().Contain("não pode passar de 3");
            (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gm, new UpdateNpcSkillRequest(3, null)))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally { await SetLimiteAsync("MaxPericia", anterior); }
    }

    private async Task<string> CreatePassivaAsync(string gm, string nome, string categoria)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gm,
            new CreateSpellAbilityEntryRequest(nome, "Passiva", 0, "Descrição.", [], false, categoria, null)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    [Fact]
    public async Task Adding_a_passiva_beyond_its_category_cap_is_rejected_other_categories_unaffected()
    {
        var (gm, sheetId, _) = await SetUpCharacterAsync("P1");
        var livre1 = await CreatePassivaAsync(gm, "Livre Um", "Livre");
        var livre2 = await CreatePassivaAsync(gm, "Livre Dois", "Livre");
        var vocacional = await CreatePassivaAsync(gm, "Vocacional Um", "Vocacional");
        Task<HttpResponseMessage> Add(string id) => _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gm,
            new AddCharacterSpellAbilityRequest(id, null, null, null, null, null)));

        var anterior = await SetLimiteAsync("MaxPassivasLivres", 1);
        try
        {
            (await Add(livre1)).StatusCode.Should().Be(HttpStatusCode.Created);
            var segunda = await Add(livre2);
            segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadBodyAsync(segunda)).Should().Contain("Livre");
            (await Add(vocacional)).StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally { await SetLimiteAsync("MaxPassivasLivres", anterior); }
    }

    [Fact]
    public async Task Npc_passiva_cap_is_enforced_too()
    {
        var gm = await RegisterGmAndGetTokenAsync("LimGmN3", "limgmn3@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);
        var livre1 = await CreatePassivaAsync(gm, "Livre Um", "Livre");
        var livre2 = await CreatePassivaAsync(gm, "Livre Dois", "Livre");
        Task<HttpResponseMessage> Add(string id) => _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(id, null, null, null, null, null)));

        var anterior = await SetLimiteAsync("MaxPassivasLivres", 1);
        try
        {
            (await Add(livre1)).StatusCode.Should().Be(HttpStatusCode.Created);
            var segunda = await Add(livre2);
            segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadBodyAsync(segunda)).Should().Contain("Livre");
        }
        finally { await SetLimiteAsync("MaxPassivasLivres", anterior); }
    }

    [Fact]
    public async Task General_npc_and_creature_put_reject_a_level_above_the_table()
    {
        var gm = await RegisterGmAndGetTokenAsync("LimGmL1", "limgml1@teste.com");
        var npcId = await CreateNpcSheetAsync(gm);
        var criaturaId = await CreateCreatureSheetAsync(gm);

        var npc = (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{npcId}", gm))).Content.ReadFromJsonAsync<NpcSheetResponse>())!;
        var npcReq = new UpdateNpcSheetRequest(npc.ImageId, npc.Nome, npc.Linhagem, npc.Variante, npc.Vocacao, npc.SubVocacao, npc.Afinidade, npc.Propriedade,
            51, npc.PossuiCoracaoDeMana, npc.ExperienciaAtual, npc.EAPAtual, 0, 0, 0, 0, 0, 0, 0, 0, 0, npc.VitalidadeAtual, npc.FocoAtual, npc.AdrenalinaAtual, 0,
            npc.Cobertura, 0, null, npc.Estrela, 0, npc.HistoricoId, null, 0);
        var npcResp = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}", gm, npcReq));
        npcResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(npcResp)).Should().Contain("O nível deve estar entre 1 e 50.");

        var cr = (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{criaturaId}", gm))).Content.ReadFromJsonAsync<CreatureSheetResponse>())!;
        var crReq = new UpdateCreatureSheetRequest(cr.ImageId, cr.Nome, cr.Raca, cr.Arquetipo, cr.SubArquetipo, cr.Afinidade, cr.Rank, 0,
            cr.ExperienciaAtual, cr.PontosDeIgnicao, cr.VitalidadeAtual, cr.FocoAtual, cr.AdrenalinaAtual, cr.Cobertura);
        var crResp = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{criaturaId}", gm, crReq));
        crResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadBodyAsync(crResp)).Should().Contain("O nível deve estar entre 1 e 50.");
    }
}
