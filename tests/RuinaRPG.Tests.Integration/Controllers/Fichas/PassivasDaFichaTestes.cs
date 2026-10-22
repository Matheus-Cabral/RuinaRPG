using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Contracts.SpellsAndAbilities;
// Pedidos e respostas de Magia/Habilidade e de Atributo têm o mesmo JSON nos três tipos.
using AdicionarMagia = RuinaRPG.Contracts.Fichas.AddSpellAbilityRequest;
using AtualizarAtributo = RuinaRPG.Contracts.Fichas.UpdateAttributeRequest;
using MagiaLida = RuinaRPG.Contracts.Fichas.SpellAbilityResponse;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Habilidades Passivas na ficha: só do Banco, bloqueio por requisitos e aviso, nos três tipos.</summary>
public abstract class PassivasDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    protected async Task PublishBankEntryAsync(string gmToken, string campaignId, string entryId)
    {
        var attach = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    protected async Task<string> CreatePassivaAsync(string gmToken, string nome, RequisitosDePassivaDto? requisitos)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Passiva", 0, "Descrição.", [], false, "Livre", requisitos)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    protected Task<HttpResponseMessage> AddFromBankAsync(string token, FichaDeTeste f, string entryId) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), token,
            new AdicionarMagia(entryId, null, null, null, null, null)));

    [Fact]
    public async Task Building_a_passiva_from_scratch_on_a_sheet_is_refused()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken,
            new AdicionarMagia(null, "P", "Passiva", 0, "d", [])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Passivas só são cadastradas no Banco de Magias e Habilidades.");
    }

    [Fact]
    public async Task A_passiva_without_requisitos_is_always_addable()
    {
        var f = await NovaFichaAsync();
        var passivaId = await CreatePassivaAsync(f.GmToken, "Livre", null);

        (await AddFromBankAsync(f.GmToken, f, passivaId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // Cobre também os antigos Npc_/Creature_adding_the_same_passiva_twice_is_refused.
    [Fact]
    public async Task Adding_the_same_passiva_twice_is_refused()
    {
        // Finding 3 of the final whole-branch review: unlike a Magia/Habilidade comum, a Passiva
        // is a scalar on/off per bank entry — the sheet must not end up with two independent copies
        // of the same one just because the player (or GM) clicked "Adicionar" twice.
        var f = await NovaFichaAsync();
        var passivaId = await CreatePassivaAsync(f.GmToken, "Pele de Pedra", null);

        (await AddFromBankAsync(f.GmToken, f, passivaId)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await AddFromBankAsync(f.GmToken, f, passivaId);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).Should().Contain("Esta Passiva já está na ficha.");
    }

    // Cobre também o antigo Npc_passivas_disponiveis_for_the_granted_player_lists_only_the_campaign_public_ones.
    [Fact]
    public async Task Passivas_disponiveis_lists_reachable_passivas_with_their_pendencias()
    {
        var f = await FichaDeJogadorAsync();
        var publica = await CreatePassivaAsync(f.GmToken, "Pública", new RequisitosDePassivaDto(Nivel: 20));
        await CreatePassivaAsync(f.GmToken, "Privada", null);
        await PublishBankEntryAsync(f.GmToken, f.CampaignId!, publica);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", f.GmToken,
            new CreateSpellAbilityEntryRequest("Uma Magia", "Magia", 1, "d", [])));

        var url = Url(f, "spell-abilities/passivas-disponiveis");
        var paraGm = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, url, f.GmToken))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraGm!.Select(p => p.Entrada.Nome).Should().BeEquivalentTo(["Pública", "Privada"]);
        paraGm!.Single(p => p.Entrada.Nome == "Pública").Pendencias.Should().Equal("Nível 20");

        var paraJogador = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, url, f.Token))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraJogador!.Select(p => p.Entrada.Nome).Should().Equal("Pública");
    }

    [Fact]
    public async Task A_passiva_does_not_touch_the_Pontos_de_Ignicao_balance_even_at_zero()
    {
        var f = await NovaFichaAsync();
        var passivaId = await CreatePassivaAsync(f.GmToken, "Sem PI", null);
        await SetPiAsync(f, 0);

        var added = await AddFromBankAsync(f.GmToken, f, passivaId);
        added.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetPiAsync(f)).Should().Be(0);

        var criada = (await added.Content.ReadFromJsonAsync<MagiaLida>())!;
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"spell-abilities/{criada.Id}"), f.GmToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPiAsync(f)).Should().Be(0);
    }
}

