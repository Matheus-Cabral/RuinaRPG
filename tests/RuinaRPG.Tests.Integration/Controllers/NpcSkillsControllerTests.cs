using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class NpcSkillsControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", "Anel de Teste", 0.1m, 500, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private static UpdateNpcSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        1, true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, historicoId, null, 0);

    [Fact]
    public async Task List_returns_39_skills_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm1", "npcskill1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();
        body!.Should().HaveCount(39);
    }

    [Fact]
    public async Task Update_a_skill_sets_Gasto_and_the_response_computes_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm2", "npcskill2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateNpcSkillRequest(9, null)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Modificador.Should().Be(3); // 9 / 3
    }

    [Fact]
    public async Task List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillHistGm1", "npcskillhistgm1@teste.com");
        await GrantRulesAuditorAsync("npcskillhistgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var historicoResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus NPC", "Descrição de teste.", "Arcano", "Biblioteca")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            UpdateWithHistorico(historico!.Id)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Arcano", gmToken,
            new UpdateNpcSkillRequest(3, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();
        var arcano = body!.Single(s => s.Pericia == "Arcano");
        arcano.Modificador.Should().Be(3); // (3+6)/3 = 3 -- Histórico bonus counts as Pericia points spent
        arcano.ContaHistorico.Should().BeTrue();
        var biblioteca = body!.Single(s => s.Pericia == "Biblioteca"); // PericiaMaisTres, Gasto still 0
        biblioteca.Modificador.Should().Be(1); // (0+3)/3 = 1
        biblioteca.ContaHistorico.Should().BeTrue();
        var fortitude = body!.Single(s => s.Pericia == "Fortitude");
        fortitude.Modificador.Should().Be(0); // untouched, no bonus
        fortitude.ContaHistorico.Should().BeFalse();
    }

    [Fact]
    public async Task Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm4", "npcskill4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Forca: Gasto 4, Bonus 0, no maestria -> Total 4 (AttributeTotalCalculator.Total)
        var attrResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));
        attrResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Atletismo: Gasto 9 -> Modificador 3 (SkillFormulas.Modificador), Atributo escolhido Forca
        var skillResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateNpcSkillRequest(9, "Forca")));
        skillResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();

        var atletismo = body!.Single(s => s.Pericia == "Atletismo");
        atletismo.AtributoEscolhido.Should().Be("Forca");
        atletismo.Modificador.Should().Be(3);
        atletismo.Total.Should().Be(7); // SkillFormulas.Total(modificador: 3, atributoTotal: 4)

        var acrobacia = body!.Single(s => s.Pericia == "Acrobacia");
        acrobacia.AtributoEscolhido.Should().BeNull();
        acrobacia.Total.Should().BeNull();
    }

    [Fact]
    public async Task An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGmArt1", "npcskillart1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateNpcAttributeRequest(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateNpcSkillRequest(9, "Forca")));

        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Pericia", "Atletismo", 3);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/artifacts", gmToken, new AddNpcArtifactRequest(artifactItemId)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Total.Should().Be(10); // modificador 3 + atributo 4 + artefato 3
    }

    [Fact]
    public async Task List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcSkillGm6", "npcskill6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var beforeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>())!;
        before.Select(s => s.Pericia).Should().BeInAscendingOrder(StringComparer.Ordinal).And.HaveCount(39);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Sobrevivencia", gmToken,
            new UpdateNpcSkillRequest(3, null)));

        var afterResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmToken));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<NpcSkillResponse>>())!;
        after.Select(s => s.Pericia).Should().Equal(before.Select(s => s.Pericia));
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcSkillGmOwner3", "npcskillowner3@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcSkillGmOther3", "npcskillother3@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/skills/Atletismo", gmTokenOther,
            new UpdateNpcSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("NpcSkillGmOwner5", "npcskillowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("NpcSkillGmOther5", "npcskillother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/skills", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
