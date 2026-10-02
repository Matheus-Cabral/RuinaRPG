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
            "sistema-basico", "caracteristicas", "graus-e-circulos", "tabela-de-niveis", "estrelas-alkerianas", "historicos", "equipagem");

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
        estrelas.Titulo.Should().Be("As Estrelas");
        estrelas.IntroHtml.Should().Contain("/rulebook/Calendario alkeriano.jpeg");
        estrelas.Sections.Should().HaveCount(11);
        estrelas.Sections.Select(s => s.Titulo).Should().Contain(s => s.Contains("Sina"));
        estrelas.Sections.Select(s => s.Titulo).Should().Contain(s => s.Contains("AEURER"));
        estrelas.Sections.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Html));
        // EstrelaSelect.razor lê o subtítulo do dropdown desse parágrafo de abertura em itálico.
        estrelas.Sections.Single(s => s.Titulo.Contains("AEURER")).Html
            .Should().StartWith("<p><em>Estrela da Curiosidade</em></p>");
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
    public async Task Historicos_omits_the_bonus_of_a_removed_Pericia()
    {
        var token = await RegisterGmAndGetTokenAsync("RulebookGmRem", "rulebookrem@teste.com");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOKREM@TESTE.COM");
            user.IsRulesAuditor = true;
            await db.SaveChangesAsync();
        }
        var seis = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token,
            new SalvarPericiaRequest("Seis Sumida Livro", null, null, false)))).Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        var tres = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token,
            new SalvarPericiaRequest("Três Sumida Livro", null, null, false)))).Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", token,
            new CreateHistoricoRequest("Só Seis Some", "Um.", seis!.Chave, "Atletismo")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", token,
            new CreateHistoricoRequest("Os Dois Somem", "Dois.", "Acrobacia", tres!.Chave)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", token,
            new CreateHistoricoRequest("Nenhum Fica", "Três.", seis.Chave, tres.Chave)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{seis.Id}", token));
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{tres.Id}", token));

        var body = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token)))
            .Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();

        var sections = body!.Single(d => d.Slug == "historicos").Sections;
        var soSeis = sections.Single(s => s.Titulo == "Só Seis Some").Html;
        soSeis.Should().Contain("+3 Atletismo").And.NotContain("+6").And.NotContain("Seis Sumida Livro");
        var soTres = sections.Single(s => s.Titulo == "Os Dois Somem").Html;
        soTres.Should().Contain("+6 Acrobacia").And.NotContain("+3").And.NotContain("Três Sumida Livro");
        var nenhum = sections.Single(s => s.Titulo == "Nenhum Fica").Html;
        nenhum.Should().NotContain("+6").And.NotContain("+3").And.NotContain("<em>");
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

    private async Task<string> RegisterAuditorAsync(string username, string email)
    {
        var token = await RegisterGmAndGetTokenAsync(username, email);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return token;
    }

    private async Task<string> TabelaDeNiveisHtmlAsync(string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", token));
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        return body!.Single(d => d.Slug == "tabela-de-niveis").IntroHtml ?? "";
    }

    private async Task<ColunaDeNivelResponse> CriarColunaAsync(string token, string nome, string tipo)
    {
        var created = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/tabela-de-niveis/colunas", token,
            new CriarColunaDeNivelRequest(nome, tipo)));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<ColunaDeNivelResponse>())!;
    }

    private async Task SetValorAsync(string token, int nivel, Guid colunaId, int? valor)
    {
        var r = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/tabela-de-niveis/{nivel}/valores/{colunaId}", token,
            new AtualizarValorDeNivelRequest(valor)));
        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TabelaDeNiveis_main_table_lists_the_bonuses_of_each_level_one_per_line()
    {
        var token = await RegisterAuditorAsync("RulebookGmTabela1", "rulebookgmtabela1@teste.com");

        var html = await TabelaDeNiveisHtmlAsync(token);

        html.Should().Contain("<th>Nível</th><th>Bônus</th>").And.NotContain("Outros bônus");
        var linha1 = System.Text.RegularExpressions.Regex.Match(html, "<tr><td>1</td><td>(.*?)</td></tr>").Groups[1].Value;
        linha1.Should().Contain("Pontos de Atributo: +9").And.Contain("<br />");
        linha1.Should().Contain("Status de Vida");
        html.Should().NotContain("<td></td>");
    }

    [Fact]
    public async Task TabelaDeNiveis_limits_table_lists_only_Por_nivel_columns_that_have_values()
    {
        var token = await RegisterAuditorAsync("RulebookGmTabela2", "rulebookgmtabela2@teste.com");

        var html = await TabelaDeNiveisHtmlAsync(token);

        var limites = html[html.IndexOf("<h3>Limites e progressão</h3>", StringComparison.Ordinal)..];
        limites.Should().Contain("<th>XP para o próximo nível</th>").And.Contain("<th>EAP base</th>");
        limites.Should().MatchRegex("<tr><td>1</td><td>\\d+</td><td>\\d+</td></tr>");
        limites.Should().NotContain("Máx. de Atributo");
    }

    [Fact]
    public async Task TabelaDeNiveis_custom_columns_show_in_the_bonus_list_or_the_limits_table_by_type()
    {
        var token = await RegisterAuditorAsync("RulebookGmTabela3", "rulebookgmtabela3@teste.com");
        var fama = await CriarColunaAsync(token, "Pontos de Fama", "Acumulativa");
        var teto = await CriarColunaAsync(token, "Teto de Fama", "PorNivel");
        try
        {
            (await TabelaDeNiveisHtmlAsync(token)).Should().NotContain("Pontos de Fama").And.NotContain("Teto de Fama");

            await SetValorAsync(token, 1, fama.Id, 7);
            await SetValorAsync(token, 1, teto.Id, 33);

            var html = await TabelaDeNiveisHtmlAsync(token);
            var bonus = html[..html.IndexOf("<h3>", StringComparison.Ordinal)];
            bonus.Should().Contain("Pontos de Fama: +7").And.NotContain("Teto de Fama");
            var limites = html[html.IndexOf("<h3>", StringComparison.Ordinal)..];
            limites.Should().Contain("<th>Teto de Fama</th>").And.Contain("<td>33</td>").And.NotContain("Pontos de Fama");
        }
        finally
        {
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/tabela-de-niveis/colunas/{fama.Id}", token));
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/tabela-de-niveis/colunas/{teto.Id}", token));
        }
    }

    [Fact]
    public async Task TabelaDeNiveis_html_encodes_auditor_supplied_column_names()
    {
        var token = await RegisterAuditorAsync("RulebookGmTabela4", "rulebookgmtabela4@teste.com");
        var coluna = await CriarColunaAsync(token, "<b>x</b>", "Acumulativa");
        try
        {
            await SetValorAsync(token, 1, coluna.Id, 2);

            var html = await TabelaDeNiveisHtmlAsync(token);
            html.Should().Contain("&lt;b&gt;x&lt;/b&gt;: +2").And.NotContain("<b>x</b>");
        }
        finally
        {
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/tabela-de-niveis/colunas/{coluna.Id}", token));
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

    [Fact]
    public async Task GetDocument_without_a_token_returns_only_that_document()
    {
        var response = await _client.GetAsync("/api/rulebook/estrelas-alkerianas");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RulebookDocumentResponse>();
        body!.Slug.Should().Be("estrelas-alkerianas");
        body.Titulo.Should().Be("As Estrelas");
        body.Sections.Should().HaveCount(11);
    }

    [Fact]
    public async Task GetDocument_with_an_unknown_slug_returns_404()
    {
        var response = await _client.GetAsync("/api/rulebook/nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDocument_reflects_a_saved_override_for_estrelas_alkerianas()
    {
        // Ficha de Personagem R0001 1.a: o popup da Estrela lê este endpoint, então uma edição do
        // Auditor tem que aparecer aqui na hora.
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookGmOverrideEstrelas", "rulebookgmoverrideestrelas@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOKGMOVERRIDEESTRELAS@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/estrelas-alkerianas", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# 🌿 I — AEURER\n\nTexto novo de Aeurer pelo Auditor.")));

        try
        {
            var body = await _client.GetFromJsonAsync<RulebookDocumentResponse>("/api/rulebook/estrelas-alkerianas");

            var aeurer = body!.Sections.Single();
            aeurer.Titulo.Should().Contain("AEURER");
            aeurer.Html.Should().Contain("Texto novo de Aeurer pelo Auditor.");
        }
        finally
        {
            // Same cleanup reasoning as Get_reflects_a_saved_RulebookDocumentOverride_immediately.
            await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/estrelas-alkerianas", gmToken));
        }
    }
}
