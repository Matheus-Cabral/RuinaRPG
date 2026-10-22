using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Fichas;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterRacialTraitsControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private static UpdateCharacterSheetRequest ValidUpdate(string linhagem, string variante) => new(
        null, "Teste", linhagem, variante, "Campeao", "Duelista", null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null, null, 0, null, null, 0);

    private async Task<string> SetVarianteAsync(string gmToken, string playerToken, string sheetId, string linhagem, string variante)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate(linhagem, variante)));
        response.EnsureSuccessStatusCode();
        return sheetId;
    }

    [Fact]
    public async Task PendingRacialTraitChoice_is_false_when_Variante_is_still_null()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm1", "charracial1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer1", "charracialplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));

        var body = await response.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>();
        body!.Pending.Should().BeFalse();
    }

    [Fact]
    public async Task PendingRacialTraitChoice_is_true_with_the_right_options_once_a_Variante_with_no_Obrigatoria_is_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm2", "charracial2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer2", "charracialplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));

        var body = await response.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>();
        body!.Pending.Should().BeTrue();
        body.Gratuita.Select(o => o.TraitNome).Should().BeEquivalentTo("Alfabetizado", "Sedutor", "Aparência Inofensiva (2 pontos)");
        body.Obrigatoria.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_for_a_Variante_with_no_Obrigatoria_grants_only_the_Gratuita_pick_at_zero_cost()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm3", "charracial3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer3", "charracialplayer3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");

        var resolveResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Alfabetizado", null)));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pendingResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));
        (await pendingResponse.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>())!.Pending.Should().BeFalse();

        var traitsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<TraitsListResponse>();
        var granted = traits!.Positivas.Should().ContainSingle(t => t.Nome == "Alfabetizado").Subject;
        granted.Custo.Should().Be(0); // real catalog cost is 1 — racial grants are free
        granted.IsRacial.Should().BeTrue();
        traits.TotalPositivas.Should().Be(0); // must not count toward the budget total either
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_for_a_Variante_with_a_real_Obrigatoria_choice_grants_both_picks()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm4", "charracial4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer4", "charracialplayer4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Phylauc", "PhylacTai");

        var resolveResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Imunidade de Venenos (2 pontos)", "Crédulo")));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var traitsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<TraitsListResponse>();
        traits!.Positivas.Should().ContainSingle(t => t.Nome == "Imunidade de Venenos (2 pontos)" && t.Custo == 0 && t.IsRacial);
        traits.Negativas.Should().ContainSingle(t => t.Nome == "Crédulo" && t.Custo == 0 && t.IsRacial);
        traits.TotalPositivas.Should().Be(0);
        traits.TotalNegativas.Should().Be(0);
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_for_Alora_auto_grants_the_fixed_Obrigatoria_with_its_Especificacao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm5", "charracial5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer5", "charracialplayer5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Econos", "Alora");

        var resolveResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Detectar Magia", null)));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var traitsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<TraitsListResponse>();
        traits!.Positivas.Should().ContainSingle(t => t.Nome == "Detectar Magia" && t.IsRacial);
        traits.Negativas.Should().ContainSingle(t => t.Nome == "Desvantagem Elemental" && t.Especificacao == "Fogo" && t.IsRacial);
    }

    [Fact]
    public async Task PendingRacialTraitChoice_flags_RequerEspecificacao_only_for_the_options_that_need_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm8", "charracial8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer8", "charracialplayer8@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Phylauc", "PhylacTai");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));

        var body = await response.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>();
        body!.Gratuita.Should().ContainSingle(o => o.TraitNome == "Sentidos Aguçados" && o.RequerEspecificacao);
        body.Gratuita.Where(o => o.TraitNome != "Sentidos Aguçados").Should().OnlyContain(o => !o.RequerEspecificacao);
        body.Obrigatoria.Should().ContainSingle(o => o.TraitNome == "Código de Honra" && o.RequerEspecificacao);
        body.Obrigatoria.Should().ContainSingle(o => o.TraitNome == "Crédulo" && !o.RequerEspecificacao);
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_rejects_a_pick_that_requires_Especificacao_without_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm9", "charracial9@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer9", "charracialplayer9@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Phylauc", "PhylacTai");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Sentidos Aguçados", "Crédulo")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_persists_user_supplied_Especificacao_for_both_slots()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm10", "charracial10@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer10", "charracialplayer10@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Phylauc", "PhylacTai");

        var resolveResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Sentidos Aguçados", "Código de Honra", "Audição", "Nunca mente para um Lorde")));
        resolveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var traitsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<TraitsListResponse>();
        traits!.Positivas.Should().ContainSingle(t => t.Nome == "Sentidos Aguçados" && t.Especificacao == "Audição" && t.IsRacial);
        traits.Negativas.Should().ContainSingle(t => t.Nome == "Código de Honra" && t.Especificacao == "Nunca mente para um Lorde" && t.IsRacial);
    }

    [Fact]
    public async Task ResolveRacialTraitChoice_with_a_Gratuita_pick_outside_the_allowed_options_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm6", "charracial6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer6", "charracialplayer6@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Coragem", null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Changing_Variante_removes_the_stale_racial_grants_and_reopens_the_pending_choice()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CharRacialGm7", "charracial7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharRacialPlayer7", "charracialplayer7@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Sinir");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/racial-traits/resolve", playerToken,
            new ResolveRacialTraitChoiceRequest("Alfabetizado", null)));

        await SetVarianteAsync(gmToken, playerToken, sheetId, "Humano", "Laonir");

        var traitsResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/traits", playerToken));
        var traits = await traitsResponse.Content.ReadFromJsonAsync<TraitsListResponse>();
        traits!.Positivas.Should().NotContain(t => t.Nome == "Alfabetizado");

        var pendingResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-traits/pending", playerToken));
        (await pendingResponse.Content.ReadFromJsonAsync<PendingRacialTraitChoiceResponse>())!.Pending.Should().BeTrue();
    }
}
