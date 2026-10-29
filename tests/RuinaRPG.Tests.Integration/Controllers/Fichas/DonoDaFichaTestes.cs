using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Auth;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// "Alterar dono" de uma ficha de NPC/Criatura do Mestre anexada a uma campanha (<c>PUT /api/{tipo}/{id}/dono</c>):
/// o Mestre a passa para um membro ativo da campanha ou a devolve a si mesmo. A ficha é a mesma, não uma cópia.
/// </summary>
public abstract class DonoDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    private const string Recusa = "O jogador informado não é membro ativo de uma campanha desta ficha.";

    /// <summary>O Tipo da concessão (<c>GrantSheetRequest.Tipo</c>).</summary>
    protected abstract string TipoDaConcessao { get; }

    /// <summary>Uma ficha concedida a um Jogador, numa campanha de que ele é membro.</summary>
    private sealed record Concessao(string GmToken, string CampaignId, string SheetId, string DonoId, string DonoToken);

    private async Task<Concessao> ConcessaoAsync()
    {
        var (gm, _) = await MestreAsync();
        var (playerId, playerToken) = await JogadorAsync(gm);
        var campaignId = await CampanhaComMembroAsync(gm, playerId);
        var grant = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gm, new GrantSheetRequest(playerId, TipoDaConcessao, null));
        return new Concessao(gm, campaignId, (await grant.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId, playerId, playerToken);
    }

    private async Task<(string Id, string Token)> OutroMembroAsync(Concessao c)
    {
        var (id, token) = await JogadorAsync(c.GmToken);
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/members", c.GmToken, new AddCampaignMemberRequest(id));
        return (id, token);
    }

    private Task<HttpResponseMessage> AlterarDonoAsync(string token, string sheetId, string? donoId) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{sheetId}/dono", token, new AlterarDonoRequest(donoId)));

    private async Task<HttpStatusCode> LerFichaAsync(string token, string sheetId) =>
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/{Tipo.Rota}/{sheetId}", token))).StatusCode;

    private async Task<List<GrantedSheetSummary>> MeusCompanheirosAsync(string token, string campaignId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/player-view", token));
        return (await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>())!.MeusCompanheiros;
    }

    private async Task DesativarContaAsync(string userId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await db.Users.Where(u => u.Id == Guid.Parse(userId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadoDaConta.Desativada));
    }

    [Fact]
    public async Task GM_passa_a_ficha_para_outro_membro()
    {
        var c = await ConcessaoAsync();
        var (novoId, novoToken) = await OutroMembroAsync(c);

        var response = await AlterarDonoAsync(c.GmToken, c.SheetId, novoId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerFichaAsync(novoToken, c.SheetId)).Should().Be(HttpStatusCode.OK);
        (await LerFichaAsync(c.DonoToken, c.SheetId)).Should().Be(HttpStatusCode.NotFound);
        (await MeusCompanheirosAsync(novoToken, c.CampaignId)).Should().ContainSingle(s => s.Id == c.SheetId);
    }

    [Fact]
    public async Task GM_devolve_a_ficha_a_si_mesmo()
    {
        var c = await ConcessaoAsync();

        var response = await AlterarDonoAsync(c.GmToken, c.SheetId, null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerFichaAsync(c.DonoToken, c.SheetId)).Should().Be(HttpStatusCode.NotFound);
        (await MeusCompanheirosAsync(c.DonoToken, c.CampaignId)).Should().NotContain(s => s.Id == c.SheetId);
        (await LerFichaAsync(c.GmToken, c.SheetId)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ficha_devolvida_continua_anexada_e_pode_ser_dada_de_novo()
    {
        var c = await ConcessaoAsync();
        (await AlterarDonoAsync(c.GmToken, c.SheetId, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await AlterarDonoAsync(c.GmToken, c.SheetId, c.DonoId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerFichaAsync(c.DonoToken, c.SheetId)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dono_que_nao_e_membro_ativo_e_recusado()
    {
        var c = await ConcessaoAsync();
        var (naoMembroId, _) = await JogadorAsync(c.GmToken);
        var (desativadoId, _) = await OutroMembroAsync(c);
        await DesativarContaAsync(desativadoId);
        var (outroGm, _) = await MestreAsync();
        var (deOutroGmId, _) = await JogadorAsync(outroGm);

        foreach (var donoId in new[] { naoMembroId, desativadoId, deOutroGmId, Guid.NewGuid().ToString(), "não-é-guid" })
        {
            var response = await AlterarDonoAsync(c.GmToken, c.SheetId, donoId);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, donoId);
            (await response.Content.ReadAsStringAsync()).Should().Be(Recusa);
        }
        (await LerFichaAsync(c.DonoToken, c.SheetId)).Should().Be(HttpStatusCode.OK, "a ficha continua com o dono de antes");
    }

    [Fact]
    public async Task Ficha_do_GM_sem_campanha_nao_tem_a_quem_ser_dada()
    {
        var ficha = await NovaFichaAsync();
        var (playerId, _) = await JogadorAsync(ficha.GmToken);
        await CampanhaComMembroAsync(ficha.GmToken, playerId);

        var response = await AlterarDonoAsync(ficha.GmToken, ficha.SheetId, playerId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Be(Recusa);
    }

    [Fact]
    public async Task Jogador_nao_altera_dono()
    {
        var c = await ConcessaoAsync();

        var response = await AlterarDonoAsync(c.DonoToken, c.SheetId, null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LerFichaAsync(c.DonoToken, c.SheetId)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Outro_GM_recebe_404()
    {
        var c = await ConcessaoAsync();
        var outroGm = await OutroMestreAsync();

        var response = await AlterarDonoAsync(outroGm, c.SheetId, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LerFichaAsync(c.DonoToken, c.SheetId)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ficha_inexistente_recebe_404()
    {
        var (gm, _) = await MestreAsync();

        var response = await AlterarDonoAsync(gm, Guid.NewGuid().ToString(), null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public sealed class DonoDaFichaTestesNpc(ApiFixture fixture) : DonoDaFichaTestes<TipoDeTesteNpc>(fixture)
{
    protected override string TipoDaConcessao => "Npc";
}

public sealed class DonoDaFichaTestesCriatura(ApiFixture fixture) : DonoDaFichaTestes<TipoDeTesteCriatura>(fixture)
{
    protected override string TipoDaConcessao => "Creature";
}
