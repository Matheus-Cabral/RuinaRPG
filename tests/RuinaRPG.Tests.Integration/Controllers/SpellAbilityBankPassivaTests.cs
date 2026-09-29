using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SpellAbilityBankPassivaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SpellAbilityBankPassivaTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private static RequisitosDePassivaDto RequisitosCompletos() => new(
        Nivel: 3, Vocacao: "Feiticeiro", Classe: "Elementalista", Linhagem: "Humano", Variante: "Sinir",
        Graduacao: 2, CoracaoDeMana: true, Afinidade: "Fogo", Estrela: "Liora",
        Atributos: [new("Forca", 4)], SubAtributos: [new("Iniciativa", 2)], Pericias: [new("Atletismo", 5)]);

    private Task<HttpResponseMessage> PostAsync(string token, CreateSpellAbilityEntryRequest body) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", token, body));

    [Fact]
    public async Task Creates_a_passiva_with_categoria_and_requisitos_and_lists_it_under_the_Passiva_filter()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassivaBankGm1", "passivabank1@teste.com");

        var response = await PostAsync(gm, new CreateSpellAbilityEntryRequest("Pele de Pedra", "Passiva", 0, "Resiste.", [], false, "Vocacional", RequisitosCompletos()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!;
        body.Tipo.Should().Be("Passiva");
        body.Categoria.Should().Be("Vocacional");
        body.Grau.Should().Be(0);
        body.GastoEmPI.Should().Be(0);
        body.Requisitos.Should().BeEquivalentTo(RequisitosCompletos() with { HistoricoId = null });

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?tipo=Passiva", gm)))
            .Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        list!.Should().ContainSingle(e => e.Nome == "Pele de Pedra");
    }

    [Fact]
    public async Task A_passiva_without_requisitos_is_valid()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassivaBankGm2", "passivabank2@teste.com");
        var response = await PostAsync(gm, new CreateSpellAbilityEntryRequest("Livre", "Passiva", 0, "d", [], false, "Livre", null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    public static TheoryData<CreateSpellAbilityEntryRequest> Invalidos => new()
    {
        new("Sem categoria", "Passiva", 0, "d", [], false, null, null),
        new("Categoria ruim", "Passiva", 0, "d", [], false, "Nenhuma", null),
        new("Com grau", "Passiva", 2, "d", [], false, "Livre", null),
        new("Com efeito", "Passiva", 0, "d", [new SpellAbilityEffectRequest("Dano", 1, 2)], false, "Livre", null),
        new("Magia com categoria", "Magia", 1, "d", [], false, "Livre", null),
        new("Magia com requisitos", "Magia", 1, "d", [], false, null, new RequisitosDePassivaDto(Nivel: 1)),
        new("Classe sem vocação", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Classe: "Duelista")),
        new("Variante sem linhagem", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Variante: "Sinir")),
        new("Variante de outra linhagem", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Linhagem: "Phylauc", Variante: "Sinir")),
        new("Atributo duplicado", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Atributos: [new("Forca", 1), new("Forca", 2)])),
        new("Atributo desconhecido", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Atributos: [new("Ego", 1)])),
        new("Mínimo negativo", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Pericias: [new("Atletismo", -1)])),
        new("Histórico inexistente", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(HistoricoId: Guid.NewGuid().ToString())),
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task Rejects_invalid_passiva_combinations(CreateSpellAbilityEntryRequest request)
    {
        var gm = await RegisterGmAndGetTokenAsync($"PassivaInv{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@teste.com");
        (await PostAsync(gm, request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_can_turn_a_passiva_back_into_a_magia_when_categoria_and_requisitos_are_cleared()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassivaBankGm3", "passivabank3@teste.com");
        var created = (await (await PostAsync(gm, new CreateSpellAbilityEntryRequest("P", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Nivel: 2))))
            .Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!;

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/spell-ability-bank/{created.Id}", gm,
            new UpdateSpellAbilityEntryRequest("P", "Magia", 1, "d", [], false, null, null)));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", gm))).Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        var entry = list!.Single(e => e.Id == created.Id);
        entry.Tipo.Should().Be("Magia");
        entry.Categoria.Should().BeNull();
        entry.Requisitos.Should().BeNull();
    }
}
