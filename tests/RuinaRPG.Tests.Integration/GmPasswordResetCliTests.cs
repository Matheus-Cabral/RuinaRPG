using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

public class GmPasswordResetCliTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public GmPasswordResetCliTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<AuthResponse> RegisterGmAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<string> RegisterJogadorAsync(string gmToken, string nickname, string email)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(message);
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private (RuinaRpgDbContext Db, UserManager<ApplicationUser> UserManager, IServiceScope Scope) NewScopedServices()
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (db, userManager, scope);
    }

    [Fact]
    public async Task ResetGmPasswordAsync_generates_a_working_temporary_password_and_flags_the_account()
    {
        await RegisterGmAsync("CliResetGm1", "clireset1@teste.com");
        GmPasswordResetResult result;
        var (db, userManager, scope) = NewScopedServices();
        using (scope)
        {
            result = await GmPasswordResetCli.ResetGmPasswordAsync(db, userManager, "clireset1@teste.com");
        }

        result.Error.Should().BeNull();
        result.Nickname.Should().Be("CliResetGm1");
        result.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("CliResetGm1", result.TemporaryPassword!));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetGmPasswordAsync_sets_MustChangePassword_on_the_account()
    {
        await RegisterGmAsync("CliResetGm2", "clireset2@teste.com");
        var (db, userManager, scope) = NewScopedServices();
        using (scope)
        {
            await GmPasswordResetCli.ResetGmPasswordAsync(db, userManager, "clireset2@teste.com");

            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "CLIRESET2@TESTE.COM");
            user.MustChangePassword.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ResetGmPasswordAsync_revokes_existing_refresh_tokens()
    {
        var tokens = await RegisterGmAsync("CliResetGm3", "clireset3@teste.com");
        var (db, userManager, scope) = NewScopedServices();
        using (scope)
        {
            await GmPasswordResetCli.ResetGmPasswordAsync(db, userManager, "clireset3@teste.com");
        }

        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetGmPasswordAsync_matches_email_case_insensitively()
    {
        await RegisterGmAsync("CliResetGm4", "clireset4@teste.com");
        var (db, userManager, scope) = NewScopedServices();
        using (scope)
        {
            var result = await GmPasswordResetCli.ResetGmPasswordAsync(db, userManager, "CLIRESET4@TESTE.COM");

            result.Error.Should().BeNull();
        }
    }

    [Fact]
    public async Task ResetGmPasswordAsync_returns_an_error_for_an_unknown_email()
    {
        var (db, userManager, scope) = NewScopedServices();
        using (scope)
        {
            var result = await GmPasswordResetCli.ResetGmPasswordAsync(db, userManager, "naoexiste@teste.com");

            result.Error.Should().NotBeNull();
            result.Nickname.Should().BeNull();
            result.TemporaryPassword.Should().BeNull();
        }
    }

    [Fact]
    public async Task ResetGmPasswordAsync_refuses_a_jogador_account()
    {
        var gm = await RegisterGmAsync("CliResetGmOwner", "clireset5owner@teste.com");
        await RegisterJogadorAsync(gm.AccessToken, "CliResetJogador5", "clireset5@teste.com");
        var (db, userManager, scope) = NewScopedServices();
        using (scope)
        {
            var result = await GmPasswordResetCli.ResetGmPasswordAsync(db, userManager, "clireset5@teste.com");

            result.Error.Should().NotBeNull();
            result.TemporaryPassword.Should().BeNull();
        }
    }
}
