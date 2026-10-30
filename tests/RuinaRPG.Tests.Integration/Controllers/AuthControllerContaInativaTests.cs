using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Domain.Auth;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Login e Cadastro R0002: conta de jogador desativada não entra (login, refresh, token ainda válido, hub);
/// conta excluída recebe a mensagem genérica de credenciais inválidas.
/// </summary>
public class AuthControllerContaInativaTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Senha = "Senha!123";

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private sealed record Jogador(string Id, string Nickname, string Token, string RefreshToken);

    private Task<string> GmAsync() => RegisterGmAndGetTokenAsync(Unique("CiGm"), Unique("cigm") + "@t.com");

    private async Task<Jogador> JogadorAsync(string gm)
    {
        var code = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gm)))
            .Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;
        var nickname = Unique("CiJg");
        var response = await Client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, Unique("cijg") + "@t.com", Senha, Senha, code));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken)))
            .Content.ReadFromJsonAsync<MeResponse>())!;
        return new Jogador(me.Id, nickname, tokens.AccessToken, tokens.RefreshToken);
    }

    private async Task AcaoAsync(string gm, string jogadorId, string acao, object? body = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/players/{jogadorId}/{acao}", gm, body));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> LoginAsync(string nickname, string senha) =>
        Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(nickname, senha));

    [Fact]
    public async Task Login_de_conta_desativada_mostra_o_texto()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        await AcaoAsync(gm, jogador.Id, "desativar");

        var response = await LoginAsync(jogador.Nickname, Senha);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Be(TextosDaConta.ContaDesativada);
    }

    [Fact]
    public async Task Senha_errada_nao_revela_o_estado()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        await AcaoAsync(gm, jogador.Id, "desativar");

        var response = await LoginAsync(jogador.Nickname, "SenhaErrada!1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Be("Credenciais inválidas.");
    }

    [Fact]
    public async Task Token_de_acesso_ainda_valido_e_recusado_apos_desativar()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        await AcaoAsync(gm, jogador.Id, "desativar");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", jogador.Token));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gm))).StatusCode
            .Should().Be(HttpStatusCode.OK, "o token do GM continua valendo");
    }

    [Fact]
    public async Task Hub_recusa_conta_desativada()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        await AcaoAsync(gm, jogador.Id, "desativar");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(jogador.Token);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        var conectar = () => connection.StartAsync();

        await conectar.Should().ThrowAsync<HttpRequestException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_de_conta_desativada_e_recusado()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        // Só o estado muda (sem revogar o token de refresh), para provar que o próprio Refresh confere o dono.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            await db.Users.Where(u => u.Id == Guid.Parse(jogador.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Estado, EstadoDaConta.Desativada));
        }

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(jogador.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Conta_reativada_volta_a_entrar()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        await AcaoAsync(gm, jogador.Id, "desativar");
        await AcaoAsync(gm, jogador.Id, "reativar");

        var response = await LoginAsync(jogador.Nickname, Senha);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken))).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Conta_excluida_nao_entra()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        await AcaoAsync(gm, jogador.Id, "excluir", new ExcluirContaRequest(jogador.Nickname));

        var response = await LoginAsync(jogador.Nickname, Senha);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Be("Credenciais inválidas.");
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", jogador.Token))).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }
}
