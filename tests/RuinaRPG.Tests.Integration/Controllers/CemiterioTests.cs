using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CemiterioTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private sealed record Cenario(string GmToken, string PlayerId, string PlayerToken, string CampaignId, string SheetId);

    private async Task<Cenario> MontarAsync()
    {
        var gmToken = await RegisterGmAndGetTokenAsync(Unique("Gm"), Unique("gm") + "@t.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, Unique("Jg"), Unique("jg") + "@t.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Camp");
        (await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId))))
            .EnsureSuccessStatusCode();
        var created = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        created.EnsureSuccessStatusCode();
        var sheet = (await created.Content.ReadFromJsonAsync<CharacterSheetResponse>())!;
        return new Cenario(gmToken, playerId, playerToken, campaignId, sheet.Id);
    }

    private async Task MarcarMortoAsync(string sheetId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await db.CharacterSheets.Where(s => s.Id == Guid.Parse(sheetId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Morto, true));
    }

    private async Task RemoverMembroAsync(Cenario c)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await db.CampaignMembers.Where(m => m.CampaignId == Guid.Parse(c.CampaignId) && m.UserId == Guid.Parse(c.PlayerId)).ExecuteDeleteAsync();
    }

    private async Task ReinserirMembroAsync(Cenario c)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        db.CampaignMembers.Add(new RuinaRPG.Infrastructure.Campaigns.CampaignMember { Id = Guid.NewGuid(), CampaignId = Guid.Parse(c.CampaignId), UserId = Guid.Parse(c.PlayerId) });
        await db.SaveChangesAsync();
    }

    private async Task<CharacterSheetResponse> GetAsync(string token, string sheetId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!;
    }

    [Fact]
    public async Task Ficha_nova_nao_esta_no_cemiterio()
    {
        var c = await MontarAsync();
        var sheet = await GetAsync(c.GmToken, c.SheetId);
        sheet.NoCemiterio.Should().BeFalse();
        sheet.MotivosDoCemiterio.Should().BeEmpty();
    }

    [Fact]
    public async Task Ficha_morta_esta_no_cemiterio_com_motivo_Morte()
    {
        var c = await MontarAsync();
        await MarcarMortoAsync(c.SheetId);
        var sheet = await GetAsync(c.GmToken, c.SheetId);
        sheet.NoCemiterio.Should().BeTrue();
        sheet.MotivosDoCemiterio.Should().Equal("Morte");
    }

    [Fact]
    public async Task Dono_fora_da_campanha_poe_a_ficha_no_cemiterio()
    {
        var c = await MontarAsync();
        await RemoverMembroAsync(c);
        var sheet = await GetAsync(c.PlayerToken, c.SheetId);
        sheet.NoCemiterio.Should().BeTrue();
        sheet.MotivosDoCemiterio.Should().Equal("RemovidoDaCampanha");
    }

    [Fact]
    public async Task Dono_de_volta_a_campanha_tira_a_ficha_do_cemiterio()
    {
        var c = await MontarAsync();
        await RemoverMembroAsync(c);
        await ReinserirMembroAsync(c);
        var sheet = await GetAsync(c.PlayerToken, c.SheetId);
        sheet.NoCemiterio.Should().BeFalse();
        sheet.MotivosDoCemiterio.Should().BeEmpty();
    }

    [Fact]
    public async Task Dono_de_volta_mas_morto_continua_no_cemiterio()
    {
        var c = await MontarAsync();
        await MarcarMortoAsync(c.SheetId);
        await RemoverMembroAsync(c);
        await ReinserirMembroAsync(c);
        var sheet = await GetAsync(c.PlayerToken, c.SheetId);
        sheet.NoCemiterio.Should().BeTrue();
        sheet.MotivosDoCemiterio.Should().Equal("Morte");
    }

    [Fact]
    public async Task Lista_do_jogador_traz_o_estado_do_cemiterio()
    {
        var c = await MontarAsync();
        await MarcarMortoAsync(c.SheetId);
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/character-sheets/mine", c.PlayerToken));
        var lista = (await response.Content.ReadFromJsonAsync<List<MyCharacterSheetSummaryResponse>>())!;
        var item = lista.Single(s => s.Id == c.SheetId);
        item.NoCemiterio.Should().BeTrue();
        item.MotivosDoCemiterio.Should().Equal("Morte");
    }

    [Fact]
    public async Task Lista_da_campanha_traz_o_estado_do_cemiterio()
    {
        var c = await MontarAsync();
        await RemoverMembroAsync(c);
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{c.CampaignId}/character-sheets", c.GmToken));
        var lista = (await response.Content.ReadFromJsonAsync<List<CharacterSheetResponse>>())!;
        var item = lista.Single(s => s.Id == c.SheetId);
        item.NoCemiterio.Should().BeTrue();
        item.MotivosDoCemiterio.Should().Equal("RemovidoDaCampanha");
    }

    [Fact]
    public async Task Minha_campanha_nao_lista_ficha_no_cemiterio()
    {
        var c = await MontarAsync();
        var antes = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{c.CampaignId}/player-view", c.PlayerToken));
        (await antes.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>())!.MinhasFichas.Should().Contain(f => f.Id == c.SheetId);

        await MarcarMortoAsync(c.SheetId);
        var depois = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{c.CampaignId}/player-view", c.PlayerToken));
        (await depois.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>())!.MinhasFichas.Should().NotContain(f => f.Id == c.SheetId);
    }

    private Task<HttpResponseMessage> DefinirMorteAsync(string token, string sheetId, bool morto) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/morte", token, new DefinirMorteRequest(morto)));

    [Fact]
    public async Task GM_marca_e_desmarca_morto()
    {
        var c = await MontarAsync();
        (await DefinirMorteAsync(c.GmToken, c.SheetId, true)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(c.GmToken, c.SheetId)).MotivosDoCemiterio.Should().Equal("Morte");
        (await DefinirMorteAsync(c.GmToken, c.SheetId, false)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(c.GmToken, c.SheetId)).NoCemiterio.Should().BeFalse();
    }

    [Fact]
    public async Task Dono_nao_pode_marcar_morto()
    {
        var c = await MontarAsync();
        (await DefinirMorteAsync(c.PlayerToken, c.SheetId, true)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Outro_GM_recebe_404()
    {
        var c = await MontarAsync();
        var outroGm = await RegisterGmAndGetTokenAsync(Unique("Gm"), Unique("gm") + "@t.com");
        (await DefinirMorteAsync(outroGm, c.SheetId, true)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DefinirMorteAsync(c.GmToken, Guid.NewGuid().ToString(), true)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Marcar_morto_entra_no_log_da_ficha()
    {
        var c = await MontarAsync();
        (await DefinirMorteAsync(c.GmToken, c.SheetId, true)).EnsureSuccessStatusCode();
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/sheet-logs/character/{c.SheetId}", c.GmToken));
        var pagina = (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.SheetLogs.SheetLogPageResponse>())!;
        var linha = pagina.Itens.Single(i => i.Campo == "Morto");
        linha.ActorIsGm.Should().BeTrue();
    }
}
