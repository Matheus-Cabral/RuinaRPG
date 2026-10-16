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

public class InviteCodesControllerTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private async Task<string> RegisterJogadorAsync(string gmToken, string nickname, string email)
    {
        var code = await GenerateCodeAsync(gmToken);

        var response = await Client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    [Fact]
    public async Task Generate_without_a_token_returns_401()
    {
        var response = await Client.PostAsync("/api/invite-codes", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Generate_returns_201_with_an_8_char_code_in_Ativo_status()
    {
        var token = await RegisterGmAndGetTokenAsync("ConviteGm1", "convite1@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", token));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InviteCodeResponse>();
        body!.Code.Should().HaveLength(8);
        body.Status.Should().Be("Ativo");
        body.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));
        body.RedeemedByNickname.Should().BeNull();
        body.RedeemedAt.Should().BeNull();
    }

    [Fact]
    public async Task List_returns_only_codes_generated_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("ConviteGmA", "convitea@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("ConviteGmB", "conviteb@teste.com");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", tokenA));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", tokenB));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/invite-codes", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<InviteCodeResponse>>();
        body!.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await Client.GetAsync("/api/invite-codes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_with_a_jogador_token_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ConviteGmRole", "conviterole@teste.com");
        var jogadorToken = await RegisterJogadorAsync(gmToken, "ConviteRoleCheck", "conviterolecheck@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/invite-codes", jogadorToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_shows_the_original_ExpiresAt_for_an_Expirado_code()
    {
        var token = await RegisterGmAndGetTokenAsync("ExpiraGm1", "expiragm1@teste.com");
        var code = await GenerateCodeAsync(token);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var inviteCode = await db.InviteCodes.SingleAsync(c => c.Code == code);
            inviteCode.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/invite-codes", token));

        var body = await response.Content.ReadFromJsonAsync<List<InviteCodeResponse>>();
        var entry = body!.Single(c => c.Code == code);
        entry.Status.Should().Be("Expirado");
        entry.ExpiresAt.Should().NotBeNull();
    }

    private async Task<string> GenerateCodeAsync(string token)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", token));
        var body = await response.Content.ReadFromJsonAsync<InviteCodeResponse>();
        return body!.Code;
    }

    [Fact]
    public async Task Revoke_an_active_code_returns_204_and_the_list_shows_Revogado()
    {
        var token = await RegisterGmAndGetTokenAsync("RevokeGm1", "revoke1@teste.com");
        var code = await GenerateCodeAsync(token);

        var revoke = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", token));
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/invite-codes", token));
        var body = await list.Content.ReadFromJsonAsync<List<InviteCodeResponse>>();
        body!.Single(c => c.Code == code).Status.Should().Be("Revogado");
    }

    [Fact]
    public async Task Revoke_a_code_owned_by_another_gm_returns_404()
    {
        var tokenOwner = await RegisterGmAndGetTokenAsync("RevokeOwner", "revokeowner@teste.com");
        var tokenOther = await RegisterGmAndGetTokenAsync("RevokeOther", "revokeother@teste.com");
        var code = await GenerateCodeAsync(tokenOwner);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", tokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Revoke_an_already_revoked_code_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("RevokeTwice", "revoketwice@teste.com");
        var code = await GenerateCodeAsync(token);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", token));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", token));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Revoke_a_nonexistent_code_returns_404()
    {
        var token = await RegisterGmAndGetTokenAsync("RevokeMissing", "revokemissing@teste.com");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes/NOTAREAL/revoke", token));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
