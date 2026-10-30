using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
// Pedidos de Atributo e Artefato têm o mesmo JSON nos três tipos.
using AdicionarArtefato = RuinaRPG.Contracts.Fichas.AddArtifactRequest;
using AtualizarAtributo = RuinaRPG.Contracts.Fichas.UpdateAttributeRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// A ficha em si (<c>/api/npc-sheets</c>, <c>/api/creature-sheets</c>) nos dois tipos que pertencem ao Mestre
/// e podem ser concedidos a um Jogador. Os casos só de um tipo ficam em <c>NpcSheetsControllerTests</c> e
/// <c>CreatureSheetsControllerTests</c>.
/// </summary>
public abstract class FichaDeNpcOuCriaturaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    /// <summary>O Tipo da concessão (<c>GrantSheetRequest.Tipo</c>).</summary>
    protected abstract string TipoDaConcessao { get; }

    /// <summary>Corpo válido do PUT da ficha, com o Nome dado.</summary>
    protected abstract object AtualizacaoComNome(string nome);

    private Task<HttpResponseMessage> GetAsync(string url, string token) => Client.SendAsync(AuthedRequest(HttpMethod.Get, url, token));

    private static string UrlDaFicha(string sheetId) => $"/api/{Tipo.Rota}/{sheetId}";

    [Fact]
    public async Task Create_returns_201_with_Nivel_1()
    {
        var (gmToken, _) = await MestreAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/{Tipo.Rota}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<FichaLida>();
        body!.Nivel.Should().Be(1);
        body.Nome.Should().BeNull();
        body.OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task CampaignId_is_populated_for_a_granted_sheet_and_null_for_an_ungranted_one()
    {
        var granted = await FichaDeJogadorAsync();
        var ungranted = await NovaFichaAsync();

        var grantedResponse = await GetAsync(UrlDaFicha(granted.SheetId), granted.Token);
        (await grantedResponse.Content.ReadFromJsonAsync<FichaLida>())!.CampaignId.Should().Be(granted.CampaignId);

        var ungrantedResponse = await GetAsync(UrlDaFicha(ungranted.SheetId), ungranted.GmToken);
        (await ungrantedResponse.Content.ReadFromJsonAsync<FichaLida>())!.CampaignId.Should().BeNull();
    }

    [Fact]
    public async Task Resposta_da_ficha_concedida_traz_a_campanha()
    {
        var concedida = await FichaDeJogadorAsync();
        var soDoGm = await NovaFichaAsync();

        var daConcessao = await (await GetAsync(UrlDaFicha(concedida.SheetId), concedida.Token)).Content.ReadFromJsonAsync<FichaLida>();
        var doGm = await (await GetAsync(UrlDaFicha(soDoGm.SheetId), soDoGm.GmToken)).Content.ReadFromJsonAsync<FichaLida>();

        daConcessao!.CampaignId.Should().Be(concedida.CampaignId);
        daConcessao.CampaignName.Should().Be("Campanha");
        doGm!.CampaignName.Should().BeNull();
    }

    [Fact]
    public async Task Get_by_the_owning_gm_returns_200()
    {
        var f = await NovaFichaAsync();

        var response = await GetAsync(UrlDaFicha(f.SheetId), f.GmToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_and_Update_by_the_player_the_sheet_was_granted_to_both_succeed()
    {
        // Requisitos - Campanha R0010: a granted NPC/Criatura "usa o mesmo modelo de edição" as
        // the player's own Ficha de Personagem — the owning player, not just the GM, can edit it.
        var f = await FichaDeJogadorAsync();

        var getResponse = await GetAsync(UrlDaFicha(f.SheetId), f.Token);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Put, UrlDaFicha(f.SheetId), f.Token, AtualizacaoComNome("Companheiro do Jogador")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getAfter = await GetAsync(UrlDaFicha(f.SheetId), f.Token);
        (await getAfter.Content.ReadFromJsonAsync<FichaLida>())!.Nome.Should().Be("Companheiro do Jogador");
    }

    [Fact]
    public async Task Get_by_a_jogador_the_sheet_was_never_granted_to_returns_404()
    {
        var f = await NovaFichaAsync();
        var (_, strangerToken) = await JogadorAsync(f.GmToken);

        var response = await GetAsync(UrlDaFicha(f.SheetId), strangerToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_shows_OwnerNickname_for_a_granted_sheet_and_null_for_the_GMs_own()
    {
        var own = await NovaFichaAsync();
        var (playerId, _) = await RegisterJogadorLinkedToAsync(own.GmToken, "GmListOwnerPlayer", "gmlistownerplayer@teste.com");
        var campaignId = await CampanhaComMembroAsync(own.GmToken, playerId);
        var grant = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", own.GmToken, new GrantSheetRequest(playerId, TipoDaConcessao, null));
        var grantedSheetId = (await grant.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;

        var response = await GetAsync($"/api/{Tipo.Rota}", own.GmToken);

        var body = await response.Content.ReadFromJsonAsync<List<ResumoDeFichaLido>>();
        body!.Single(s => s.Id == own.SheetId).OwnerNickname.Should().BeNull();
        body!.Single(s => s.Id == grantedSheetId).OwnerNickname.Should().Be("GmListOwnerPlayer");
    }

    [Fact]
    public async Task Delete_by_the_owning_gm_returns_204()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, UrlDaFicha(f.SheetId), f.GmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Só o GM apaga (spec 2.1.0 B.1): o Jogador a quem a ficha foi concedida edita, mas não apaga.</summary>
    [Fact]
    public async Task Dono_jogador_nao_apaga_ficha_concedida()
    {
        var f = await FichaDeJogadorAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, UrlDaFicha(f.SheetId), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetAsync(UrlDaFicha(f.SheetId), f.GmToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GM_apaga_ficha_concedida()
    {
        var f = await FichaDeJogadorAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, UrlDaFicha(f.SheetId), f.GmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Outro Mestre recebe 404 em todos os endpoints da ficha (não confirma que ela existe).</summary>
    [Fact]
    public async Task Get_by_a_different_gm_returns_404()
    {
        var f = await NovaFichaAsync();
        var gmTokenOther = await OutroMestreAsync();

        var response = await GetAsync(UrlDaFicha(f.SheetId), gmTokenOther);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_a_different_gm_returns_404()
    {
        var f = await NovaFichaAsync();
        var gmTokenOther = await OutroMestreAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, UrlDaFicha(f.SheetId), gmTokenOther, AtualizacaoComNome("Sentinela da Ruína")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_by_a_different_gm_returns_404()
    {
        var f = await NovaFichaAsync();
        var gmTokenOther = await OutroMestreAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, UrlDaFicha(f.SheetId), gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dismiss_by_a_different_gm_returns_404()
    {
        var f = await NovaFichaAsync();
        var gmTokenOther = await OutroMestreAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "dismiss-level-up-notice"), gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubAttributes_by_a_different_gm_returns_404()
    {
        var f = await NovaFichaAsync();
        var gmTokenOther = await OutroMestreAsync();

        var response = await GetAsync(Url(f, "sub-attributes"), gmTokenOther);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateNivel_by_a_different_gm_returns_404()
    {
        var f = await NovaFichaAsync();
        var gmTokenOther = await OutroMestreAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "nivel"), gmTokenOther, 5));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubAttributes_sums_an_equipped_SubAtributo_Artefato_into_the_matching_term()
    {
        var f = await NovaFichaAsync();
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Agilidade"), f.GmToken,
            new AtualizarAtributo(4, 0, false)));

        var artifactResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", f.GmToken,
            new CreateItemRequest("Artefato", "Amuleto de Reflexos", 0.1m, 500, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, "SubAtributo", "Esquiva Natural", 3, null)));
        var artifactItemId = (await artifactResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.GmToken, new AdicionarArtefato(artifactItemId)));

        var response = await GetAsync(Url(f, "sub-attributes"), f.GmToken);

        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.EsquivaNatural.Should().Be(7); // agilidade 4 + brutoReflexos 0 + artefato 3 - 0 penalidade
    }

    [Fact]
    public async Task UpdateExperienciaAtual_pulls_Nivel_via_the_same_rule_as_Ficha_de_Personagem()
    {
        // 750 is Nível 5's own threshold — reaching it exactly already counts as Nível 6 (same
        // "reaching it exactly already counts" rule NivelCalculator applies for Ficha de
        // Personagem).
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "experiencia-atual"), f.GmToken, 750));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await (await GetAsync(UrlDaFicha(f.SheetId), f.GmToken)).Content.ReadFromJsonAsync<FichaLida>();
        body!.ExperienciaAtual.Should().Be(750);
        body.Nivel.Should().Be(6);
    }

    [Fact]
    public async Task UpdateNivel_pulls_ExperienciaAtual_to_that_levels_minimum_XP()
    {
        // Real thresholds from "Tabelas de XP, Atributos, Características e EAP": Nível 4's is
        // 500 — the XP that first reaches Nível 5.
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "nivel"), f.GmToken, 5));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await (await GetAsync(UrlDaFicha(f.SheetId), f.GmToken)).Content.ReadFromJsonAsync<FichaLida>();
        body!.Nivel.Should().Be(5);
        body.ExperienciaAtual.Should().Be(500);
    }
}

public sealed class FichaDeNpcOuCriaturaTestesNpc(ApiFixture fixture) : FichaDeNpcOuCriaturaTestes<TipoDeTesteNpc>(fixture)
{
    protected override string TipoDaConcessao => "Npc";

    protected override object AtualizacaoComNome(string nome) => NpcSheetsControllerTests.ValidUpdate() with { Nome = nome };
}

public sealed class FichaDeNpcOuCriaturaTestesCriatura(ApiFixture fixture) : FichaDeNpcOuCriaturaTestes<TipoDeTesteCriatura>(fixture)
{
    protected override string TipoDaConcessao => "Creature";

    protected override object AtualizacaoComNome(string nome) => CreatureSheetsControllerTests.ValidUpdate() with { Nome = nome };
}
