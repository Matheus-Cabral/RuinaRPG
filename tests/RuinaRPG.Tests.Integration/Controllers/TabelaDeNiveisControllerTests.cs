using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Auditoria da Tabela de Níveis. A tabela é global ao banco da classe: todo teste que a edita
/// restaura o que mudou no finally, para os demais continuarem vendo 50 níveis e todas as colunas de sistema.
/// </summary>
public class TabelaDeNiveisControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public TabelaDeNiveisControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null) =>
        _client.SendAsync(AuthedRequest(method, url, token, body));

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await SendAsync(HttpMethod.Post, "/api/invite-codes", gmToken);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> RegisterAuditorAsync(string nickname, string email)
    {
        var token = await RegisterGmAndGetTokenAsync(nickname, email);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return token;
    }

    private async Task<TabelaDeNiveisResponse> GetTabelaAsync(string token) =>
        (await (await SendAsync(HttpMethod.Get, "/api/tabela-de-niveis", token)).Content.ReadFromJsonAsync<TabelaDeNiveisResponse>())!;

    private static Guid ColunaDe(TabelaDeNiveisResponse t, string chave) => t.Colunas.Single(c => c.ChaveDeSistema == chave).Id;

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/npc-sheets", gmToken);
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    [Fact]
    public async Task Get_returns_the_system_columns_and_50_levels_for_any_user()
    {
        var gm = await RegisterGmAndGetTokenAsync("TabNivGm1", "tabniv1@teste.com");
        var jogador = await RegisterJogadorTokenAsync(gm, "TabNivJog1", "tabnivjog1@teste.com");

        var tabela = await GetTabelaAsync(jogador);

        tabela.Colunas.Count(c => c.DoSistema).Should().Be(ChavesDeNivel.Sistema.Count);
        tabela.Linhas.Should().HaveCount(50);
        var nivel1 = tabela.Linhas.Single(l => l.Nivel == 1);
        nivel1.Valores[ColunaDe(tabela, ChavesDeNivel.PontosDeAtributo)].Should().Be(9);
    }

    [Fact]
    public async Task Get_exposes_MostrarLimitesNoLivro_defaulting_to_false_and_the_auditor_can_round_trip_it()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud11", "tabniv11@teste.com");
        try
        {
            (await GetTabelaAsync(auditor)).MostrarLimitesNoLivro.Should().BeFalse();

            (await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/config", auditor, new AtualizarConfigDaTabelaDeNiveisRequest(true))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetTabelaAsync(auditor)).MostrarLimitesNoLivro.Should().BeTrue();

            (await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/config", auditor, new AtualizarConfigDaTabelaDeNiveisRequest(false))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetTabelaAsync(auditor)).MostrarLimitesNoLivro.Should().BeFalse();
        }
        finally
        {
            await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/config", auditor, new AtualizarConfigDaTabelaDeNiveisRequest(false));
        }
    }

    [Fact]
    public async Task Config_write_returns_403_for_a_non_auditor_and_leaves_the_flag_untouched()
    {
        var gm = await RegisterGmAndGetTokenAsync("TabNivGm12", "tabniv12@teste.com");

        (await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/config", gm, new AtualizarConfigDaTabelaDeNiveisRequest(true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await GetTabelaAsync(gm)).MostrarLimitesNoLivro.Should().BeFalse();
    }

    [Fact]
    public async Task Writes_return_403_for_a_non_auditor()
    {
        var gm = await RegisterGmAndGetTokenAsync("TabNivGm2", "tabniv2@teste.com");
        var tabela = await GetTabelaAsync(gm);
        var colunaId = tabela.Colunas[0].Id;

        (await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/1/valores/{colunaId}", gm, new AtualizarValorDeNivelRequest(1))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/1/outros-bonus", gm, new AtualizarOutrosBonusRequest("x"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/niveis", gm)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, "/api/tabela-de-niveis/niveis/ultimo", gm)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/colunas", gm, new CriarColunaDeNivelRequest("X", "Acumulativa"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/colunas/{colunaId}", gm, new RenomearColunaDeNivelRequest("X"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Delete, $"/api/tabela-de-niveis/colunas/{colunaId}", gm)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/colunas/ordem", gm, new ReordenarColunasDeNivelRequest(tabela.Colunas.Select(c => c.Id).ToList()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editing_a_value_changes_the_budget_seen_by_a_sheet()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud3", "tabniv3@teste.com");
        var sheetId = await CreateNpcSheetAsync(auditor);
        (await SendAsync(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", auditor, 49)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var tabela = await GetTabelaAsync(auditor);
        var colunaId = ColunaDe(tabela, ChavesDeNivel.PontosDeAtributo);
        var original = tabela.Linhas.Single(l => l.Nivel == 49).Valores.GetValueOrDefault(colunaId);

        try
        {
            (await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/49/valores/{colunaId}", auditor, new AtualizarValorDeNivelRequest(99))).StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await GetTabelaAsync(auditor)).Linhas.Single(l => l.Nivel == 49).Valores[colunaId].Should().Be(99);
            var budget = await (await SendAsync(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/attributes/budget", auditor)).Content.ReadFromJsonAsync<AttributePointBudgetResponse>();
            budget!.PontosDisponiveis.Should().BeGreaterThanOrEqualTo(99);
        }
        finally
        {
            await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/49/valores/{colunaId}", auditor, new AtualizarValorDeNivelRequest(original));
        }
    }

    [Fact]
    public async Task Negative_value_returns_400()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud4", "tabniv4@teste.com");
        var tabela = await GetTabelaAsync(auditor);
        var colunaId = ColunaDe(tabela, ChavesDeNivel.PontosDeAtributo);

        var response = await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/1/valores/{colunaId}", auditor, new AtualizarValorDeNivelRequest(-1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetTabelaAsync(auditor)).Linhas.Single(l => l.Nivel == 1).Valores[colunaId].Should().Be(9);
    }

    [Fact]
    public async Task Editing_outros_bonus_shows_up_in_the_level_up_notice()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud5", "tabniv5@teste.com");
        var sheetId = await CreateNpcSheetAsync(auditor);
        // 149 XP keeps the sheet at Nível 2 (same fixture value NpcSheetsControllerTests uses).
        await SendAsync(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/experiencia-atual", auditor, 149);
        var original = (await GetTabelaAsync(auditor)).Linhas.Single(l => l.Nivel == 2).OutrosBonus;

        try
        {
            (await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/2/outros-bonus", auditor, new AtualizarOutrosBonusRequest("Bônus de teste"))).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var notice = await (await SendAsync(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/level-up-notice", auditor)).Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
            notice!.BonusTexts.Should().Contain("Bônus de teste");
        }
        finally
        {
            await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/2/outros-bonus", auditor, new AtualizarOutrosBonusRequest(original));
        }
    }

    [Fact]
    public async Task Add_level_then_remove_last_level()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud6", "tabniv6@teste.com");

        var added = await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/niveis", auditor);
        try
        {
            added.StatusCode.Should().Be(HttpStatusCode.Created);
            (await added.Content.ReadFromJsonAsync<LinhaDeNivelResponse>())!.Nivel.Should().Be(51);
            (await GetTabelaAsync(auditor)).Linhas.Should().HaveCount(51);
        }
        finally
        {
            (await SendAsync(HttpMethod.Delete, "/api/tabela-de-niveis/niveis/ultimo", auditor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await GetTabelaAsync(auditor)).Linhas.Should().HaveCount(50);
    }

    [Fact]
    public async Task Remove_last_level_in_use_returns_400()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud7", "tabniv7@teste.com");
        var sheetId = await CreateNpcSheetAsync(auditor);
        var xpColuna = ColunaDe(await GetTabelaAsync(auditor), ChavesDeNivel.XpParaProximoNivel);
        var xpOriginal50 = (await GetTabelaAsync(auditor)).Linhas.Single(l => l.Nivel == 50).Valores.GetValueOrDefault(xpColuna);
        var added = false;

        try
        {
            (await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/niveis", auditor)).StatusCode.Should().Be(HttpStatusCode.Created);
            added = true;
            await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/50/valores/{xpColuna}", auditor, new AtualizarValorDeNivelRequest(99999));
            (await SendAsync(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", auditor, 51)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var blocked = await SendAsync(HttpMethod.Delete, "/api/tabela-de-niveis/niveis/ultimo", auditor);

            blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await blocked.Content.ReadAsStringAsync()).Should().Contain("1 ficha");

            await SendAsync(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", auditor, 1);
            (await SendAsync(HttpMethod.Delete, "/api/tabela-de-niveis/niveis/ultimo", auditor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            added = false;
        }
        finally
        {
            await SendAsync(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/nivel", auditor, 1);
            if (added)
                await SendAsync(HttpMethod.Delete, "/api/tabela-de-niveis/niveis/ultimo", auditor);
            await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/50/valores/{xpColuna}", auditor, new AtualizarValorDeNivelRequest(xpOriginal50));
        }

        (await GetTabelaAsync(auditor)).Linhas.Should().HaveCount(50);
    }

    [Fact]
    public async Task Custom_column_crud_and_reorder()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud8", "tabniv8@teste.com");
        Guid? id = null;

        try
        {
            var created = await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/colunas", auditor, new CriarColunaDeNivelRequest("Pontos de Fama", "Acumulativa"));
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var coluna = (await created.Content.ReadFromJsonAsync<ColunaDeNivelResponse>())!;
            id = coluna.Id;
            coluna.DoSistema.Should().BeFalse();
            coluna.Tipo.Should().Be("Acumulativa");

            var tabela = await GetTabelaAsync(auditor);
            tabela.Colunas.OrderBy(c => c.Ordem).Last().Id.Should().Be(id.Value);

            (await SendAsync(HttpMethod.Put, $"/api/tabela-de-niveis/colunas/{id}", auditor, new RenomearColunaDeNivelRequest("Fama"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetTabelaAsync(auditor)).Colunas.Single(c => c.Id == id).Nome.Should().Be("Fama");

            var novaOrdem = new[] { id.Value }.Concat(tabela.Colunas.OrderBy(c => c.Ordem).Select(c => c.Id).Where(i => i != id)).ToList();
            (await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/colunas/ordem", auditor, new ReordenarColunasDeNivelRequest(novaOrdem))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetTabelaAsync(auditor)).Colunas.OrderBy(c => c.Ordem).Select(c => c.Id).Should().Equal(novaOrdem);

            (await SendAsync(HttpMethod.Delete, $"/api/tabela-de-niveis/colunas/{id}", auditor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            id = null;
            (await GetTabelaAsync(auditor)).Colunas.Should().HaveCount(ChavesDeNivel.Sistema.Count);
        }
        finally
        {
            if (id is not null)
                await SendAsync(HttpMethod.Delete, $"/api/tabela-de-niveis/colunas/{id}", auditor);
            // restore the original system ordering the reorder above shuffled
            var atual = await GetTabelaAsync(auditor);
            var original = ChavesDeNivel.Sistema.OrderBy(d => d.Ordem).Select(d => ColunaDe(atual, d.Chave)).ToList();
            await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/colunas/ordem", auditor, new ReordenarColunasDeNivelRequest(original));
        }
    }

    [Fact]
    public async Task System_column_cannot_be_deleted_and_bad_type_is_rejected()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud9", "tabniv9@teste.com");
        var tabela = await GetTabelaAsync(auditor);
        var sistemaId = ColunaDe(tabela, ChavesDeNivel.PontosDeAtributo);

        (await SendAsync(HttpMethod.Delete, $"/api/tabela-de-niveis/colunas/{sistemaId}", auditor)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/colunas", auditor, new CriarColunaDeNivelRequest("X", "Inexistente"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/colunas", auditor, new CriarColunaDeNivelRequest("X", "1"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, "/api/tabela-de-niveis/colunas", auditor, new CriarColunaDeNivelRequest("  ", "Acumulativa"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetTabelaAsync(auditor)).Colunas.Should().HaveCount(ChavesDeNivel.Sistema.Count);
    }

    [Fact]
    public async Task Reorder_with_a_missing_id_returns_400()
    {
        var auditor = await RegisterAuditorAsync("TabNivAud10", "tabniv10@teste.com");
        var tabela = await GetTabelaAsync(auditor);
        var ids = tabela.Colunas.OrderBy(c => c.Ordem).Select(c => c.Id).ToList();
        ids.RemoveAt(0);

        var response = await SendAsync(HttpMethod.Put, "/api/tabela-de-niveis/colunas/ordem", auditor, new ReordenarColunasDeNivelRequest(ids));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
