using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EncountersControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EncountersControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private async Task<string> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private async Task<string> CreateEncounterAsync(string gmToken, string campaignId, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gmToken, new CreateEncounterRequest(nome)));
        return (await response.Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
    }

    private async Task<string> CreateNpcSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> AddParticipantAsync(string gmToken, string encounterId, string npcSheetId, int iniciativa)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, npcSheetId, null, iniciativa)));
        return (await response.Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
    }

    private async Task<List<EncounterParticipantResponse>> ListParticipantsAsync(string gmToken, string encounterId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>())!;
    }

    private async Task SetAcoesRestantesAsync(string gmToken, string encounterId, EncounterParticipantResponse participant, int acoesRestantes) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{participant.Id}", gmToken,
            new UpdateParticipantRequest(participant.Iniciativa, participant.PV, participant.PF, participant.PA, acoesRestantes, new List<string>())));

    [Fact]
    public async Task Create_returns_201_starting_at_Round_1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGm1", "enc1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Encontro");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gmToken,
            new CreateEncounterRequest("Emboscada na Ponte")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<EncounterResponse>();
        body!.Nome.Should().Be("Emboscada na Ponte");
        body.CurrentRound.Should().Be(1);
        body.CurrentParticipantIndex.Should().Be(0);
    }

    [Fact]
    public async Task List_returns_only_encounters_for_campaigns_the_caller_owns()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("EncGmA", "enca@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("EncGmB", "encb@teste.com");
        var campaignA = await CreateCampaignAsync(tokenA, "Campanha A");
        var campaignB = await CreateCampaignAsync(tokenB, "Campanha B");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignA}/encounters", tokenA, new CreateEncounterRequest("Encontro A")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignB}/encounters", tokenB, new CreateEncounterRequest("Encontro B")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignA}/encounters", tokenA));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<EncounterResponse>>();
        body!.Should().HaveCount(1);
        body.Should().ContainSingle(e => e.Nome == "Encontro A");
        body.Should().NotContain(e => e.Nome == "Encontro B");
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmJog", "encgmjog@teste.com");
        var jogadorToken = await RegisterJogadorLinkedToAsync(gmToken, "EncJogador", "encjogador@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha do Jogador");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", jogadorToken,
            new CreateEncounterRequest("Não deveria funcionar")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_in_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EncOwner", "encowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EncOther", "encother@teste.com");
        var campaignOfOwner = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfOwner}/encounters", gmTokenOther,
            new CreateEncounterRequest("Invasão")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdvanceTurn_moves_to_the_next_participant_and_resets_their_AcoesRestantes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmAdv1", "encadv1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Avançar Turno");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Avançar Turno");

        var npcHigh = await CreateNpcSheetAsync(gmToken);
        var npcMid = await CreateNpcSheetAsync(gmToken);
        var npcLow = await CreateNpcSheetAsync(gmToken);
        var highId = await AddParticipantAsync(gmToken, encounterId, npcHigh, 20);
        var midId = await AddParticipantAsync(gmToken, encounterId, npcMid, 10);
        var lowId = await AddParticipantAsync(gmToken, encounterId, npcLow, 5);

        // Drive every participant's AcoesRestantes down to 0 first so a spurious reset (wrong
        // participant, or all of them) is detectable below — the default from Add is already 3.
        foreach (var p in await ListParticipantsAsync(gmToken, encounterId))
            await SetAcoesRestantesAsync(gmToken, encounterId, p, 0);

        // Regression guard: a SECOND, unrelated encounter with its own participant at an Iniciativa
        // (100) higher than every participant above. If AdvanceTurn's participant lookup ever lost its
        // `Where(p => p.EncounterId == encounterId)` filter, a global OrderByDescending(Iniciativa)
        // .Skip(1) would land on "Alta" (Iniciativa 20, this encounter's index 0) instead of "Media"
        // (Iniciativa 10, the real next participant) — the assertions below would then fail.
        var otherCampaignId = await CreateCampaignAsync(gmToken, "Campanha Não Relacionada");
        var otherEncounterId = await CreateEncounterAsync(gmToken, otherCampaignId, "Encontro Não Relacionado");
        var npcOther = await CreateNpcSheetAsync(gmToken);
        await AddParticipantAsync(gmToken, otherEncounterId, npcOther, 100);

        var advanceResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmToken));

        advanceResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var encountersList = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/encounters", gmToken));
        var encounters = await encountersList.Content.ReadFromJsonAsync<List<EncounterResponse>>();
        var encounter = encounters!.Should().ContainSingle(e => e.Id == encounterId).Which;
        encounter.CurrentRound.Should().Be(1);
        encounter.CurrentParticipantIndex.Should().Be(1);

        var after = await ListParticipantsAsync(gmToken, encounterId);
        after.Single(p => p.Id == highId).AcoesRestantes.Should().Be(0);
        after.Single(p => p.Id == midId).AcoesRestantes.Should().Be(3);
        after.Single(p => p.Id == lowId).AcoesRestantes.Should().Be(0);

        var otherAfter = await ListParticipantsAsync(gmToken, otherEncounterId);
        otherAfter.Should().ContainSingle().Which.AcoesRestantes.Should().Be(3);
    }

    [Fact]
    public async Task AdvanceTurn_past_the_last_participant_starts_a_new_round()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmAdv2", "encadv2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Nova Rodada");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Nova Rodada");

        var npcA = await CreateNpcSheetAsync(gmToken);
        var npcB = await CreateNpcSheetAsync(gmToken);
        var firstId = await AddParticipantAsync(gmToken, encounterId, npcA, 20);
        await AddParticipantAsync(gmToken, encounterId, npcB, 10);

        // Advance once: index 0 -> 1 (still round 1).
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmToken));

        // Advance again: index 1 was the last participant -> wraps to round 2, index 0.
        var advanceResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmToken));

        advanceResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var encountersList = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/encounters", gmToken));
        var encounters = await encountersList.Content.ReadFromJsonAsync<List<EncounterResponse>>();
        var encounter = encounters!.Should().ContainSingle(e => e.Id == encounterId).Which;
        encounter.CurrentRound.Should().Be(2);
        encounter.CurrentParticipantIndex.Should().Be(0);

        var after = await ListParticipantsAsync(gmToken, encounterId);
        after.Single(p => p.Id == firstId).AcoesRestantes.Should().Be(3);
    }
}
