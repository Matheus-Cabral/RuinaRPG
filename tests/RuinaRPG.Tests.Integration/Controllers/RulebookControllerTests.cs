using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RulebookControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RulebookControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    [Fact]
    public async Task Get_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/rulebook");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_returns_the_four_documents_with_rendered_html()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm1", "rulebook1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        body!.Select(d => d.Slug).Should().Equal(
            "caracteristicas", "sistema-basico", "graus-e-circulos", "tabela-de-niveis");
        body!.Single(d => d.Slug == "tabela-de-niveis").Html.Should().Contain("<table");
        body!.Where(d => d.Slug != "tabela-de-niveis").Should().OnlyContain(d => d.Html.Contains("<h"));
    }

    [Fact]
    public async Task GrausECirculos_html_includes_the_two_reference_images()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm2", "rulebook2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var grausECirculos = body!.Single(d => d.Slug == "graus-e-circulos");
        grausECirculos.Html.Should().Contain("/rulebook/Escolas_de_Magia.png");
        grausECirculos.Html.Should().Contain("/rulebook/Matriz_Elemental.png");
    }
}
