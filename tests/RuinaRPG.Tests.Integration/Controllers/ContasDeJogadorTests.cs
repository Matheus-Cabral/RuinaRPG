using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Domain.Auth;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Convite de Jogador R0006–R0009: o GM que convidou o jogador desativa, reativa e exclui a conta dele.
/// </summary>
public class ContasDeJogadorTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private sealed record Jogador(string Id, string Nickname, string Email, string Token, string RefreshToken);

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod method, string url, string token, object? body = null, HttpStatusCode? esperado = null)
    {
        var response = await Client.SendAsync(AuthedRequest(method, url, token, body));
        if (esperado is { } status)
            response.StatusCode.Should().Be(status, $"{method} {url}: {await response.Content.ReadAsStringAsync()}");
        else
            response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} devolveu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private async Task<T> LerAsync<T>(string token, string url) =>
        (await (await EnviarAsync(HttpMethod.Get, url, token, esperado: HttpStatusCode.OK)).Content.ReadFromJsonAsync<T>())!;

    private async Task<HttpResponseMessage> CadastrarAsync(string gm, string nickname, string email)
    {
        var code = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;
        return await Client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
    }

    private async Task<Jogador> JogadorAsync(string gm)
    {
        var nickname = Unique("CjJg");
        var email = Unique("cjjg") + "@t.com";
        var response = await CadastrarAsync(gm, nickname, email);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await LerAsync<MeResponse>(tokens.AccessToken, "/api/auth/me");
        return new Jogador(me.Id, nickname, email, tokens.AccessToken, tokens.RefreshToken);
    }

    private Task<string> GmAsync() => RegisterGmAndGetTokenAsync(Unique("CjGm"), Unique("cjgm") + "@t.com");

    private Task<HttpResponseMessage> AcaoAsync(string gm, string jogadorId, string acao, object? body = null) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/players/{jogadorId}/{acao}", gm, body));

    private Task AdicionarMembroAsync(string gm, string campaignId, string userId) =>
        EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(userId), HttpStatusCode.NoContent);

    private async Task<string> PersonagemAsync(string gm, string campaignId, string donoId) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gm, new CreateCharacterSheetRequest(donoId)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

    private async Task<string> EncontroAsync(string gm, string campaignId, params AddParticipantRequest[] participantes)
    {
        var id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gm, new CreateEncounterRequest("Encontro")))
            .Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
        foreach (var participante in participantes)
            await EnviarAsync(HttpMethod.Post, $"/api/encounters/{id}/participants", gm, participante, HttpStatusCode.Created);
        return id;
    }

    private async Task<string> EstadoAsync(string jogadorId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == Guid.Parse(jogadorId))).Estado.ToString();
    }

    [Fact]
    public async Task Desativar_tira_de_todas_as_campanhas_e_revoga_tokens()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        var x = await CreateCampaignAsync(gm, "X");
        var y = await CreateCampaignAsync(gm, "Y");
        var semEle = await CreateCampaignAsync(gm, "Sem ele");
        await AdicionarMembroAsync(gm, x, jogador.Id);
        await AdicionarMembroAsync(gm, y, jogador.Id);
        var ariaX = await PersonagemAsync(gm, x, jogador.Id);
        var ariaY = await PersonagemAsync(gm, y, jogador.Id);
        var fera = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{x}/grants", gm, new GrantSheetRequest(jogador.Id, "Npc", null)))
            .Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            // Fera concedida e anexada às duas campanhas que ele deixa.
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.CampaignAttachments.Add(new CampaignAttachment { Id = Guid.NewGuid(), CampaignId = Guid.Parse(y), NpcSheetId = Guid.Parse(fera) });
            await db.SaveChangesAsync();
        }
        var encontroX = await EncontroAsync(gm, x, new AddParticipantRequest(ariaX, null, null, 10));
        var encontroY = await EncontroAsync(gm, y, new AddParticipantRequest(ariaY, null, null, 10), new AddParticipantRequest(null, fera, null, 5));

        (await AcaoAsync(gm, jogador.Id, "desativar")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EstadoAsync(jogador.Id)).Should().Be("Desativada");
        (await LerAsync<List<CampaignMemberResponse>>(gm, $"/api/campaigns/{x}/members")).Should().BeEmpty();
        (await LerAsync<List<CampaignMemberResponse>>(gm, $"/api/campaigns/{y}/members")).Should().BeEmpty();
        (await LerAsync<List<CampaignMemberResponse>>(gm, $"/api/campaigns/{semEle}/members")).Should().BeEmpty();
        (await LerAsync<List<EncounterParticipantResponse>>(gm, $"/api/encounters/{encontroX}/participants")).Should().BeEmpty();
        (await LerAsync<List<EncounterParticipantResponse>>(gm, $"/api/encounters/{encontroY}/participants")).Should().BeEmpty();
        var ficha = await LerAsync<CharacterSheetResponse>(gm, $"/api/character-sheets/{ariaX}");
        ficha.MotivosDoCemiterio.Should().Equal("RemovidoDaCampanha", "ContaDesativada");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.NpcSheets.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(fera))).OwnerId.Should().BeNull();
            (await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == Guid.Parse(jogador.Id)).ToListAsync())
                .Should().NotBeEmpty().And.OnlyContain(t => t.RevokedAt != null);
        }
        (await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(jogador.RefreshToken))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reativar_volta_a_ativa_sem_campanhas()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        var campanha = await CreateCampaignAsync(gm, "Campanha");
        await AdicionarMembroAsync(gm, campanha, jogador.Id);
        (await AcaoAsync(gm, jogador.Id, "desativar")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AcaoAsync(gm, jogador.Id, "reativar")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EstadoAsync(jogador.Id)).Should().Be("Ativa");
        (await LerAsync<List<CampaignMemberResponse>>(gm, $"/api/campaigns/{campanha}/members")).Should().BeEmpty();
        await AdicionarMembroAsync(gm, campanha, jogador.Id);
    }

    [Fact]
    public async Task Excluir_exige_o_apelido()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);

        var response = await AcaoAsync(gm, jogador.Id, "excluir", new ExcluirContaRequest("outro apelido"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("O apelido não confere.");
        (await EstadoAsync(jogador.Id)).Should().Be("Ativa");
    }

    [Fact]
    public async Task Excluir_libera_o_email_e_reserva_o_apelido()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        var campanha = await CreateCampaignAsync(gm, "Campanha");
        await AdicionarMembroAsync(gm, campanha, jogador.Id);
        var aria = await PersonagemAsync(gm, campanha, jogador.Id);

        (await AcaoAsync(gm, jogador.Id, "excluir", new ExcluirContaRequest(jogador.Nickname))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var conta = await db.Users.AsNoTracking().SingleAsync(u => u.Id == Guid.Parse(jogador.Id));
            conta.Estado.Should().Be(EstadoDaConta.Excluida);
            conta.Email.Should().BeNull();
            conta.NormalizedEmail.Should().BeNull();
            conta.PasswordHash.Should().BeNull();
            conta.UserName.Should().Be($"excluida-{conta.Id:N}");
            conta.NormalizedUserName.Should().Be($"EXCLUIDA-{conta.Id:N}".ToUpperInvariant());
            conta.Nickname.Should().Be(jogador.Nickname);
            (await db.CampaignMembers.AnyAsync(m => m.UserId == conta.Id)).Should().BeFalse();
            (await db.RefreshTokens.AnyAsync(t => t.UserId == conta.Id && t.RevokedAt == null)).Should().BeFalse();
        }
        (await LerAsync<CharacterSheetResponse>(gm, $"/api/character-sheets/{aria}")).MotivosDoCemiterio
            .Should().Equal("RemovidoDaCampanha", "ContaExcluida");

        (await CadastrarAsync(gm, Unique("CjNovo"), jogador.Email)).StatusCode.Should().Be(HttpStatusCode.Created);
        var mesmoApelido = await CadastrarAsync(gm, jogador.Nickname, Unique("cjout") + "@t.com");
        mesmoApelido.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await mesmoApelido.Content.ReadAsStringAsync()).Should().Contain("Este Nickname já está em uso.");
    }

    [Fact]
    public async Task Transicao_invalida_da_409()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);

        (await AcaoAsync(gm, jogador.Id, "reativar")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await AcaoAsync(gm, jogador.Id, "excluir", new ExcluirContaRequest(jogador.Nickname))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AcaoAsync(gm, jogador.Id, "desativar")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AcaoAsync(gm, jogador.Id, "reativar")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AcaoAsync(gm, jogador.Id, "excluir", new ExcluirContaRequest(jogador.Nickname))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Outro_GM_recebe_404()
    {
        var gm = await GmAsync();
        var outroGm = await GmAsync();
        var jogador = await JogadorAsync(gm);

        (await AcaoAsync(outroGm, jogador.Id, "desativar")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AcaoAsync(outroGm, jogador.Id, "reativar")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AcaoAsync(outroGm, jogador.Id, "excluir", new ExcluirContaRequest(jogador.Nickname))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AcaoAsync(gm, Guid.NewGuid().ToString(), "desativar")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EstadoAsync(jogador.Id)).Should().Be("Ativa");
    }

    [Fact]
    public async Task Busca_mostra_estado_e_omite_excluidas()
    {
        var gm = await GmAsync();
        var ativa = await JogadorAsync(gm);
        var desativada = await JogadorAsync(gm);
        var excluida = await JogadorAsync(gm);
        (await AcaoAsync(gm, desativada.Id, "desativar")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AcaoAsync(gm, excluida.Id, "excluir", new ExcluirContaRequest(excluida.Nickname))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var todos = await LerAsync<List<PlayerSearchResultResponse>>(gm, "/api/players");
        todos.Select(p => (p.Id, p.Estado)).Should().BeEquivalentTo([(ativa.Id, "Ativa"), (desativada.Id, "Desativada")]);
        todos.Single(p => p.Id == desativada.Id).Email.Should().Be(desativada.Email);

        (await LerAsync<List<PlayerSearchResultResponse>>(gm, $"/api/players?q={excluida.Nickname}")).Should().BeEmpty();
    }

    [Fact]
    public async Task Nao_adiciona_membro_desativado()
    {
        var gm = await GmAsync();
        var jogador = await JogadorAsync(gm);
        var campanha = await CreateCampaignAsync(gm, "Campanha");
        (await AcaoAsync(gm, jogador.Id, "desativar")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/members", gm, new AddCampaignMemberRequest(jogador.Id)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("O jogador informado não está vinculado à sua conta.");
        (await LerAsync<List<CampaignMemberResponse>>(gm, $"/api/campaigns/{campanha}/members")).Should().BeEmpty();
    }

    [Fact]
    public async Task Nao_redefine_senha_de_excluida()
    {
        var gm = await GmAsync();
        var desativada = await JogadorAsync(gm);
        var excluida = await JogadorAsync(gm);
        (await AcaoAsync(gm, desativada.Id, "desativar")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AcaoAsync(gm, excluida.Id, "excluir", new ExcluirContaRequest(excluida.Nickname))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var nova = new ResetPlayerPasswordRequest("NovaSenha!123", "NovaSenha!123");

        (await AcaoAsync(gm, excluida.Id, "reset-password", nova)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AcaoAsync(gm, desativada.Id, "reset-password", nova)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
