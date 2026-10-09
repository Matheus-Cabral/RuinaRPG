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

/// <summary>
/// Auditoria endpoint over the Tabela de Afinidades (see "Tabela de Afinidades.md"): GET is open to
/// any authenticated user; POST/PUT/DELETE are gated to the Rules Auditor, same DB check as
/// DurabilidadesPorRankController. Tests that create/edit/delete a row restore the table afterwards,
/// since every Fact in this class shares one database. Created rows use Afinidade 900+ so they never
/// collide with the 22 seeded ones.
/// </summary>
public class TabelaDeAfinidadesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Url = "/api/tabela-de-afinidades";

    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public TabelaDeAfinidadesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> AuditorTokenAsync(string nickname, string email)
    {
        var token = await RegisterGmAndGetTokenAsync(nickname, email);
        await GrantRulesAuditorAsync(email);
        return token;
    }

    private async Task<List<LinhaDaTabelaDeAfinidadesResponse>> ListAsync(string token) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, Url, token)))
            .Content.ReadFromJsonAsync<List<LinhaDaTabelaDeAfinidadesResponse>>())!;

    private async Task<LinhaDaTabelaDeAfinidadesResponse> CreateAsync(string token, int afinidade, int eficiencia, int dano)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token,
            new SalvarLinhaDaTabelaDeAfinidadesRequest(afinidade, eficiencia, dano)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<LinhaDaTabelaDeAfinidadesResponse>())!;
    }

    private Task DeleteAsync(string token, Guid id) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{id}", token));

    [Fact]
    public async Task List_returns_the_rows_ordered_by_Afinidade_to_any_authenticated_user()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AfinList1", "afinlist1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, Url, gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<List<LinhaDaTabelaDeAfinidadesResponse>>())!;
        body.Should().HaveCount(22);
        body.First().Afinidade.Should().Be(0);
        body.Last().Afinidade.Should().Be(21);
        body.Select(l => l.Afinidade).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_adds_a_row()
    {
        var token = await AuditorTokenAsync("AfinCreate1", "afincreate1@teste.com");

        var criada = await CreateAsync(token, 900, 5, 4);
        try
        {
            criada.Id.Should().NotBeEmpty();
            var linha = (await ListAsync(token)).Single(l => l.Afinidade == 900);
            (linha.Id, linha.Eficiencia, linha.Dano).Should().Be((criada.Id, 5, 4));
        }
        finally
        {
            await DeleteAsync(token, criada.Id);
        }
    }

    [Fact]
    public async Task Create_rejects_a_repeated_Afinidade_with_409()
    {
        var token = await AuditorTokenAsync("AfinCreate2", "afincreate2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token,
            new SalvarLinhaDaTabelaDeAfinidadesRequest(7, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Já existe uma linha para a Afinidade 7.");
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(901, -1, 1)]
    [InlineData(902, 1, -1)]
    public async Task Create_rejects_negative_values_with_400(int afinidade, int eficiencia, int dano)
    {
        var token = await AuditorTokenAsync($"AfinNeg{afinidade + 1}", $"afinneg{afinidade + 1}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, token,
            new SalvarLinhaDaTabelaDeAfinidadesRequest(afinidade, eficiencia, dano)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("devem ser números inteiros maiores ou iguais a zero");
    }

    [Fact]
    public async Task Update_changes_a_row()
    {
        var token = await AuditorTokenAsync("AfinUpd1", "afinupd1@teste.com");
        var criada = await CreateAsync(token, 910, 1, 1);
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{criada.Id}", token,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(911, 8, 6)));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            var linha = (await ListAsync(token)).Single(l => l.Id == criada.Id);
            (linha.Afinidade, linha.Eficiencia, linha.Dano).Should().Be((911, 8, 6));
        }
        finally
        {
            await DeleteAsync(token, criada.Id);
        }
    }

    [Fact]
    public async Task Update_keeping_its_own_Afinidade_is_not_a_conflict()
    {
        var token = await AuditorTokenAsync("AfinUpd2", "afinupd2@teste.com");
        var criada = await CreateAsync(token, 920, 1, 1);
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{criada.Id}", token,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(920, 9, 9)));

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await DeleteAsync(token, criada.Id);
        }
    }

    [Fact]
    public async Task Update_to_another_rows_Afinidade_is_409()
    {
        var token = await AuditorTokenAsync("AfinUpd3", "afinupd3@teste.com");
        var criada = await CreateAsync(token, 930, 1, 1);
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{criada.Id}", token,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(7, 1, 1)));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            await DeleteAsync(token, criada.Id);
        }
    }

    [Fact]
    public async Task Update_rejects_negative_values_with_400()
    {
        var token = await AuditorTokenAsync("AfinUpd4", "afinupd4@teste.com");
        var criada = await CreateAsync(token, 940, 1, 1);
        try
        {
            var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{criada.Id}", token,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(940, -1, 1)));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await DeleteAsync(token, criada.Id);
        }
    }

    [Fact]
    public async Task Update_and_Delete_of_an_unknown_id_are_404()
    {
        var token = await AuditorTokenAsync("AfinUnknown1", "afinunknown1@teste.com");
        var id = Guid.NewGuid();

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{id}", token,
            new SalvarLinhaDaTabelaDeAfinidadesRequest(950, 1, 1)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{id}", token));

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_a_row()
    {
        var token = await AuditorTokenAsync("AfinDel1", "afindel1@teste.com");
        var criada = await CreateAsync(token, 960, 1, 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{criada.Id}", token));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(token)).Should().NotContain(l => l.Id == criada.Id);
    }

    [Fact]
    public async Task Create_Update_and_Delete_are_403_for_a_non_Auditor()
    {
        var auditorToken = await AuditorTokenAsync("AfinForbA", "afinforba@teste.com");
        var gmToken = await RegisterGmAndGetTokenAsync("AfinForbB", "afinforbb@teste.com");
        var criada = await CreateAsync(auditorToken, 970, 1, 1);
        try
        {
            var post = await _client.SendAsync(AuthedRequest(HttpMethod.Post, Url, gmToken,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(971, 1, 1)));
            var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"{Url}/{criada.Id}", gmToken,
                new SalvarLinhaDaTabelaDeAfinidadesRequest(970, 2, 2)));
            var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"{Url}/{criada.Id}", gmToken));

            post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await ListAsync(auditorToken)).Should().Contain(l => l.Id == criada.Id && l.Eficiencia == 1);
        }
        finally
        {
            await DeleteAsync(auditorToken, criada.Id);
        }
    }
}
