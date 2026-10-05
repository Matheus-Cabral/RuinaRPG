using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Penalidade automática de equipamento nos cálculos da Ficha de Criatura (Catálogo de Itens, Requisitos/Penalidade).</summary>
public class CreatureEquipmentPenaltiesTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CreatureEquipmentPenaltiesTests(PostgresFixture postgres) => _postgres = postgres;

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
        (await (await SendOkAsync(HttpMethod.Get, $"/api/creature-sheets/{ctx.SheetId}/{path}", ctx.ViewerToken)).Content.ReadFromJsonAsync<T>())!;

    private async Task<string> RegisterGmAsync(string nickname)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, $"{nickname}@x.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<Ctx> NovaCriaturaAsync(string prefix)
    {
        var gm = await RegisterGmAsync($"{prefix}Gm");
        var response = await SendOkAsync(HttpMethod.Post, "/api/creature-sheets", gm);
        return new Ctx(gm, gm, (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id);
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

    private async Task<CreatureWeaponResponse> AdicionarArmaAsync(Ctx ctx, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{ctx.SheetId}/weapons", ctx.GmToken, new AddCreatureWeaponRequest(itemId, null, null, null, null))).Content.ReadFromJsonAsync<CreatureWeaponResponse>())!;

    private Task EquiparArmaAsync(Ctx ctx, string id, bool equipada) =>
        SendOkAsync(HttpMethod.Put, $"/api/creature-sheets/{ctx.SheetId}/weapons/{id}", ctx.GmToken, equipada);

    private Task<List<CreatureWeaponResponse>> ListarArmasAsync(Ctx ctx) => GetAsync<List<CreatureWeaponResponse>>(ctx, "weapons");

    private async Task<CreatureShieldResponse> AdicionarEscudoAsync(Ctx ctx, string itemId) =>
        (await (await SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{ctx.SheetId}/shields", ctx.GmToken, new AddCreatureShieldRequest(itemId))).Content.ReadFromJsonAsync<CreatureShieldResponse>())!;

    private Task EquiparEscudoAsync(Ctx ctx, string id, bool equipado) =>
        SendOkAsync(HttpMethod.Put, $"/api/creature-sheets/{ctx.SheetId}/shields/{id}", ctx.GmToken, equipado);

    private Task VestirArmaduraAsync(Ctx ctx, string slot, string itemId) =>
        SendOkAsync(HttpMethod.Put, $"/api/creature-sheets/{ctx.SheetId}/armor-slots/{slot}", ctx.GmToken, new UpdateCreatureArmorSlotRequest(itemId));

    private Task AdicionarArtefatoAsync(Ctx ctx, string itemId) =>
        SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{ctx.SheetId}/artifacts", ctx.GmToken, new AddCreatureArtifactRequest(itemId));

    private async Task<int> TotalDoAtributoAsync(Ctx ctx, string atributo) =>
        (await GetAsync<List<CreatureAttributeResponse>>(ctx, "attributes")).Single(a => a.Atributo == atributo).Total;

    private Task<List<CreatureSkillResponse>> ListarPericiasAsync(Ctx ctx) => GetAsync<List<CreatureSkillResponse>>(ctx, "skills");

    private Task<SubAttributesResponse> SubAtributosAsync(Ctx ctx) => GetAsync<SubAttributesResponse>(ctx, "sub-attributes");

    private Task<List<PenalidadeAtivaResponse>> PenalidadesAtivasAsync(Ctx ctx) => GetAsync<List<PenalidadeAtivaResponse>>(ctx, "equipment-penalties");

    // ---- testes ----

    [Fact]
    public async Task An_equipped_weapon_with_an_unmet_attribute_requirement_penalizes_the_creature()
    {
        var ctx = await NovaCriaturaAsync("PenCri1");
        var antes = await TotalDoAtributoAsync(ctx, "Forca");
        var arma = await CriarItemAsync(ctx, Arma("Clava", new(Atributos: [new("Vigor", 99)]), new(Atributos: [new("Forca", 2)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(antes - 2);
        (await PenalidadesAtivasAsync(ctx)).Single().ItemNome.Should().Be("Clava");
    }

    [Fact]
    public async Task Vocacao_classe_and_estrela_requirements_are_ignored_on_a_creature()
    {
        var ctx = await NovaCriaturaAsync("PenCri2");
        var arma = await CriarItemAsync(ctx, Arma("Lâmina do Campeão", new(Vocacao: "Campeao", Classe: "Qualquer"), new(Atributos: [new("Forca", 2)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        (await ListarArmasAsync(ctx)).Single().RequisitosPendentes.Should().BeEmpty();
        (await PenalidadesAtivasAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_natural_attack_without_a_catalog_item_is_ignored()
    {
        var ctx = await NovaCriaturaAsync("PenCri3");
        var antes = await TotalDoAtributoAsync(ctx, "Forca");
        var ataque = (await (await SendOkAsync(HttpMethod.Post, $"/api/creature-sheets/{ctx.SheetId}/weapons", ctx.GmToken,
            new AddCreatureWeaponRequest(null, "Garra", "Cortante", "1D6", 3))).Content.ReadFromJsonAsync<CreatureWeaponResponse>())!;
        await EquiparArmaAsync(ctx, ataque.Id, true);

        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(antes);
        var linha = (await ListarArmasAsync(ctx)).Single();
        linha.ItemId.Should().BeNull();
        linha.Requisitos.Should().BeNullOrEmpty();
        linha.RequisitosPendentes.Should().BeNullOrEmpty();
        linha.Penalidade.Should().BeNullOrEmpty();
        (await PenalidadesAtivasAsync(ctx)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_requirement_on_an_attribute_the_creature_does_not_have_is_ignored()
    {
        // Instinto existe na Ficha de Personagem mas não na de Criatura (que tem Ego no lugar):
        // FichaParaRequisitos.Atributos não traz a chave, então o requisito é ignorado.
        var ctx = await NovaCriaturaAsync("PenCri4");
        var antes = await TotalDoAtributoAsync(ctx, "Forca");
        var arma = await CriarItemAsync(ctx, Arma("Cajado", new(Atributos: [new("Instinto", 99)]), new(Atributos: [new("Forca", 2)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        (await ListarArmasAsync(ctx)).Single().RequisitosPendentes.Should().BeEmpty();
        (await PenalidadesAtivasAsync(ctx)).Should().BeEmpty();
        (await TotalDoAtributoAsync(ctx, "Forca")).Should().Be(antes);
    }

    [Fact]
    public async Task Armor_shield_and_artifact_penalize_a_creature_and_lines_report_requirements()
    {
        var ctx = await NovaCriaturaAsync("PenCri5");
        var antes = await TotalDoAtributoAsync(ctx, "Agilidade");
        var exige = new RequisitosDePassivaDto(Atributos: [new("Vigor", 99)]);
        var menosUm = new PenalidadeDeEquipamentoDto(Atributos: [new("Agilidade", 1)]);

        await VestirArmaduraAsync(ctx, "Superior", (await CriarItemAsync(ctx, Item("Armadura", "Cota", exige, menosUm))).Id);
        var escudo = await AdicionarEscudoAsync(ctx, (await CriarItemAsync(ctx, Item("Escudo", "Broquel", exige, menosUm))).Id);
        await EquiparEscudoAsync(ctx, escudo.Id, true);
        await AdicionarArtefatoAsync(ctx, (await CriarItemAsync(ctx, Item("Artefato", "Anel", exige, menosUm) with { TipoDeAlvo = "Atributo", Alvo = "Vigor", Valor = 1 })).Id);

        (await TotalDoAtributoAsync(ctx, "Agilidade")).Should().Be(antes - 3);
        (await PenalidadesAtivasAsync(ctx)).Select(p => p.ItemNome).Should().BeEquivalentTo("Cota", "Broquel", "Anel");
        (await GetAsync<List<CreatureShieldResponse>>(ctx, "shields")).Single().RequisitosPendentes.Should().Equal("Vigor ≥ 99");
        (await GetAsync<List<CreatureArtifactResponse>>(ctx, "artifacts")).Single().Penalidade.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Creature_skill_and_sub_attribute_totals_follow_the_penalty()
    {
        var ctx = await NovaCriaturaAsync("PenCri6");
        var escolhida = (await ListarPericiasAsync(ctx)).First();
        await SendOkAsync(HttpMethod.Put, $"/api/creature-sheets/{ctx.SheetId}/skills/{escolhida.Pericia}", ctx.GmToken, new UpdateCreatureSkillRequest(1, "Forca"));
        var pericia = (await ListarPericiasAsync(ctx)).Single(p => p.Pericia == escolhida.Pericia);
        pericia.Total.Should().NotBeNull();
        var subAntes = await SubAtributosAsync(ctx);
        var arma = await CriarItemAsync(ctx, Arma("Pesada", new(Atributos: [new("Vigor", 99)]), new(Pericias: [new(pericia.Pericia, 4)], SubAtributos: [new("Iniciativa", 2)])));
        await EquiparArmaAsync(ctx, (await AdicionarArmaAsync(ctx, arma.Id)).Id, true);

        (await ListarPericiasAsync(ctx)).Single(p => p.Pericia == pericia.Pericia).Total.Should().Be(pericia.Total - 4);
        (await SubAtributosAsync(ctx)).Iniciativa.Should().Be(subAntes.Iniciativa - 2);
    }
}
