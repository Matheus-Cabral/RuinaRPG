using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class HistoricosControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await Client.GetAsync("/api/historicos");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_with_no_filter_returns_a_real_Id_for_a_seeded_Historico()
    {
        var token = await RegisterGmAndGetTokenAsync("HistGm1", "hist1@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        var estudoAcademico = body!.Should().ContainSingle(h => h.Nome == "Estudo Acadêmico").Subject;
        Guid.TryParse(estudoAcademico.Id, out _).Should().BeTrue();
        estudoAcademico.PericiaMaisSeis.Should().Be("Arcano");
        estudoAcademico.PericiaMaisTres.Should().Be("Biblioteca");
    }

    [Fact]
    public async Task CreateHistorico_by_a_Rules_Auditor_persists_it_as_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm1", "histcrudgm1@teste.com");
        await GrantRulesAuditorAsync("histcrudgm1@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Testado a Ferro", "Descrição de teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<HistoricoResponse>();
        body!.Nome.Should().Be("Testado a Ferro");
        body.IsCustomized.Should().BeTrue();

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Should().Contain(h => h.Nome == "Testado a Ferro");
    }

    [Fact]
    public async Task CreateHistorico_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm2", "histcrudgm2@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Não Deveria Salvar", "Teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateHistorico_with_the_same_Pericia_twice_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm3", "histcrudgm3@teste.com");
        await GrantRulesAuditorAsync("histcrudgm3@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Perícia Repetida", "Teste.", "Atletismo", "Atletismo")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateHistorico_with_an_empty_Nome_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm10", "histcrudgm10@teste.com");
        await GrantRulesAuditorAsync("histcrudgm10@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("   ", "Teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateHistorico_with_an_invalid_Pericia_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm4", "histcrudgm4@teste.com");
        await GrantRulesAuditorAsync("histcrudgm4@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Perícia Inválida", "Teste.", "NaoExiste", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateHistorico_by_a_Rules_Auditor_persists_the_change_and_marks_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm5", "histcrudgm5@teste.com");
        await GrantRulesAuditorAsync("histcrudgm5@teste.com");
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Editar", "Antes.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created!.Id}", gmToken,
            new UpdateHistoricoRequest("Para Editar", "Depois.", "Atletismo", "Acrobacia")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Single(h => h.Id == created.Id).Descricao.Should().Be("Depois.");
    }

    [Fact]
    public async Task UpdateHistorico_with_the_same_Pericia_twice_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm6", "histcrudgm6@teste.com");
        await GrantRulesAuditorAsync("histcrudgm6@teste.com");
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Editar Errado", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created!.Id}", gmToken,
            new UpdateHistoricoRequest("Para Editar Errado", "Teste.", "Atletismo", "Atletismo")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<PericiaAuditoriaResponse> CreatePericiaAsync(string token, string nome) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token,
            new SalvarPericiaRequest(nome, null, null, false)))).Content.ReadFromJsonAsync<PericiaAuditoriaResponse>())!;

    private async Task RemovePericiaAsync(string token, int id) =>
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);

    [Fact]
    public async Task UpdateHistorico_keeping_a_removed_Pericia_is_accepted_but_switching_to_another_removed_one_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm11", "histcrudgm11@teste.com");
        await GrantRulesAuditorAsync("histcrudgm11@teste.com");
        var seis = await CreatePericiaAsync(gmToken, "Perícia Seis Removida");
        var tres = await CreatePericiaAsync(gmToken, "Perícia Três Removida");
        var outra = await CreatePericiaAsync(gmToken, "Perícia Outra Removida");
        var created = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Com Removida", "Antes.", seis.Chave, tres.Chave)))).Content.ReadFromJsonAsync<HistoricoResponse>();
        await RemovePericiaAsync(gmToken, seis.Id);
        await RemovePericiaAsync(gmToken, tres.Id);
        await RemovePericiaAsync(gmToken, outra.Id);

        var keep = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created!.Id}", gmToken,
            new UpdateHistoricoRequest("Com Removida Renomeada", "Depois.", seis.Chave, tres.Chave)));
        var change = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created.Id}", gmToken,
            new UpdateHistoricoRequest("Com Removida Renomeada", "Depois.", outra.Chave, tres.Chave)));

        keep.StatusCode.Should().Be(HttpStatusCode.NoContent);
        change.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteHistorico_soft_deletes_and_it_no_longer_appears_in_List()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm7", "histcrudgm7@teste.com");
        await GrantRulesAuditorAsync("histcrudgm7@teste.com");
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Excluir", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Should().NotContain(h => h.Id == created.Id);
    }

    // Mirrors CharacterSkillsControllerTests.UpdateWithHistorico — a minimal, valid full-form
    // UpdateCharacterSheetRequest whose only field this test class cares about is HistoricoId.
    private static UpdateCharacterSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, historicoId, null, 0);

    // Mirrors NpcSkillsControllerTests.UpdateWithHistorico — same shape for UpdateNpcSheetRequest,
    // which additionally carries Nivel (int, not nullable) right after the 8 string? fields.
    private static RuinaRPG.Contracts.NpcSheets.UpdateNpcSheetRequest UpdateNpcWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        1, true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, historicoId, null, 0);

    [Fact]
    public async Task DeleteHistorico_that_is_already_in_use_on_a_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm8", "histcrudgm8@teste.com");
        await GrantRulesAuditorAsync("histcrudgm8@teste.com");
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Em Uso", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "HistCrudPlayer8", "histcrudplayer8@teste.com");
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha Histórico", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, UpdateWithHistorico(created!.Id)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteHistorico_that_is_already_in_use_on_an_npc_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm9", "histcrudgm9@teste.com");
        await GrantRulesAuditorAsync("histcrudgm9@teste.com");
        var createResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Em Uso NPC", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var npcCreateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var npc = await npcCreateResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.NpcSheets.NpcSheetResponse>();

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npc!.Id}", gmToken, UpdateNpcWithHistorico(created!.Id)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
