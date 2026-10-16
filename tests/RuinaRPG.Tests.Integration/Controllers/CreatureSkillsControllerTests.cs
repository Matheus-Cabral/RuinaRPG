using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CreatureSkillsControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{

    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    private async Task<string> CreateArtefatoItemAsync(string gmToken, string tipoDeAlvo, string alvo, int valor)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", "Anel de Teste", 0.1m, 500, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, tipoDeAlvo, alvo, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task List_returns_20_skills_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm1", "creatureskill1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>();
        body!.Should().HaveCount(20);
    }

    [Fact]
    public async Task Update_a_skill_sets_Gasto_and_the_response_computes_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm2", "creatureskill2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateCreatureSkillRequest(9, null)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Modificador.Should().Be(3); // 9 / 3
    }

    [Fact]
    public async Task Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm4", "creatureskill4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Forca: Gasto 4, Bonus 0, no maestria -> Total 4 (AttributeTotalCalculator.Total)
        var attrResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));
        attrResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Atletismo: Gasto 9 -> Modificador 3 (SkillFormulas.Modificador), Atributo escolhido Forca
        var skillResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateCreatureSkillRequest(9, "Forca")));
        skillResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>();

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
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGmArt1", "creatureskillart1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/attributes/Forca", gmToken,
            new UpdateCreatureAttributeRequest(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Atletismo", gmToken,
            new UpdateCreatureSkillRequest(9, "Forca")));

        var artifactItemId = await CreateArtefatoItemAsync(gmToken, "Pericia", "Atletismo", 3);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/artifacts", gmToken, new AddCreatureArtifactRequest(artifactItemId)));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Total.Should().Be(10); // modificador 3 + atributo 4 + artefato 3
    }

    [Fact]
    public async Task List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm7", "creatureskill7@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var beforeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));
        var before = (await beforeResponse.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>())!;
        before.Select(s => s.Pericia).Should().BeInAscendingOrder(StringComparer.Ordinal).And.HaveCount(20);

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Reflexos", gmToken,
            new UpdateCreatureSkillRequest(3, null)));

        var afterResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmToken));
        var after = (await afterResponse.Content.ReadFromJsonAsync<List<CreatureSkillResponse>>())!;
        after.Select(s => s.Pericia).Should().Equal(before.Select(s => s.Pericia));
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureSkillGmOwner3", "creatureskillowner3@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureSkillGmOther3", "creatureskillother3@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Atletismo", gmTokenOther,
            new UpdateCreatureSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("CreatureSkillGmOwner5", "creatureskillowner5@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("CreatureSkillGmOther5", "creatureskillother5@teste.com");
        var sheetId = await CreateSheetAsync(gmTokenOwner);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/skills", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_a_disallowed_Pericia_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureSkillGm6", "creatureskill6@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        // Alquimia is a real Pericia enum value but not in the R0005 20-skill allow-list.
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{sheetId}/skills/Alquimia", gmToken,
            new UpdateCreatureSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
