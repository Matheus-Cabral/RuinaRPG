using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerLoginTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerLoginTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task RegisterAsync(string nickname, string email, string senha) =>
        await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, senha, senha));

    [Fact]
    public async Task Login_with_correct_credentials_returns_200_and_tokens()
    {
        await RegisterAsync("LoginGm", "login@teste.com", "Senha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LoginGm", "Senha!123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_with_generic_message()
    {
        await RegisterAsync("LoginGm2", "login2@teste.com", "Senha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LoginGm2", "SenhaErrada"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("senha", "a mensagem não pode indicar qual campo estava errado (R0002)");
    }

    [Fact]
    public async Task Login_with_unknown_nickname_returns_the_same_401_message_as_wrong_password()
    {
        var unknownResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("NaoExiste", "Qualquer1!"));
        await RegisterAsync("LoginGm3", "login3@teste.com", "Senha!123");
        var wrongPasswordResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LoginGm3", "SenhaErrada"));

        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadAsStringAsync();
        unknownBody.Should().Be(wrongPasswordBody);
    }

    [Fact]
    public async Task Refresh_with_a_valid_token_returns_a_new_token_pair()
    {
        await RegisterAsync("RefreshGm", "refresh@teste.com", "Senha!123");
        var login = await (await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("RefreshGm", "Senha!123")))
            .Content.ReadFromJsonAsync<AuthResponse>();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login!.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.RefreshToken.Should().NotBe(login.RefreshToken, "refresh should rotate the token");
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_so_it_cannot_be_reused()
    {
        await RegisterAsync("LogoutGm", "logout@teste.com", "Senha!123");
        var login = await (await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LogoutGm", "Senha!123")))
            .Content.ReadFromJsonAsync<AuthResponse>();

        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(login!.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
