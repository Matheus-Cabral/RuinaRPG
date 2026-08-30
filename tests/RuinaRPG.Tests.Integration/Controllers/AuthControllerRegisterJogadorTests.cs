using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerRegisterJogadorTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerRegisterJogadorTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<(string Token, Guid GmId)> RegisterGmAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        return (tokens.AccessToken, Guid.Parse(meBody!.Id));
    }

    private async Task<string> GenerateCodeAsync(string gmToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<InviteCodeResponse>();
        return body!.Code;
    }

    [Fact]
    public async Task Register_jogador_with_a_valid_active_code_returns_201_and_links_to_the_gm()
    {
        var (gmToken, gmId) = await RegisterGmAsync("JogadorGm1", "jogadorgm1@teste.com");
        var code = await GenerateCodeAsync(gmToken);

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador1", "jogador1@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var jogador = await db.Users.SingleAsync(u => u.Nickname == "Jogador1");
        jogador.InvitedByGmId.Should().Be(gmId);
    }

    [Fact]
    public async Task Register_jogador_marks_the_code_as_Usado_so_it_cannot_be_reused()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm2", "jogadorgm2@teste.com");
        var code = await GenerateCodeAsync(gmToken);
        await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador2", "jogador2@teste.com", "Senha!123", "Senha!123", code));

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador2b", "jogador2b@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_a_nonexistent_code_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("JogadorNoCode", "jogadornocode@teste.com", "Senha!123", "Senha!123", "NAOEXIST"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_a_revoked_code_returns_400()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm3", "jogadorgm3@teste.com");
        var code = await GenerateCodeAsync(gmToken);
        var revoke = new HttpRequestMessage(HttpMethod.Post, $"/api/invite-codes/{code}/revoke");
        revoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        await _client.SendAsync(revoke);

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador3", "jogador3@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_an_expired_code_returns_400()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm4", "jogadorgm4@teste.com");
        var code = await GenerateCodeAsync(gmToken);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var inviteCode = await db.InviteCodes.SingleAsync(c => c.Code == code);
            inviteCode.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador4", "jogador4@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_mismatched_password_confirmation_returns_400()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm5", "jogadorgm5@teste.com");
        var code = await GenerateCodeAsync(gmToken);

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador5", "jogador5@teste.com", "Senha!123", "Outra!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("", "0")]
    [InlineData("   ", "1")]
    public async Task Register_jogador_with_an_empty_or_whitespace_nickname_returns_400(string nickname, string suffix)
    {
        var (gmToken, _) = await RegisterGmAsync($"JogadorGm6{suffix}", $"jogadorgm6{suffix}@teste.com");
        var code = await GenerateCodeAsync(gmToken);

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, $"jogador6{suffix}@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
