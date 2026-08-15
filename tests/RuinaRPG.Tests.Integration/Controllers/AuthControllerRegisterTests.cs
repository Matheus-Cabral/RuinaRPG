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
}
