using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Creating/editing/deleting an Efeito on the Auditoria keeps its "## Nome" block in the Livro de
/// Regras' Graus & Círculos document in sync — see
/// docs/superpowers/specs/2026-09-29-efeitos-no-livro-de-regras-design.md.
/// </summary>
public class EfeitosNoLivroDeRegrasTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EfeitosNoLivroDeRegrasTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterAuditorAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return token;
    }

    private async Task<RulebookDocumentResponse> GetLivroAsync()
    {
        var response = await _client.GetAsync("/api/rulebook/graus-e-circulos");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<RulebookDocumentResponse>())!;
    }

    /// <summary>Grau N is the Nth `#` section of the document.</summary>
    private static string GrauHtml(RulebookDocumentResponse livro, int grau) => livro.Sections[grau - 1].Html;

    private static string AllHtml(RulebookDocumentResponse livro) =>
        (livro.IntroHtml ?? "") + string.Join("\n", livro.Sections.Select(s => s.Html));

    private async Task<string> GetMarkdownAsync(string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", token));
        var docs = await response.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        return docs!.Single(d => d.Slug == "graus-e-circulos").MarkdownText.Replace("\r\n", "\n");
    }

    private async Task<EfeitoResponse> GetEfeitoAsync(string token, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", token));
        var efeitos = await response.Content.ReadFromJsonAsync<List<EfeitoResponse>>();
        return efeitos!.Single(e => e.Nome == nome);
    }

    /// <summary>The "## nome" block's text: its heading through the line before the next `#`/`##` heading.</summary>
    private static string Bloco(string md, string nome)
    {
        var match = Regex.Match(md, $@"^## {Regex.Escape(nome)}[ \t]*\n(.*?)(?=^#|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        match.Success.Should().BeTrue($"the Markdown should contain a \"## {nome}\" block");
        return match.Value;
    }

    [Fact]
    public async Task Create_edit_and_delete_an_Efeito_keeps_its_block_in_the_right_Grau_section()
    {
        var token = await RegisterAuditorAsync("LivroEfeito1", "livroefeito1@teste.com");

        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest("Chama Viva", 2, "Uma chama que não se apaga.", "Fixo", 3, null, null, null, null, false, null, null, null, [])));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<EfeitoResponse>())!.Id;

        var livro = await GetLivroAsync();
        GrauHtml(livro, 2).Should().Contain("Chama Viva").And.Contain("3 PI");
        GrauHtml(livro, 1).Should().NotContain("Chama Viva");

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{id}", token,
            new UpdateEfeitoRequest("Chama Eterna", 3, "Uma chama que não se apaga.", "Fixo", 3, null, null, null, null, false, null, null, null, [])));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        livro = await GetLivroAsync();
        GrauHtml(livro, 2).Should().NotContain("Chama Viva").And.NotContain("Chama Eterna");
        GrauHtml(livro, 3).Should().Contain("Chama Eterna");
        AllHtml(livro).Should().NotContain("Chama Viva");

        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/efeitos/{id}", token));
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        livro = await GetLivroAsync();
        AllHtml(livro).Should().NotContain("Chama Eterna");
    }

    [Fact]
    public async Task Editing_an_original_Efeito_rewrites_only_its_own_block()
    {
        var token = await RegisterAuditorAsync("LivroEfeito2", "livroefeito2@teste.com");
        var antes = await GetMarkdownAsync(token);
        var cura = await GetEfeitoAsync(token, "Cura");

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{cura.Id}", token,
            new UpdateEfeitoRequest("Cura", 1, "Descrição nova da Cura pelo Auditor.", "Fixo", 2, null, null, null, null, false, null, null, null, [["Dano"]])));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var depois = await GetMarkdownAsync(token);
        Bloco(depois, "Cura").Should().Contain("Descrição nova da Cura pelo Auditor.");
        // Everything outside the Cura block is byte-for-byte unchanged.
        depois.Replace(Bloco(depois, "Cura"), "<<CURA>>").Should().Be(antes.Replace(Bloco(antes, "Cura"), "<<CURA>>"));
        GrauHtml(await GetLivroAsync(), 1).Should().Contain("Descrição nova da Cura pelo Auditor.");
    }

    [Fact]
    public async Task Editing_a_shared_basic_Efeito_leaves_the_Livro_untouched()
    {
        var token = await RegisterAuditorAsync("LivroEfeito3", "livroefeito3@teste.com");
        var antes = await GetMarkdownAsync(token);
        var dano = await GetEfeitoAsync(token, "Dano");

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{dano.Id}", token,
            new UpdateEfeitoRequest("Dano", 1, "Descrição custom do Dano.", "PorUnidade", null, 2, "Dado", null, null, false, null, null, null, [])));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetMarkdownAsync(token)).Should().Be(antes);
    }

    [Fact]
    public async Task Creating_an_Efeito_keeps_the_Auditors_hand_edits_to_the_Livro()
    {
        var token = await RegisterAuditorAsync("LivroEfeito4", "livroefeito4@teste.com");
        var md = await GetMarkdownAsync(token);
        var editado = md.Replace("## Cura\n", "Parágrafo extra escrito à mão pelo Auditor.\n\n## Cura\n");
        editado.Should().NotBe(md);
        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/graus-e-circulos", token,
            new UpdateRulebookDocumentOverrideRequest(editado)));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest("Selo Rúnico", 4, "Sela uma runa.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var depois = await GetMarkdownAsync(token);
        depois.Should().Contain("Parágrafo extra escrito à mão pelo Auditor.");
        GrauHtml(await GetLivroAsync(), 4).Should().Contain("Selo Rúnico");
    }

    [Fact]
    public async Task Creating_an_Efeito_whose_heading_was_already_written_by_hand_adds_no_duplicate()
    {
        var token = await RegisterAuditorAsync("LivroEfeito5", "livroefeito5@teste.com");
        var md = await GetMarkdownAsync(token);
        var editado = md.Replace("## Aceleração\n", "## Chama Rubra\n\nEscrito à mão.\n\n## Aceleração\n");
        editado.Should().NotBe(md);
        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/graus-e-circulos", token,
            new UpdateRulebookDocumentOverrideRequest(editado)));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest("Chama Rubra", 2, "Chama vermelha.", "Fixo", 3, null, null, null, null, false, null, null, null, [])));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var depois = await GetMarkdownAsync(token);
        Regex.Matches(depois, @"^## Chama Rubra[ \t]*$", RegexOptions.Multiline).Should().HaveCount(1);
    }

    [Fact]
    public async Task Startup_pass_adds_a_missing_catalog_Efeito_once_and_is_idempotent()
    {
        var token = await RegisterAuditorAsync("LivroEfeito6", "livroefeito6@teste.com");
        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.Efeitos.Add(new Efeito
            {
                Id = Guid.NewGuid(), Nome = "Eco Distante", Grau = 5, Descricao = "Um eco que chega de longe.",
                TipoDeCusto = TipoDeCusto.Fixo, CustoFixo = 4, PreRequisitosJson = JsonSerializer.Serialize(new List<List<string>>()),
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sync = scope.ServiceProvider.GetRequiredService<LivroDeRegrasEfeitosSync>();
            (await sync.SincronizarFaltantesAsync()).Should().Be(1);
        }

        GrauHtml(await GetLivroAsync(), 5).Should().Contain("Eco Distante");
        DateTime updatedAt;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var over = await db.RulebookDocumentOverrides.AsNoTracking().SingleAsync(o => o.Slug == "graus-e-circulos");
            over.UpdatedByUserId.Should().BeNull();
            updatedAt = over.UpdatedAt;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sync = scope.ServiceProvider.GetRequiredService<LivroDeRegrasEfeitosSync>();
            (await sync.SincronizarFaltantesAsync()).Should().Be(0);
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.RulebookDocumentOverrides.AsNoTracking().SingleAsync(o => o.Slug == "graus-e-circulos")).UpdatedAt.Should().Be(updatedAt);
        }

        (await GetMarkdownAsync(token)).Should().Contain("## Eco Distante");
    }

    [Fact]
    public async Task Restoring_the_default_Livro_still_contains_every_catalog_Efeito()
    {
        var token = await RegisterAuditorAsync("LivroEfeito7", "livroefeito7@teste.com");
        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest("Brasa Oculta", 6, "Brasa escondida.", "Fixo", 5, null, null, null, null, false, null, null, null, [])));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var restore = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/graus-e-circulos", token));
        restore.StatusCode.Should().Be(HttpStatusCode.NoContent);

        GrauHtml(await GetLivroAsync(), 6).Should().Contain("Brasa Oculta");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task Create_with_a_Grau_outside_1_to_9_returns_400(int grau)
    {
        var token = await RegisterAuditorAsync($"LivroEfeitoGrauC{grau}", $"livroefeitograuc{grau}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest($"Fora do Livro {grau}", grau, "Grau inválido.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Grau deve estar entre 1 e 9.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task Update_with_a_Grau_outside_1_to_9_returns_400_and_keeps_the_Livro_block(int grau)
    {
        var token = await RegisterAuditorAsync($"LivroEfeitoGrauU{grau}", $"livroefeitograuu{grau}@teste.com");
        var nome = $"Faísca Válida {grau}";
        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest(nome, 2, "Faísca.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));
        var id = (await create.Content.ReadFromJsonAsync<EfeitoResponse>())!.Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{id}", token,
            new UpdateEfeitoRequest($"Faísca Renomeada {grau}", grau, "Faísca.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Grau deve estar entre 1 e 9.");
        GrauHtml(await GetLivroAsync(), 2).Should().Contain(nome);
    }

    [Theory]
    [InlineData(1, "cura")]
    [InlineData(2, " CÚRA ")]
    public async Task Create_with_a_Nome_equal_to_another_ignoring_case_and_accents_returns_400(int caso, string nome)
    {
        var token = await RegisterAuditorAsync($"LivroEfeitoNomeC{caso}", $"livroefeitonomec{caso}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest(nome, 1, "Duplicada.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Já existe um Efeito com esse Nome.");
    }

    [Fact]
    public async Task Update_to_a_Nome_equal_to_another_ignoring_case_and_accents_returns_400_but_recasing_its_own_Nome_is_fine()
    {
        var token = await RegisterAuditorAsync("LivroEfeitoNomeU", "livroefeitonomeu@teste.com");
        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", token,
            new CreateEfeitoRequest("Lampejo Sutil", 2, "Lampejo.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));
        var id = (await create.Content.ReadFromJsonAsync<EfeitoResponse>())!.Id;

        var duplicada = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{id}", token,
            new UpdateEfeitoRequest("CURA", 2, "Lampejo.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));
        duplicada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await duplicada.Content.ReadAsStringAsync()).Should().Contain("Já existe um Efeito com esse Nome.");

        var recase = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{id}", token,
            new UpdateEfeitoRequest("Lampejo SUTIL", 2, "Lampejo.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));
        recase.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
