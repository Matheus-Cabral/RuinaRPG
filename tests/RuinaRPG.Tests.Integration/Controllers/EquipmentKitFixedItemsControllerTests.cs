using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EquipmentKitFixedItemsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Url = "/api/equipment-kit-fixed-items";

    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EquipmentKitFixedItemsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> RegisterAuditorAsync(string nickname)
    {
        var email = $"{nickname.ToLowerInvariant()}@teste.com";
        var token = await RegisterGmAndGetTokenAsync(nickname, email);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return token;
    }

    private static string Unico(string prefixo) => $"{prefixo} {Guid.NewGuid():N}";

    private static CreateItemRequest Arma(string nome) =>
        new("Arma", nome, 1.5m, 50, null, "Espadas", "Uma lâmina curta e leve.",
            "F", "UmaMao", "2D6", 6, "19", 2, "Cortante",
            null, null, null, null,
            null, null, null, null, null);

    private static CreateItemRequest ItemGeral(string nome) =>
        new("ItemGeral", nome, 0.5m, 5, null, "Equipamentos de Aventura", "Uma corda resistente.",
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, null, null, null, null);

    private static CreateItemRequest Armadura(string nome) =>
        new("Armadura", nome, 8m, 100, null, null, "Couro curtido.",
            "D", null, null, null, null, null, null,
            "Leve", 5, 2, 1,
            null, null, null, null, null);

    private async Task<EquipmentKitFixedItemResponse> CriarAsync(string token, CreateItemRequest request)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token, request));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EquipmentKitFixedItemResponse>())!;
    }

    private async Task<List<EquipmentKitFixedItemResponse>> ListarAsync(string token, string query = "")
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, Url + query, token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<EquipmentKitFixedItemResponse>>())!;
    }

    private async Task<EquipmentKitResponse> CriarKitAsync(string token, string nome, List<EquipmentKitItemInput> items, List<EquipmentKitChoiceSlotInput>? slots = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", token,
            new CreateEquipmentKitRequest(nome, "Descrição", 0, items, slots ?? [])));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EquipmentKitResponse>())!;
    }

    [Fact]
    public async Task Every_endpoint_is_forbidden_to_a_gm_who_is_not_the_auditor()
    {
        var auditor = await RegisterAuditorAsync("FixosAuditor0");
        var fixo = await CriarAsync(auditor, ItemGeral(Unico("Corda")));
        var gm = await RegisterGmAndGetTokenAsync("FixosGmComum0", "fixosgmcomum0@teste.com");

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, Url, gm))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, gm, ItemGeral(Unico("Outra"))))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{fixo.Id}", gm, ItemGeral(fixo.Nome)))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{fixo.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_stores_the_full_item_and_returns_it_complete()
    {
        var token = await RegisterAuditorAsync("FixosAuditor1");
        var nome = Unico("Espada Longa");
        var requisitos = new RequisitosDePassivaDto(Vocacao: "Campeao", Atributos: [new("Vigor", 8)]);
        var penalidade = new PenalidadeDeEquipamentoDto(Atributos: [new("Forca", 2)], Texto: "Desvantagem em furtividade");
        var request = Arma(nome) with { Requisitos = requisitos, PenalidadeDeRequisitos = penalidade };

        var criado = await CriarAsync(token, request);

        void Verificar(EquipmentKitFixedItemResponse f)
        {
            f.Nome.Should().Be(nome);
            f.Tipo.Should().Be("Arma");
            f.DetalhesIncompletos.Should().BeFalse();
            f.Kits.Should().BeEmpty();
            f.Dados.Nome.Should().Be(nome);
            f.Dados.Tipo.Should().Be("Arma");
            f.Dados.Dano.Should().Be(6);
            f.Dados.Rank.Should().Be("F");
            f.Dados.Empunhadura.Should().Be("UmaMao");
            f.Dados.Dados.Should().Be("2D6");
            f.Dados.Critico.Should().Be("19");
            f.Dados.Alcance.Should().Be(2);
            f.Dados.TipoDeDano.Should().Be("Cortante");
            f.Dados.Subcategoria.Should().Be("Espadas");
            f.Dados.Peso.Should().Be(1.5m);
            f.Dados.Preco.Should().Be(50);
            f.Dados.Descricao.Should().Be("Uma lâmina curta e leve.");
            f.Dados.Requisitos!.Vocacao.Should().Be("Campeao");
            f.Dados.Requisitos.Atributos.Should().Equal(new RequisitoMinimoDto("Vigor", 8));
            f.Dados.PenalidadeDeRequisitos!.Atributos.Should().Equal(new PenalidadeLinhaDto("Forca", 2));
            f.Dados.PenalidadeDeRequisitos.Texto.Should().Be("Desvantagem em furtividade");
        }

        Verificar(criado);
        Verificar((await ListarAsync(token)).Single(f => f.Id == criado.Id));
    }

    [Fact]
    public async Task Create_with_a_duplicate_name_and_type_returns_409_and_the_same_name_with_another_type_is_allowed()
    {
        var token = await RegisterAuditorAsync("FixosAuditor2");
        var nome = Unico("Duplicado");
        await CriarAsync(token, ItemGeral(nome));

        var duplicado = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token, ItemGeral(nome)));
        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var outroTipo = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token, Arma(nome)));
        outroTipo.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("Coisa", "Nome", "F", 1, "Tipo de item desconhecido.")]
    [InlineData("Arma", "  ", "F", 1, "Nome é obrigatório.")]
    [InlineData("Arma", "Nome", "ZZ", 1, "Rank inválido")]
    [InlineData("Arma", "Nome", "F", 0, "A penalidade de Atributo deve ser pelo menos 1.")]
    public async Task Create_validates_tipo_nome_rank_and_requirements(string tipo, string nome, string rank, int valorPenalidade, string mensagem)
    {
        var token = await RegisterAuditorAsync($"FixosAuditorVal{Guid.NewGuid():N}");
        var request = Arma(nome) with
        {
            Tipo = tipo, Rank = rank,
            PenalidadeDeRequisitos = new PenalidadeDeEquipamentoDto(Atributos: [new("Forca", valorPenalidade)]),
        };

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(mensagem);
    }

    [Fact]
    public async Task List_filters_by_tipo_and_by_a_case_insensitive_name_fragment()
    {
        var token = await RegisterAuditorAsync("FixosAuditor3");
        var marca = Guid.NewGuid().ToString("N");
        var arma = await CriarAsync(token, Arma($"Lamina {marca}"));
        var corda = await CriarAsync(token, ItemGeral($"Corda {marca}"));
        var armadura = await CriarAsync(token, Armadura($"Gibao {marca}"));

        var porTipo = await ListarAsync(token, $"?tipo=Arma&q={marca}");
        porTipo.Select(f => f.Id).Should().Equal(arma.Id);

        var porFragmento = await ListarAsync(token, $"?q=CORDA%20{marca.ToUpperInvariant()}");
        porFragmento.Select(f => f.Id).Should().Equal(corda.Id);

        var todos = await ListarAsync(token, $"?q={marca}");
        todos.Select(f => f.Id).Should().Equal(corda.Id, armadura.Id, arma.Id); // ordenado por Nome: Corda, Gibao, Lamina

        var invalido = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{Url}?tipo=Coisa", token));
        invalido.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_replaces_the_data_and_clears_DetalhesIncompletos()
    {
        var token = await RegisterAuditorAsync("FixosAuditor4");
        var nome = Unico("Incompleto");
        Guid id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var fixo = new EquipmentKitFixedItem
            {
                Id = Guid.NewGuid(), Nome = nome, Tipo = ItemTipo.Arma, DetalhesIncompletos = true,
                Dados = RuinaRPG.Infrastructure.Items.ItemFactory.Serializar(Arma(nome) with { Dano = null, Rank = null }),
            };
            db.EquipmentKitFixedItems.Add(fixo);
            await db.SaveChangesAsync();
            id = fixo.Id;
        }
        (await ListarAsync(token, $"?q={nome.Replace(" ", "%20")}")).Single().DetalhesIncompletos.Should().BeTrue();

        var novoNome = nome + " Completo";
        var atualizado = Arma(novoNome) with
        {
            Dano = 9, Rank = "E",
            Requisitos = new RequisitosDePassivaDto(Atributos: [new("Vigor", 3)]),
            PenalidadeDeRequisitos = new PenalidadeDeEquipamentoDto(Texto: "Lento"),
        };
        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{id}", token, atualizado));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var depois = (await ListarAsync(token, $"?q={novoNome.Replace(" ", "%20")}")).Single();
        depois.Id.Should().Be(id.ToString());
        depois.DetalhesIncompletos.Should().BeFalse();
        depois.Nome.Should().Be(novoNome);
        depois.Dados.Dano.Should().Be(9);
        depois.Dados.Rank.Should().Be("E");
        depois.Dados.Requisitos!.Atributos.Should().Equal(new RequisitoMinimoDto("Vigor", 3));
        depois.Dados.PenalidadeDeRequisitos!.Texto.Should().Be("Lento");
    }

    [Fact]
    public async Task Update_cannot_change_the_tipo()
    {
        var token = await RegisterAuditorAsync("FixosAuditor5");
        var nome = Unico("Fixo");
        var fixo = await CriarAsync(token, Arma(nome));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{fixo.Id}", token, ItemGeral(nome)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("O Tipo de um item fixo não pode ser alterado.");
    }

    [Fact]
    public async Task Update_of_a_missing_item_returns_404_and_a_name_clash_with_another_row_returns_409()
    {
        var token = await RegisterAuditorAsync("FixosAuditor5b");
        var a = await CriarAsync(token, ItemGeral(Unico("Fixo A")));
        var b = await CriarAsync(token, ItemGeral(Unico("Fixo B")));

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{Guid.NewGuid()}", token, ItemGeral("Qualquer")))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{b.Id}", token, ItemGeral(a.Nome)))).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_of_an_item_used_by_a_kit_or_a_slot_bonus_returns_409_naming_the_kits()
    {
        var token = await RegisterAuditorAsync("FixosAuditor6");
        var usadoNoKit = await CriarAsync(token, ItemGeral(Unico("Usado No Kit")));
        var usadoNoBonus = await CriarAsync(token, ItemGeral(Unico("Usado No Bonus")));
        var nomeKit = Unico("Kit Usa Fixos");
        await CriarKitAsync(token, nomeKit,
            [new EquipmentKitItemInput(usadoNoKit.Id, 1)],
            [new EquipmentKitChoiceSlotInput("Arma", "Arma", ["Arcos"], "F", 1, "Arcos", usadoNoBonus.Id, 5, null)]);

        var r1 = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{usadoNoKit.Id}", token));
        r1.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await r1.Content.ReadAsStringAsync()).Should().Contain(nomeKit);

        var r2 = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{usadoNoBonus.Id}", token));
        r2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await r2.Content.ReadAsStringAsync()).Should().Contain(nomeKit);

        (await ListarAsync(token, $"?q={usadoNoKit.Nome.Replace(" ", "%20")}")).Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_of_an_unused_item_returns_204()
    {
        var token = await RegisterAuditorAsync("FixosAuditor7");
        var fixo = await CriarAsync(token, ItemGeral(Unico("Sem Uso")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{fixo.Id}", token));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListarAsync(token, $"?q={fixo.Nome.Replace(" ", "%20")}")).Should().BeEmpty();
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{fixo.Id}", token))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Kits_lists_the_names_of_the_kits_that_use_the_item_once_each()
    {
        var token = await RegisterAuditorAsync("FixosAuditor8");
        var fixo = await CriarAsync(token, ItemGeral(Unico("Compartilhado")));
        var kitB = Unico("Kit B");
        var kitA = Unico("Kit A");
        // O mesmo item como item fixo e como bônus de slot no mesmo kit: o nome aparece uma vez só.
        await CriarKitAsync(token, kitB,
            [new EquipmentKitItemInput(fixo.Id, 1)],
            [new EquipmentKitChoiceSlotInput("Arma", "Arma", ["Arcos"], "F", 1, "Arcos", fixo.Id, 5, null)]);
        await CriarKitAsync(token, kitA, [new EquipmentKitItemInput(fixo.Id, 2)]);

        var listado = (await ListarAsync(token, $"?q={fixo.Nome.Replace(" ", "%20")}")).Single();

        listado.Kits.Should().Equal(new[] { kitA, kitB }.OrderBy(n => n));
    }

    [Theory]
    [InlineData("1")]
    [InlineData(" Arma")]
    [InlineData("Arma ")]
    [InlineData("arma")]
    public async Task A_tipo_that_is_not_the_exact_enum_name_returns_400_on_post_put_and_get(string tipo)
    {
        var token = await RegisterAuditorAsync($"FixosAuditorTipo{Guid.NewGuid():N}");
        var existente = await CriarAsync(token, ItemGeral(Unico("Existente")));

        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token, Arma(Unico("X")) with { Tipo = tipo }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{existente.Id}", token, ItemGeral(existente.Nome) with { Tipo = tipo }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"{Url}?tipo={Uri.EscapeDataString(tipo)}", token))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_of_an_item_held_only_by_a_soft_deleted_kit_returns_204_and_clears_that_rows_link(bool comoBonus)
    {
        var token = await RegisterAuditorAsync($"FixosAuditorDel{Guid.NewGuid():N}");
        var fixo = await CriarAsync(token, ItemGeral(Unico("Preso")));
        var kit = comoBonus
            ? await CriarKitAsync(token, Unico("Kit Excluido"), [new EquipmentKitItemInput((await CriarAsync(token, ItemGeral(Unico("Outro")))).Id, 1)],
                [new EquipmentKitChoiceSlotInput("Arma", "Arma", ["Arcos"], "F", 1, "Arcos", fixo.Id, 5, null)])
            : await CriarKitAsync(token, Unico("Kit Excluido"), [new EquipmentKitItemInput(fixo.Id, 1)]);

        // Enquanto o kit está vivo, a referência continua dando 409.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{fixo.Id}", token))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.EquipmentKitItems.CountAsync(i => i.FixedItemId == Guid.Parse(fixo.Id))
                + await db.EquipmentKitChoiceSlots.CountAsync(s => s.BonusFixedItemId == Guid.Parse(fixo.Id))).Should().Be(1);
        }

        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/equipment-kits/{kit.Id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{fixo.Id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope2 = _factory.Services.CreateAsyncScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var kitId = Guid.Parse(kit.Id);
        if (comoBonus)
            (await db2.EquipmentKitChoiceSlots.AsNoTracking().SingleAsync(s => s.KitId == kitId)).BonusFixedItemId.Should().BeNull();
        else
            (await db2.EquipmentKitItems.AsNoTracking().SingleAsync(i => i.KitId == kitId)).FixedItemId.Should().BeNull();
        (await db2.EquipmentKitFixedItems.AnyAsync(f => f.Id == Guid.Parse(fixo.Id))).Should().BeFalse();

        // O backfill (make migrate / startup em Development) não pode recriar o item apagado a partir do Nome que a linha do kit excluído guarda.
        await RuinaRPG.Infrastructure.Rules.EquipmentKitFixedItemBackfill.RunAsync(db2);
        (await db2.EquipmentKitFixedItems.AnyAsync(f => f.Nome == fixo.Nome)).Should().BeFalse();
        if (comoBonus)
            (await db2.EquipmentKitChoiceSlots.AsNoTracking().SingleAsync(s => s.KitId == kitId)).BonusFixedItemId.Should().BeNull();
        else
            (await db2.EquipmentKitItems.AsNoTracking().SingleAsync(i => i.KitId == kitId)).FixedItemId.Should().BeNull();
    }
}
