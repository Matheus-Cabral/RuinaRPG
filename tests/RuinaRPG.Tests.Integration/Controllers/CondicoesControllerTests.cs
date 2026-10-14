using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CondicoesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CondicoesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAsync(string nickname, string email, bool auditor)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        if (auditor)
            await GrantRulesAuditorAsync(email);
        return token;
    }

    private static SalvarCondicaoRequest Valida(string nome = "Envenenado") => new(nome, "Perde vida e vigor.", "Veneno", 3,
    [
        new CondicaoModificadorDto("RecursoAtual", "Vitalidade", "Fixo", -2, "ACadaVez"),
        new CondicaoModificadorDto("Atributo", "Vigor", "Percentual", -10, null),
    ],
    ["Magia", "Runa"]);

    private async Task<List<CondicaoResponse>> ListAsync(string token, string? nome = null) =>
        (await (await _client.SendAsync(Authed(HttpMethod.Get, "/api/condicoes" + (nome is null ? "" : $"?nome={nome}"), token))).Content.ReadFromJsonAsync<List<CondicaoResponse>>())!;

    [Fact]
    public async Task Crud_round_trip_with_two_modifiers_and_two_disables()
    {
        var token = await RegisterGmAsync("CondGm1", "cond1@teste.com", true);

        var create = await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida()));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = (await create.Content.ReadFromJsonAsync<CondicaoResponse>())!;
        criada.Modificadores.Should().HaveCount(2);

        var listada = (await ListAsync(token)).Single(c => c.Id == criada.Id);
        listada.Nome.Should().Be("Envenenado");
        listada.Icone.Should().Be("Veneno");
        listada.DuracaoPadraoTurnos.Should().Be(3);
        listada.Modificadores.Select(m => (m.Alvo, m.AlvoEspecifico, m.TipoDeValor, m.Valor, m.Momento)).Should().Equal(
            ("RecursoAtual", "Vitalidade", "Fixo", -2, "ACadaVez"), ("Atributo", "Vigor", "Percentual", -10, null));
        listada.Desabilitacoes.Should().BeEquivalentTo("Magia", "Runa");

        var novo = new SalvarCondicaoRequest("Envenenado grave", null, "Fogo", null,
            [new CondicaoModificadorDto("Dano", null, "Fixo", 2, null)], ["Arma"]);
        (await _client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{criada.Id}", token, novo))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var editada = (await ListAsync(token)).Single(c => c.Id == criada.Id);
        editada.Nome.Should().Be("Envenenado grave");
        editada.DuracaoPadraoTurnos.Should().BeNull();
        editada.Modificadores.Should().ContainSingle().Which.Alvo.Should().Be("Dano");
        editada.Desabilitacoes.Should().Equal("Arma");

        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{criada.Id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(token)).Should().NotContain(c => c.Id == criada.Id);
    }

    [Fact]
    public async Task A_non_Auditor_reads_but_cannot_write()
    {
        var auditor = await RegisterGmAsync("CondGm2", "cond2@teste.com", true);
        var gm = await RegisterGmAsync("CondGm3", "cond3@teste.com", false);
        var criada = (await (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", auditor, Valida("Lenta")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;

        (await _client.SendAsync(Authed(HttpMethod.Get, "/api/condicoes", gm))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync(gm, "Lent")).Should().Contain(c => c.Id == criada.Id);
        (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", gm, Valida("Outra")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{criada.Id}", gm, Valida("Outra")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{criada.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_duplicate_name_is_rejected_but_reusable_after_a_soft_delete()
    {
        var token = await RegisterGmAsync("CondGm4", "cond4@teste.com", true);
        var primeira = (await (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Repetida")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;

        (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Repetida")))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{primeira.Id}", token));
        (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Repetida")))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_invalid_payload_returns_400_with_the_validator_message()
    {
        var token = await RegisterGmAsync("CondGm5", "cond5@teste.com", true);
        var semMomento = Valida("Sem momento") with { Modificadores = [new CondicaoModificadorDto("RecursoAtual", "Vitalidade", "Fixo", -2, null)] };

        var response = await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, semMomento));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("momento");
    }

    [Fact]
    public async Task An_unknown_icon_or_pericia_is_rejected_and_an_existing_pericia_accepted()
    {
        var token = await RegisterGmAsync("CondGm6", "cond6@teste.com", true);

        (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Icone ruim") with { Icone = "Banana" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var pericia = (n: "Pericia ruim", chave: "Inexistente");
        var ruim = Valida(pericia.n) with { Modificadores = [new CondicaoModificadorDto("Pericia", pericia.chave, "Fixo", -1, null)] };
        (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, ruim))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var boa = Valida("Pericia boa") with { Modificadores = [new CondicaoModificadorDto("Pericia", "Prontidao", "Fixo", -1, null)] };
        (await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, boa))).StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
