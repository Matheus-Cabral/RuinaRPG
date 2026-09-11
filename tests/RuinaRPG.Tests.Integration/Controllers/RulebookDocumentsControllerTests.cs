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

public class RulebookDocumentsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RulebookDocumentsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RuinaRPG.Contracts.Auth.RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_returns_the_3_documents_all_default_when_no_override_exists()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm1", "rulebookdocgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        body!.Select(d => d.Slug).Should().BeEquivalentTo("sistema-basico", "graus-e-circulos", "tabela-de-niveis");
        body!.Should().OnlyContain(d => d.IsDefault);
        body!.Should().OnlyContain(d => !string.IsNullOrEmpty(d.MarkdownText)); // the embedded default text, non-empty
    }

    [Fact]
    public async Task List_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm2", "rulebookdocgm2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/sistema-basico", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Novo texto")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateDocument_persists_the_override_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm3", "rulebookdocgm3@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm3@teste.com");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/sistema-basico", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Sistema Básico Editado\n\nTexto novo do Auditor.")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        var edited = body!.Single(d => d.Slug == "sistema-basico");
        edited.IsDefault.Should().BeFalse();
        edited.MarkdownText.Should().Contain("Texto novo do Auditor.");
    }

    [Fact]
    public async Task UpdateDocument_with_an_unknown_slug_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm4", "rulebookdocgm4@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/caracteristicas", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Não permitido")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteDocument_reverts_to_the_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm5", "rulebookdocgm5@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm5@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/tabela-de-niveis", gmToken,
            new UpdateRulebookDocumentOverrideRequest("| Custom |")));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/tabela-de-niveis", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        body!.Single(d => d.Slug == "tabela-de-niveis").IsDefault.Should().BeTrue();
    }
}
