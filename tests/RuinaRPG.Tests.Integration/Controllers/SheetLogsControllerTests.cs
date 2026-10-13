using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.SheetLogs;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>Campanha R0016: consulta paginada do log de ficha, só para o GM.</summary>
public class SheetLogsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SheetLogsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private sealed record Cenario(string GmToken, string PlayerToken, Guid PlayerId, string CampaignId, Guid SheetId);

    private Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        var response = await _client.SendAsync(message);
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} respondeu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private async Task<string> RegisterGmAsync(string nickname)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, $"{nickname.ToLowerInvariant()}@teste.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string Token, Guid Id)> RegisterJogadorAsync(string gmToken, string nickname)
    {
        var codeResponse = await SendOkAsync(HttpMethod.Post, "/api/invite-codes", gmToken);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, $"{nickname.ToLowerInvariant()}@teste.com", "Senha!123", "Senha!123", code));
        var token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var me = await (await SendOkAsync(HttpMethod.Get, "/api/auth/me", token)).Content.ReadFromJsonAsync<MeResponse>();
        return (token, Guid.Parse(me!.Id));
    }

    private async Task<Cenario> CenarioAsync(string sufixo)
    {
        var gmToken = await RegisterGmAsync($"LogRdGm{sufixo}");
        var (playerToken, playerId) = await RegisterJogadorAsync(gmToken, $"LogRdJog{sufixo}");
        var campaign = await SendOkAsync(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest($"Campanha {sufixo}", ""));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId.ToString()));
        var sheet = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId.ToString()));
        var sheetId = Guid.Parse((await sheet.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id);
        return new Cenario(gmToken, playerToken, playerId, campaignId, sheetId);
    }

    private async Task<Guid> ConcederAsync(Cenario c, string tipo)
    {
        var response = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/grants", c.GmToken, new GrantSheetRequest(c.PlayerId.ToString(), tipo, null));
        return Guid.Parse((await response.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId);
    }

    private static readonly DateTime Base = new(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

    private static SheetLogEntry Linha(Guid sheetId, DateTime quando, SheetLogCategoria categoria = SheetLogCategoria.Geral, string campo = "Nome") => new()
    {
        Id = Guid.NewGuid(), CharacterSheetId = sheetId, OccurredAt = quando, ActorUserId = Guid.NewGuid(), ActorNickname = "Ator",
        ActorIsGm = true, Categoria = categoria, Acao = SheetLogAcao.Alterou, Secao = "Ficha", Campo = campo, ValorAntes = "a", ValorDepois = "b",
    };

    private async Task InserirAsync(IEnumerable<SheetLogEntry> linhas)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        db.SheetLogEntries.AddRange(linhas);
        await db.SaveChangesAsync();
    }

    private async Task<SheetLogPageResponse> PaginaAsync(string url, string token)
    {
        var response = await GetAsync(url, token);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SheetLogPageResponse>())!;
    }

    private static string Url(Guid sheetId, string query = "") => $"/api/sheet-logs/character/{sheetId}{query}";

    private static string Iso(DateTime d) => Uri.EscapeDataString(d.ToString("O"));

    [Fact]
    public async Task The_campaign_gm_gets_the_entries_newest_first_with_utc_timestamps()
    {
        var c = await CenarioAsync("Gm");
        await InserirAsync([Linha(c.SheetId, Base.AddHours(1), campo: "Antiga"), Linha(c.SheetId, Base.AddHours(5), campo: "Nova")]);

        var raw = await (await GetAsync(Url(c.SheetId), c.GmToken)).Content.ReadAsStringAsync();
        var pagina = await PaginaAsync(Url(c.SheetId), c.GmToken);

        pagina.Itens.Select(i => i.Campo).Should().ContainInOrder("Nova", "Antiga");
        pagina.Itens.Should().OnlyContain(i => i.OccurredAt.Kind == DateTimeKind.Utc);
        raw.Should().Contain("\"occurredAt\":\"2026-03-10T05:00:00Z\"");
        var nova = pagina.Itens.Single(i => i.Campo == "Nova");
        nova.Categoria.Should().Be("Geral");
        nova.Acao.Should().Be("Alterou");
        nova.ActorNickname.Should().Be("Ator");
        nova.ValorAntes.Should().Be("a");
        nova.ValorDepois.Should().Be("b");
        pagina.Itens[0].Acao.Should().Be("Criou"); // criada "agora", é a mais recente
        pagina.ProximoCursor.Should().BeNull();
    }

    [Fact]
    public async Task Real_edits_through_the_api_show_up_in_the_log()
    {
        var c = await CenarioAsync("Real");
        var criada = (await PaginaAsync(Url(c.SheetId), c.GmToken)).Itens.Should().ContainSingle().Subject;
        criada.Acao.Should().Be("Criou");

        await SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}", c.GmToken, new UpdateCharacterSheetRequest(
            null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, "Marcado pela Ruína",
            true, 749, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null, null, 0, null, null, 0));

        var pagina = await PaginaAsync(Url(c.SheetId), c.GmToken);
        pagina.Itens.Should().HaveCountGreaterThan(1);
        pagina.Itens.Last().Id.Should().Be(criada.Id);
        var nome = pagina.Itens.Single(i => i.Acao == "Alterou" && i.ValorDepois == "Vann Astrel");
        nome.ActorIsGm.Should().BeTrue();
        pagina.Itens.Select(i => i.OccurredAt).Should().BeInDescendingOrder();
        pagina.Itens.Take(pagina.Itens.Count - 1).Should().OnlyContain(i => i.Acao != "Criou");
    }

    [Fact]
    public async Task Paging_across_a_group_of_equal_timestamps_loses_and_repeats_nothing()
    {
        var c = await CenarioAsync("Empate");
        var empate = Base.AddHours(6);
        await InserirAsync([
            Linha(c.SheetId, empate.AddMinutes(-5), campo: "antes"),
            .. Enumerable.Range(0, 5).Select(i => Linha(c.SheetId, empate, campo: $"empate{i}")),
            Linha(c.SheetId, empate.AddMinutes(5), campo: "depois")]);

        List<SheetLogEntry> doBanco;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            doBanco = await db.SheetLogEntries.AsNoTracking().Where(l => l.CharacterSheetId == c.SheetId).ToListAsync();
        }

        var vistos = new List<SheetLogEntryResponse>();
        string? cursor = null;
        do
        {
            var query = "?limite=2" + (cursor is null ? "" : $"&antesDe={Uri.EscapeDataString(cursor)}");
            var pagina = await PaginaAsync(Url(c.SheetId, query), c.GmToken);
            vistos.AddRange(pagina.Itens);
            cursor = pagina.ProximoCursor;
        } while (cursor is not null);

        vistos.Select(v => v.Id).Should().OnlyHaveUniqueItems();
        vistos.Select(v => v.Id).Should().BeEquivalentTo(doBanco.Select(l => l.Id.ToString()));
        vistos.Select(v => v.OccurredAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Everyone_but_the_campaign_gm_gets_404()
    {
        var c = await CenarioAsync("Neg");
        var outroGm = await RegisterGmAsync("LogRdOutroGm");

        (await GetAsync(Url(c.SheetId), c.PlayerToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetAsync(Url(c.SheetId), outroGm)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetAsync(Url(Guid.NewGuid()), c.GmToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetAsync($"/api/sheet-logs/xyz/{c.SheetId}", c.GmToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Npc", "npc")]
    [InlineData("Creature", "creature")]
    public async Task Granted_sheets_are_readable_by_their_gm_only(string tipo, string kind)
    {
        var c = await CenarioAsync($"Grant{kind}");
        var sheetId = await ConcederAsync(c, tipo);
        var outroGm = await RegisterGmAsync($"LogRdOutro{kind}");

        (await GetAsync($"/api/sheet-logs/{kind}/{sheetId}", c.GmToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync($"/api/sheet-logs/{kind}/{sheetId}", c.PlayerToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetAsync($"/api/sheet-logs/{kind}/{sheetId}", outroGm)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_date_window_is_inclusive_at_the_start_and_exclusive_at_the_end()
    {
        var c = await CenarioAsync("Janela");
        await InserirAsync([
            Linha(c.SheetId, Base.AddHours(10), campo: "dez"),
            Linha(c.SheetId, Base.AddHours(12), campo: "doze"),
            Linha(c.SheetId, Base.AddHours(14), campo: "catorze")]);

        var pagina = await PaginaAsync(Url(c.SheetId, $"?de={Iso(Base.AddHours(11))}&ate={Iso(Base.AddHours(14))}"), c.GmToken);
        pagina.Itens.Should().ContainSingle().Which.Campo.Should().Be("doze");

        var inclusivo = await PaginaAsync(Url(c.SheetId, $"?de={Iso(Base.AddHours(12))}&ate={Iso(Base.AddHours(15))}"), c.GmToken);
        inclusivo.Itens.Select(i => i.Campo).Should().Equal("catorze", "doze");
    }

    [Fact]
    public async Task Category_toggles_filter_resources_and_stress_independently()
    {
        var c = await CenarioAsync("Filtros");
        await InserirAsync([
            Linha(c.SheetId, Base.AddHours(1), SheetLogCategoria.Geral, "geral"),
            Linha(c.SheetId, Base.AddHours(2), SheetLogCategoria.RecursoVFA, "recurso"),
            Linha(c.SheetId, Base.AddHours(3), SheetLogCategoria.Estresse, "estresse")]);

        async Task<string[]> Campos(string query) =>
            (await PaginaAsync(Url(c.SheetId, query), c.GmToken)).Itens.Where(i => i.Acao == "Alterou").Select(i => i.Campo!).OrderBy(x => x).ToArray();

        (await Campos("?recursos=false")).Should().Equal("estresse", "geral");
        (await Campos("?estresse=false")).Should().Equal("geral", "recurso");
        (await Campos("?recursos=false&estresse=false")).Should().Equal("geral");
        (await Campos("")).Should().Equal("estresse", "geral", "recurso");
    }

    [Fact]
    public async Task Cursor_paging_walks_every_row_once_even_with_equal_timestamps()
    {
        var c = await CenarioAsync("Pag");
        var empate = Base.AddHours(3);
        var linhas = Enumerable.Range(0, 247).Select(i => Linha(c.SheetId, Base.AddMinutes(i + 10), campo: $"c{i}")).ToList();
        linhas.AddRange(Enumerable.Range(0, 3).Select(i => Linha(c.SheetId, empate, campo: $"empate{i}")));
        await InserirAsync(linhas);
        // 250 inseridas + a linha Criou da própria ficha.
        var esperados = linhas.Select(l => l.Id.ToString()).ToHashSet();

        var vistos = new List<string>();
        var tamanhos = new List<int>();
        string? cursor = null;
        do
        {
            var query = "?limite=100" + (cursor is null ? "" : $"&antesDe={Uri.EscapeDataString(cursor)}");
            var pagina = await PaginaAsync(Url(c.SheetId, query), c.GmToken);
            tamanhos.Add(pagina.Itens.Count);
            vistos.AddRange(pagina.Itens.Select(i => i.Id));
            cursor = pagina.ProximoCursor;
        } while (cursor is not null);

        tamanhos.Should().Equal(100, 100, 51);
        vistos.Should().OnlyHaveUniqueItems();
        vistos.Where(esperados.Contains).Should().HaveCount(250);
    }

    [Fact]
    public async Task The_limit_is_clamped_and_a_malformed_cursor_is_a_bad_request()
    {
        var c = await CenarioAsync("Limite");
        await InserirAsync(Enumerable.Range(0, 600).Select(i => Linha(c.SheetId, Base.AddMinutes(i))));

        (await PaginaAsync(Url(c.SheetId, "?limite=0"), c.GmToken)).Itens.Should().HaveCount(1);
        (await PaginaAsync(Url(c.SheetId, "?limite=9999"), c.GmToken)).Itens.Should().HaveCount(500);
        (await GetAsync(Url(c.SheetId, "?antesDe=lixo"), c.GmToken)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetAsync(Url(c.SheetId, "?antesDe=1_naoGuid"), c.GmToken)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Filters_apply_before_paging()
    {
        var c = await CenarioAsync("FiltroPag");
        var linhas = Enumerable.Range(0, 300).Select(i => Linha(c.SheetId, Base.AddMinutes(i + 10),
            i % 2 == 0 ? SheetLogCategoria.RecursoVFA : SheetLogCategoria.Geral, $"c{i}")).ToList();
        await InserirAsync(linhas);

        var primeira = await PaginaAsync(Url(c.SheetId, "?recursos=false&limite=100"), c.GmToken);
        primeira.Itens.Should().HaveCount(100);
        primeira.ProximoCursor.Should().NotBeNull();
        var segunda = await PaginaAsync(Url(c.SheetId, $"?recursos=false&limite=100&antesDe={Uri.EscapeDataString(primeira.ProximoCursor!)}"), c.GmToken);

        // 150 não-recurso + a linha Criou.
        segunda.Itens.Should().HaveCount(51);
        segunda.ProximoCursor.Should().BeNull();
        primeira.Itens.Concat(segunda.Itens).Should().OnlyContain(i => i.Categoria != "RecursoVFA");
    }
}
