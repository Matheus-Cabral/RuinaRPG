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
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Penalidade automática de equipamento nos cálculos da Ficha de Personagem (Catálogo de Itens, Requisitos/Penalidade).</summary>
public class CharacterEquipmentPenaltiesTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterEquipmentPenaltiesTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private sealed record Ctx(string GmToken, string PlayerToken, string SheetId);

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<HttpResponseMessage> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await _client.SendAsync(AuthedRequest(method, url, token, body));
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} returned {(int)response.StatusCode}");
        return response;
    }

    private async Task<T> GetAsync<T>(Ctx ctx, string path) =>
        (await (await SendOkAsync(HttpMethod.Get, $"/api/character-sheets/{ctx.SheetId}/{path}", ctx.PlayerToken)).Content.ReadFromJsonAsync<T>())!;

    private async Task<string> RegisterGmAsync(string nickname)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, $"{nickname}@x.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string Id, string Token)> RegisterJogadorLinkedToAsync(string gmToken, string nickname)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, $"{nickname}@x.com", "Senha!123", "Senha!123", code));
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<Ctx> NovaFichaAsync(string prefix)
    {
        var gm = await RegisterGmAsync($"{prefix}Gm");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, $"{prefix}Jog");
        var campaign = await (await SendOkAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest("Campanha", ""))).Content.ReadFromJsonAsync<CampaignResponse>();
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaign!.Id}/members", gm, new AddCampaignMemberRequest(playerId));
        var sheet = await (await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/character-sheets", gm, new CreateCharacterSheetRequest(playerId))).Content.ReadFromJsonAsync<CharacterSheetResponse>();
        return new Ctx(gm, playerToken, sheet!.Id);
    }

    // ---- itens do catálogo ----

    private static CreateItemRequest Arma(string nome, RequisitosDePassivaDto? requisitos, PenalidadeDeEquipamentoDto? penalidade) =>
        new("Arma", nome, 1.5m, 50, null, "Espadas", "Uma lâmina.", "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
            null, null, null, null, null, null, null, null, null, requisitos, penalidade);

    private static CreateItemRequest Item(string tipo, string nome, RequisitosDePassivaDto? requisitos, PenalidadeDeEquipamentoDto? penalidade) => tipo switch
    {
        "Armadura" => new("Armadura", nome, 8m, 100, null, null, "Placas.", "D", null, null, null, null, null, null,
            "Pesada", 5, 2, 1, null, null, null, null, null, requisitos, penalidade),
        "Escudo" => new("Escudo", nome, 4m, 60, null, null, "Um escudo.", "F", null, null, null, null, null, null,
            "Leve", null, null, null, 3, null, null, null, null, requisitos, penalidade),
        _ => new("Artefato", nome, 0.2m, 200, null, null, "Um anel.", null, null, null, null, null, null, null,
            null, null, null, null, null, "Atributo", "Forca", 2, null, requisitos, penalidade),
    };

    private async Task<ItemResponse> CriarItemAsync(Ctx ctx, CreateItemRequest request)
    {
        var response = await SendOkAsync(HttpMethod.Post, "/api/items", ctx.GmToken, request);
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!;
    }

    private async Task AtualizarRequisitosAsync(Ctx ctx, ItemResponse i, RequisitosDePassivaDto requisitos)
    {
        var update = new UpdateItemRequest(
            Nome: i.Nome, Peso: i.Peso, Preco: i.Preco, ImageId: null, Subcategoria: i.Subcategoria, Descricao: i.Descricao,
            Rank: i.Rank, Empunhadura: i.Empunhadura, Dados: i.Dados, Dano: i.Dano, Critico: i.Critico, Alcance: i.Alcance,
            TipoDeDano: i.TipoDeDano, Categoria: i.Categoria, Defesa: i.Defesa,
            RF: i.RF, RM: i.RM, BonusDefesa: i.BonusDefesa,
            TipoDeAlvo: i.TipoDeAlvo, Alvo: i.Alvo, Valor: i.Valor, CapacidadeExtra: i.CapacidadeExtra,
            Requisitos: requisitos, PenalidadeDeRequisitos: i.PenalidadeDeRequisitos);
        await SendOkAsync(HttpMethod.Put, $"/api/items/{i.Id}", ctx.GmToken, update);
    }

    // ---- ações sobre a ficha ----

    private async Task<CharacterWeaponResponse> AdicionarArmaAsync(Ctx ctx, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, $"/api/character-sheets/{ctx.SheetId}/weapons", ctx.PlayerToken, new AddCharacterWeaponRequest(itemId))).Content.ReadFromJsonAsync<CharacterWeaponResponse>())!;

    private Task EquiparArmaAsync(Ctx ctx, string id, bool equipada) =>
        SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{ctx.SheetId}/weapons/{id}", ctx.PlayerToken, equipada);

    private Task<List<CharacterWeaponResponse>> ListarArmasAsync(Ctx ctx) => GetAsync<List<CharacterWeaponResponse>>(ctx, "weapons");

    private async Task<CharacterShieldResponse> AdicionarEscudoAsync(Ctx ctx, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, $"/api/character-sheets/{ctx.SheetId}/shields", ctx.PlayerToken, new AddCharacterShieldRequest(itemId))).Content.ReadFromJsonAsync<CharacterShieldResponse>())!;

    private Task EquiparEscudoAsync(Ctx ctx, string id, bool equipado) =>
        SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{ctx.SheetId}/shields/{id}", ctx.PlayerToken, equipado);

    private Task<List<CharacterShieldResponse>> ListarEscudosAsync(Ctx ctx) => GetAsync<List<CharacterShieldResponse>>(ctx, "shields");

    private Task VestirArmaduraAsync(Ctx ctx, string slot, string itemId) =>
        SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{ctx.SheetId}/armor-slots/{slot}", ctx.PlayerToken, new UpdateCharacterArmorSlotRequest(itemId));

    private Task AdicionarArtefatoAsync(Ctx ctx, string itemId) =>
        SendOkAsync(HttpMethod.Post, $"/api/character-sheets/{ctx.SheetId}/artifacts", ctx.PlayerToken, new AddCharacterArtifactRequest(itemId));

    private Task DefinirAtributoAsync(Ctx ctx, string atributo, int gasto) =>
        SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{ctx.SheetId}/attributes/{atributo}", ctx.PlayerToken, new UpdateCharacterAttributeRequest(gasto, 0, false));

    private async Task<int> TotalDoAtributoAsync(Ctx ctx, string atributo) =>
        (await GetAsync<List<CharacterAttributeResponse>>(ctx, "attributes")).Single(a => a.Atributo == atributo).Total;

    private Task<List<CharacterSkillResponse>> ListarPericiasAsync(Ctx ctx) => GetAsync<List<CharacterSkillResponse>>(ctx, "skills");

    private Task<SubAttributesResponse> SubAtributosAsync(Ctx ctx) => GetAsync<SubAttributesResponse>(ctx, "sub-attributes");

    private Task<List<PenalidadeAtivaResponse>> PenalidadesAtivasAsync(Ctx ctx) => GetAsync<List<PenalidadeAtivaResponse>>(ctx, "equipment-penalties");

    // ---- testes ----

    [Fact]
    public async Task An_equipped_weapon_with_an_unmet_requirement_reduces_the_attribute_total_and_recovers_when_met()
    {
        var ctx = await NovaFichaAsync("PenChar1");
        var forcaAntes = await TotalDoAtributoAsync(ctx, "Forca");
        var arma = await CriarItemAsync(ctx, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)])));
        var linha = await AdicionarArmaAsync(ctx, arma.Id);

        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(forcaAntes, "an unequipped weapon penalizes nothing");

        await EquiparArmaAsync(ctx, linha.Id, true);
        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(forcaAntes - 2);

        await AtualizarRequisitosAsync(ctx, arma, new(Atributos: [new("Vigor", 0)]));
        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(forcaAntes);
    }

    [Fact]
    public async Task The_weapon_line_reports_requirements_pendencias_and_penalty()
    {
        var ctx = await NovaFichaAsync("PenChar2");
        var arma = await CriarItemAsync(ctx, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)], Texto: "Lenta")));
        await AdicionarArmaAsync(ctx, arma.Id);

        var linha = (await ListarArmasAsync(ctx)).Single();

        linha.Requisitos.Should().Equal("Vigor ≥ 99");
        linha.RequisitosPendentes.Should().Equal("Vigor ≥ 99");
        linha.Penalidade.Should().Equal("Força −2");
        linha.OutrasPenalidades.Should().Be("Lenta");
    }

    [Fact]
    public async Task Armor_in_a_slot_shield_equipped_and_artifact_on_the_list_all_penalize()
    {
        var ctx = await NovaFichaAsync("PenChar3");
        var antes = await TotalDoAtributoAsync(ctx, "Agilidade");
        var exige = new RequisitosDePassivaDto(Atributos: [new("Vigor", 99)]);
        var menosUm = new PenalidadeDeEquipamentoDto(Atributos: [new("Agilidade", 1)]);

        await VestirArmaduraAsync(ctx, "Superior", (await CriarItemAsync(ctx, Item("Armadura", "Cota", exige, menosUm))).Id);
        var escudo = await AdicionarEscudoAsync(ctx, (await CriarItemAsync(ctx, Item("Escudo", "Broquel", exige, menosUm))).Id);
        await EquiparEscudoAsync(ctx, escudo.Id, true);
        await AdicionarArtefatoAsync(ctx, (await CriarItemAsync(ctx, Item("Artefato", "Anel", exige, menosUm) with { TipoDeAlvo = "Atributo", Alvo = "Agilidade", Valor = 3 })).Id);

        // três penalidades de 1 somam; o Artefato sem requisito cumprido continua dando o próprio bônus (+3).
        (await TotalDoAtributoAsync(ctx, "Agilidade")).Should().Be(antes - 3 + 3);
        (await PenalidadesAtivasAsync(ctx)).Select(p => p.ItemNome).Should().BeEquivalentTo("Cota", "Broquel", "Anel");
    }

    [Fact]
    public async Task A_penalty_never_makes_another_item_or_a_passiva_fail_its_requirements()
    {
        var ctx = await NovaFichaAsync("PenChar4");
        await DefinirAtributoAsync(ctx, "Forca", gasto: 5);
        var forca = await TotalDoAtributoAsync(ctx, "Forca");
        // A (arma): exige o impossível e tira 3 de Força. B (escudo; só uma arma fica equipada por vez):
        // exige exatamente a Força atual — cumpre sem a penalidade de A.
        var a = await AdicionarArmaAsync(ctx, (await CriarItemAsync(ctx, Arma("A", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 3)])))).Id);
        var b = await AdicionarEscudoAsync(ctx, (await CriarItemAsync(ctx, Item("Escudo", "B", new(Atributos: [new("Forca", forca)]), new(Atributos: [new("Vigor", 1)])))).Id);
        await EquiparArmaAsync(ctx, a.Id, true);
        await EquiparEscudoAsync(ctx, b.Id, true);

        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(forca - 3);
        (await ListarEscudosAsync(ctx)).Single(s => s.Nome == "B").RequisitosPendentes.Should().BeEmpty();
        (await PenalidadesAtivasAsync(ctx)).Select(p => p.ItemNome).Should().Equal("A");
    }

    [Fact]
    public async Task Skill_and_sub_attribute_penalties_reach_their_totals()
    {
        var ctx = await NovaFichaAsync("PenChar5");
        // Uma Perícia sem Atributo sugerido não tem Total: escolhe-se o Atributo para ela ter um.
        var escolhida = (await ListarPericiasAsync(ctx)).First();
        await SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{ctx.SheetId}/skills/{escolhida.Pericia}", ctx.PlayerToken, new UpdateCharacterSkillRequest(1, "Forca"));
        var pericia = (await ListarPericiasAsync(ctx)).Single(p => p.Pericia == escolhida.Pericia);
        pericia.Total.Should().NotBeNull();
        var subAntes = await SubAtributosAsync(ctx);
        var arma = await CriarItemAsync(ctx, Arma("Pesada", new(Atributos: [new("Vigor", 99)]),
            new(Pericias: [new(pericia.Pericia, 4)], SubAtributos: [new("Iniciativa", 2)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        (await ListarPericiasAsync(ctx)).Single(p => p.Pericia == pericia.Pericia).Total.Should().Be(pericia.Total - 4);
        (await SubAtributosAsync(ctx)).Iniciativa.Should().Be(subAntes.Iniciativa - 2);
    }

    [Fact]
    public async Task A_penalty_larger_than_the_value_follows_the_formula_floor_and_does_not_fail()
    {
        var ctx = await NovaFichaAsync("PenChar6");
        var arma = await CriarItemAsync(ctx, Arma("Âncora", new(Atributos: [new("Vigor", 99)]), new(SubAtributos: [new("Movimentacao", 999)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        (await SubAtributosAsync(ctx)).Movimentacao.Should().Be(1);
    }

    [Fact]
    public async Task A_penalty_on_a_pericia_the_auditor_removed_is_ignored()
    {
        var ctx = await NovaFichaAsync("PenChar7");
        string chave;
        string nome;
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var p = await db.Pericias.Where(x => !x.IsDeleted).OrderBy(x => x.Id).FirstAsync();
            (chave, nome, id) = (p.Chave, p.Nome, p.Id);
        }
        var arma = await CriarItemAsync(ctx, Arma("Arco Pesado", new(Atributos: [new("Vigor", 99)]),
            new(Pericias: [new(chave, 2)], Atributos: [new("Forca", 1)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.Pericias.SingleAsync(x => x.Id == id)).IsDeleted = true;
            await db.SaveChangesAsync();
        }

        // Tudo responde 200 (GetAsync afirma sucesso) e nenhuma linha de penalidade cita a Perícia removida.
        (await ListarPericiasAsync(ctx)).Should().NotContain(p => p.Pericia == chave);
        var atributos = await GetAsync<List<CharacterAttributeResponse>>(ctx, "attributes");
        atributos.Should().NotBeEmpty();
        await SubAtributosAsync(ctx);
        var linha = (await ListarArmasAsync(ctx)).Single();
        linha.Penalidade.Should().Equal("Força −1");
        var ativas = await PenalidadesAtivasAsync(ctx);
        ativas.Should().ContainSingle();
        ativas.Single().Penalidade.Should().NotContain(t => t.Contains(nome));
    }

    [Fact]
    public async Task Equipment_penalties_is_forbidden_to_a_jogador_who_does_not_own_the_sheet()
    {
        var ctx = await NovaFichaAsync("PenChar8");
        var outroGm = await RegisterGmAsync("PenChar8OutroGm");
        var (_, outroJogador) = await RegisterJogadorLinkedToAsync(outroGm, "PenChar8Outro");

        var proibido = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{ctx.SheetId}/equipment-penalties", outroJogador));
        proibido.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var inexistente = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{Guid.NewGuid()}/equipment-penalties", ctx.PlayerToken));
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var dono = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{ctx.SheetId}/equipment-penalties", ctx.PlayerToken));
        dono.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing()
    {
        var ctx = await NovaFichaAsync("PenCharXA");
        var antes = await TotalDoAtributoAsync(ctx, "Forca");
        var arma = await CriarItemAsync(ctx, Arma("Clava", null, new(Atributos: [new("Forca", 2)], Texto: "Lenta")));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        var linha = (await ListarArmasAsync(ctx)).Single();
        linha.Requisitos.Should().BeEmpty();
        linha.RequisitosPendentes.Should().BeEmpty();
        linha.Penalidade.Should().Equal("Força −2");
        linha.OutrasPenalidades.Should().Be("Lenta");
        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(antes);
        (await PenalidadesAtivasAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line()
    {
        var ctx = await NovaFichaAsync("PenCharXB");
        var antes = await TotalDoAtributoAsync(ctx, "Forca");
        var arma = await CriarItemAsync(ctx, Arma("Clava", new(Atributos: [new("Vigor", 0)]), new(Atributos: [new("Forca", 2)], Texto: "Lenta")));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        var linha = (await ListarArmasAsync(ctx)).Single();
        linha.Requisitos.Should().NotBeEmpty();
        linha.RequisitosPendentes.Should().BeEmpty();
        linha.Penalidade.Should().Equal("Força −2");
        linha.OutrasPenalidades.Should().Be("Lenta");
        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(antes);
        (await PenalidadesAtivasAsync(ctx)).Should().BeEmpty();
    }
}
