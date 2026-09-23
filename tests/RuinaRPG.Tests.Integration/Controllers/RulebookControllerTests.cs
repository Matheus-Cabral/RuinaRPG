using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    [Fact]
    public async Task Get_without_a_token_returns_200()
    {
        var response = await _client.GetAsync("/api/rulebook");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_returns_the_seven_documents_split_into_sections()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm1", "rulebook1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        body!.Select(d => d.Slug).Should().Equal(
            "caracteristicas", "sistema-basico", "graus-e-circulos", "tabela-de-niveis", "estrelas-alkerianas", "historicos", "equipagem");

        var sistemaBasico = body!.Single(d => d.Slug == "sistema-basico");
        sistemaBasico.Sections.Should().HaveCount(7);
        sistemaBasico.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Html));

        var tabelaDeNiveis = body!.Single(d => d.Slug == "tabela-de-niveis");
        tabelaDeNiveis.Sections.Should().BeEmpty();
        tabelaDeNiveis.IntroHtml.Should().Contain("<table");
    }

    [Fact]
    public async Task Caracteristicas_sections_are_tagged_with_their_Positivas_or_Negativas_group()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm2", "rulebook2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var caracteristicas = body!.Single(d => d.Slug == "caracteristicas");
        caracteristicas.Sections.Should().HaveCountGreaterThan(50);
        caracteristicas.Sections.Should().OnlyContain(s => s.Grupo == "Positivas" || s.Grupo == "Negativas");
    }

    [Fact]
    public async Task GrausECirculos_IntroHtml_includes_the_two_reference_images()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm3", "rulebook3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var grausECirculos = body!.Single(d => d.Slug == "graus-e-circulos");
        grausECirculos.IntroHtml.Should().Contain("/rulebook/Escolas_de_Magia.png");
        grausECirculos.IntroHtml.Should().Contain("/rulebook/Matriz_Elemental.png");
        grausECirculos.Sections.Should().HaveCount(9);
    }

    [Fact]
    public async Task EstrelasAlkerianas_IntroHtml_includes_the_calendar_image_and_has_11_sections()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm4", "rulebook4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var estrelas = body!.Single(d => d.Slug == "estrelas-alkerianas");
        estrelas.IntroHtml.Should().Contain("/rulebook/Calendario alkeriano.jpeg");
        estrelas.Sections.Should().HaveCount(11);
        estrelas.Sections.Select(s => s.Titulo).Should().Contain(s => s.Contains("Sina"));
        estrelas.Sections.Select(s => s.Titulo).Should().Contain(s => s.Contains("AEURER"));
        estrelas.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Html));
    }

    [Fact]
    public async Task Historicos_has_26_sections_and_the_intro_paragraph()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGm5", "rulebook5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var historicos = body!.Single(d => d.Slug == "historicos");
        (historicos.IntroHtml ?? "").Should().Contain("marcaram a vida do personagem");
        historicos.Sections.Should().HaveCount(26);
        historicos.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Html));
        historicos.Sections.Select(s => s.Titulo).Should().Contain("Estudo Acadêmico");

        // Regression guard for Finding 1: the bonus line must render the proper Portuguese Perícia
        // label (e.g. "Investigação", "Artefatos Mágicos"), not the raw enum identifier
        // ("Investigacao", "ArtefatosMagicos") that Pericia.ToString() would produce. WebUtility.
        // HtmlEncode turns accented characters into numeric entities (e.g. "ç" -> "&#231;"), so
        // decode before comparing rather than asserting on the raw entity-encoded string.
        var fascinioPeloPassado = historicos.Sections.Single(s => s.Titulo == "Fascínio pelo Passado");
        var decodedHtml = WebUtility.HtmlDecode(fascinioPeloPassado.Html);
        decodedHtml.Should().Contain("Investigação").And.Contain("Artefatos Mágicos");
    }

    [Fact]
    public async Task Historicos_reflects_a_catalog_edit_immediately()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookGm6", "rulebook6@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOK6@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new RuinaRPG.Contracts.Rules.CreateHistoricoRequest("Recém Cadastrado", "Aparece na aba na hora.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Rules.HistoricoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", gmToken));
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var historicos = body!.Single(d => d.Slug == "historicos");
        historicos.Sections.Should().Contain(s => s.Titulo == "Recém Cadastrado");

        // Cleanup so this created row can't affect other tests' section counts in this class.
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created!.Id}", gmToken));
    }

    [Fact]
    public async Task GetDocuments_includes_an_Equipagem_tab_built_from_the_live_kit_catalog()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookEquipGm1", "rulebookequip1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));

        var documents = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var equipagem = documents!.Single(d => d.Slug == "equipagem");
        equipagem.IntroHtml.Should().NotBeNullOrWhiteSpace();
        equipagem.Sections.Should().Contain(s => s.Titulo == "Viajante");
    }

    [Fact]
    public async Task Get_reflects_a_saved_RulebookDocumentOverride_immediately()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookGmOverride1", "rulebookgmoverride1@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOKGMOVERRIDE1@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var updateMessage = new HttpRequestMessage(HttpMethod.Put, "/api/rulebook-documents/tabela-de-niveis");
        updateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        updateMessage.Content = JsonContent.Create(new UpdateRulebookDocumentOverrideRequest("# Texto Substituído Pelo Auditor"));
        await _client.SendAsync(updateMessage);

        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", gmToken));

            var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
            var tabelaDeNiveis = body!.Single(d => d.Slug == "tabela-de-niveis");
            (tabelaDeNiveis.IntroHtml ?? "").Should().Contain("Texto Substituído Pelo Auditor");
        }
        finally
        {
            // The override lives in the same Postgres container for every test in this class
            // (IClassFixture<PostgresFixture>, no per-test reset) — clean it up so it can't leak
            // into another test that expects the embedded default for this Slug.
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/tabela-de-niveis", gmToken));
        }
    }

    [Fact]
    public async Task Get_reflects_a_saved_RulebookDocumentOverride_immediately_for_sistema_basico()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookGmOverride2", "rulebookgmoverride2@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOKGMOVERRIDE2@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var updateMessage = new HttpRequestMessage(HttpMethod.Put, "/api/rulebook-documents/sistema-basico");
        updateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        updateMessage.Content = JsonContent.Create(new UpdateRulebookDocumentOverrideRequest("Texto Substituído Pelo Auditor Sistema Básico"));
        await _client.SendAsync(updateMessage);

        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", gmToken));

            var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
            var sistemaBasico = body!.Single(d => d.Slug == "sistema-basico");
            (sistemaBasico.IntroHtml ?? "").Should().Contain("Texto Substituído Pelo Auditor Sistema Básico");
        }
        finally
        {
            // Same cross-test leak concern as the tabela-de-niveis override test above.
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/sistema-basico", gmToken));
        }
    }

    [Fact]
    public async Task Get_reflects_a_saved_RulebookDocumentOverride_immediately_for_graus_e_circulos()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookGmOverride3", "rulebookgmoverride3@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOKGMOVERRIDE3@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var updateMessage = new HttpRequestMessage(HttpMethod.Put, "/api/rulebook-documents/graus-e-circulos");
        updateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        updateMessage.Content = JsonContent.Create(new UpdateRulebookDocumentOverrideRequest("Texto Substituído Pelo Auditor Graus e Círculos"));
        await _client.SendAsync(updateMessage);

        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", gmToken));

            var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
            var grausECirculos = body!.Single(d => d.Slug == "graus-e-circulos");
            (grausECirculos.IntroHtml ?? "").Should().Contain("Texto Substituído Pelo Auditor Graus e Círculos");
        }
        finally
        {
            // Same cross-test leak concern as the tabela-de-niveis override test above — this is
            // exactly the leak that made GrausECirculos_IntroHtml_includes_the_two_reference_images
            // fail (0 sections instead of 9) before this cleanup was added.
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/graus-e-circulos", gmToken));
        }
    }
}
