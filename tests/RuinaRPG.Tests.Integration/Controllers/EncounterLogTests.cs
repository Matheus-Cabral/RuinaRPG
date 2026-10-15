using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EncounterLogTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EncounterLogTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> CreateEncounterAsync(string gmToken)
    {
        var campaign = await _client.SendAsync(Authed(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha Log", "")));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        var response = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gmToken, new CreateEncounterRequest("Encontro Log")));
        return (await response.Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
    }

    // GM-owned NPC with the given name; PV/PF/PA are copied to the participant once.
    private async Task<EncounterParticipantResponse> AddNpcAsync(string gmToken, string encounterId, string nome, int iniciativa)
    {
        var npc = await _client.SendAsync(Authed(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var npcId = (await npc.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
        await _client.SendAsync(Authed(HttpMethod.Put, $"/api/npc-sheets/{npcId}", gmToken, new UpdateNpcSheetRequest(
            null, nome, "Humano", "Sinir", "Campeao", "Duelista", null, "Descrição de Teste",
            5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, null, null, 0, null, null, 0)));
        var add = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gmToken,
            new AddParticipantRequest(null, npcId, null, iniciativa)));
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await add.Content.ReadFromJsonAsync<EncounterParticipantResponse>())!;
    }

    // A, B, C by descending Iniciativa.
    private async Task<List<EncounterParticipantResponse>> AddThreeAsync(string gmToken, string encounterId) =>
    [
        await AddNpcAsync(gmToken, encounterId, "Alfa", 30),
        await AddNpcAsync(gmToken, encounterId, "Beta", 20),
        await AddNpcAsync(gmToken, encounterId, "Gama", 10),
    ];

    private async Task<HttpResponseMessage> PostAsync(string gmToken, string url) =>
        await _client.SendAsync(Authed(HttpMethod.Post, url, gmToken));

    private async Task<List<EncounterLogEntryResponse>> GetLogAsync(string gmToken, string encounterId)
    {
        var response = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/log", gmToken));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<EncounterLogEntryResponse>>())!;
    }

    private Task<HttpResponseMessage> UpdateAsync(string gmToken, string encounterId, EncounterParticipantResponse p, int iniciativa, int? pv, int acoes, params string[] condicoes) =>
        _client.SendAsync(Authed(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{p.Id}", gmToken,
            new UpdateParticipantRequest(iniciativa, pv, p.PF, p.PA, acoes, condicoes.ToList())));

    // Free-text conditions can no longer be added through the API (they come from the catalog): seed the legacy rows directly.
    private async Task SeedLegacyConditionsAsync(EncounterParticipantResponse p, params string[] textos)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        foreach (var texto in textos)
            db.EncounterParticipantConditions.Add(new() { Id = Guid.NewGuid(), EncounterParticipantId = Guid.Parse(p.Id), Texto = texto });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Nothing_is_logged_in_preparation()
    {
        var gm = await RegisterGmAsync("LogGm1", "log1@teste.com");
        var encounterId = await CreateEncounterAsync(gm);

        var a = await AddNpcAsync(gm, encounterId, "Alfa", 30);
        await SeedLegacyConditionsAsync(a, "Envenenado", "Cego");
        (await UpdateAsync(gm, encounterId, a, 25, 10, 3, "Envenenado")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{a.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetLogAsync(gm, encounterId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Start_logs_Iniciado_then_the_first_turn()
    {
        var gm = await RegisterGmAsync("LogGm2", "log2@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        await AddThreeAsync(gm, encounterId);

        await PostAsync(gm, $"/api/encounters/{encounterId}/start");

        var log = await GetLogAsync(gm, encounterId);
        log.Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada");
        log[0].Turno.Should().Be(1);
        log[1].Turno.Should().Be(1);
        log[1].ParticipanteNome.Should().Be("Alfa");
        log[1].VezNome.Should().Be("Alfa");
    }

    [Fact]
    public async Task Advancing_through_a_wrap_logs_NovoTurno_before_the_turn()
    {
        var gm = await RegisterGmAsync("LogGm3", "log3@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        await AddThreeAsync(gm, encounterId);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");

        for (var i = 0; i < 3; i++)
            await PostAsync(gm, $"/api/encounters/{encounterId}/advance-turn");

        var log = await GetLogAsync(gm, encounterId);
        log.Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada", "VezPassada", "VezPassada", "NovoTurno", "VezPassada");
        log[2].ParticipanteNome.Should().Be("Beta");
        log[3].ParticipanteNome.Should().Be("Gama");
        log[4].Turno.Should().Be(2);
        log[5].Turno.Should().Be(2);
        log[5].ParticipanteNome.Should().Be("Alfa");
    }

    [Fact]
    public async Task Previous_and_choose_log_their_own_types()
    {
        var gm = await RegisterGmAsync("LogGm4", "log4@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var people = await AddThreeAsync(gm, encounterId);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        await PostAsync(gm, $"/api/encounters/{encounterId}/advance-turn");

        await PostAsync(gm, $"/api/encounters/{encounterId}/previous-turn");
        await PostAsync(gm, $"/api/encounters/{encounterId}/turn/{people[2].Id}");

        var log = await GetLogAsync(gm, encounterId);
        log.Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada", "VezPassada", "VezVoltou", "VezEscolhida");
        log[3].ParticipanteNome.Should().Be("Alfa");
        log[4].ParticipanteNome.Should().Be("Gama");
        log[4].VezNome.Should().Be("Gama");
    }

    [Fact]
    public async Task Editing_a_participant_logs_each_changed_field_with_before_and_after_and_nothing_for_unchanged_ones()
    {
        var gm = await RegisterGmAsync("LogGm5", "log5@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var a = await AddNpcAsync(gm, encounterId, "Alfa", 30);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        var pvAntes = a.PV!.Value;

        (await UpdateAsync(gm, encounterId, a, 25, pvAntes - 10, 2)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var log = (await GetLogAsync(gm, encounterId)).Skip(2).ToList();
        log.Select(e => e.Tipo).Should().OnlyContain(t => t == "CampoAlterado");
        log.Select(e => e.Campo).Should().Equal("Iniciativa", "Ações", "PV");
        log[0].ValorAntes.Should().Be("30");
        log[0].ValorDepois.Should().Be("25");
        log[1].ValorAntes.Should().Be("3");
        log[1].ValorDepois.Should().Be("2");
        log[2].ValorAntes.Should().Be(pvAntes.ToString());
        log[2].ValorDepois.Should().Be((pvAntes - 10).ToString());
        log.Should().OnlyContain(e => e.ParticipanteNome == "Alfa");

        (await UpdateAsync(gm, encounterId, a, 25, pvAntes - 10, 2)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetLogAsync(gm, encounterId)).Should().HaveCount(5);
    }

    [Fact]
    public async Task Legacy_conditions_log_removals_and_an_unchanged_list_logs_nothing()
    {
        // Additions come from the catalog now (see EncounterParticipantCondicoesControllerTests); the free-text rows are legacy.
        var gm = await RegisterGmAsync("LogGm6", "log6@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var a = await AddNpcAsync(gm, encounterId, "Alfa", 30);
        await SeedLegacyConditionsAsync(a, "Envenenado", "Enraizado");
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        var antes = (await GetLogAsync(gm, encounterId)).Count;

        (await UpdateAsync(gm, encounterId, a, 30, a.PV, 3, "Envenenado", "Enraizado")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetLogAsync(gm, encounterId)).Should().HaveCount(antes);

        await UpdateAsync(gm, encounterId, a, 30, a.PV, 3, "Enraizado");
        var log = (await GetLogAsync(gm, encounterId)).Skip(antes).ToList();
        log.Should().ContainSingle();
        log[0].Tipo.Should().Be("CondicaoRemovida");
        log[0].Texto.Should().Be("Envenenado");
        log[0].ParticipanteNome.Should().Be("Alfa");
    }

    [Fact]
    public async Task Adding_and_removing_a_participant_in_progress_is_logged_and_removing_the_one_on_turn_logs_the_new_turn()
    {
        var gm = await RegisterGmAsync("LogGm7", "log7@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var a = await AddNpcAsync(gm, encounterId, "Alfa", 30);
        var b = await AddNpcAsync(gm, encounterId, "Beta", 20);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");

        await AddNpcAsync(gm, encounterId, "Gama", 10);
        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{a.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var log = (await GetLogAsync(gm, encounterId)).Skip(2).ToList();
        log.Select(e => e.Tipo).Should().Equal("ParticipanteAdicionado", "ParticipanteRemovido", "VezPassada");
        log[0].ParticipanteNome.Should().Be("Gama");
        log[1].ParticipanteNome.Should().Be("Alfa");
        log[2].ParticipanteNome.Should().Be("Beta");
        log[2].VezNome.Should().Be("Beta");

        // Removing the last of the order on turn wraps to a new Turno.
        await PostAsync(gm, $"/api/encounters/{encounterId}/turn/{b.Id}");
        var gamaId = (await (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gm)))
            .Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>())!.Single(p => p.Nome == "Gama").Id;
        await PostAsync(gm, $"/api/encounters/{encounterId}/turn/{gamaId}");
        var antes = (await GetLogAsync(gm, encounterId)).Count;
        await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{gamaId}", gm));

        log = (await GetLogAsync(gm, encounterId)).Skip(antes).ToList();
        log.Select(e => e.Tipo).Should().Equal("ParticipanteRemovido", "NovoTurno", "VezPassada");
        log[1].Turno.Should().Be(2);
        log[2].ParticipanteNome.Should().Be("Beta");
    }

    [Fact]
    public async Task Finish_and_reopen_are_logged_and_nothing_else_is_while_finished()
    {
        var gm = await RegisterGmAsync("LogGm8", "log8@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var a = await AddNpcAsync(gm, encounterId, "Alfa", 30);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");

        (await PostAsync(gm, $"/api/encounters/{encounterId}/finish")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UpdateAsync(gm, encounterId, a, 5, a.PV, 3)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostAsync(gm, $"/api/encounters/{encounterId}/reopen")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var log = await GetLogAsync(gm, encounterId);
        log.Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada", "Finalizado", "Reaberto");
        log[2].VezNome.Should().Be("Alfa");
        log[3].VezNome.Should().Be("Alfa");
    }

    [Fact]
    public async Task The_log_lists_the_events_in_the_order_they_happened_and_each_row_carries_Turno_and_whose_turn_it_was()
    {
        var gm = await RegisterGmAsync("LogGm9", "log9@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        await AddThreeAsync(gm, encounterId);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        await PostAsync(gm, $"/api/encounters/{encounterId}/advance-turn");
        await PostAsync(gm, $"/api/encounters/{encounterId}/advance-turn");
        await PostAsync(gm, $"/api/encounters/{encounterId}/advance-turn");

        var log = await GetLogAsync(gm, encounterId);

        // The identity Sequencia is not an order promise (EF does not insert one save's rows in the
        // order they were added); the log's order is the application-assigned Ordem, so what is
        // asserted here is the sequence of events itself.
        log.Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada", "VezPassada", "VezPassada", "NovoTurno", "VezPassada");
        log.Select(e => (e.Turno, e.VezNome)).Should().Equal(
            (1, "Alfa"), (1, "Alfa"), (1, "Beta"), (1, "Gama"), (2, "Alfa"), (2, "Alfa"));
    }

    [Fact]
    public async Task The_log_is_404_for_another_gm()
    {
        var gm = await RegisterGmAsync("LogGm10", "log10@teste.com");
        var other = await RegisterGmAsync("LogGm11", "log11@teste.com");
        var encounterId = await CreateEncounterAsync(gm);

        var response = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/log", other));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_log_is_read_by_Ordem_not_by_insert_order()
    {
        var gm = await RegisterGmAsync("LogGm12", "log12@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        await AddNpcAsync(gm, encounterId, "Alfa", 30);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        var antes = (await GetLogAsync(gm, encounterId)).Count;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            // Ordem 3, 1, 2 added in that order, in one save, with random Ids.
            foreach (var (ordem, texto) in new[] { (long.MaxValue - 2, "terceira"), (long.MaxValue - 4, "primeira"), (long.MaxValue - 3, "segunda") })
                db.EncounterLogEntries.Add(new EncounterLogEntry
                {
                    Id = Guid.NewGuid(), EncounterId = Guid.Parse(encounterId), Ordem = ordem, OccurredAt = DateTime.UtcNow,
                    Turno = 1, Tipo = EncounterLogTipo.CondicaoAdicionada, Texto = texto,
                });
            await db.SaveChangesAsync();
        }

        var log = await GetLogAsync(gm, encounterId);
        log.Skip(antes).Select(e => e.Texto).Should().Equal("primeira", "segunda", "terceira");
    }

    [Fact]
    public async Task Removing_the_last_participant_of_the_order_on_turn_files_the_removal_under_its_own_Turno_and_vez()
    {
        var gm = await RegisterGmAsync("LogGm13", "log13@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var a = await AddNpcAsync(gm, encounterId, "Alfa", 30);
        var b = await AddNpcAsync(gm, encounterId, "Beta", 20);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        await PostAsync(gm, $"/api/encounters/{encounterId}/advance-turn");
        var antes = (await GetLogAsync(gm, encounterId)).Count;

        await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{b.Id}", gm));

        var log = (await GetLogAsync(gm, encounterId)).Skip(antes).ToList();
        log.Select(e => e.Tipo).Should().Equal("ParticipanteRemovido", "NovoTurno", "VezPassada");
        log[0].Turno.Should().Be(1);
        log[0].VezNome.Should().Be("Beta");
        log[0].VezParticipantId.Should().Be(b.Id);
        log[1].Turno.Should().Be(2);
        log[2].VezParticipantId.Should().Be(a.Id);
    }

    [Fact]
    public async Task Log_rows_expose_the_participant_who_had_the_vez()
    {
        var gm = await RegisterGmAsync("LogGm14", "log14@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var a = await AddNpcAsync(gm, encounterId, "Alfa", 30);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");

        var log = await GetLogAsync(gm, encounterId);

        log.Last().VezParticipantId.Should().Be(a.Id);
    }

    [Fact]
    public async Task The_log_is_403_for_a_jogador()
    {
        var gm = await RegisterGmAsync("LogGm15", "log15@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var code = (await (await _client.SendAsync(Authed(HttpMethod.Post, "/api/invite-codes", gm)))
            .Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var player = (await (await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("LogPlayer15", "logp15@teste.com", "Senha!123", "Senha!123", code)))
            .Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        var response = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/log", player));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
