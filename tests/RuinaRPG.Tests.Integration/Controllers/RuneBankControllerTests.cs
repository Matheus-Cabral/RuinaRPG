using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Runes;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RuneBankControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RuneBankControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<RuneBankEntryResponse> CreateAsync(string token, string nome, string descricao, int grau)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", token, new CreateRuneBankEntryRequest(nome, descricao, grau)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;
    }

    private async Task<List<RuneBankEntryResponse>> ListAsync(string token, string query = "")
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/rune-bank{query}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/rune-bank", new CreateRuneBankEntryRequest("Runa", "Desc.", 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_as_the_gm_returns_201_with_the_saved_fields()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm1", "runebankgm1@teste.com");

        var created = await CreateAsync(token, "Runa do Fogo", "Queima o alvo.", 2);

        created.Nome.Should().Be("Runa do Fogo");
        created.Descricao.Should().Be("Queima o alvo.");
        created.Grau.Should().Be(2);
        created.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Every_endpoint_is_forbidden_to_a_jogador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBankGm2", "runebankgm2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "RuneBankJogador2", "runebankjogador2@teste.com");
        var entry = await CreateAsync(gmToken, "Runa Secreta", "Só o GM vê.", 1);

        var post = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", jogadorToken, new CreateRuneBankEntryRequest("X", "Y", 1)));
        var get = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rune-bank", jogadorToken));
        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", jogadorToken, new UpdateRuneBankEntryRequest("X", "Y", 1)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", jogadorToken));

        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        get.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_returns_only_the_entries_of_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("RuneBankGmA3", "runebankgma3@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("RuneBankGmB3", "runebankgmb3@teste.com");
        var minha = await CreateAsync(tokenA, "Runa do GM A", "A.", 1);
        var alheia = await CreateAsync(tokenB, "Runa do GM B", "B.", 1);

        var body = await ListAsync(tokenA);

        body.Should().Contain(e => e.Id == minha.Id);
        body.Should().NotContain(e => e.Id == alheia.Id);
    }

    [Fact]
    public async Task List_filters_by_nome_case_insensitively_and_by_grau()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm4", "runebankgm4@teste.com");
        var fogo1 = await CreateAsync(token, "Runa do Fogo", "F.", 1);
        var fogo3 = await CreateAsync(token, "Runa do FOGO Maior", "F3.", 3);
        var gelo1 = await CreateAsync(token, "Runa do Gelo", "G.", 1);

        var porNome = await ListAsync(token, "?nome=fogo");
        porNome.Select(e => e.Id).Should().BeEquivalentTo([fogo1.Id, fogo3.Id]);

        var porGrau = await ListAsync(token, "?grau=1");
        porGrau.Select(e => e.Id).Should().BeEquivalentTo([fogo1.Id, gelo1.Id]);

        var combinado = await ListAsync(token, "?nome=fogo&grau=3");
        combinado.Select(e => e.Id).Should().BeEquivalentTo([fogo3.Id]);
    }

    [Fact]
    public async Task Update_replaces_the_fields_and_returns_204()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm5", "runebankgm5@teste.com");
        var entry = await CreateAsync(token, "Runa Velha", "Antiga.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", token, new UpdateRuneBankEntryRequest("Runa Nova", "Atual.", 4)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var atualizada = (await ListAsync(token)).Single(e => e.Id == entry.Id);
        atualizada.Nome.Should().Be("Runa Nova");
        atualizada.Descricao.Should().Be("Atual.");
        atualizada.Grau.Should().Be(4);
    }

    [Fact]
    public async Task Update_and_delete_of_another_gms_entry_return_404()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("RuneBankGmA6", "runebankgma6@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("RuneBankGmB6", "runebankgmb6@teste.com");
        var entry = await CreateAsync(tokenA, "Runa do A", "A.", 1);

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", tokenB, new UpdateRuneBankEntryRequest("Roubada", "X.", 9)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", tokenB));

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ListAsync(tokenA)).Single(e => e.Id == entry.Id).Nome.Should().Be("Runa do A");
    }

    [Fact]
    public async Task Delete_removes_the_entry_and_returns_204()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm7", "runebankgm7@teste.com");
        var entry = await CreateAsync(token, "Runa Efêmera", "Some.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", token));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(token)).Should().NotContain(e => e.Id == entry.Id);
    }

    [Fact]
    public async Task Update_and_delete_of_an_unknown_id_return_404()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm8", "runebankgm8@teste.com");

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{Guid.NewGuid()}", token, new UpdateRuneBankEntryRequest("X", "Y", 1)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{Guid.NewGuid()}", token));

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
