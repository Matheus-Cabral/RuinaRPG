using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;
// Pedidos de Atributo, Perícia, Armadura e Artefato têm o mesmo JSON nos três tipos.
using AdicionarArtefato = RuinaRPG.Contracts.CharacterSheets.AddCharacterArtifactRequest;
using AtualizarAtributo = RuinaRPG.Contracts.CharacterSheets.UpdateCharacterAttributeRequest;
using AtualizarPericia = RuinaRPG.Contracts.CharacterSheets.UpdateCharacterSkillRequest;
using AtualizarSlot = RuinaRPG.Contracts.CharacterSheets.UpdateCharacterArmorSlotRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// A ficha em si, nos três tipos: base de <c>CharacterSheetsControllerTests</c>, <c>NpcSheetsControllerTests</c>
/// e <c>CreatureSheetsControllerTests</c>, que guardam os casos só daquele tipo.
/// </summary>
public abstract class FichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    /// <summary>
    /// Leva a Experiência da ficha ao valor dado pelo caminho que o tipo usava: o PUT completo da ficha no
    /// Personagem; <c>/experiencia-atual</c> no NPC/Criatura.
    /// </summary>
    protected abstract Task DefinirXpAsync(FichaDeTeste f, int xp);

    protected async Task<string> CreateArmaduraItemAsync(string gmToken, int rf, int rm)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Armadura", "Peitoral de Testes", 3m, 30, null, null, null, null, null, null, null, null, null, null, "Medio", 5, rf, rm, null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<string> CreateDanoArtefatoItemAsync(string gmToken, string tipoDeDano, int valor)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Artefato", "Anel de Dano", 0.1m, 500, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, "Dano", tipoDeDano, valor, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<int> PontosDeAtributoAcumuladosAsync(string token, int primeiroNivel, int ultimoNivel)
    {
        var tabela = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/tabela-de-niveis", token))).Content.ReadFromJsonAsync<TabelaDeNiveisResponse>();
        var coluna = tabela!.Colunas.Single(c => c.ChaveDeSistema == "PontosDeAtributo").Id;
        return tabela.Linhas.Where(l => l.Nivel >= primeiroNivel && l.Nivel <= ultimoNivel).Sum(l => l.Valores.GetValueOrDefault(coluna) ?? 0);
    }

    [Fact]
    public async Task Get_a_nonexistent_sheet_returns_404()
    {
        var (gmToken, _) = await MestreAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{Guid.NewGuid()}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_computes_AdrenalinaMaximo_with_an_equipped_SubAtributo_Artefato()
    {
        var f = await NovaFichaAsync();

        var artifactResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", f.GmToken,
            new CreateItemRequest("Artefato", "Bracelete de Vigor", 0.1m, 500, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, "SubAtributo", "Adrenalina", 5, null)));
        var artifactItemId = (await artifactResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(artifactItemId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token));

        var body = await response.Content.ReadFromJsonAsync<FichaLida>();
        body!.AdrenalinaMaximo.Should().Be(15); // 10 + Artefato(5)
    }

    [Fact]
    public async Task LevelUpNotice_lists_bonus_text_for_every_level_gained_since_the_last_dismissal()
    {
        var f = await NovaFichaAsync();
        // 149 XP is one below Nível 3's threshold (150), keeping this at Nível 2 — Nível is
        // derived now, not settable directly.
        await DefinirXpAsync(f, 149);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "level-up-notice"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
        body!.BonusTexts.Should().OnlyContain(t => !t.Contains("<br>"));
        // Auditoria da Tabela de Níveis: numeric bonuses are rendered as "<nome da coluna>: +N"
        // (column names are free text the Auditor can edit, so they can't be singularized), and
        // the same column across the levels gained at once is summed into a single line.
        var esperado = await PontosDeAtributoAcumuladosAsync(f.GmToken, 1, 2);
        body.BonusTexts.Should().ContainSingle(t => t.StartsWith("Pontos de Atributo:")).Which.Should().Be($"Pontos de Atributo: +{esperado}");
    }

    [Fact]
    public async Task Dismiss_stops_the_dismissed_levels_from_reappearing()
    {
        var f = await NovaFichaAsync();
        await DefinirXpAsync(f, 149);

        var dismissResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "dismiss-level-up-notice"), f.Token));
        dismissResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var noticeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "level-up-notice"), f.Token));
        var body = await noticeResponse.Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
        body!.BonusTexts.Should().BeEmpty();
    }

    [Fact]
    public async Task ModificadorDeDano_sums_equipped_Dano_Artefatos_per_TipoDeDano()
    {
        // Ficha de Personagem 3.f / Formulas.md ("Modificador de dano [tipo] = Artefato").
        var f = await NovaFichaAsync();

        var cortanteItemId = await CreateDanoArtefatoItemAsync(f.GmToken, "Cortante", 3);
        var arcanoItemId = await CreateDanoArtefatoItemAsync(f.GmToken, "Arcano", 2);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(cortanteItemId)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(arcanoItemId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "modificador-de-dano"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ModificadorDeDanoResponse>();
        body!.Cortante.Should().Be(3);
        body.Arcano.Should().Be(2);
        body.Perfurante.Should().Be(0);
        body.Contundente.Should().Be(0);
    }

    [Fact]
    public async Task SubAttributes_computes_from_attributes_and_arsenal()
    {
        var f = await NovaFichaAsync();

        // Agilidade Gasto 4, no bônus/maestria/artefato → Total 4. Vigor same → Total 4.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Agilidade"), f.Token,
            new AtualizarAtributo(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Vigor"), f.Token,
            new AtualizarAtributo(4, 0, false)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Movimentacao.Should().Be(8); // (4*2) + 0 artefato - 0 sobrepeso (nothing carried yet)
    }

    [Fact]
    public async Task SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador()
    {
        var f = await NovaFichaAsync();

        // Agilidade Gasto 4, Vigor Gasto 4, no bônus/maestria/artefato → Total 4 each.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Agilidade"), f.Token,
            new AtualizarAtributo(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Vigor"), f.Token,
            new AtualizarAtributo(4, 0, false)));

        // Prontidao Gasto 9 -> Modificador 3, Reflexos Gasto 6 -> Modificador 2, Fortitude Gasto 3 -> Modificador 1.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Prontidao"), f.Token,
            new AtualizarPericia(9, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Reflexos"), f.Token,
            new AtualizarPericia(6, null)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "skills/Fortitude"), f.Token,
            new AtualizarPericia(3, null)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Iniciativa.Should().Be(7); // agilidade 4 + brutoProntidao 3 + 0 artefato
        body.EsquivaNatural.Should().Be(6); // agilidade 4 + brutoReflexos 2 + 0 artefatos - 0 penalidade
        body.DefesaNatural.Should().Be(5); // vigor 4 + brutoFortitude 1 + 0 escudo + 0 artefatos + 0 cobertura
    }

    [Fact]
    public async Task SubAttributes_includes_equipped_armor_RF_and_RM()
    {
        var f = await NovaFichaAsync();
        var armorItemId = await CreateArmaduraItemAsync(f.GmToken, rf: 3, rm: 2);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token,
            new AtualizarSlot(armorItemId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.ReducaoFisica.Should().Be(3);
        body.ReducaoMagica.Should().Be(2);
    }
}
