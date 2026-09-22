using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class HistoricosControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public HistoricosControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
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
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/historicos");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_with_no_filter_returns_a_real_Id_for_a_seeded_Historico()
    {
        var token = await RegisterGmAndGetTokenAsync("HistGm1", "hist1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", token));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        var estudoAcademico = body!.Should().ContainSingle(h => h.Nome == "Estudo Acadêmico").Subject;
        Guid.TryParse(estudoAcademico.Id, out _).Should().BeTrue();
        estudoAcademico.PericiaMaisSeis.Should().Be("Arcano");
        estudoAcademico.PericiaMaisTres.Should().Be("Biblioteca");
    }

    [Fact]
    public async Task CreateHistorico_by_a_Rules_Auditor_persists_it_as_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm1", "histcrudgm1@teste.com");
        await GrantRulesAuditorAsync("histcrudgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Testado a Ferro", "Descrição de teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<HistoricoResponse>();
        body!.Nome.Should().Be("Testado a Ferro");
        body.IsCustomized.Should().BeTrue();

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Should().Contain(h => h.Nome == "Testado a Ferro");
    }

    [Fact]
    public async Task CreateHistorico_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm2", "histcrudgm2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Não Deveria Salvar", "Teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateHistorico_with_the_same_Pericia_twice_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm3", "histcrudgm3@teste.com");
        await GrantRulesAuditorAsync("histcrudgm3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Perícia Repetida", "Teste.", "Atletismo", "Atletismo")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateHistorico_with_an_empty_Nome_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm10", "histcrudgm10@teste.com");
        await GrantRulesAuditorAsync("histcrudgm10@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("   ", "Teste.", "Atletismo", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateHistorico_with_an_invalid_Pericia_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm4", "histcrudgm4@teste.com");
        await GrantRulesAuditorAsync("histcrudgm4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Perícia Inválida", "Teste.", "NaoExiste", "Acrobacia")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateHistorico_by_a_Rules_Auditor_persists_the_change_and_marks_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm5", "histcrudgm5@teste.com");
        await GrantRulesAuditorAsync("histcrudgm5@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Editar", "Antes.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created!.Id}", gmToken,
            new UpdateHistoricoRequest("Para Editar", "Depois.", "Atletismo", "Acrobacia")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Single(h => h.Id == created.Id).Descricao.Should().Be("Depois.");
    }

    [Fact]
    public async Task UpdateHistorico_with_the_same_Pericia_twice_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm6", "histcrudgm6@teste.com");
        await GrantRulesAuditorAsync("histcrudgm6@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Editar Errado", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/historicos/{created!.Id}", gmToken,
            new UpdateHistoricoRequest("Para Editar Errado", "Teste.", "Atletismo", "Atletismo")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteHistorico_soft_deletes_and_it_no_longer_appears_in_List()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm7", "histcrudgm7@teste.com");
        await GrantRulesAuditorAsync("histcrudgm7@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Para Excluir", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        list!.Should().NotContain(h => h.Id == created.Id);
    }

    // Mirrors CharacterSkillsControllerTests.UpdateWithHistorico — a minimal, valid full-form
    // UpdateCharacterSheetRequest whose only field this test class cares about is HistoricoId.
    private static UpdateCharacterSheetRequest UpdateWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, historicoId);

    // Mirrors NpcSkillsControllerTests.UpdateWithHistorico — same shape for UpdateNpcSheetRequest,
    // which additionally carries Nivel (int, not nullable) right after the 8 string? fields.
    private static RuinaRPG.Contracts.NpcSheets.UpdateNpcSheetRequest UpdateNpcWithHistorico(string? historicoId) => new(
        null, "Teste", null, null, null, null, null, null,
        1, true, 0, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 0, 0, 0, 0, "Nenhuma", 0, null, null, 0, historicoId);

    [Fact]
    public async Task DeleteHistorico_that_is_already_in_use_on_a_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm8", "histcrudgm8@teste.com");
        await GrantRulesAuditorAsync("histcrudgm8@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Em Uso", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "HistCrudPlayer8", "histcrudplayer8@teste.com");
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha Histórico", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, UpdateWithHistorico(created!.Id)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteHistorico_that_is_already_in_use_on_an_npc_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistCrudGm9", "histcrudgm9@teste.com");
        await GrantRulesAuditorAsync("histcrudgm9@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/historicos", gmToken,
            new CreateHistoricoRequest("Em Uso NPC", "Teste.", "Atletismo", "Acrobacia")));
        var created = await createResponse.Content.ReadFromJsonAsync<HistoricoResponse>();

        var npcCreateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var npc = await npcCreateResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.NpcSheets.NpcSheetResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npc!.Id}", gmToken, UpdateNpcWithHistorico(created!.Id)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/historicos/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
