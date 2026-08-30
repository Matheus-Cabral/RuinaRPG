using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerRegisterTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerRegisterTests(PostgresFixture postgres) => _postgres = postgres;

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

    [Fact]
    public async Task Register_gm_with_valid_data_returns_201_and_tokens()
    {
        var request = new RegisterGmRequest("MestreTeste", "mestre@teste.com", "Senha!123", "Senha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_gm_with_mismatched_password_confirmation_returns_400()
    {
        var request = new RegisterGmRequest("MestreTeste2", "mestre2@teste.com", "Senha!123", "OutraSenha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_gm_with_duplicate_email_returns_400()
    {
        var request = new RegisterGmRequest("Original", "duplicado@teste.com", "Senha!123", "Senha!123");
        await _client.PostAsJsonAsync("/api/auth/register/gm", request);

        var duplicate = new RegisterGmRequest("Duplicado", "duplicado@teste.com", "Senha!123", "Senha!123");
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", duplicate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_gm_with_a_nickname_that_differs_only_by_case_returns_400()
    {
        // Login e Cadastro R0003: o Nickname é único no sistema. Case-insensitively so,
        // otherwise "Mestre" and "mestre" become two indistinguishable accounts.
        var first = new RegisterGmRequest("NickUnico", "nick.unico@teste.com", "Senha!123", "Senha!123");
        var firstResponse = await _client.PostAsJsonAsync("/api/auth/register/gm", first);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = new RegisterGmRequest("nickunico", "outro.nick@teste.com", "Senha!123", "Senha!123");
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", duplicate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_gm_with_an_empty_or_whitespace_nickname_returns_400(string nickname)
    {
        var request = new RegisterGmRequest(nickname, "nickvazio@teste.com", "Senha!123", "Senha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_gm_with_a_duplicate_nickname_leaves_login_working()
    {
        // A duplicate that slips into the database makes the nickname lookup ambiguous,
        // which turns every subsequent login for that nickname into a permanent 500.
        var first = new RegisterGmRequest("NickLogin", "nick.login@teste.com", "Senha!123", "Senha!123");
        await _client.PostAsJsonAsync("/api/auth/register/gm", first);
        await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest("NickLogin", "nick.login2@teste.com", "Senha!123", "Senha!123"));

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("NickLogin", "Senha!123"));

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_finds_the_user_regardless_of_nickname_casing()
    {
        await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest("NickCase", "nick.case@teste.com", "Senha!123", "Senha!123"));

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nIcKcAsE", "Senha!123"));

        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Two_gms_with_independently_generated_random_nicknames_can_both_register()
    {
        var first = new RegisterGmRequest(TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail(), "Senha!123", "Senha!123");
        var second = new RegisterGmRequest(TestDataFaker.UniqueNickname(), TestDataFaker.UniqueEmail(), "Senha!123", "Senha!123");

        var firstResponse = await _client.PostAsJsonAsync("/api/auth/register/gm", first);
        var secondResponse = await _client.PostAsJsonAsync("/api/auth/register/gm", second);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
