using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CompendioControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CompendioControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
    public async Task Search_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/compendio/search?q=ambidestria");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_finds_a_known_characteristic_by_name()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm1", "compendio1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search?q=Ambidestria", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Should().Contain(r => r.Categoria == "Caracteristica" && r.Titulo == "Ambidestria");
    }

    [Fact]
    public async Task Search_finds_a_known_effect_by_name()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm2", "compendio2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search?q=Aumentar+Armadura", token));

        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Should().Contain(r => r.Categoria == "Efeito" && r.Titulo == "Aumentar Armadura");
    }

    [Fact]
    public async Task Search_can_filter_to_a_single_category()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm3", "compendio3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search?categorias=Caracteristica", token));

        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Should().OnlyContain(r => r.Categoria == "Caracteristica");
    }

    [Fact]
    public async Task Search_with_no_query_and_no_filter_returns_results_from_multiple_categories()
    {
        var token = await RegisterGmAndGetTokenAsync("CompendioGm4", "compendio4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/compendio/search", token));

        var body = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        body!.Select(r => r.Categoria).Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Search_never_returns_a_creature_exclusive_characteristic_even_by_its_exact_Nome()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CompendioCreatureExclusiveGm", "compendiocreatureexclusive@teste.com");
        const string nome = "Casco Reforcado De Teste Unico";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            db.Set<RuinaRPG.Infrastructure.Rules.CreatureExclusiveTrait>().Add(new RuinaRPG.Infrastructure.Rules.CreatureExclusiveTrait
            {
                Id = Guid.NewGuid(),
                Nome = nome,
                Descricao = "Descrição de teste.",
                Custo = 2,
                Polaridade = RuinaRPG.Domain.Enums.Polaridade.Positiva,
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/compendio/search?q={Uri.EscapeDataString(nome)}", gmToken));

        var results = await response.Content.ReadFromJsonAsync<List<CompendioSearchResultResponse>>();
        results!.Should().NotContain(r => r.Titulo == nome);
    }
}
