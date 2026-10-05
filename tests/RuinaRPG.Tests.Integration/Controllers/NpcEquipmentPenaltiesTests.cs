using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Penalidade automática de equipamento nos cálculos da Ficha de NPC (Catálogo de Itens, Requisitos/Penalidade).</summary>
public class NpcEquipmentPenaltiesTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public NpcEquipmentPenaltiesTests(PostgresFixture postgres) => _postgres = postgres;

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

    private sealed record Ctx(string GmToken, string ViewerToken, string SheetId);

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
        (await (await SendOkAsync(HttpMethod.Get, $"/api/npc-sheets/{ctx.SheetId}/{path}", ctx.ViewerToken)).Content.ReadFromJsonAsync<T>())!;

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

    private async Task<Ctx> NovoNpcAsync(string prefix)
    {
        var gm = await RegisterGmAsync($"{prefix}Gm");
        var response = await SendOkAsync(HttpMethod.Post, "/api/npc-sheets", gm);
        return new Ctx(gm, gm, (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id);
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

    private async Task<ItemResponse> CriarItemAsync(Ctx ctx, CreateItemRequest request) =>
        (await (await SendOkAsync(HttpMethod.Post, "/api/items", ctx.GmToken, request)).Content.ReadFromJsonAsync<ItemResponse>())!;

    // ---- ações sobre a ficha ----

    private async Task<NpcWeaponResponse> AdicionarArmaAsync(Ctx ctx, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, $"/api/npc-sheets/{ctx.SheetId}/weapons", ctx.GmToken, new AddNpcWeaponRequest(itemId))).Content.ReadFromJsonAsync<NpcWeaponResponse>())!;

    private Task EquiparArmaAsync(Ctx ctx, string id, bool equipada) =>
        SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{ctx.SheetId}/weapons/{id}", ctx.GmToken, equipada);

    private Task<List<NpcWeaponResponse>> ListarArmasAsync(Ctx ctx) => GetAsync<List<NpcWeaponResponse>>(ctx, "weapons");

    private async Task<NpcShieldResponse> AdicionarEscudoAsync(Ctx ctx, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, $"/api/npc-sheets/{ctx.SheetId}/shields", ctx.GmToken, new AddNpcShieldRequest(itemId))).Content.ReadFromJsonAsync<NpcShieldResponse>())!;

    private Task EquiparEscudoAsync(Ctx ctx, string id, bool equipado) =>
        SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{ctx.SheetId}/shields/{id}", ctx.GmToken, equipado);

    private Task VestirArmaduraAsync(Ctx ctx, string slot, string itemId) =>
        SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{ctx.SheetId}/armor-slots/{slot}", ctx.GmToken, new UpdateNpcArmorSlotRequest(itemId));

    private Task AdicionarArtefatoAsync(Ctx ctx, string itemId) =>
        SendOkAsync(HttpMethod.Post, $"/api/npc-sheets/{ctx.SheetId}/artifacts", ctx.GmToken, new AddNpcArtifactRequest(itemId));

    private async Task<int> TotalDoAtributoAsync(Ctx ctx, string atributo) =>
        (await GetAsync<List<NpcAttributeResponse>>(ctx, "attributes")).Single(a => a.Atributo == atributo).Total;

    private Task<List<NpcSkillResponse>> ListarPericiasAsync(Ctx ctx) => GetAsync<List<NpcSkillResponse>>(ctx, "skills");

    private Task<SubAttributesResponse> SubAtributosAsync(Ctx ctx) => GetAsync<SubAttributesResponse>(ctx, "sub-attributes");

    private Task<List<PenalidadeAtivaResponse>> PenalidadesAtivasAsync(Ctx ctx) => GetAsync<List<PenalidadeAtivaResponse>>(ctx, "equipment-penalties");

    // ---- testes ----

    [Fact]
    public async Task An_equipped_weapon_with_an_unmet_requirement_reduces_the_npc_attribute_total_and_an_unequipped_one_does_not()
    {
        var ctx = await NovoNpcAsync("PenNpc1");
        var antes = await TotalDoAtributoAsync(ctx, "Forca");
        var arma = await CriarItemAsync(ctx, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)])));
        var linha = await AdicionarArmaAsync(ctx, arma.Id);

        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(antes);
        await EquiparArmaAsync(ctx, linha.Id, true);
        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(antes - 2);
    }

    [Fact]
    public async Task Armor_shield_and_artifact_penalize_an_npc_and_are_listed_as_active()
    {
        var ctx = await NovoNpcAsync("PenNpc2");
        var antes = await TotalDoAtributoAsync(ctx, "Agilidade");
        var exige = new RequisitosDePassivaDto(Atributos: [new("Vigor", 99)]);
        var menosUm = new PenalidadeDeEquipamentoDto(Atributos: [new("Agilidade", 1)]);

        await VestirArmaduraAsync(ctx, "Superior", (await CriarItemAsync(ctx, Item("Armadura", "Cota", exige, menosUm))).Id);
        var escudo = await AdicionarEscudoAsync(ctx, (await CriarItemAsync(ctx, Item("Escudo", "Broquel", exige, menosUm))).Id);
        await EquiparEscudoAsync(ctx, escudo.Id, true);
        await AdicionarArtefatoAsync(ctx, (await CriarItemAsync(ctx, Item("Artefato", "Anel", exige, menosUm) with { TipoDeAlvo = "Atributo", Alvo = "Vigor", Valor = 1 })).Id);

        (await TotalDoAtributoAsync(ctx, "Agilidade")).Should().Be(antes - 3);
        (await PenalidadesAtivasAsync(ctx)).Select(p => p.ItemNome).Should().BeEquivalentTo("Cota", "Broquel", "Anel");
    }

    [Fact]
    public async Task The_npc_lines_report_requirements_pendencias_and_penalty()
    {
        var ctx = await NovoNpcAsync("PenNpc3");
        await AdicionarArmaAsync(ctx, (await CriarItemAsync(ctx, Arma("Montante", new(Atributos: [new("Vigor", 99)]), new(Texto: "Lenta")))).Id);

        var linha = (await ListarArmasAsync(ctx)).Single();
        linha.Requisitos.Should().Equal("Vigor ≥ 99");
        linha.RequisitosPendentes.Should().Equal("Vigor ≥ 99");
        linha.Penalidade.Should().BeEmpty();
        linha.OutrasPenalidades.Should().Be("Lenta");
    }

    [Fact]
    public async Task Npc_skill_and_sub_attribute_totals_follow_the_penalty()
    {
        var ctx = await NovoNpcAsync("PenNpc4");
        // Uma Perícia sem Atributo sugerido não tem Total: escolhe-se o Atributo para ela ter um.
        var escolhida = (await ListarPericiasAsync(ctx)).First();
        await SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{ctx.SheetId}/skills/{escolhida.Pericia}", ctx.GmToken, new UpdateNpcSkillRequest(1, "Forca"));
        var pericia = (await ListarPericiasAsync(ctx)).Single(p => p.Pericia == escolhida.Pericia);
        pericia.Total.Should().NotBeNull();
        var subAntes = await SubAtributosAsync(ctx);
        var arma = await CriarItemAsync(ctx, Arma("Pesada", new(Atributos: [new("Vigor", 99)]), new(Pericias: [new(pericia.Pericia, 4)], SubAtributos: [new("Iniciativa", 2)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        (await ListarPericiasAsync(ctx)).Single(p => p.Pericia == pericia.Pericia).Total.Should().Be(pericia.Total - 4);
        (await SubAtributosAsync(ctx)).Iniciativa.Should().Be(subAntes.Iniciativa - 2);
    }

    [Fact]
    public async Task A_granted_npc_evaluates_requirements_for_the_player_too()
    {
        var gm = await RegisterGmAsync("PenNpc5Gm");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PenNpc5Jog");
        var campaign = (await (await SendOkAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest("Campanha", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!;
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/members", gm, new AddCampaignMemberRequest(playerId));
        var grant = (await (await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/grants", gm, new GrantSheetRequest(playerId, "Npc", null))).Content.ReadFromJsonAsync<GrantSheetResponse>())!;
        var doGm = new Ctx(gm, gm, grant.SheetId);
        var doJogador = new Ctx(gm, playerToken, grant.SheetId);

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

    [Fact]
    public async Task An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing()
    {
        var ctx = await NovoNpcAsync("PenNpcXA");
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
        var ctx = await NovoNpcAsync("PenNpcXB");
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
