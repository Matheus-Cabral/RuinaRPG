using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
// Os pedidos de arma (com ItemId do catálogo), escudo, armadura, Artefato, Atributo e Perícia têm o mesmo
// JSON nos três tipos de ficha.
using AdicionarArma = RuinaRPG.Contracts.Fichas.AddWeaponRequest;
using AdicionarArtefato = RuinaRPG.Contracts.Fichas.AddArtifactRequest;
using AdicionarEscudo = RuinaRPG.Contracts.Fichas.AddShieldRequest;
using AtualizarAtributo = RuinaRPG.Contracts.Fichas.UpdateAttributeRequest;
using AtualizarPericia = RuinaRPG.Contracts.Fichas.UpdateSkillRequest;
using AtualizarSlot = RuinaRPG.Contracts.Fichas.UpdateArmorSlotRequest;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Penalidade automática de equipamento nos cálculos da ficha (Catálogo de Itens, Requisitos/Penalidade), nos três tipos.</summary>
public abstract class PenalidadesDeEquipamentoTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    protected async Task<T> GetAsync<T>(FichaDeTeste f, string path) =>
        (await (await SendOkAsync(HttpMethod.Get, Url(f, path), f.Token)).Content.ReadFromJsonAsync<T>())!;

    // ---- itens do catálogo ----

    protected static CreateItemRequest Arma(string nome, RequisitosDePassivaDto? requisitos, PenalidadeDeEquipamentoDto? penalidade) =>
        new("Arma", nome, 1.5m, 50, null, "Espadas", "Uma lâmina.", "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null, null, null, null, null, null, requisitos, penalidade);

    protected static CreateItemRequest Item(string tipo, string nome, RequisitosDePassivaDto? requisitos, PenalidadeDeEquipamentoDto? penalidade) => tipo switch
    {
        "Armadura" => new("Armadura", nome, 8m, 100, null, null, "Placas.", "D", null, null, null, null, null, null,
            "Pesada", 5, 2, 1, null, null, null, null, null, requisitos, penalidade),
        "Escudo" => new("Escudo", nome, 4m, 60, null, null, "Um escudo.", "F", null, null, null, null, null, null,
            "Leve", null, null, null, 3, null, null, null, null, requisitos, penalidade),
        _ => new("Artefato", nome, 0.2m, 200, null, null, "Um anel.", null, null, null, null, null, null, null,
            null, null, null, null, null, "Atributo", "Forca", 2, null, requisitos, penalidade),
    };

    protected async Task<ItemResponse> CriarItemAsync(FichaDeTeste f, CreateItemRequest request) =>
        (await (await SendOkAsync(HttpMethod.Post, "/api/items", f.GmToken, request)).Content.ReadFromJsonAsync<ItemResponse>())!;

    private async Task AtualizarRequisitosAsync(FichaDeTeste f, ItemResponse i, RequisitosDePassivaDto requisitos)
    {
        var update = new UpdateItemRequest(
            Nome: i.Nome, Peso: i.Peso, Preco: i.Preco, ImageId: null, Subcategoria: i.Subcategoria, Descricao: i.Descricao,
            Rank: i.Rank, Empunhadura: i.Empunhadura, Dados: i.Dados, Dano: i.Dano, Critico: i.Critico, Alcance: i.Alcance,
            TipoDeDano: i.TipoDeDano, Categoria: i.Categoria, Defesa: i.Defesa,
            RF: i.RF, RM: i.RM, BonusDefesa: i.BonusDefesa,
            TipoDeAlvo: i.TipoDeAlvo, Alvo: i.Alvo, Valor: i.Valor, CapacidadeExtra: i.CapacidadeExtra,
            Requisitos: requisitos, PenalidadeDeRequisitos: i.PenalidadeDeRequisitos);
        await SendOkAsync(HttpMethod.Put, $"/api/items/{i.Id}", f.GmToken, update);
    }

    // ---- ações sobre a ficha (por quem a edita) ----

    protected async Task<ArmaLida> AdicionarArmaAsync(FichaDeTeste f, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, Url(f, "weapons"), f.Token, new AdicionarArma(itemId))).Content.ReadFromJsonAsync<ArmaLida>())!;

    protected Task EquiparArmaAsync(FichaDeTeste f, string id, bool equipada) =>
        SendOkAsync(HttpMethod.Put, Url(f, $"weapons/{id}"), f.Token, equipada);

    protected Task<List<ArmaLida>> ListarArmasAsync(FichaDeTeste f) => GetAsync<List<ArmaLida>>(f, "weapons");

    protected async Task<EscudoLido> AdicionarEscudoAsync(FichaDeTeste f, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, Url(f, "shields"), f.Token, new AdicionarEscudo(itemId))).Content.ReadFromJsonAsync<EscudoLido>())!;

    protected Task EquiparEscudoAsync(FichaDeTeste f, string id, bool equipado) =>
        SendOkAsync(HttpMethod.Put, Url(f, $"shields/{id}"), f.Token, equipado);

    protected Task<List<EscudoLido>> ListarEscudosAsync(FichaDeTeste f) => GetAsync<List<EscudoLido>>(f, "shields");

    private Task VestirArmaduraAsync(FichaDeTeste f, string slot, string itemId) =>
        SendOkAsync(HttpMethod.Put, Url(f, $"armor-slots/{slot}"), f.Token, new AtualizarSlot(itemId));

    private Task AdicionarArtefatoAsync(FichaDeTeste f, string itemId) =>
        SendOkAsync(HttpMethod.Post, Url(f, "artifacts"), f.Token, new AdicionarArtefato(itemId));

    protected Task DefinirAtributoAsync(FichaDeTeste f, string atributo, int gasto) =>
        SendOkAsync(HttpMethod.Put, Url(f, $"attributes/{atributo}"), f.Token, new AtualizarAtributo(gasto, 0, false));

    protected async Task<int> TotalDoAtributoAsync(FichaDeTeste f, string atributo) =>
        (await GetAsync<List<AtributoLido>>(f, "attributes")).Single(a => a.Atributo == atributo).Total;

    protected Task<List<PericiaLida>> ListarPericiasAsync(FichaDeTeste f) => GetAsync<List<PericiaLida>>(f, "skills");

    protected Task<SubAttributesResponse> SubAtributosAsync(FichaDeTeste f) => GetAsync<SubAttributesResponse>(f, "sub-attributes");

    protected Task<List<PenalidadeAtivaResponse>> PenalidadesAtivasAsync(FichaDeTeste f) => GetAsync<List<PenalidadeAtivaResponse>>(f, "equipment-penalties");

    // ---- testes ----

    [Fact]
    public async Task An_equipped_weapon_with_an_unmet_requirement_reduces_the_attribute_total_and_recovers_when_met()
    {
        var f = await NovaFichaAsync();
        var forcaAntes = await TotalDoAtributoAsync(f, "Forca");
        var arma = await CriarItemAsync(f, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)])));
        var linha = await AdicionarArmaAsync(f, arma.Id);

        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(forcaAntes, "an unequipped weapon penalizes nothing");

        await EquiparArmaAsync(f, linha.Id, true);
        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(forcaAntes - 2);

        await AtualizarRequisitosAsync(f, arma, new(Atributos: [new("Vigor", 0)]));
        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(forcaAntes);
    }

    [Fact]
    public async Task The_weapon_line_reports_requirements_pendencias_and_penalty()
    {
        var f = await NovaFichaAsync();
        var arma = await CriarItemAsync(f, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)], Texto: "Lenta")));
        await AdicionarArmaAsync(f, arma.Id);

        var linha = (await ListarArmasAsync(f)).Single();

        linha.Requisitos.Should().Equal("Vigor ≥ 99");
        linha.RequisitosPendentes.Should().Equal("Vigor ≥ 99");
        linha.Penalidade.Should().Equal("Força −2");
        linha.OutrasPenalidades.Should().Be("Lenta");
    }

    [Fact]
    public async Task The_weapon_line_reports_requirements_pendencias_and_a_text_only_penalty()
    {
        var f = await NovaFichaAsync();
        await AdicionarArmaAsync(f, (await CriarItemAsync(f, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Texto: "Lenta")))).Id);

        var linha = (await ListarArmasAsync(f)).Single();
        linha.Requisitos.Should().Equal("Vigor ≥ 99");
        linha.RequisitosPendentes.Should().Equal("Vigor ≥ 99");
        linha.Penalidade.Should().BeEmpty();
        linha.OutrasPenalidades.Should().Be("Lenta");
    }

    [Fact]
    public async Task Armor_in_a_slot_shield_equipped_and_artifact_on_the_list_all_penalize()
    {
        var f = await NovaFichaAsync();
        var antes = await TotalDoAtributoAsync(f, "Agilidade");
        var exige = new RequisitosDePassivaDto(Atributos: [new("Vigor", 99)]);
        var menosUm = new PenalidadeDeEquipamentoDto(Atributos: [new("Agilidade", 1)]);

        await VestirArmaduraAsync(f, "Superior", (await CriarItemAsync(f, Item("Armadura", "Cota", exige, menosUm))).Id);
        var escudo = await AdicionarEscudoAsync(f, (await CriarItemAsync(f, Item("Escudo", "Broquel", exige, menosUm))).Id);
        await EquiparEscudoAsync(f, escudo.Id, true);
        await AdicionarArtefatoAsync(f, (await CriarItemAsync(f, Item("Artefato", "Anel", exige, menosUm) with { TipoDeAlvo = "Atributo", Alvo = "Agilidade", Valor = 3 })).Id);

        // três penalidades de 1 somam; o Artefato sem requisito cumprido continua dando o próprio bônus (+3).
        (await TotalDoAtributoAsync(f, "Agilidade")).Should().Be(antes - 3 + 3);
        (await PenalidadesAtivasAsync(f)).Select(p => p.ItemNome).Should().BeEquivalentTo("Cota", "Broquel", "Anel");
    }

    [Fact]
    public async Task Armor_shield_and_artifact_penalize_and_are_listed_as_active_and_lines_report_requirements()
    {
        var f = await NovaFichaAsync();
        var antes = await TotalDoAtributoAsync(f, "Agilidade");
        var exige = new RequisitosDePassivaDto(Atributos: [new("Vigor", 99)]);
        var menosUm = new PenalidadeDeEquipamentoDto(Atributos: [new("Agilidade", 1)]);

        await VestirArmaduraAsync(f, "Superior", (await CriarItemAsync(f, Item("Armadura", "Cota", exige, menosUm))).Id);
        var escudo = await AdicionarEscudoAsync(f, (await CriarItemAsync(f, Item("Escudo", "Broquel", exige, menosUm))).Id);
        await EquiparEscudoAsync(f, escudo.Id, true);
        await AdicionarArtefatoAsync(f, (await CriarItemAsync(f, Item("Artefato", "Anel", exige, menosUm) with { TipoDeAlvo = "Atributo", Alvo = "Vigor", Valor = 1 })).Id);

        (await TotalDoAtributoAsync(f, "Agilidade")).Should().Be(antes - 3);
        (await PenalidadesAtivasAsync(f)).Select(p => p.ItemNome).Should().BeEquivalentTo("Cota", "Broquel", "Anel");
        (await GetAsync<List<EscudoLido>>(f, "shields")).Single().RequisitosPendentes.Should().Equal("Vigor ≥ 99");
        (await GetAsync<List<ArtefatoLido>>(f, "artifacts")).Single().Penalidade.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Skill_and_sub_attribute_penalties_reach_their_totals()
    {
        var f = await NovaFichaAsync();
        // Uma Perícia sem Atributo sugerido não tem Total: escolhe-se o Atributo para ela ter um.
        var escolhida = (await ListarPericiasAsync(f)).First();
        await SendOkAsync(HttpMethod.Put, Url(f, $"skills/{escolhida.Pericia}"), f.Token, new AtualizarPericia(1, "Forca"));
        var pericia = (await ListarPericiasAsync(f)).Single(p => p.Pericia == escolhida.Pericia);
        pericia.Total.Should().NotBeNull();
        var subAntes = await SubAtributosAsync(f);
        var arma = await CriarItemAsync(f, Arma("Pesada", new(Atributos: [new("Vigor", 99)]),
            new(Pericias: [new(pericia.Pericia, 4)], SubAtributos: [new("Iniciativa", 2)])));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        (await ListarPericiasAsync(f)).Single(p => p.Pericia == pericia.Pericia).Total.Should().Be(pericia.Total - 4);
        (await SubAtributosAsync(f)).Iniciativa.Should().Be(subAntes.Iniciativa - 2);
    }

    [Fact]
    public async Task An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing()
    {
        var f = await NovaFichaAsync();
        var antes = await TotalDoAtributoAsync(f, "Forca");
        var arma = await CriarItemAsync(f, Arma("Clava", null, new(Atributos: [new("Forca", 2)], Texto: "Lenta")));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        var linha = (await ListarArmasAsync(f)).Single();
        linha.Requisitos.Should().BeEmpty();
        linha.RequisitosPendentes.Should().BeEmpty();
        linha.Penalidade.Should().Equal("Força −2");
        linha.OutrasPenalidades.Should().Be("Lenta");
        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(antes);
        (await PenalidadesAtivasAsync(f)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line()
    {
        var f = await NovaFichaAsync();
        var antes = await TotalDoAtributoAsync(f, "Forca");
        var arma = await CriarItemAsync(f, Arma("Clava", new(Atributos: [new("Vigor", 0)]), new(Atributos: [new("Forca", 2)], Texto: "Lenta")));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        var linha = (await ListarArmasAsync(f)).Single();
        linha.Requisitos.Should().NotBeEmpty();
        linha.RequisitosPendentes.Should().BeEmpty();
        linha.Penalidade.Should().Equal("Força −2");
        linha.OutrasPenalidades.Should().Be("Lenta");
        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(antes);
        (await PenalidadesAtivasAsync(f)).Should().BeEmpty();
    }
}

public sealed class PenalidadesDeEquipamentoTestesPersonagem(ApiFixture fixture) : PenalidadesDeEquipamentoTestes<TipoDeTestePersonagem>(fixture)
{
    [Fact]
    public async Task A_penalty_never_makes_another_item_or_a_passiva_fail_its_requirements()
    {
        var f = await NovaFichaAsync();
        await DefinirAtributoAsync(f, "Forca", gasto: 5);
        var forca = await TotalDoAtributoAsync(f, "Forca");
        // A (arma): exige o impossível e tira 3 de Força. B (escudo; só uma arma fica equipada por vez):
        // exige exatamente a Força atual — cumpre sem a penalidade de A.
        var a = await AdicionarArmaAsync(f, (await CriarItemAsync(f, Arma("A", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 3)])))).Id);
        var b = await AdicionarEscudoAsync(f, (await CriarItemAsync(f, Item("Escudo", "B", new(Atributos: [new("Forca", forca)]), new(Atributos: [new("Vigor", 1)])))).Id);
        await EquiparArmaAsync(f, a.Id, true);
        await EquiparEscudoAsync(f, b.Id, true);

        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(forca - 3);
        (await ListarEscudosAsync(f)).Single(s => s.Nome == "B").RequisitosPendentes.Should().BeEmpty();
        (await PenalidadesAtivasAsync(f)).Select(p => p.ItemNome).Should().Equal("A");
    }

    [Fact]
    public async Task A_penalty_larger_than_the_value_follows_the_formula_floor_and_does_not_fail()
    {
        var f = await NovaFichaAsync();
        var arma = await CriarItemAsync(f, Arma("Âncora", new(Atributos: [new("Vigor", 99)]), new(SubAtributos: [new("Movimentacao", 999)])));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        (await SubAtributosAsync(f)).Movimentacao.Should().Be(1);
    }

    [Fact]
    public async Task A_penalty_on_a_pericia_the_auditor_removed_is_ignored()
    {
        var f = await NovaFichaAsync();
        string chave;
        string nome;
        int id;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var p = await db.Pericias.Where(x => !x.IsDeleted).OrderBy(x => x.Id).FirstAsync();
            (chave, nome, id) = (p.Chave, p.Nome, p.Id);
        }
        var arma = await CriarItemAsync(f, Arma("Arco Pesado", new(Atributos: [new("Vigor", 99)]),
            new(Pericias: [new(chave, 2)], Atributos: [new("Forca", 1)])));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.Pericias.SingleAsync(x => x.Id == id)).IsDeleted = true;
            await db.SaveChangesAsync();
        }

        // Tudo responde 200 (GetAsync afirma sucesso) e nenhuma linha de penalidade cita a Perícia removida.
        (await ListarPericiasAsync(f)).Should().NotContain(p => p.Pericia == chave);
        var atributos = await GetAsync<List<AttributeResponse>>(f, "attributes");
        atributos.Should().NotBeEmpty();
        await SubAtributosAsync(f);
        var linha = (await ListarArmasAsync(f)).Single();
        linha.Penalidade.Should().Equal("Força −1");
        var ativas = await PenalidadesAtivasAsync(f);
        ativas.Should().ContainSingle();
        ativas.Single().Penalidade.Should().NotContain(t => t.Contains(nome));
    }

    [Fact]
    public async Task Equipment_penalties_is_forbidden_to_a_jogador_who_does_not_own_the_sheet()
    {
        var f = await NovaFichaAsync();
        var outroGm = await OutroMestreAsync();
        var (_, outroJogador) = await JogadorAsync(outroGm);

        var proibido = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "equipment-penalties"), outroJogador));
        proibido.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var inexistente = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{Guid.NewGuid()}/equipment-penalties", f.Token));
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var dono = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "equipment-penalties"), f.Token));
        dono.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public sealed class PenalidadesDeEquipamentoTestesNpc(ApiFixture fixture) : PenalidadesDeEquipamentoTestes<TipoDeTesteNpc>(fixture)
{
    [Fact]
    public async Task A_granted_npc_evaluates_requirements_for_the_player_too()
    {
        var (gm, _) = await MestreAsync();
        var (playerId, playerToken) = await JogadorAsync(gm);
        var campaign = (await (await SendOkAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest("Campanha", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!;
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/members", gm, new AddCampaignMemberRequest(playerId));
        var grant = (await (await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/grants", gm, new GrantSheetRequest(playerId, "Npc", null))).Content.ReadFromJsonAsync<GrantSheetResponse>())!;
        var doGm = new FichaDeTeste(gm, "", gm, grant.SheetId);
        var doJogador = new FichaDeTeste(gm, "", playerToken, grant.SheetId);

        var arma = await CriarItemAsync(doGm, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)])));
        var linha = await AdicionarArmaAsync(doGm, arma.Id);
        await EquiparArmaAsync(doGm, linha.Id, true);

        var penalidadesGm = await PenalidadesAtivasAsync(doGm);
        var penalidadesJogador = await PenalidadesAtivasAsync(doJogador);
        penalidadesJogador.Should().ContainSingle().Which.ItemNome.Should().Be("Montante");
        penalidadesJogador.Should().BeEquivalentTo(penalidadesGm);

        var armasGm = await ListarArmasAsync(doGm);
        var armasJogador = await ListarArmasAsync(doJogador);
        armasJogador.Single().RequisitosPendentes.Should().Equal("Vigor ≥ 99");
        armasJogador.Single().RequisitosPendentes.Should().Equal(armasGm.Single().RequisitosPendentes);
        armasJogador.Single().Penalidade.Should().Equal(armasGm.Single().Penalidade);
    }
}

public sealed class PenalidadesDeEquipamentoTestesCriatura(ApiFixture fixture) : PenalidadesDeEquipamentoTestes<TipoDeTesteCriatura>(fixture)
{
    [Fact]
    public async Task An_equipped_weapon_with_an_unmet_attribute_requirement_penalizes_the_creature()
    {
        var f = await NovaFichaAsync();
        var antes = await TotalDoAtributoAsync(f, "Forca");
        var arma = await CriarItemAsync(f, Arma("Clava", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)])));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(antes - 2);
        (await PenalidadesAtivasAsync(f)).Single().ItemNome.Should().Be("Clava");
    }

    [Fact]
    public async Task Vocacao_classe_and_estrela_requirements_are_ignored_on_a_creature()
    {
        var f = await NovaFichaAsync();
        var arma = await CriarItemAsync(f, Arma("Lâmina do Campeão", new(Vocacao: "Campeao", Classe: "Qualquer"), new(Atributos: [new("Forca", 2)])));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        (await ListarArmasAsync(f)).Single().RequisitosPendentes.Should().BeEmpty();
        (await PenalidadesAtivasAsync(f)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_natural_attack_without_a_catalog_item_is_ignored()
    {
        var f = await NovaFichaAsync();
        var antes = await TotalDoAtributoAsync(f, "Forca");
        var ataque = (await (await SendOkAsync(HttpMethod.Post, Url(f, "weapons"), f.GmToken,
            new AddCreatureWeaponRequest(null, "Garra", "Cortante", "1D6", 3))).Content.ReadFromJsonAsync<CreatureWeaponResponse>())!;
        await EquiparArmaAsync(f, ataque.Id, true);

        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(antes);
        var linha = (await ListarArmasAsync(f)).Single();
        linha.ItemId.Should().BeNull();
        linha.Requisitos.Should().BeNullOrEmpty();
        linha.RequisitosPendentes.Should().BeNullOrEmpty();
        linha.Penalidade.Should().BeNullOrEmpty();
        linha.OutrasPenalidades.Should().BeNull();
        (await PenalidadesAtivasAsync(f)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_requirement_on_an_attribute_the_creature_does_not_have_is_ignored()
    {
        // Instinto existe na Ficha de Personagem mas não na de Criatura (que tem Ego no lugar):
        // FichaParaRequisitos.Atributos não traz a chave, então o requisito é ignorado.
        var f = await NovaFichaAsync();
        var antes = await TotalDoAtributoAsync(f, "Forca");
        var arma = await CriarItemAsync(f, Arma("Cajado", new(Atributos: [new("Instinto", 99)]), new(Atributos: [new("Forca", 2)])));
        await EquiparArmaAsync(f, (await AdicionarArmaAsync(f, arma.Id)).Id, true);

        (await ListarArmasAsync(f)).Single().RequisitosPendentes.Should().BeEmpty();
        (await PenalidadesAtivasAsync(f)).Should().BeEmpty();
        (await TotalDoAtributoAsync(f, "Forca")).Should().Be(antes);
    }
}
