using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Exercises CharacterSheetStats/NpcSheetStats/CreatureSheetStats — the services that back both
/// each sheet's /sub-attributes endpoint (unchanged after the Task 4 move) and the
/// FichaParaRequisitos snapshot consumed by Passiva requisitos (Task 5). Asserts the snapshot's
/// numbers equal what the sheet's own endpoints already show, rather than re-deriving formulas.
/// </summary>
public class SheetStatsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SheetStatsTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateCreatureSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
    }

    [Fact]
    public async Task Character_snapshot_matches_what_the_sheet_endpoints_show()
    {
        var gm = await RegisterGmAndGetTokenAsync("StatsGm1", "statsgm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "StatsPlayer1", "statsplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gm, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken, new UpdateCharacterAttributeRequest(5, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken, new UpdateCharacterAttributeRequest(2, 0, false)));

        var subAttributes = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken)))
            .Content.ReadFromJsonAsync<SubAttributesResponse>();
        var skills = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken)))
            .Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var stats = scope.ServiceProvider.GetRequiredService<CharacterSheetStats>();
        var sheet = await db.CharacterSheets.FindAsync(Guid.Parse(sheetId));
        var ficha = await stats.FichaParaRequisitosAsync(sheet!);

        ficha.TemIdentidadeDePersonagem.Should().BeTrue();
        ficha.Nivel.Should().Be(sheet!.Nivel);
        ficha.Atributos[Atributo.Forca].Should().Be(5);
        ficha.Atributos.Should().HaveCount(8);
        ficha.SubAtributos[SubAtributo.Iniciativa].Should().Be(subAttributes!.Iniciativa);
        ficha.SubAtributos[SubAtributo.DefesaNatural].Should().Be(subAttributes.DefesaNatural);
        foreach (var skill in skills!)
            ficha.Pericias[PericiasIniciais.Todas.Single(p => p.Chave == skill.Pericia).Id].Should().Be(skill.Total);
    }

    [Fact]
    public async Task Creature_snapshot_has_no_personagem_identity_and_only_its_own_attributes_and_pericias()
    {
        var gm = await RegisterGmAndGetTokenAsync("StatsGm2", "statsgm2@teste.com");
        var sheetId = await CreateCreatureSheetAsync(gm);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var stats = scope.ServiceProvider.GetRequiredService<CreatureSheetStats>();
        var ficha = await stats.FichaParaRequisitosAsync((await db.CreatureSheets.FindAsync(Guid.Parse(sheetId)))!);

        ficha.TemIdentidadeDePersonagem.Should().BeFalse();
        ficha.Atributos.Keys.Should().BeEquivalentTo([Atributo.Forca, Atributo.Vigor, Atributo.Agilidade, Atributo.Destreza, Atributo.Astucia]);
        ficha.Pericias.Keys.Should().BeEquivalentTo(PericiasIniciais.Todas.Where(p => p.DisponivelParaCriaturas).Select(p => p.Id));
        ficha.SubAtributos.Should().HaveCount(6);
    }

    [Fact]
    public async Task Npc_snapshot_has_personagem_identity()
    {
        var gm = await RegisterGmAndGetTokenAsync("StatsGm3", "statsgm3@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var stats = scope.ServiceProvider.GetRequiredService<NpcSheetStats>();
        var ficha = await stats.FichaParaRequisitosAsync((await db.NpcSheets.FindAsync(Guid.Parse(sheetId)))!);

        ficha.TemIdentidadeDePersonagem.Should().BeTrue();
        ficha.Atributos.Should().HaveCount(8);
    }
}
