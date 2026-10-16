using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerChangePasswordTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> RegisterGmAsync(string nickname, string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    [Fact]
    public async Task ChangePassword_without_a_token_returns_401()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("NovaSenha!456", "NovaSenha!456"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_with_mismatched_confirmation_returns_400()
    {
        var token = await RegisterGmAsync("ChangePwGm1", "changepwgm1@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/auth/change-password", token,
            new ChangePasswordRequest("NovaSenha!456", "Outra!789")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_with_matching_passwords_returns_204_and_the_new_password_works()
    {
        var token = await RegisterGmAsync("ChangePwGm2", "changepwgm2@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/auth/change-password", token,
            new ChangePasswordRequest("NovaSenha!456", "NovaSenha!456")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ChangePwGm2", "NovaSenha!456"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_clears_MustChangePassword_that_the_GM_password_reset_CLI_set()
    {
        var token = await RegisterGmAsync("ChangePwGm3", "changepwgm3@teste.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await GmPasswordResetCli.ResetGmPasswordAsync(db, userManager, "changepwgm3@teste.com");
        }

        // Re-login with a fresh (still valid) token isn't needed — the original access token from
        // registration remains valid; MustChangePassword is checked live via /me, not baked into it.
        var meBefore = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", token));
        (await meBefore.Content.ReadFromJsonAsync<MeResponse>())!.MustChangePassword.Should().BeTrue();

        var change = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/auth/change-password", token,
            new ChangePasswordRequest("NovaSenha!456", "NovaSenha!456")));
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfter = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", token));
        (await meAfter.Content.ReadFromJsonAsync<MeResponse>())!.MustChangePassword.Should().BeFalse();
    }
}
