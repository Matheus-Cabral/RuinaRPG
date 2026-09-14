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

public class EfeitosControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EfeitosControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return email;
    }

    [Fact]
    public async Task List_is_seeded_with_51_effects_and_is_open_to_any_authenticated_GM()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm1", "efeito1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var efeitos = await response.Content.ReadFromJsonAsync<List<EfeitoResponse>>();
        efeitos!.Should().HaveCount(51);
        efeitos.Should().ContainSingle(e => e.Nome == "Detrito" && e.PreRequisitos.Any(g => g.Contains("Congelar")));
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm2", "efeito2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", gmToken,
            new CreateEfeitoRequest("Selar", 4, "Descrição de teste.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_by_the_Auditor_adds_a_new_effect_the_seeder_never_defines()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm3", "efeito3@teste.com");
        await GrantRulesAuditorAsync("efeito3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", gmToken,
            new CreateEfeitoRequest("Selar", 4, "Sela o alvo, impedindo certas ações.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        (await listResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Should().Contain(e => e.Nome == "Selar");
    }

    [Fact]
    public async Task Update_by_the_Auditor_changes_the_row_and_survives_a_reseed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm4", "efeito4@teste.com");
        await GrantRulesAuditorAsync("efeito4@teste.com");
        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        var cura = (await listResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Single(e => e.Nome == "Cura");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{cura.Id}", gmToken,
            new UpdateEfeitoRequest("Cura", 1, "Descrição custom.", "Fixo", 99, null, null, null, null, false, null, null, null, [["Dano"]])));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.Set<RuinaRPG.Infrastructure.Rules.Efeito>().SingleAsync(e => e.Nome == "Cura")).IsCustomized.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_a_seeded_effect_that_is_unused_removes_it_from_the_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm5", "efeito5@teste.com");
        await GrantRulesAuditorAsync("efeito5@teste.com");
        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        var amplificacaoArcana = (await listResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Single(e => e.Nome == "Amplificação Arcana");

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/efeitos/{amplificacaoArcana.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        (await afterResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Should().NotContain(e => e.Nome == "Amplificação Arcana");
    }
}
