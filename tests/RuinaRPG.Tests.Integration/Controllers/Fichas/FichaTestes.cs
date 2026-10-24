using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;
// Pedidos de Atributo, Perícia, Armadura e Artefato têm o mesmo JSON nos três tipos.
using AdicionarArma = RuinaRPG.Contracts.Fichas.AddWeaponRequest;
using AdicionarArtefato = RuinaRPG.Contracts.Fichas.AddArtifactRequest;
using AdicionarEscudo = RuinaRPG.Contracts.Fichas.AddShieldRequest;
using AdicionarItem = RuinaRPG.Contracts.Fichas.AddInventoryItemRequest;
using AtualizarAtributo = RuinaRPG.Contracts.Fichas.UpdateAttributeRequest;
using AtualizarPericia = RuinaRPG.Contracts.Fichas.UpdateSkillRequest;
using AtualizarSlot = RuinaRPG.Contracts.Fichas.UpdateArmorSlotRequest;

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

/// <summary>A ficha de Personagem e de NPC: os dois têm Linhagem, Histórico, Estrela e Inventário.</summary>
public abstract class FichaDePersonagemOuNpcTestes<TTipo>(ApiFixture fixture) : FichaTestes<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    /// <summary>Corpo válido do PUT da ficha (o <c>ValidUpdate()</c> de cada classe de teste).</summary>
    protected abstract object AtualizacaoValida();

    /// <summary>
    /// <see cref="AtualizacaoValida"/> com os campos de <paramref name="mudancas"/> trocados pelo nome — o
    /// <c>ValidUpdate() with { ... }</c> dos testes por tipo, sobre o JSON que vai no corpo.
    /// </summary>
    protected object AtualizacaoValidaCom(object mudancas)
    {
        var corpo = JsonSerializer.SerializeToNode(AtualizacaoValida())!.AsObject();
        foreach (var (campo, valor) in JsonSerializer.SerializeToNode(mudancas)!.AsObject())
        {
            // Um nome errado criaria um campo novo que a API ignora em silêncio; falha alto em vez disso.
            if (!corpo.ContainsKey(campo))
                throw new ArgumentException($"O corpo de atualização não tem o campo '{campo}'.", nameof(mudancas));
            corpo[campo] = valor?.DeepClone();
        }
        return corpo;
    }

    protected async Task<string> CreateItemGeralAsync(string gmToken, string nome, decimal peso, decimal? capacidadeExtra = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("ItemGeral", nome, peso, 5, null, "Diversos", "Um item qualquer",
                null, null, null, null, null, null, null,
                null, null, null, null,
                null, null, null, null, capacidadeExtra)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<string> CreateArmaItemAsync(string gmToken, string nome, decimal peso)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Arma", nome, peso, 50, null, "Espadas", null,
                "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
                null, null, null, null,
                null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    protected async Task<string> CreateEscudoItemAsync(string gmToken, string nome, decimal peso)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new CreateItemRequest("Escudo", nome, peso, 25, null, null, null,
                null, null, null, null, null, null, null,
                "Leve", null, null, null,
                2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    [Fact]
    public async Task RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after()
    {
        var f = await NovaFichaAsync();

        var beforeResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "racial-ability"), f.Token));
        (await beforeResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().BeNull();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, AtualizacaoValidaCom(new { Linhagem = "Nephrytes", Variante = "Yavos" })));

        var afterResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "racial-ability"), f.Token));
        (await afterResponse.Content.ReadFromJsonAsync<RacialAbilityResponse>())!.Nome.Should().Be("Racial (Sobre Voo)");
    }

    [Fact]
    public async Task Update_accepts_any_Afinidade_whatever_the_Vocacao()
    {
        var f = await NovaFichaAsync();

        // Vocacao=Campeao não liberava Fogo (Dobra) pelo mapa antigo — a restrição foi removida.
        var update = AtualizacaoValidaCom(new { Vocacao = "Campeao", Afinidade = "Fogo" });
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token));
        var body = await getResponse.Content.ReadFromJsonAsync<FichaLida>();
        body!.Afinidade.Should().Be("Fogo");
    }

    [Fact]
    public async Task Update_never_changes_EquipmentKitId_even_if_the_request_tries_to()
    {
        var f = await NovaFichaAsync();

        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{f.SheetId}", f.GmToken));
        var before = await getResponse.Content.ReadFromJsonAsync<FichaLida>();
        before!.EquipmentKitId.Should().BeNull();

        // AtualizacaoValida() (this file's existing helper) doesn't carry a real EquipmentKitId — the
        // request contract simply has no field for it, since Update never accepts one; this test
        // only confirms the response surfaces the sheet's real (still-null) value after an
        // unrelated update.
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, AtualizacaoValida()));

        var afterResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{f.SheetId}", f.GmToken));
        var after = await afterResponse.Content.ReadFromJsonAsync<FichaLida>();
        after!.EquipmentKitId.Should().BeNull();
    }

    [Fact]
    public async Task Update_with_a_garbage_Estrela_returns_400_instead_of_silently_saving_null()
    {
        var f = await NovaFichaAsync();

        var invalid = AtualizacaoValidaCom(new { Estrela = "Xyz" });
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_garbage_HistoricoId_returns_400()
    {
        var f = await NovaFichaAsync();

        var invalid = AtualizacaoValidaCom(new { HistoricoId = "not-a-guid" });
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_HistoricoId_that_does_not_exist_returns_400()
    {
        var f = await NovaFichaAsync();

        var invalid = AtualizacaoValidaCom(new { HistoricoId = Guid.NewGuid().ToString() });
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_a_Variante_that_does_not_belong_to_the_Linhagem_returns_400()
    {
        var f = await NovaFichaAsync();

        var invalid = AtualizacaoValidaCom(new { Linhagem = "Humano", Variante = "Yavos" }); // Yavos belongs to Nephrytes
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, invalid));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LevelUpNotice_after_jumping_from_Nivel_1_to_3_sums_Pontos_de_Atributo_into_one_line()
    {
        var f = await NovaFichaAsync();
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "dismiss-level-up-notice"), f.Token));

        // 150 XP é o limiar do Nível 3: pula direto do Nível 1 para o 3.
        await DefinirXpAsync(f, 150);
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "level-up-notice"), f.Token));

        var body = await response.Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
        var esperado = await PontosDeAtributoAcumuladosAsync(f.GmToken, 2, 3);
        body!.BonusTexts.Should().ContainSingle(t => t.StartsWith("Pontos de Atributo:")).Which.Should().Be($"Pontos de Atributo: +{esperado}");
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_excludes_equipped_weapons_and_shields_but_includes_unequipped_ones()
    {
        var f = await NovaFichaAsync();

        var equippedWeaponItemId = await CreateArmaItemAsync(f.GmToken, "Espada Equipada", peso: 1.5m);
        var addEquippedResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(equippedWeaponItemId)));
        var equippedWeaponId = (await addEquippedResponse.Content.ReadFromJsonAsync<ArmaLida>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"weapons/{equippedWeaponId}"), f.Token, true));

        var reserveWeaponItemId = await CreateArmaItemAsync(f.GmToken, "Espada Reserva", peso: 1.5m);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(reserveWeaponItemId)));

        var shieldItemId = await CreateEscudoItemAsync(f.GmToken, "Escudo Guardado", peso: 2m);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(shieldItemId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(3.5m); // 1.5 (unequipped reserve weapon) + 2 (unequipped shield) — the equipped weapon is excluded
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_never_counts_armor()
    {
        // An armor slot with an ItemId is inherently worn — no unequipped state exists for
        // armor, so it never contributes to PesoAtual.
        var f = await NovaFichaAsync();
        var armorItemId = await CreateArmaduraItemAsync(f.GmToken, rf: 0, rm: 0);
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "armor-slots/Capacete"), f.Token,
            new AtualizarSlot(armorItemId)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(0m);
    }

    [Fact]
    public async Task Update_persists_a_valid_HistoricoId()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var historico = await db.Historicos.FirstAsync();

        var f = await NovaFichaAsync();

        var update = AtualizacaoValidaCom(new { HistoricoId = historico.Id.ToString() });
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{f.SheetId}", f.Token));
        var body = await getResponse.Content.ReadFromJsonAsync<FichaLida>();
        body!.HistoricoId.Should().Be(historico.Id.ToString());
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_includes_Inventario_items()
    {
        // The bug this task fixes: PesoAtual used to only sum Armas/Armaduras/Escudos, never the
        // Inventário list, contradicting Requisitos - Ficha de Personagem 2.b's own formula text.
        var f = await NovaFichaAsync();
        var itemId = await CreateItemGeralAsync(f.GmToken, "Corda", peso: 2m);

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token,
            new AdicionarItem(itemId, 3)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(6m); // 2 * 3
    }

    [Fact]
    public async Task SubAttributes_Capacidade_Extra_raises_PesoMaximo_and_its_own_Peso_is_excluded_from_PesoAtual()
    {
        var f = await NovaFichaAsync();

        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), f.Token, new AtualizarAtributo(4, 0, false)));
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Vigor"), f.Token, new AtualizarAtributo(4, 0, false)));

        var mochilaItemId = await CreateItemGeralAsync(f.GmToken, "Mochila", peso: 1m, capacidadeExtra: 10m);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "inventory"), f.Token,
            new AdicionarItem(mochilaItemId, 2)));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "sub-attributes"), f.Token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(0m); // the 2 mochilas' own Peso (1*2=2) is excluded
        body.PesoMaximo.Should().Be(24m); // floor((4+4)/2)=4, + (10*2)=20
    }
}
