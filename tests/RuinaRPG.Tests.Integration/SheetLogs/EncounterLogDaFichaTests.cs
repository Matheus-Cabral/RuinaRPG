using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration.SheetLogs;

/// <summary>
/// Gerenciador de Encontros R0010 / Técnico R0012: a Vitalidade, o Foco ou a Adrenalina de uma ficha que
/// participa de um encontro em andamento, quando mudam, entram também no log desse encontro.
/// </summary>
public class EncounterLogDaFichaTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string RecursoDaFicha = "RecursoDaFicha";
    private const string NomeDoPersonagem = "Vann Astrel";
    private const string NomeDoNpc = "Sentinela da Ruína";

    private static string Rotulo<T>(string propriedade) => SheetLogMap.Entidades[typeof(T)].Campos[propriedade];

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return await Client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await SendAsync(method, url, token, body);
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} respondeu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private async Task<T> ReadAsync<T>(HttpMethod method, string url, string token, object? body = null) =>
        (await (await SendOkAsync(method, url, token, body)).Content.ReadFromJsonAsync<T>())!;

    private sealed record Cenario(string GmToken, string PlayerToken, Guid PlayerId, string CampaignId, Guid SheetId);

    /// <summary>Um GM, um jogador ligado a ele, uma campanha com o jogador e a Ficha de Personagem dele, já preenchida.</summary>
    private async Task<Cenario> CenarioAsync(string sufixo)
    {
        var gm = await Client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest($"EncGm{sufixo}", $"encgm{sufixo.ToLowerInvariant()}@teste.com", "Senha!123", "Senha!123"));
        var gmToken = (await gm.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var code = (await ReadAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>(HttpMethod.Post, "/api/invite-codes", gmToken)).Code;
        var player = await Client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest($"EncJogador{sufixo}", $"encjogador{sufixo.ToLowerInvariant()}@teste.com", "Senha!123", "Senha!123", code));
        var playerToken = (await player.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var playerId = Guid.Parse((await ReadAsync<MeResponse>(HttpMethod.Get, "/api/auth/me", playerToken)).Id);
        var campaignId = (await ReadAsync<CampaignResponse>(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest($"Campanha {sufixo}", ""))).Id;
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId.ToString()));
        var sheetId = Guid.Parse((await ReadAsync<CharacterSheetResponse>(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken,
            new CreateCharacterSheetRequest(playerId.ToString()))).Id);
        await PutSheetAsync(gmToken, sheetId, ValidUpdate());
        return new Cenario(gmToken, playerToken, playerId, campaignId, sheetId);
    }

    private static UpdateCharacterSheetRequest ValidUpdate() => new(
        null, NomeDoPersonagem, "Humano", "Sinir", "Campeao", "Duelista", null, "Marcado pela Ruína",
        true, 749, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null, null, 0, null, null, 0);

    private static UpdateNpcSheetRequest ValidNpcUpdate(string nome = NomeDoNpc) => new(
        null, nome, "Humano", "Sinir", "Campeao", "Duelista", null, "Guardiã do Portal",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, null, null, 0, null, null, 0);

    private Task PutSheetAsync(string token, Guid sheetId, UpdateCharacterSheetRequest request) =>
        SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{sheetId}", token, request);

    private Task<CharacterSheetResponse> GetSheetAsync(string token, Guid sheetId) =>
        ReadAsync<CharacterSheetResponse>(HttpMethod.Get, $"/api/character-sheets/{sheetId}", token);

    private async Task<string> CreateEncounterAsync(Cenario c) =>
        (await ReadAsync<EncounterResponse>(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/encounters", c.GmToken, new CreateEncounterRequest("Encontro"))).Id;

    private Task<EncounterParticipantResponse> AddAsync(Cenario c, string encounterId, AddParticipantRequest request) =>
        ReadAsync<EncounterParticipantResponse>(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", c.GmToken, request);

    private Task<EncounterParticipantResponse> AddPersonagemAsync(Cenario c, string encounterId, int iniciativa = 20) =>
        AddAsync(c, encounterId, new AddParticipantRequest(c.SheetId.ToString(), null, null, iniciativa));

    private Task StartAsync(Cenario c, string encounterId) => SendOkAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/start", c.GmToken);

    /// <summary>Um encontro em andamento com a Ficha de Personagem do jogador como único participante.</summary>
    private async Task<string> EncontroEmAndamentoAsync(Cenario c)
    {
        var encounterId = await CreateEncounterAsync(c);
        await AddPersonagemAsync(c, encounterId);
        await StartAsync(c, encounterId);
        return encounterId;
    }

    /// <summary>Uma ficha de NPC do próprio GM (não concedida), já preenchida.</summary>
    private async Task<Guid> NpcDoGmAsync(Cenario c, string nome)
    {
        var id = Guid.Parse((await ReadAsync<NpcSheetResponse>(HttpMethod.Post, "/api/npc-sheets", c.GmToken)).Id);
        await SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{id}", c.GmToken, ValidNpcUpdate(nome));
        return id;
    }

    private async Task<List<EncounterLogEntryResponse>> RecursosNoLogAsync(Cenario c, string encounterId) =>
        (await ReadAsync<List<EncounterLogEntryResponse>>(HttpMethod.Get, $"/api/encounters/{encounterId}/log", c.GmToken))
            .Where(e => e.Tipo == RecursoDaFicha).ToList();

    private async Task<int> TotalDeLinhasDeEncontroAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>().EncounterLogEntries.CountAsync();
    }

    [Fact]
    public async Task A_Vitalidade_change_on_a_sheet_in_an_in_progress_encounter_is_logged_there()
    {
        var c = await CenarioAsync("Vitalidade");
        var encounterId = await CreateEncounterAsync(c);
        await AddPersonagemAsync(c, encounterId, iniciativa: 10);
        await AddAsync(c, encounterId, new AddParticipantRequest(null, (await NpcDoGmAsync(c, "Alfa")).ToString(), null, 30));
        await StartAsync(c, encounterId);
        var guardada = (await GetSheetAsync(c.GmToken, c.SheetId)).VitalidadeAtual;
        guardada.Should().BeGreaterThan(0);

        await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = guardada - 1 });

        var log = await ReadAsync<List<EncounterLogEntryResponse>>(HttpMethod.Get, $"/api/encounters/{encounterId}/log", c.GmToken);
        log.Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada", RecursoDaFicha);
        var linha = log[2];
        linha.ParticipanteNome.Should().Be(NomeDoPersonagem);
        linha.Campo.Should().Be(Rotulo<CharacterSheet>("VitalidadeAtual"));
        linha.ValorAntes.Should().Be(guardada.ToString());
        linha.ValorDepois.Should().Be((guardada - 1).ToString());
        linha.Texto.Should().BeNull();
        // A mesma fotografia das linhas do GM: o Turno e de quem era a vez.
        linha.Turno.Should().Be(1);
        linha.VezNome.Should().Be("Alfa");
        linha.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Foco_and_Adrenalina_are_logged_too_and_Estresse_and_other_fields_are_not()
    {
        var c = await CenarioAsync("Foco");
        var encounterId = await EncontroEmAndamentoAsync(c);
        var ficha = await GetSheetAsync(c.GmToken, c.SheetId);
        ficha.FocoAtual.Should().BeGreaterThan(0);
        ficha.AdrenalinaAtual.Should().BeGreaterThan(0);
        ficha.EstresseAtual.Should().BeGreaterThan(0);

        await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with
        {
            Nome = "Vann, o Marcado", FocoAtual = ficha.FocoAtual - 1, AdrenalinaAtual = ficha.AdrenalinaAtual - 2,
            EstresseAtual = ficha.EstresseAtual - 1, Ciclos = 101,
        });

        var log = await RecursosNoLogAsync(c, encounterId);
        log.Select(e => e.Campo).Should().BeEquivalentTo([Rotulo<CharacterSheet>("FocoAtual"), Rotulo<CharacterSheet>("AdrenalinaAtual")]);
        var foco = log.Single(e => e.Campo == Rotulo<CharacterSheet>("FocoAtual"));
        foco.ValorAntes.Should().Be(ficha.FocoAtual.ToString());
        foco.ValorDepois.Should().Be((ficha.FocoAtual - 1).ToString());
        var adrenalina = log.Single(e => e.Campo == Rotulo<CharacterSheet>("AdrenalinaAtual"));
        adrenalina.ValorAntes.Should().Be(ficha.AdrenalinaAtual.ToString());
        adrenalina.ValorDepois.Should().Be((ficha.AdrenalinaAtual - 2).ToString());
        // O nome do participante é o do encontro (copiado ao entrar), não o novo nome da ficha.
        log.Should().OnlyContain(e => e.ParticipanteNome == NomeDoPersonagem);
    }

    [Fact]
    public async Task Nothing_is_logged_at_Turno_0_or_after_finish_and_logging_resumes_after_reopen()
    {
        var c = await CenarioAsync("Fases");
        var encounterId = await CreateEncounterAsync(c);
        await AddPersonagemAsync(c, encounterId);
        var vitalidade = (await GetSheetAsync(c.GmToken, c.SheetId)).VitalidadeAtual;
        vitalidade.Should().BeGreaterThan(3);

        // Em preparação (Turno 0).
        await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = vitalidade - 1 });
        (await ReadAsync<List<EncounterLogEntryResponse>>(HttpMethod.Get, $"/api/encounters/{encounterId}/log", c.GmToken)).Should().BeEmpty();

        // Finalizado.
        await StartAsync(c, encounterId);
        await SendOkAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/finish", c.GmToken);
        await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = vitalidade - 2 });
        (await RecursosNoLogAsync(c, encounterId)).Should().BeEmpty();

        // Reaberto.
        await SendOkAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/reopen", c.GmToken);
        await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = vitalidade - 3 });

        var linha = (await RecursosNoLogAsync(c, encounterId)).Should().ContainSingle().Subject;
        linha.ValorAntes.Should().Be((vitalidade - 2).ToString());
        linha.ValorDepois.Should().Be((vitalidade - 3).ToString());
        (await ReadAsync<List<EncounterLogEntryResponse>>(HttpMethod.Get, $"/api/encounters/{encounterId}/log", c.GmToken))
            .Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada", "Finalizado", "Reaberto", RecursoDaFicha);
    }

    [Fact]
    public async Task A_sheet_in_two_in_progress_encounters_is_logged_in_both()
    {
        var c = await CenarioAsync("Dois");
        var primeiro = await EncontroEmAndamentoAsync(c);
        var segundo = await EncontroEmAndamentoAsync(c);
        // Um terceiro, ainda em preparação, fica de fora.
        var terceiro = await CreateEncounterAsync(c);
        await AddPersonagemAsync(c, terceiro);
        var guardada = (await GetSheetAsync(c.GmToken, c.SheetId)).VitalidadeAtual;

        await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = guardada - 1 });

        foreach (var encounterId in new[] { primeiro, segundo })
        {
            var linha = (await RecursosNoLogAsync(c, encounterId)).Should().ContainSingle().Subject;
            linha.ParticipanteNome.Should().Be(NomeDoPersonagem);
            linha.ValorDepois.Should().Be((guardada - 1).ToString());
            linha.VezNome.Should().Be(NomeDoPersonagem);
        }
        (await RecursosNoLogAsync(c, terceiro)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_granted_npc_sheet_edited_by_its_player_is_logged()
    {
        var c = await CenarioAsync("NpcConcedido");
        var npcId = Guid.Parse((await ReadAsync<GrantSheetResponse>(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/grants", c.GmToken,
            new GrantSheetRequest(c.PlayerId.ToString(), "Npc", null))).SheetId);
        await SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{npcId}", c.PlayerToken, ValidNpcUpdate());
        var guardada = (await ReadAsync<NpcSheetResponse>(HttpMethod.Get, $"/api/npc-sheets/{npcId}", c.PlayerToken)).VitalidadeAtual;
        guardada.Should().BeGreaterThan(0);
        var encounterId = await CreateEncounterAsync(c);
        await AddAsync(c, encounterId, new AddParticipantRequest(null, npcId.ToString(), null, 20));
        await StartAsync(c, encounterId);

        await SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{npcId}", c.PlayerToken, ValidNpcUpdate() with { VitalidadeAtual = guardada - 1 });

        var linha = (await RecursosNoLogAsync(c, encounterId)).Should().ContainSingle().Subject;
        linha.ParticipanteNome.Should().Be(NomeDoNpc);
        linha.Campo.Should().Be(Rotulo<NpcSheet>("VitalidadeAtual"));
        linha.ValorAntes.Should().Be(guardada.ToString());
        linha.ValorDepois.Should().Be((guardada - 1).ToString());
    }

    [Fact]
    public async Task A_gm_owned_npc_instance_is_not_affected_by_edits_to_its_source_sheet()
    {
        var c = await CenarioAsync("NpcDoGm");
        var npcId = await NpcDoGmAsync(c, "Alfa");
        var guardada = (await ReadAsync<NpcSheetResponse>(HttpMethod.Get, $"/api/npc-sheets/{npcId}", c.GmToken)).VitalidadeAtual;
        guardada.Should().BeGreaterThan(0);
        var encounterId = await CreateEncounterAsync(c);
        await AddAsync(c, encounterId, new AddParticipantRequest(null, npcId.ToString(), null, 20));
        await StartAsync(c, encounterId);

        await SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{npcId}", c.GmToken, ValidNpcUpdate("Alfa") with { VitalidadeAtual = guardada - 1 });

        (await ReadAsync<List<EncounterLogEntryResponse>>(HttpMethod.Get, $"/api/encounters/{encounterId}/log", c.GmToken))
            .Select(e => e.Tipo).Should().Equal("Iniciado", "VezPassada");
    }

    [Fact]
    public async Task The_GM_editing_the_players_sheet_is_logged_too()
    {
        var c = await CenarioAsync("GmEdita");
        var encounterId = await EncontroEmAndamentoAsync(c);
        var guardada = (await GetSheetAsync(c.GmToken, c.SheetId)).VitalidadeAtual;

        await PutSheetAsync(c.GmToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = guardada - 1 });

        var linha = (await RecursosNoLogAsync(c, encounterId)).Should().ContainSingle().Subject;
        linha.ParticipanteNome.Should().Be(NomeDoPersonagem);
        linha.ValorAntes.Should().Be(guardada.ToString());
        linha.ValorDepois.Should().Be((guardada - 1).ToString());
    }

    [Fact]
    public async Task A_sheet_that_is_in_no_encounter_adds_no_query_result_and_no_row()
    {
        var c = await CenarioAsync("Fora");
        // Há um encontro em andamento na campanha, mas a ficha do jogador não participa dele.
        var encounterId = await CreateEncounterAsync(c);
        await AddAsync(c, encounterId, new AddParticipantRequest(null, (await NpcDoGmAsync(c, "Alfa")).ToString(), null, 30));
        await StartAsync(c, encounterId);
        var guardada = (await GetSheetAsync(c.GmToken, c.SheetId)).VitalidadeAtual;
        var antes = await TotalDeLinhasDeEncontroAsync();

        await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = guardada - 1 });

        (await GetSheetAsync(c.GmToken, c.SheetId)).VitalidadeAtual.Should().Be(guardada - 1);
        (await TotalDeLinhasDeEncontroAsync()).Should().Be(antes);
        (await RecursosNoLogAsync(c, encounterId)).Should().BeEmpty();
    }
}
