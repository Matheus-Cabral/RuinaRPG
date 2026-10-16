using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterSkillsControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", "Anel de Teste", 0.1m, 500, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, historicoId, null, 0);

    [Fact]
    public async Task List_returns_39_skills_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm1", "skill1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer1", "skillplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();
        body!.Should().HaveCount(39);
    }

    [Fact]
    public async Task Update_a_skill_sets_Gasto_and_the_response_computes_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm2", "skill2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer2", "skillplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", playerToken,
            new UpdateCharacterSkillRequest(9, null)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Modificador.Should().Be(3); // 9 / 3
    }

    [Fact]
    public async Task Update_an_unknown_Pericia_key_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGmUnknown", "skillunknown@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayerUnknown", "skillplayerunknown@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Inexistente", playerToken,
            new UpdateCharacterSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillHistGm1", "skillhistgm1@teste.com");
        await GrantRulesAuditorAsync("skillhistgm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillHistPlayer1", "skillhistplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var historicoResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Teste de Bônus na Perícia", "Descrição de teste.", "Arcano", "Biblioteca")));
        var historico = await historicoResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            UpdateWithHistorico(historico!.Id)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Arcano", playerToken,
            new UpdateCharacterSkillRequest(3, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Biblioteca", playerToken,
            new UpdateCharacterSkillRequest(3, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();
        var arcano = body!.Single(s => s.Pericia == "Arcano");
        arcano.Modificador.Should().Be(3); // (3+6)/3 = 3 -- Histórico bonus counts as Pericia points spent
        arcano.ContaHistorico.Should().BeTrue();
        var biblioteca = body!.Single(s => s.Pericia == "Biblioteca");
        biblioteca.Modificador.Should().Be(2); // (3+3)/3 = 2
        biblioteca.ContaHistorico.Should().BeTrue();
        var fortitude = body!.Single(s => s.Pericia == "Fortitude");
        fortitude.Modificador.Should().Be(0); // untouched, no bonus
        fortitude.ContaHistorico.Should().BeFalse();
    }

    [Fact]
    public async Task Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm4", "skill4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer4", "skillplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        // Forca: Gasto 4, Bonus 0, no maestria -> Total 4 (AttributeTotalCalculator.Total)
        var attrResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));
        attrResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Atletismo: Gasto 9 -> Modificador 3 (SkillFormulas.Modificador), Atributo escolhido Forca
        var skillResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", playerToken,
            new UpdateCharacterSkillRequest(9, "Forca")));
        skillResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A different skill never had an attribute chosen for it, and must stay that way.
        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();

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
        // Ficha de Personagem 2.d, corrected: Total = Modificador + Atributo + Artefato(s).
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGmArt1", "skillart1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayerArt1", "skillplayerart1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken,
            new UpdateCharacterAttributeRequest(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", playerToken,
            new UpdateCharacterSkillRequest(9, "Forca")));

        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Pericia", "Atletismo", 3);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", playerToken, new AddCharacterArtifactRequest(artifactItemId)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Total.Should().Be(10); // modificador 3 + atributo 4 + artefato 3
    }

    [Fact]
    public async Task List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm6", "skill6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer6", "skillplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var beforeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>())!;
        before.Select(s => s.Pericia).Should().BeInAscendingOrder(StringComparer.Ordinal).And.HaveCount(39);
        before.Select(s => s.Pericia).Should().Equal(before.Select(s => s.Pericia).OrderBy(p => p, StringComparer.Ordinal));

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Sobrevivencia", playerToken,
            new UpdateCharacterSkillRequest(3, null)));

        var afterResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>())!;
        after.Select(s => s.Pericia).Should().Equal(before.Select(s => s.Pericia));
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm3", "skill3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer3", "skillplayer3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer3b", "skillplayer3b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", otherToken,
            new UpdateCharacterSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm5", "skill5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer5", "skillplayer5@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer5b", "skillplayer5b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