/// <summary>Personagem e NPC: os requisitos de Personagem (Atributo, Vocação, Histórico) valem.</summary>
public abstract class PassivasDePersonagemOuNpcTestes<TTipo>(ApiFixture fixture) : PassivasDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    private Task SetForcaAsync(string token, FichaDeTeste f, int gasto) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, "attributes/Forca"), token, new AtualizarAtributo(gasto, 0, false)));

    /// <summary>Era o NPC <c>Npc_add_from_bank_checks_requisitos_and_from_scratch_passiva_is_refused</c> (a parte "do zero" é o caso genérico).</summary>
    [Fact]
    public async Task Add_from_bank_checks_a_vocacao_requisito()
    {
        var f = await NovaFichaAsync();
        var passivaId = await CreatePassivaAsync(f.GmToken, "Nobreza", new RequisitosDePassivaDto(Vocacao: "Bruxo"));

        var add = await AddFromBankAsync(f.GmToken, f, passivaId);
        add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await add.Content.ReadAsStringAsync()).Should().Contain("Requisitos não cumpridos: Vocação: Bruxo");
    }

    [Fact]
    public async Task Unmet_requisitos_block_the_gm_and_the_player_alike()
    {
        var f = await FichaDeJogadorAsync();
        var passivaId = await CreatePassivaAsync(f.GmToken, "Força Bruta", new RequisitosDePassivaDto(Atributos: [new("Forca", 4)]));
        await PublishBankEntryAsync(f.GmToken, f.CampaignId!, passivaId);

        foreach (var token in new[] { f.GmToken, f.Token })
        {
            var response = await AddFromBankAsync(token, f, passivaId);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Requisitos não cumpridos: Força ≥ 4");
        }
    }

    [Fact]
    public async Task Met_requisitos_copy_categoria_and_requisitos_and_a_later_change_shows_the_warning()
    {
        var f = await FichaDeJogadorAsync();
        var passivaId = await CreatePassivaAsync(f.GmToken, "Força Bruta", new RequisitosDePassivaDto(Atributos: [new("Forca", 4)]));
        await PublishBankEntryAsync(f.GmToken, f.CampaignId!, passivaId);
        await SetForcaAsync(f.Token, f, 5);

        var response = await AddFromBankAsync(f.Token, f, passivaId);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<MagiaLida>())!;
        created.Tipo.Should().Be("Passiva");
        created.Categoria.Should().Be("Livre");
        created.RequisitosPendentes.Should().BeEmpty();

        await SetForcaAsync(f.Token, f, 2);
        var list = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spell-abilities"), f.Token)))
            .Content.ReadFromJsonAsync<List<MagiaLida>>();
        list!.Single(e => e.Id == created.Id).RequisitosPendentes.Should().Equal("Força ≥ 4");
    }

    [Fact]
    public async Task A_player_never_sees_the_requisitos_of_a_passiva_that_is_not_public()
    {
        var f = await FichaDeJogadorAsync();
        var passivaId = await CreatePassivaAsync(f.GmToken, "Secreta", new RequisitosDePassivaDto(Nivel: 20));

        var response = await AddFromBankAsync(f.Token, f, passivaId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Entrada do banco não encontrada.");
        text.Should().NotContain("Nível");
    }

    [Fact]
    public async Task A_historico_requisito_is_checked_and_named()
    {
        var f = await NovaFichaAsync();
        var historicos = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", f.GmToken))).Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        var historico = historicos!.First();
        var passivaId = await CreatePassivaAsync(f.GmToken, "De Berço", new RequisitosDePassivaDto(HistoricoId: historico.Id));

        var response = await AddFromBankAsync(f.GmToken, f, passivaId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain($"Histórico: {historico.Nome}");
    }
}

public sealed class PassivasDaFichaTestesPersonagem(ApiFixture fixture) : PassivasDePersonagemOuNpcTestes<TipoDeTestePersonagem>(fixture);

/// <summary>NPC: a concessão copia as Passivas.</summary>
public sealed class PassivasDaFichaTestesNpc(ApiFixture fixture) : PassivasDePersonagemOuNpcTestes<TipoDeTesteNpc>(fixture)
{
    // Mesma chamada de FichaConcedidaAsync, com a ficha de origem informada.
    private async Task<string> GrantNpcAsync(string gmToken, string campaignId, string playerId, string? sourceSheetId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new GrantSheetRequest(playerId, "Npc", sourceSheetId)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
    }

    [Fact]
    public async Task Granting_an_npc_copies_its_passivas_categoria_and_requisitos()
    {
        var source = await NovaFichaAsync();
        var gm = source.GmToken;
        var (playerId, playerToken) = await JogadorAsync(gm);
        var campaignId = await CampanhaComMembroAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Vigia", new RequisitosDePassivaDto(Nivel: 1));
        (await AddFromBankAsync(gm, source, passivaId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var grantedId = await GrantNpcAsync(gm, campaignId, playerId, source.SheetId);

        var list = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{grantedId}/spell-abilities", playerToken)))
            .Content.ReadFromJsonAsync<List<SpellAbilityResponse>>();
        var copy = list!.Single(e => e.Nome == "Vigia");
        copy.Categoria.Should().Be("Livre");
        copy.Requisitos!.Nivel.Should().Be(1);
    }

    [Fact]
    public async Task Granting_an_npc_whose_passiva_left_the_bank_recreates_it_with_categoria_and_requisitos()
    {
        var source = await NovaFichaAsync();
        var gm = source.GmToken;
        var (playerId, _) = await JogadorAsync(gm);
        var campaignId = await CampanhaComMembroAsync(gm, playerId);
        var create = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gm,
            new CreateSpellAbilityEntryRequest("Sentinela", "Passiva", 0, "Descrição.", [], false, "Vocacional", new RequisitosDePassivaDto(Nivel: 1))));
        var passivaId = (await create.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
        (await AddFromBankAsync(gm, source, passivaId)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/spell-ability-bank/{passivaId}", gm))).IsSuccessStatusCode.Should().BeTrue();

        await GrantNpcAsync(gm, campaignId, playerId, source.SheetId);

        var bank = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?tipo=Passiva", gm)))
            .Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        var recriada = bank!.Single(e => e.Nome == "Sentinela");
        recriada.Id.Should().NotBe(passivaId);
        recriada.Categoria.Should().Be("Vocacional");
        recriada.Requisitos!.Nivel.Should().Be(1);
    }
}

/// <summary>Criatura: só o requisito de Nível vale; os de Personagem são ignorados.</summary>
public sealed class PassivasDaFichaTestesCriatura(ApiFixture fixture) : PassivasDaFichaTestes<TipoDeTesteCriatura>(fixture)
{
    [Fact]
    public async Task Creature_ignores_personagem_only_requisitos_but_checks_nivel()
    {
        var f = await NovaFichaAsync();
        var ignorada = await CreatePassivaAsync(f.GmToken, "De Bruxo", new RequisitosDePassivaDto(Vocacao: "Bruxo", Estrela: "Sadir", Atributos: [new("Instinto", 99)]));
        var alta = await CreatePassivaAsync(f.GmToken, "Alta", new RequisitosDePassivaDto(Nivel: 30));

        var aceita = await AddFromBankAsync(f.GmToken, f, ignorada);
        aceita.StatusCode.Should().Be(HttpStatusCode.Created);
        (await aceita.Content.ReadFromJsonAsync<MagiaLida>())!.RequisitosPendentes.Should().BeEmpty();

        var bloqueada = await AddFromBankAsync(f.GmToken, f, alta);
        bloqueada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bloqueada.Content.ReadAsStringAsync()).Should().Contain("Nível 30");

        var scratch = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken,
            new AdicionarMagia(null, "P", "Passiva", 0, "d", [])));
        scratch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var disponiveis = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spell-abilities/passivas-disponiveis"), f.GmToken)))
            .Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        disponiveis!.Single(p => p.Entrada.Nome == "De Bruxo").Pendencias.Should().BeEmpty();
        disponiveis!.Single(p => p.Entrada.Nome == "Alta").Pendencias.Should().Equal("Nível 30");
    }
}
