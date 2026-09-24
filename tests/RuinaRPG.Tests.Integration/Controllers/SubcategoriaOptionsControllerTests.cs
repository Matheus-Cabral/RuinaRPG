using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SubcategoriaOptionsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SubcategoriaOptionsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Copied verbatim from HistoricosControllerTests.cs's established pattern — no grant endpoint
    // exists, real grants happen via `make grant-rules-auditor`.
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_is_open_to_any_authenticated_caller_and_filters_by_Tipo_and_Facet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm1", "subcat1@teste.com");
        await GrantRulesAuditorAsync("subcat1@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Varinha")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Categoria", "Mágica")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Armadura", "Familia", "Couro")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/subcategoria-options?tipo=Arma&facet=Familia", gmToken));

        var options = await response.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>();
        options!.Should().ContainSingle(o => o.Valor == "Varinha");
        options.Should().NotContain(o => o.Valor == "Mágica" || o.Valor == "Couro");
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_GM_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm2", "subcat2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Arcos")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_rejects_Tipo_ItemGeral_and_an_unknown_Facet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm3", "subcat3@teste.com");
        await GrantRulesAuditorAsync("subcat3@teste.com");

        var badTipo = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("ItemGeral", "Familia", "X")));
        badTipo.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badFacet = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "NaoExiste", "X")));
        badFacet.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // The composed Subcategoria string has no escaping (SubcategoriaBuilder joins 4 segments with
    // " - "), so a Valor containing the separator would corrupt the format — reject it up front.
    [Fact]
    public async Task Create_rejects_a_Valor_containing_the_SubcategoriaBuilder_Separator()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm6", "subcat6@teste.com");
        await GrantRulesAuditorAsync("subcat6@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Adaga - Curva")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_by_a_non_Auditor_GM_returns_403_and_by_the_Auditor_soft_deletes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm4", "subcat4@teste.com");
        await GrantRulesAuditorAsync("subcat4@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Escudo", "Familia", "Rodela"))))
            .Content.ReadFromJsonAsync<SubcategoriaOptionResponse>();

        var otherGmToken = await RegisterGmAndGetTokenAsync("SubcatGm5", "subcat5@teste.com");
        var forbidden = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/subcategoria-options/{created!.Id}", otherGmToken));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleted = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/subcategoria-options/{created.Id}", gmToken));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/subcategoria-options?tipo=Escudo&facet=Familia", gmToken));
        var options = await listResponse.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>();
        options!.Should().NotContain(o => o.Id == created.Id);
    }
}
