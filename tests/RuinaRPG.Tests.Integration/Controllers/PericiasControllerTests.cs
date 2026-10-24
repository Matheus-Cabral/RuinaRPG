using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class PericiasControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await Client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await Client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> RegisterAuditorAsync(string nickname, string email)
    {
        var token = await RegisterGmAndGetTokenAsync(nickname, email);
        await GrantRulesAuditorAsync(email);
        return token;
    }

    [Fact]
    public async Task List_returns_the_active_pericias_ordered_by_name_for_any_authenticated_user()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PerGm1", "per1@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "PerJog1", "perjog1@teste.com");

        var body = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias", jogadorToken)))
            .Content.ReadFromJsonAsync<List<PericiaResponse>>();

        body.Should().HaveCountGreaterThanOrEqualTo(39);
        body!.Select(p => p.Nome).Should().BeInAscendingOrder(StringComparer.CurrentCulture);
        body!.Single(p => p.Chave == "Prontidao").Protegida.Should().BeTrue();
        body!.Single(p => p.Chave == "Atletismo").Protegida.Should().BeFalse();
    }

    [Fact]
    public async Task Auditor_endpoints_return_403_for_a_non_auditor_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PerGm2", "per2@teste.com");

        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias/auditoria", gmToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", gmToken, new SalvarPericiaRequest("Nova", null, null, false)))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/pericias/7", gmToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_generates_a_key_and_the_next_id()
    {
        var token = await RegisterAuditorAsync("PerAud3", "peraud3@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token,
            new SalvarPericiaRequest("Navegação Aérea", "Pilotar aeronaves.", "Destreza", true)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        body!.Chave.Should().StartWith("NavegacaoAerea");
        body.Id.Should().BeGreaterThanOrEqualTo(39);
        body.AtributoSugerido.Should().Be("Destreza");
        body.DisponivelParaCriaturas.Should().BeTrue();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Atletismo")]
    [InlineData("atletismo")]
    public async Task Create_with_a_blank_or_duplicate_active_name_returns_400(string nome)
    {
        var token = await RegisterAuditorAsync($"PerAud4{nome.Trim().Length}{nome.GetHashCode() & 0xffff}", $"peraud4{nome.Trim().Length}{nome.GetHashCode() & 0xffff}@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest(nome, null, null, false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_an_unknown_attribute_returns_400()
    {
        var token = await RegisterAuditorAsync("PerAud5", "peraud5@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Xadrez", null, "Sorte", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_changes_fields_but_never_the_key()
    {
        var token = await RegisterAuditorAsync("PerAud6", "peraud6@teste.com");
        var created = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Cartografia", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        var updated = await (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/pericias/{created!.Id}", token,
            new SalvarPericiaRequest("Cartografia Arcana", "Mapas mágicos.", "Astucia", true)))).Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        updated!.Nome.Should().Be("Cartografia Arcana");
        updated.Chave.Should().Be(created.Chave);
        updated.Descricao.Should().Be("Mapas mágicos.");
    }

    [Fact]
    public async Task Update_of_a_protected_pericia_keeps_it_available_for_criaturas()
    {
        var token = await RegisterAuditorAsync("PerAud7", "peraud7@teste.com");

        var updated = await (await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/17", token,
            new SalvarPericiaRequest("Fortitude", "Resistir a venenos.", "Vigor", false)))).Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        updated!.DisponivelParaCriaturas.Should().BeTrue();
        updated.Descricao.Should().Be("Resistir a venenos.");
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/17", token, new SalvarPericiaRequest("Fortitude", null, null, true)));
    }

    [Fact]
    public async Task Delete_of_a_protected_pericia_returns_400()
    {
        var token = await RegisterAuditorAsync("PerAud8", "peraud8@teste.com");

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/pericias/32", token))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_hides_it_from_List_and_Restore_brings_it_back()
    {
        var token = await RegisterAuditorAsync("PerAud9", "peraud9@teste.com");
        var created = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Heráldica", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{created!.Id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var ativas = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias", token))).Content.ReadFromJsonAsync<List<PericiaResponse>>();
        ativas!.Should().NotContain(p => p.Id == created.Id);
        var todas = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias/auditoria", token))).Content.ReadFromJsonAsync<List<PericiaAuditoriaResponse>>();
        todas!.Single(p => p.Id == created.Id).IsDeleted.Should().BeTrue();

        var restored = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{created.Id}/restaurar", token));
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        ativas = await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias", token))).Content.ReadFromJsonAsync<List<PericiaResponse>>();
        ativas!.Should().Contain(p => p.Id == created.Id);
    }

    [Fact]
    public async Task Restore_when_an_active_pericia_already_has_that_name_returns_400()
    {
        var token = await RegisterAuditorAsync("PerAud10", "peraud10@teste.com");
        var first = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Genealogia", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        await Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{first!.Id}", token));
        var second = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Genealogia", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        second!.Chave.Should().NotBe(first.Chave);

        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{first.Id}/restaurar", token))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        var token = await RegisterAuditorAsync("PerAud11", "peraud11@teste.com");

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/9999", token, new SalvarPericiaRequest("X", null, null, false)))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/pericias/9999", token))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
