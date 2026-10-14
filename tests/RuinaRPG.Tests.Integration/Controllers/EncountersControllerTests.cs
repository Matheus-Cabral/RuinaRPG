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

    private async Task<HttpResponseMessage> PostAsync(string gmToken, string url) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, url, gmToken));

    private async Task<HttpResponseMessage> StartAsync(string gmToken, string encounterId) =>
        await PostAsync(gmToken, $"/api/encounters/{encounterId}/start");

    private async Task<HttpResponseMessage> AdvanceAsync(string gmToken, string encounterId) =>
        await PostAsync(gmToken, $"/api/encounters/{encounterId}/advance-turn");

    private async Task<EncounterResponse> GetAsync(string gmToken, string campaignId, string encounterId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/encounters", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<EncounterResponse>>())!.Single(e => e.Id == encounterId);
    }

    private async Task<(string Campaign, string Encounter, List<string> Ids)> EncounterWithAsync(string gmToken, params int[] iniciativas)
    {
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha " + Guid.NewGuid());
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro");
        var ids = new List<string>();
        foreach (var ini in iniciativas)
            ids.Add(await AddParticipantAsync(gmToken, encounterId, await CreateNpcSheetAsync(gmToken), ini));
        return (campaignId, encounterId, ids);
    }

    private static async Task<string> BodyAsync(HttpResponseMessage r) => await r.Content.ReadAsStringAsync();

    [Fact]
    public async Task AdvanceTurn_with_no_participants_asks_to_add_one_before_passing_the_vez()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncAdv0", "encadv0@teste.com");
        var (_, enc, ids) = await EncounterWithAsync(gm, 10);
        await StartAsync(gm, enc);
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/encounters/{enc}/participants/{ids[0]}", gm));

        var r = await AdvanceAsync(gm, enc);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BodyAsync(r)).Should().Contain("Adicione ao menos um participante antes de passar a vez.");
    }

    [Fact]
    public async Task Start_requires_a_participant()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS1", "encs1@teste.com");
        var (_, enc, _) = await EncounterWithAsync(gm);

        var r = await StartAsync(gm, enc);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BodyAsync(r)).Should().Contain("Adicione ao menos um participante antes de iniciar o encontro.");
    }

    [Fact]
    public async Task Start_moves_to_Turno_1_on_the_highest_Iniciativa_and_gives_it_3_actions()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS2", "encs2@teste.com");
        var (camp, enc, ids) = await EncounterWithAsync(gm, 5, 20, 10);
        foreach (var p in await ListParticipantsAsync(gm, enc))
            await SetAcoesRestantesAsync(gm, enc, p, 0);

        var r = await StartAsync(gm, enc);

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var e = await GetAsync(gm, camp, enc);
        e.Status.Should().Be("EmAndamento");
        e.CurrentRound.Should().Be(1);
        e.CurrentParticipantId.Should().Be(ids[1]);
        var ps = await ListParticipantsAsync(gm, enc);
        ps.Single(p => p.Id == ids[1]).AcoesRestantes.Should().Be(3);
        ps.Where(p => p.Id != ids[1]).Should().OnlyContain(p => p.AcoesRestantes == 0);
    }

    [Fact]
    public async Task Start_twice_is_409()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS3", "encs3@teste.com");
        var (_, enc, _) = await EncounterWithAsync(gm, 10);
        await StartAsync(gm, enc);

        var r = await StartAsync(gm, enc);

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BodyAsync(r)).Should().Contain("O encontro já foi iniciado.");
    }

    [Fact]
    public async Task Advance_previous_choose_and_finish_are_409_before_start()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS4", "encs4@teste.com");
        var (_, enc, ids) = await EncounterWithAsync(gm, 10);

        foreach (var url in new[] { "advance-turn", "previous-turn", $"turn/{ids[0]}", "finish" })
        {
            var r = await PostAsync(gm, $"/api/encounters/{enc}/{url}");
            r.StatusCode.Should().Be(HttpStatusCode.Conflict, url);
            (await BodyAsync(r)).Should().Contain("O encontro não está em andamento.", url);
        }
    }

    [Fact]
    public async Task Advance_walks_the_order_and_wraps_into_the_next_Turno()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS5", "encs5@teste.com");
        var (camp, enc, ids) = await EncounterWithAsync(gm, 30, 20, 10);
        await StartAsync(gm, enc);

        var expected = new[] { (1, 1), (2, 1), (0, 2), (1, 2) };
        foreach (var (idx, round) in expected)
        {
            foreach (var p in await ListParticipantsAsync(gm, enc))
                await SetAcoesRestantesAsync(gm, enc, p, 0);
            (await AdvanceAsync(gm, enc)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var e = await GetAsync(gm, camp, enc);
            e.CurrentParticipantId.Should().Be(ids[idx]);
            e.CurrentRound.Should().Be(round);
            (await ListParticipantsAsync(gm, enc)).Single(p => p.Id == ids[idx]).AcoesRestantes.Should().Be(3);
        }
    }

    [Fact]
    public async Task Previous_steps_back_wraps_to_the_Turno_before_and_is_400_at_the_first_of_Turno_1()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS6", "encs6@teste.com");
        var (camp, enc, ids) = await EncounterWithAsync(gm, 30, 20);
        await StartAsync(gm, enc);

        var atStart = await PostAsync(gm, $"/api/encounters/{enc}/previous-turn");
        atStart.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BodyAsync(atStart)).Should().Contain("Não há vez anterior.");

        await AdvanceAsync(gm, enc); // ids[1], Turno 1
        await AdvanceAsync(gm, enc); // ids[0], Turno 2
        // Not reset when stepping back: put the destination at 1 action.
        var target = (await ListParticipantsAsync(gm, enc)).Single(p => p.Id == ids[1]);
        await SetAcoesRestantesAsync(gm, enc, target, 1);

        (await PostAsync(gm, $"/api/encounters/{enc}/previous-turn")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var e = await GetAsync(gm, camp, enc);
        e.CurrentParticipantId.Should().Be(ids[1]);
        e.CurrentRound.Should().Be(1);
        (await ListParticipantsAsync(gm, enc)).Single(p => p.Id == ids[1]).AcoesRestantes.Should().Be(1);

        (await PostAsync(gm, $"/api/encounters/{enc}/previous-turn")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(gm, camp, enc)).CurrentParticipantId.Should().Be(ids[0]);
        (await PostAsync(gm, $"/api/encounters/{enc}/previous-turn")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Choosing_a_participant_gives_it_the_turn_in_the_same_Turno_with_3_actions()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS7", "encs7@teste.com");
        var (camp, enc, ids) = await EncounterWithAsync(gm, 30, 20, 10);
        await StartAsync(gm, enc);
        await AdvanceAsync(gm, enc);
        foreach (var p in await ListParticipantsAsync(gm, enc))
            await SetAcoesRestantesAsync(gm, enc, p, 0);

        var r = await PostAsync(gm, $"/api/encounters/{enc}/turn/{ids[2]}");

        r.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var e = await GetAsync(gm, camp, enc);
        e.CurrentParticipantId.Should().Be(ids[2]);
        e.CurrentRound.Should().Be(1);
        var ps = await ListParticipantsAsync(gm, enc);
        ps.Single(p => p.Id == ids[2]).AcoesRestantes.Should().Be(3);
        ps.Where(p => p.Id != ids[2]).Should().OnlyContain(p => p.AcoesRestantes == 0);
    }

    [Fact]
    public async Task Choosing_a_participant_of_another_encounter_is_404()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS8", "encs8@teste.com");
        var (_, enc, _) = await EncounterWithAsync(gm, 30);
        var (_, _, otherIds) = await EncounterWithAsync(gm, 30);
        await StartAsync(gm, enc);

        var r = await PostAsync(gm, $"/api/encounters/{enc}/turn/{otherIds[0]}");

        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Two_advances_in_a_row_each_move_exactly_one_step()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS9", "encs9@teste.com");
        var (camp, enc, ids) = await EncounterWithAsync(gm, 40, 30, 20, 10);
        await StartAsync(gm, enc);

        (await AdvanceAsync(gm, enc)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AdvanceAsync(gm, enc)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var e = await GetAsync(gm, camp, enc);
        e.CurrentParticipantId.Should().Be(ids[2]);
        e.CurrentRound.Should().Be(1);
    }

    [Fact]
    public async Task Finish_makes_everything_but_rename_and_reopen_409()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS10", "encs10@teste.com");
        var (camp, enc, ids) = await EncounterWithAsync(gm, 30, 20);
        await StartAsync(gm, enc);

        (await PostAsync(gm, $"/api/encounters/{enc}/finish")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var e = await GetAsync(gm, camp, enc);
        e.Status.Should().Be("Finalizado");
        e.FinalizadoEm.Should().NotBeNull();

        var npc = await CreateNpcSheetAsync(gm);
        var requests = new List<HttpRequestMessage>
        {
            AuthedRequest(HttpMethod.Post, $"/api/encounters/{enc}/participants", gm, new AddParticipantRequest(null, npc, null, 5)),
            AuthedRequest(HttpMethod.Put, $"/api/encounters/{enc}/participants/{ids[0]}", gm, new UpdateParticipantRequest(30, null, null, null, 2, new List<string>())),
            AuthedRequest(HttpMethod.Delete, $"/api/encounters/{enc}/participants/{ids[0]}", gm),
            AuthedRequest(HttpMethod.Post, $"/api/encounters/{enc}/advance-turn", gm),
            AuthedRequest(HttpMethod.Post, $"/api/encounters/{enc}/previous-turn", gm),
            AuthedRequest(HttpMethod.Post, $"/api/encounters/{enc}/turn/{ids[1]}", gm),
            AuthedRequest(HttpMethod.Post, $"/api/encounters/{enc}/start", gm),
            AuthedRequest(HttpMethod.Post, $"/api/encounters/{enc}/finish", gm),
        };
        foreach (var req in requests)
        {
            var r = await _client.SendAsync(req);
            r.StatusCode.Should().Be(HttpStatusCode.Conflict, $"{req.Method} {req.RequestUri}");
            (await BodyAsync(r)).Should().Contain("Encontro finalizado.");
        }

        var rename = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{enc}", gm, new UpdateEncounterRequest("Novo")));
        rename.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reopen_restores_the_same_Turno_and_vez_and_clears_FinalizadoEm()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS11", "encs11@teste.com");
        var (camp, enc, ids) = await EncounterWithAsync(gm, 30, 20);
        await StartAsync(gm, enc);
        await AdvanceAsync(gm, enc);
        await AdvanceAsync(gm, enc); // ids[0], Turno 2
        await PostAsync(gm, $"/api/encounters/{enc}/finish");

        (await PostAsync(gm, $"/api/encounters/{enc}/reopen")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var e = await GetAsync(gm, camp, enc);
        e.Status.Should().Be("EmAndamento");
        e.FinalizadoEm.Should().BeNull();
        e.CurrentRound.Should().Be(2);
        e.CurrentParticipantId.Should().Be(ids[0]);
    }

    [Fact]
    public async Task Reopen_before_finish_is_409()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncS12", "encs12@teste.com");
        var (_, enc, _) = await EncounterWithAsync(gm, 30);
        await StartAsync(gm, enc);

        var r = await PostAsync(gm, $"/api/encounters/{enc}/reopen");

        r.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BodyAsync(r)).Should().Contain("O encontro não está finalizado.");
    }

    [Fact]
    public async Task A_gm_of_another_campaign_gets_404_on_every_new_endpoint()
    {
        var owner = await RegisterGmAndGetTokenAsync("EncS13a", "encs13a@teste.com");
        var other = await RegisterGmAndGetTokenAsync("EncS13b", "encs13b@teste.com");
        var (_, enc, ids) = await EncounterWithAsync(owner, 30, 20);
        await StartAsync(owner, enc);

        foreach (var (method, url) in new[]
        {
            (HttpMethod.Post, $"/api/encounters/{enc}/start"),
            (HttpMethod.Post, $"/api/encounters/{enc}/previous-turn"),
            (HttpMethod.Post, $"/api/encounters/{enc}/turn/{ids[0]}"),
            (HttpMethod.Post, $"/api/encounters/{enc}/finish"),
            (HttpMethod.Post, $"/api/encounters/{enc}/reopen"),
            (HttpMethod.Delete, $"/api/encounters/{enc}/participants/{ids[0]}"),
        })
        {
            var r = await _client.SendAsync(AuthedRequest(method, url, other));
            r.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{method} {url}");
        }
    }

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
        body.CurrentRound.Should().Be(0);
        body.CurrentParticipantIndex.Should().Be(0);
    }

    [Fact]
    public async Task Create_starts_in_Preparacao_at_Turno_0_with_nobody_on_turn()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmFase", "encfase@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Fase");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gmToken,
            new CreateEncounterRequest("Fase")));

        var body = await response.Content.ReadFromJsonAsync<EncounterResponse>();
        body!.Status.Should().Be("Preparacao");
        body.CurrentRound.Should().Be(0);
        body.CurrentParticipantId.Should().BeNull();
        body.FinalizadoEm.Should().BeNull();
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
    public async Task AdvanceTurn_on_an_encounter_owned_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EncOwnerAdv", "encowneradv@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EncOtherAdv", "encotheradv@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono do Encontro");
        var encounterId = await CreateEncounterAsync(gmTokenOwner, campaignId, "Encontro do Dono");
        var npcId = await CreateNpcSheetAsync(gmTokenOwner);
        await AddParticipantAsync(gmTokenOwner, encounterId, npcId, 10);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmTokenOther));

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

        await StartAsync(gmToken, encounterId);

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

        // Start: Turno 1 on index 0. Advance once: index 0 -> 1 (still Turno 1).
        await StartAsync(gmToken, encounterId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmToken));

        // Advance again: index 1 was the last participant -> wraps to Turno 2, index 0.
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

    [Fact]
    public async Task List_and_AdvanceTurn_agree_on_ordering_when_Iniciativa_is_tied()
    {
        // R0002's own "3 goblins" example: equal Iniciativa is expected and must not let List's
        // ordering and AdvanceTurn's ordering disagree on who is "next" — both must ThenBy(Id)
        // so the two independent queries are guaranteed to produce the same order.
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmTie1", "enctie1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Empate");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Empate");

        var npc1 = await CreateNpcSheetAsync(gmToken);
        var npc2 = await CreateNpcSheetAsync(gmToken);
        var npc3 = await CreateNpcSheetAsync(gmToken);
        await AddParticipantAsync(gmToken, encounterId, npc1, 10);
        await AddParticipantAsync(gmToken, encounterId, npc2, 10);
        await AddParticipantAsync(gmToken, encounterId, npc3, 10);

        var beforeOrder = await ListParticipantsAsync(gmToken, encounterId);
        beforeOrder.Should().HaveCount(3);
        // Start puts the vez on index 0, so a single AdvanceTurn call moves to index 1 — the
        // participant List puts SECOND is the one that must get its AcoesRestantes reset.
        var expectedNextId = beforeOrder[1].Id;
        await StartAsync(gmToken, encounterId);

        // Drive every participant's AcoesRestantes down to 0 so the reset is detectable — the
        // default from Add is already 3.
        foreach (var p in beforeOrder)
            await SetAcoesRestantesAsync(gmToken, encounterId, p, 0);

        var advanceResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmToken));
        advanceResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // AdvanceTurn must have reset the SAME participant that List put at index 1 — not a
        // different one due to an ordering disagreement between the two independent queries.
        var afterOrder = await ListParticipantsAsync(gmToken, encounterId);
        afterOrder.Select(p => p.Id).Should().ContainInOrder(beforeOrder.Select(p => p.Id),
            "List's ordering must be stable and agree with itself across calls when Iniciativa is tied");
        afterOrder.Single(p => p.Id == expectedNextId).AcoesRestantes.Should().Be(3);
        afterOrder.Where(p => p.Id != expectedNextId).Should().OnlyContain(p => p.AcoesRestantes == 0);
    }

    [Fact]
    public async Task AdvanceTurn_still_targets_the_right_participant_after_a_higher_Iniciativa_participant_is_added_mid_round()
    {
        // Épico 5 item 2 of the gap audit: CurrentParticipantIndex used to be a raw position,
        // re-resolved against whatever the CURRENT ordering happened to be — so adding a
        // participant ahead of the active one mid-round silently retargeted "next" to the wrong
        // participant. Identity-based tracking must not have this bug.
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmReorder1", "encreorder1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Reordenação");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Encontro Reordenação");

        var npcHigh = await CreateNpcSheetAsync(gmToken);
        var npcMid = await CreateNpcSheetAsync(gmToken);
        var npcLow = await CreateNpcSheetAsync(gmToken);
        await AddParticipantAsync(gmToken, encounterId, npcHigh, 20); // index 0
        await AddParticipantAsync(gmToken, encounterId, npcMid, 10); // index 1 — the active turn after one advance
        await AddParticipantAsync(gmToken, encounterId, npcLow, 5); // index 2

        // Start (Alta), then first advance: 0 (Alta) -> 1 (Media). Confirmed by the existing AdvanceTurn test above.
        await StartAsync(gmToken, encounterId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmToken));

        // Now insert a participant with a HIGHER Iniciativa than the currently-active one (Media,
        // 10) but lower than Alta (20) — this reorders the list to [Alta 20, Nova 15, Media 10, Baixa 5],
        // shifting Media from index 1 to index 2. A raw-index scheme would advance from stale
        // index 1 to whoever now sits at index 2 (Media itself, wrongly re-triggering her own turn)
        // or worse depending on exact arithmetic — identity-based tracking must still correctly
        // advance PAST Media to Baixa, unaffected by the insertion.
        var npcNova = await CreateNpcSheetAsync(gmToken);
        await AddParticipantAsync(gmToken, encounterId, npcNova, 15);

        foreach (var p in await ListParticipantsAsync(gmToken, encounterId))
            await SetAcoesRestantesAsync(gmToken, encounterId, p, 0);

        var advanceResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/encounters/{encounterId}/advance-turn", gmToken));
        advanceResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await ListParticipantsAsync(gmToken, encounterId);
        var lowId = after.Single(p => p.Iniciativa == 5).Id;
        after.Single(p => p.Id == lowId).AcoesRestantes.Should().Be(3, "the turn must advance from Media (the true active participant) to Baixa, not be retargeted by Nova's insertion");
        after.Where(p => p.Id != lowId).Should().OnlyContain(p => p.AcoesRestantes == 0);
    }

    [Fact]
    public async Task Update_renames_the_encounter()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EncGmRename1", "encrename1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Renomear");
        var encounterId = await CreateEncounterAsync(gmToken, campaignId, "Nome Original");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{encounterId}", gmToken,
            new UpdateEncounterRequest("Nome Novo")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/encounters", gmToken));
        var body = await list.Content.ReadFromJsonAsync<List<EncounterResponse>>();
        body!.Should().ContainSingle(e => e.Id == encounterId && e.Nome == "Nome Novo");
    }

    [Fact]
    public async Task Update_on_an_encounter_owned_by_a_different_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("EncGmRenameOwner", "encrenameowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("EncGmRenameOther", "encrenameother@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Renomear Alheia");
        var encounterId = await CreateEncounterAsync(gmTokenOwner, campaignId, "Nome Original");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/encounters/{encounterId}", gmTokenOther,
            new UpdateEncounterRequest("Nome Novo")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Two_simultaneous_AdvanceTurn_requests_apply_only_one_and_the_other_is_409()
    {
        var gm = await RegisterGmAndGetTokenAsync("EncRace1", "encrace1@teste.com");
        var (campaign, enc, _) = await EncounterWithAsync(gm, 30, 20, 10);

        // Interleaving is up to the scheduler: repeat until a pair actually collides (bounded).
        var conflictSeen = false;
        (await StartAsync(gm, enc)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        for (var attempt = 0; attempt < 15 && !conflictSeen; attempt++)
        {
            var before = (await GetAsync(gm, campaign, enc)).CurrentParticipantId;

            var results = await Task.WhenAll(AdvanceAsync(gm, enc), AdvanceAsync(gm, enc));

            var codes = results.Select(r => r.StatusCode).ToList();
            codes.Should().OnlyContain(c => c == HttpStatusCode.NoContent || c == HttpStatusCode.Conflict);
            var ok = codes.Count(c => c == HttpStatusCode.NoContent);
            var conflict = codes.Count(c => c == HttpStatusCode.Conflict);
            ok.Should().BeGreaterThanOrEqualTo(1);
            if (conflict == 1)
            {
                conflictSeen = true;
                (await BodyAsync(results.Single(r => r.StatusCode == HttpStatusCode.Conflict)))
                    .Should().Contain("O encontro foi alterado por outra ação. Recarregue e tente de novo.");
                var after = (await GetAsync(gm, campaign, enc)).CurrentParticipantId;
                var order = (await ListParticipantsAsync(gm, enc)).Select(p => p.Id).ToList();
                order.IndexOf(after!).Should().Be((order.IndexOf(before!) + 1) % order.Count);
            }
        }
        conflictSeen.Should().BeTrue("a simultaneous pair should collide at least once in 15 attempts");
    }
}
