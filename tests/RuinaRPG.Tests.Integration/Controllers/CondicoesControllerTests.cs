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

public class CondicoesControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
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
        var response = await Client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
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
        (await (await Client.SendAsync(Authed(HttpMethod.Get, "/api/condicoes" + (nome is null ? "" : $"?nome={nome}"), token))).Content.ReadFromJsonAsync<List<CondicaoResponse>>())!;

    [Theory]
    [InlineData("Hipnotizado")]
    [InlineData("Carregando")]
    public async Task A_key_from_the_larger_icon_set_is_accepted_and_kept(string icone)
    {
        var token = await RegisterGmAsync($"CondGmIc{icone}", $"condic{icone.ToLowerInvariant()}@teste.com", true);

        var create = await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida($"Com ícone {icone}") with { Icone = icone }));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ListAsync(token)).Single(c => c.Nome == $"Com ícone {icone}").Icone.Should().Be(icone);
    }

    [Fact]
    public async Task Crud_round_trip_with_two_modifiers_and_two_disables()
    {
        var token = await RegisterGmAsync("CondGm1", "cond1@teste.com", true);

        var create = await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida()));
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
        (await Client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{criada.Id}", token, novo))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var editada = (await ListAsync(token)).Single(c => c.Id == criada.Id);
        editada.Nome.Should().Be("Envenenado grave");
        editada.DuracaoPadraoTurnos.Should().BeNull();
        editada.Modificadores.Should().ContainSingle().Which.Alvo.Should().Be("Dano");
        editada.Desabilitacoes.Should().Equal("Arma");

        (await Client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{criada.Id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(token)).Should().NotContain(c => c.Id == criada.Id);
    }

    [Fact]
    public async Task A_non_Auditor_reads_but_cannot_write()
    {
        var auditor = await RegisterGmAsync("CondGm2", "cond2@teste.com", true);
        var gm = await RegisterGmAsync("CondGm3", "cond3@teste.com", false);
        var criada = (await (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", auditor, Valida("Lenta")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;

        (await Client.SendAsync(Authed(HttpMethod.Get, "/api/condicoes", gm))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync(gm, "Lent")).Should().Contain(c => c.Id == criada.Id);
        (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", gm, Valida("Outra")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{criada.Id}", gm, Valida("Outra")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{criada.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_duplicate_name_is_rejected_but_reusable_after_a_soft_delete()
    {
        var token = await RegisterGmAsync("CondGm4", "cond4@teste.com", true);
        var primeira = (await (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Repetida")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;

        (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Repetida")))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await Client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{primeira.Id}", token));
        (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Repetida")))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_invalid_payload_returns_400_with_the_validator_message()
    {
        var token = await RegisterGmAsync("CondGm5", "cond5@teste.com", true);
        var semMomento = Valida("Sem momento") with { Modificadores = [new CondicaoModificadorDto("RecursoAtual", "Vitalidade", "Fixo", -2, null)] };

        var response = await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, semMomento));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("momento");
    }

    [Fact]
    public async Task An_unknown_icon_or_pericia_is_rejected_and_an_existing_pericia_accepted()
    {
        var token = await RegisterGmAsync("CondGm6", "cond6@teste.com", true);

        (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Icone ruim") with { Icone = "Banana" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var pericia = (n: "Pericia ruim", chave: "Inexistente");
        var ruim = Valida(pericia.n) with { Modificadores = [new CondicaoModificadorDto("Pericia", pericia.chave, "Fixo", -1, null)] };
        (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, ruim))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var boa = Valida("Pericia boa") with { Modificadores = [new CondicaoModificadorDto("Pericia", "Prontidao", "Fixo", -1, null)] };
        (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, boa))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Delete_is_a_soft_delete_that_keeps_the_children_and_hides_the_row_from_later_calls()
    {
        var token = await RegisterGmAsync("CondGm7", "cond7@teste.com", true);
        var criada = (await (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Soft")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;
        var id = Guid.Parse(criada.Id);

        (await Client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.Condicoes.SingleAsync(c => c.Id == id)).IsDeleted.Should().BeTrue();
            (await db.CondicaoModificadores.CountAsync(m => m.CondicaoId == id)).Should().Be(2);
            (await db.CondicaoDesabilitacoes.CountAsync(d => d.CondicaoId == id)).Should().Be(2);
        }

        (await Client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{id}", token))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{id}", token, Valida("Soft")))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_nome_query_excludes_conditions_that_do_not_match()
    {
        var token = await RegisterGmAsync("CondGm8", "cond8@teste.com", true);
        var alfa = (await (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Filtro Alfa")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;
        var beta = (await (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Outra Beta")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;

        var filtradas = await ListAsync(token, "alfa");

        filtradas.Should().Contain(c => c.Id == alfa.Id);
        filtradas.Should().NotContain(c => c.Id == beta.Id);
    }

    [Fact]
    public async Task Update_onto_another_conditions_name_returns_400_but_keeping_its_own_name_returns_204()
    {
        var token = await RegisterGmAsync("CondGm9", "cond9@teste.com", true);
        await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Nome Um")));
        var dois = (await (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, Valida("Nome Dois")))).Content.ReadFromJsonAsync<CondicaoResponse>())!;

        (await Client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{dois.Id}", token, Valida("Nome Um")))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{dois.Id}", token, Valida("Nome Dois")))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public static IEnumerable<object[]> PayloadsComEnumInvalido() =>
    [
        [Valida("Enum ruim") with { Modificadores = [new CondicaoModificadorDto("Inventado", "Vigor", "Fixo", -1, null)] }],
        [Valida("Enum ruim") with { Modificadores = [new CondicaoModificadorDto("Atributo", "Vigor", "Inventado", -1, null)] }],
        [Valida("Enum ruim") with { Modificadores = [new CondicaoModificadorDto("RecursoAtual", "Vitalidade", "Fixo", -1, "Inventado")] }],
        [Valida("Enum ruim") with { Desabilitacoes = ["Inventada"] }],
        [Valida("Enum ruim") with { Modificadores = [null!] }],
        [Valida("Enum ruim") with { Desabilitacoes = [null!] }],
    ];

    [Theory]
    [MemberData(nameof(PayloadsComEnumInvalido))]
    public async Task An_invalid_enum_string_or_a_null_element_returns_400(SalvarCondicaoRequest payload)
    {
        var token = await RegisterGmAsync("CondGmEnum" + Guid.NewGuid().ToString("N")[..6], $"condenum{Guid.NewGuid():N}@teste.com", true);

        (await Client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", token, payload))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_and_delete_of_an_unknown_id_return_404()
    {
        var token = await RegisterGmAsync("CondGm10", "cond10@teste.com", true);

        (await Client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{Guid.NewGuid()}", token, Valida("Fantasma")))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{Guid.NewGuid()}", token))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unauthenticated_GET_returns_401()
    {
        (await Client.GetAsync("/api/condicoes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
