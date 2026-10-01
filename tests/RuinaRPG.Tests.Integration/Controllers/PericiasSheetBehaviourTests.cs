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
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class PericiasSheetBehaviourTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public PericiasSheetBehaviourTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<T> GetAsync<T>(string url, string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, url, token));
        response.StatusCode.Should().Be(HttpStatusCode.OK, url);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<string> RegisterAuditorAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return token;
    }

    private async Task<PericiaAuditoriaResponse> CreatePericiaAsync(string auditor, string nome, string? atributo, bool criaturas)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", auditor, new SalvarPericiaRequest(nome, null, atributo, criaturas)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PericiaAuditoriaResponse>())!;
    }

    private async Task<(string SheetId, string PlayerToken)> CreateCharacterSheetAsync(string gmToken, string prefix)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var reg = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(prefix + "Player", prefix.ToLowerInvariant() + "player@teste.com", "Senha!123", "Senha!123", code));
        var tokens = (await reg.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await GetAsync<MeResponse>("/api/auth/me", tokens.AccessToken);

        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(prefix + " Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(me.Id)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(me.Id)));
        return ((await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id, tokens.AccessToken);
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
    public async Task A_pericia_created_after_the_sheet_is_listed_with_zero_and_its_suggested_attribute_and_can_be_saved()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud1", "persheetaud1@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet1");
        var nova = await CreatePericiaAsync(auditor, "Esgrima Élfica", "Destreza", criaturas: false);

        var skills = await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token);
        var linha = skills.Single(s => s.Pericia == nova.Chave);
        linha.Gasto.Should().Be(0);
        linha.AtributoEscolhido.Should().Be("Destreza");
        linha.Nome.Should().Be("Esgrima Élfica");

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/{nova.Chave}", token, new UpdateCharacterSkillRequest(2, "Destreza")));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(2);
    }

    [Fact]
    public async Task Removing_a_pericia_refunds_its_points_hides_it_and_restoring_brings_it_back_at_zero()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud2", "persheetaud2@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet2");
        var nova = await CreatePericiaAsync(auditor, "Falcoaria", null, criaturas: false);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/{nova.Chave}", token, new UpdateCharacterSkillRequest(3, null)));
        var antes = await GetAsync<SkillPointBudgetResponse>($"/api/character-sheets/{sheetId}/skills/budget", token);

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));

        var depois = await GetAsync<SkillPointBudgetResponse>($"/api/character-sheets/{sheetId}/skills/budget", token);
        depois.GastoTotal.Should().Be(antes.GastoTotal - 3);
        (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Should().NotContain(s => s.Pericia == nova.Chave);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/{nova.Chave}", token, new UpdateCharacterSkillRequest(1, null))))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{nova.Id}/restaurar", auditor));
        (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(0);
    }

    [Fact]
    public async Task Removal_zeroes_the_pericia_on_npc_and_creature_sheets_too()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud3", "persheetaud3@teste.com");
        var nova = await CreatePericiaAsync(auditor, "Domar Feras", null, criaturas: true);
        var npcId = await CreateNpcSheetAsync(auditor);
        var creatureId = await CreateCreatureSheetAsync(auditor);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}/skills/{nova.Chave}", auditor, new UpdateNpcSkillRequest(4, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}/skills/{nova.Chave}", auditor, new UpdateCreatureSkillRequest(4, null)));

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{nova.Id}/restaurar", auditor));

        (await GetAsync<List<NpcSkillResponse>>($"/api/npc-sheets/{npcId}/skills", auditor)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(0);
        (await GetAsync<List<CreatureSkillResponse>>($"/api/creature-sheets/{creatureId}/skills", auditor)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(0);
    }

    [Fact]
    public async Task Creature_sheets_only_list_pericias_available_for_criaturas()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud4", "persheetaud4@teste.com");
        var sim = await CreatePericiaAsync(auditor, "Rugir", null, criaturas: true);
        var nao = await CreatePericiaAsync(auditor, "Contabilidade", null, criaturas: false);
        var creatureId = await CreateCreatureSheetAsync(auditor);

        var skills = await GetAsync<List<CreatureSkillResponse>>($"/api/creature-sheets/{creatureId}/skills", auditor);

        skills.Should().Contain(s => s.Pericia == sim.Chave);
        skills.Should().NotContain(s => s.Pericia == nao.Chave);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}/skills/{nao.Chave}", auditor, new UpdateCreatureSkillRequest(1, null))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Skill_list_shows_the_description_and_is_ordered_by_name()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud5", "persheetaud5@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/7", auditor, new SalvarPericiaRequest("Atletismo", "Correr, saltar, escalar.", null, true)));
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet5");

        var skills = await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token);

        skills.Single(s => s.Pericia == "Atletismo").Descricao.Should().Be("Correr, saltar, escalar.");
        skills.Select(s => s.Nome).Should().BeInAscendingOrder(StringComparer.CurrentCulture);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/7", auditor, new SalvarPericiaRequest("Atletismo", null, null, true)));
    }

    [Fact]
    public async Task A_mastery_on_a_removed_pericia_is_hidden()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud6", "persheetaud6@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet6");
        var nova = await CreatePericiaAsync(auditor, "Tecelagem", null, criaturas: false);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", token, new AddCharacterMasteryRequest("Tear Rápido", nova.Chave, "Destreza", 1)));

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));

        var masteries = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/masteries", token));
        masteries.StatusCode.Should().Be(HttpStatusCode.OK);
        (await masteries.Content.ReadFromJsonAsync<List<CharacterMasteryResponse>>())!.Should().NotContain(m => m.Pericia == nova.Chave);
    }

    [Fact]
    public async Task A_legacy_artefato_targeting_ArmasBrancas_still_adds_its_bonus()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud7", "persheetaud7@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet7");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/ArmasBrancas", token, new UpdateCharacterSkillRequest(0, "Forca")));
        var antes = (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == "ArmasBrancas").Total;

        // Same CreateItemRequest shape as CharacterSheetsControllerTests.CreateDanoArtefatoItemAsync,
        // with TipoDeAlvo "Pericia" and Alvo "ArmasBrancas" (the legacy enum name = seeded Chave).
        var item = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", auditor,
            new CreateItemRequest("Artefato", "Anel do Duelista", 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, "Pericia", "ArmasBrancas", 2, null)))).Content.ReadFromJsonAsync<ItemResponse>();
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", token, new AddCharacterArtifactRequest(item!.Id)));

        var depois = (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == "ArmasBrancas").Total;
        depois.Should().Be(antes + 2);
    }

    [Fact]
    public async Task Unknown_and_removed_keys_return_404_on_npc_and_creature_skill_updates()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud8", "persheetaud8@teste.com");
        var nova = await CreatePericiaAsync(auditor, "Heráldica", null, criaturas: true);
        var npcId = await CreateNpcSheetAsync(auditor);
        var creatureId = await CreateCreatureSheetAsync(auditor);
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));

        foreach (var chave in new[] { "ChaveInexistente", nova.Chave })
        {
            (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}/skills/{chave}", auditor, new UpdateNpcSkillRequest(1, null))))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}/skills/{chave}", auditor, new UpdateCreatureSkillRequest(1, null))))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Adding_a_mastery_for_a_removed_pericia_returns_400()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud9", "persheetaud9@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet9");
        var nova = await CreatePericiaAsync(auditor, "Cestaria", null, criaturas: false);
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", token, new AddCharacterMasteryRequest("Cesto", nova.Chave, "Destreza", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_mastery_on_a_pericia_created_after_the_sheet_can_be_added_and_listed_on_every_sheet_kind()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud10", "persheetaud10@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet10");
        var nova = await CreatePericiaAsync(auditor, "Ourivesaria", null, criaturas: true);
        var npcId = await CreateNpcSheetAsync(auditor);
        var creatureId = await CreateCreatureSheetAsync(auditor);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", token, new AddCharacterMasteryRequest("Filigrana", nova.Chave, "Destreza", 1))))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{npcId}/masteries", auditor, new AddNpcMasteryRequest("Filigrana", nova.Chave, "Destreza", 1))))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{creatureId}/masteries", auditor, new AddCreatureMasteryRequest("Filigrana", nova.Chave, "Destreza", 1))))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await GetAsync<List<CharacterMasteryResponse>>($"/api/character-sheets/{sheetId}/masteries", token)).Should().ContainSingle(m => m.Pericia == nova.Chave);
        (await GetAsync<List<NpcMasteryResponse>>($"/api/npc-sheets/{npcId}/masteries", auditor)).Should().ContainSingle(m => m.Pericia == nova.Chave);
        (await GetAsync<List<CreatureMasteryResponse>>($"/api/creature-sheets/{creatureId}/masteries", auditor)).Should().ContainSingle(m => m.Pericia == nova.Chave);
    }

    [Fact]
    public async Task A_pericia_created_after_the_sheet_can_be_saved_on_npc_and_creature_sheets()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud11", "persheetaud11@teste.com");
        var npcId = await CreateNpcSheetAsync(auditor);
        var creatureId = await CreateCreatureSheetAsync(auditor);
        var nova = await CreatePericiaAsync(auditor, "Mergulho", "Forca", criaturas: true);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}/skills/{nova.Chave}", auditor, new UpdateNpcSkillRequest(2, null))))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}/skills/{nova.Chave}", auditor, new UpdateCreatureSkillRequest(2, null))))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetAsync<List<NpcSkillResponse>>($"/api/npc-sheets/{npcId}/skills", auditor)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(2);
        (await GetAsync<List<CreatureSkillResponse>>($"/api/creature-sheets/{creatureId}/skills", auditor)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(2);
    }
}
