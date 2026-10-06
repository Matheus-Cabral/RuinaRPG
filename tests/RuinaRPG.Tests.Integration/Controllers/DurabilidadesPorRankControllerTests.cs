using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Auditoria endpoint over the DurabilidadesPorRank table (see DurabilidadePorRankItemsTests for
/// how the table drives an item's resolved durability). GET is open to any authenticated user;
/// PUT is gated to the Rules Auditor, same DB check as HistoricosController. Tests that mutate a
/// row restore it afterwards, since every Fact in this class shares one database.
/// </summary>
public class DurabilidadesPorRankControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public DurabilidadesPorRankControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    /// <summary>Restores the row at the end of a test that edits it — the table is shared by every Fact.</summary>
    private async Task<Func<Task>> CaptureUndoAsync(string rank)
    {
        var parsedRank = Enum.Parse<RuinaRPG.Domain.Items.RankDeItem>(rank);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var row = await db.DurabilidadesPorRank.SingleAsync(d => d.Rank == parsedRank);
        var (durabilidade, inquebravel) = (row.Durabilidade, row.Inquebravel);
        return async () =>
        {
            using var undoScope = _factory.Services.CreateScope();
            var undoDb = undoScope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var undoRow = await undoDb.DurabilidadesPorRank.SingleAsync(d => d.Rank == parsedRank);
            undoRow.Durabilidade = durabilidade;
            undoRow.Inquebravel = inquebravel;
            await undoDb.SaveChangesAsync();
        };
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/durabilidades-por-rank");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_as_any_authenticated_user_returns_the_8_seeded_ranks_in_order()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudList1", "duraudlist1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/durabilidades-por-rank", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<DurabilidadePorRankResponse>>();
        body!.Select(r => r.Rank).Should().Equal("F", "E", "D", "C", "B", "A", "S", "SS");

        var f = body!.Single(r => r.Rank == "F");
        f.Durabilidade.Should().Be(20);
        f.Inquebravel.Should().BeFalse();

        var ss = body!.Single(r => r.Rank == "SS");
        ss.Durabilidade.Should().BeNull();
        ss.Inquebravel.Should().BeTrue();
    }

    [Fact]
    public async Task Update_by_a_Rules_Auditor_persists_the_new_value_and_the_list_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut1", "duraudput1@teste.com");
        await GrantRulesAuditorAsync("duraudput1@teste.com");
        var undo = await CaptureUndoAsync("C");

        try
        {
            var putResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/C", gmToken,
                new UpdateDurabilidadePorRankRequest(130, false)));
            putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/durabilidades-por-rank", gmToken));
            var list = await listResponse.Content.ReadFromJsonAsync<List<DurabilidadePorRankResponse>>();
            var c = list!.Single(r => r.Rank == "C");
            c.Durabilidade.Should().Be(130);
            c.Inquebravel.Should().BeFalse();
        }
        finally
        {
            await undo();
        }
    }

    [Fact]
    public async Task An_Auditoria_change_is_immediately_visible_on_an_Item_of_that_Rank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudItem1", "durauditem1@teste.com");
        await GrantRulesAuditorAsync("durauditem1@teste.com");
        var undo = await CaptureUndoAsync("C");

        try
        {
            var putResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/C", gmToken,
                new UpdateDurabilidadePorRankRequest(130, false)));
            putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var itemRequest = new CreateItemRequest("Arma", "Espada de Teste Auditoria", 1.5m, 50, null, "Espadas", null, "C", "UmaMao", "2D6", 3, "19", 2, "Cortante",
                null, null, null, null, null, null, null, null, null);
            var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, itemRequest));
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var item = await createResponse.Content.ReadFromJsonAsync<ItemResponse>();
            item!.DurabilidadeMaxima.Should().Be(130);
        }
        finally
        {
            await undo();
        }
    }

    [Fact]
    public async Task Update_setting_Inquebravel_true_with_a_Durabilidade_value_stores_null_Durabilidade()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut2", "duraudput2@teste.com");
        await GrantRulesAuditorAsync("duraudput2@teste.com");
        var undo = await CaptureUndoAsync("A");

        try
        {
            var putResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/A", gmToken,
                new UpdateDurabilidadePorRankRequest(999, true)));
            putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/durabilidades-por-rank", gmToken));
            var list = await listResponse.Content.ReadFromJsonAsync<List<DurabilidadePorRankResponse>>();
            var a = list!.Single(r => r.Rank == "A");
            a.Durabilidade.Should().BeNull();
            a.Inquebravel.Should().BeTrue();
        }
        finally
        {
            await undo();
        }
    }

    [Fact]
    public async Task Update_can_turn_an_Inquebravel_rank_back_into_a_numbered_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut3", "duraudput3@teste.com");
        await GrantRulesAuditorAsync("duraudput3@teste.com");
        var undo = await CaptureUndoAsync("S");

        try
        {
            var putResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/S", gmToken,
                new UpdateDurabilidadePorRankRequest(300, false)));
            putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/durabilidades-por-rank", gmToken));
            var list = await listResponse.Content.ReadFromJsonAsync<List<DurabilidadePorRankResponse>>();
            var s = list!.Single(r => r.Rank == "S");
            s.Durabilidade.Should().Be(300);
            s.Inquebravel.Should().BeFalse();
        }
        finally
        {
            await undo();
        }
    }

    [Fact]
    public async Task Update_with_no_Durabilidade_and_not_Inquebravel_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut4", "duraudput4@teste.com");
        await GrantRulesAuditorAsync("duraudput4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/D", gmToken,
            new UpdateDurabilidadePorRankRequest(null, false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_Durabilidade_0_and_not_Inquebravel_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut5", "duraudput5@teste.com");
        await GrantRulesAuditorAsync("duraudput5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/D", gmToken,
            new UpdateDurabilidadePorRankRequest(0, false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_with_an_unknown_rank_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut6", "duraudput6@teste.com");
        await GrantRulesAuditorAsync("duraudput6@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/Z", gmToken,
            new UpdateDurabilidadePorRankRequest(10, false)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_with_a_numeric_string_rank_returns_404()
    {
        // Enum.TryParse<RankDeItem>("3", ...) would succeed (RankDeItem is stored as int) — the
        // controller must reject this rather than resolve it to a rank by ordinal.
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut7", "duraudput7@teste.com");
        await GrantRulesAuditorAsync("duraudput7@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/3", gmToken,
            new UpdateDurabilidadePorRankRequest(10, false)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("DurAudPut8", "duraudput8@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/durabilidades-por-rank/D", gmToken,
            new UpdateDurabilidadePorRankRequest(90, false)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
