using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EncounterParticipantCondicoesControllerTests : IClassFixture<BlankDatabaseFixture>, IAsyncLifetime
{
    private readonly BlankDatabaseFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EncounterParticipantCondicoesControllerTests(BlankDatabaseFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAsync(string nickname, string email, bool auditor = false)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        if (auditor)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
            user.IsRulesAuditor = true;
            await db.SaveChangesAsync();
        }
        return token;
    }

    private async Task<string> CreateEncounterAsync(string gm)
    {
        var campaign = await _client.SendAsync(Authed(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest("Campanha Condições", "")));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        var response = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gm, new CreateEncounterRequest("Encontro")));
        return (await response.Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
    }

    private async Task<string> CreateNpcSheetAsync(string gm)
    {
        var response = await _client.SendAsync(Authed(HttpMethod.Post, "/api/npc-sheets", gm));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }

    private async Task<string> CreateCreatureSheetAsync(string gm)
    {
        var response = await _client.SendAsync(Authed(HttpMethod.Post, "/api/creature-sheets", gm));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.CreatureSheets.CreatureSheetResponse>())!.Id;
    }

    private async Task<string> AddNpcParticipantAsync(string gm, string encounterId, string? npcId = null)
    {
        npcId ??= await CreateNpcSheetAsync(gm);
        var add = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gm, new AddParticipantRequest(null, npcId, null, 10)));
        add.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await add.Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
    }

    private static SalvarCondicaoRequest Catalogo(string nome, int? duracao = 3, params string[] desabilitacoes) => new(nome, "Descrição.", "Veneno", duracao,
    [
        new CondicaoModificadorDto("RecursoAtual", "Vitalidade", "Fixo", -2, "ACadaVez"),
        new CondicaoModificadorDto("Atributo", "Vigor", "Percentual", -10, null),
    ],
    desabilitacoes.ToList());

    private async Task<CondicaoResponse> CriarNoCatalogoAsync(string auditor, SalvarCondicaoRequest request)
    {
        var response = await _client.SendAsync(Authed(HttpMethod.Post, "/api/condicoes", auditor, request));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CondicaoResponse>())!;
    }

    private Task<HttpResponseMessage> AplicarAsync(string gm, string encounterId, string participantId, AplicarCondicaoRequest request) =>
        _client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes", gm, request));

    private async Task<List<EncounterParticipantResponse>> ListarAsync(string gm, string encounterId) =>
        (await (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", gm))).Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>())!;

    private async Task<List<EncounterLogEntryResponse>> LogAsync(string gm, string encounterId) =>
        (await (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/log", gm))).Content.ReadFromJsonAsync<List<EncounterLogEntryResponse>>())!;

    private Task<HttpResponseMessage> PostAsync(string gm, string url) => _client.SendAsync(Authed(HttpMethod.Post, url, gm));

    private async Task<EncounterParticipantCondicaoSnapshot> LerAsync(string participantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var c = await db.EncounterParticipantCondicoes.AsNoTracking().SingleAsync(x => x.EncounterParticipantId == Guid.Parse(participantId));
        return new(c.Nome, c.Descricao, c.Icone, c.AplicadaNoTurno, c.DuracaoTurnos, c.CondicaoId,
            c.Modificadores.Select(m => (m.LinhaId, m.Alvo, m.AlvoEspecifico, m.TipoDeValor, m.Valor, m.Momento)).ToList(),
            c.Desabilitacoes.ToDictionary(d => d.Key, d => d.Value.ToList()), c.AjustesAplicados.Count);
    }

    private sealed record EncounterParticipantCondicaoSnapshot(string Nome, string Descricao, string Icone, int AplicadaNoTurno, int? DuracaoTurnos, Guid? CondicaoId,
        List<(Guid LinhaId, AlvoDeCondicao Alvo, string? AlvoEspecifico, TipoDeValorDeCondicao TipoDeValor, int Valor, MomentoDeCondicao? Momento)> Modificadores,
        Dictionary<CategoriaDesabilitavel, List<Guid>> Desabilitacoes, int Ajustes);

    [Fact]
    public async Task Apply_copies_the_catalog_entry_into_a_snapshot_with_the_default_duration_and_stable_line_ids()
    {
        var gm = await RegisterGmAsync("CpGm1", "cp1@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Envenenado", 3, "Magia"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);

        var response = await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var criada = (await response.Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!;
        criada.Nome.Should().Be("Envenenado");
        criada.Icone.Should().Be("Veneno");

        var snapshot = await LerAsync(participantId);
        snapshot.CondicaoId.Should().Be(Guid.Parse(condicao.Id));
        snapshot.DuracaoTurnos.Should().Be(3);
        snapshot.AplicadaNoTurno.Should().Be(0);
        snapshot.Modificadores.Select(m => (m.Alvo, m.AlvoEspecifico, m.TipoDeValor, m.Valor, m.Momento)).Should().Equal(
            (AlvoDeCondicao.RecursoAtual, "Vitalidade", TipoDeValorDeCondicao.Fixo, -2, MomentoDeCondicao.ACadaVez),
            (AlvoDeCondicao.Atributo, "Vigor", TipoDeValorDeCondicao.Percentual, -10, (MomentoDeCondicao?)null));
        snapshot.Modificadores.Select(m => m.LinhaId).Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);
        snapshot.Desabilitacoes.Should().ContainKey(CategoriaDesabilitavel.Magia).WhoseValue.Should().BeEmpty();
        snapshot.Ajustes.Should().Be(0);
    }

    [Fact]
    public async Task Apply_takes_the_requested_duration_and_adjusted_values_by_line_index()
    {
        var gm = await RegisterGmAsync("CpGm2", "cp2@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Fraqueza", 3));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);

        var response = await AplicarAsync(gm, encounterId, participantId,
            new AplicarCondicaoRequest(condicao.Id, 5, [new(1, "Atributo", "Vigor", -25)], null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var snapshot = await LerAsync(participantId);
        snapshot.DuracaoTurnos.Should().Be(5);
        snapshot.Modificadores.Select(m => m.Valor).Should().Equal(-2, -25);
    }

    [Fact]
    public async Task Apply_with_SemDuracao_has_no_duration_even_when_the_catalog_has_a_default()
    {
        var gm = await RegisterGmAsync("CpGmSd1", "cpsd1@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Eterna", 3));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);

        var response = await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null, SemDuracao: true));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!.TurnosRestantes.Should().BeNull();
        (await LerAsync(participantId)).DuracaoTurnos.Should().BeNull();
    }

    [Fact]
    public async Task Apply_with_SemDuracao_and_a_duration_returns_400()
    {
        var gm = await RegisterGmAsync("CpGmSd2", "cpsd2@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Confusa", 3));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);

        var response = await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, 2, null, null, SemDuracao: true));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Informe a duração ou marque sem duração, não os dois.");
        (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Apply_the_same_catalog_condition_twice_to_one_participant_returns_400()
    {
        var gm = await RegisterGmAsync("CpGm3", "cp3@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Repetida"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null))).StatusCode.Should().Be(HttpStatusCode.Created);

        var segunda = await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null));

        segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await segunda.Content.ReadAsStringAsync()).Should().Contain("Esta condição já está neste participante.");
    }

    [Fact]
    public async Task Apply_an_unknown_or_deleted_condition_returns_400()
    {
        var gm = await RegisterGmAsync("CpGm4", "cp4@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Apagada"));
        await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{condicao.Id}", gm));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);

        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(Guid.NewGuid().ToString(), null, null, null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest("nao-e-guid", null, null, null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Apply_with_an_adjusted_value_of_zero_or_an_unknown_line_index_returns_400()
    {
        var gm = await RegisterGmAsync("CpGm5", "cp5@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Ajustes"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);

        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, [new(0, "RecursoAtual", "Vitalidade", 0)], null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, [new(2, "Atributo", "Vigor", 4)], null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, [new(-1, "Atributo", "Vigor", 4)], null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, 0, null, null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Apply_checks_an_adjusted_value_against_the_catalog_bounds_of_its_line()
    {
        var gm = await RegisterGmAsync("CpGmLim", "cplim@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Limites"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);

        foreach (var valor in new[] { -101, 1001 })
        {
            var response = await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, [new(1, "Atributo", "Vigor", valor)], null));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Um modificador percentual deve estar entre -100 e 1000.");
        }
        var zero = await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, [new(0, "RecursoAtual", "Vitalidade", 0)], null));
        (await zero.Content.ReadAsStringAsync()).Should().Contain("O valor de um modificador não pode ser zero.");
        var repetida = await AplicarAsync(gm, encounterId, participantId,
            new AplicarCondicaoRequest(condicao.Id, null, [new(1, "Atributo", "Vigor", -20), new(1, "Atributo", "Vigor", -30)], null));
        repetida.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Um fixo pode passar do percentual: o limite é o do tipo da linha.
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, [new(0, "RecursoAtual", "Vitalidade", -2000)], null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Apply_with_an_adjusted_line_that_no_longer_matches_the_catalog_returns_409()
    {
        var gm = await RegisterGmAsync("CpGmIdt", "cpidt@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Mutante"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        // O GM abriu a escolha com a linha 1 = Vigor; enquanto escolhia, o catálogo trocou a ordem e o atributo.
        var request = new AplicarCondicaoRequest(condicao.Id, null, [new(1, "Atributo", "Vigor", -20)], null);
        var editada = Catalogo("Mutante") with
        {
            Modificadores = [new("Atributo", "Forca", "Percentual", -10, null), new("RecursoAtual", "Vitalidade", "Fixo", -2, "ACadaVez")],
        };
        (await _client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{condicao.Id}", gm, editada))).IsSuccessStatusCode.Should().BeTrue();

        foreach (var pedido in new[] { request, request with { ValoresAjustados = [new(0, "Atributo", "Vigor", -20)] } })
        {
            var response = await AplicarAsync(gm, encounterId, participantId, pedido);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadAsStringAsync()).Should().Contain("A Condição foi alterada no catálogo. Reabra a escolha.");
        }
        (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.Should().BeEmpty();

        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, [new(0, "Atributo", "Forca", -20)], null)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Apply_only_accepts_item_ids_that_are_rows_of_the_participants_sheet_in_that_category()
    {
        var gm = await RegisterGmAsync("CpGm6", "cp6@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Desarmado", 3, "Arma"));
        var encounterId = await CreateEncounterAsync(gm);
        var npcId = await CreateNpcSheetAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId, npcId);
        var (armaId, runaId) = await SemearArmaERunaAsync(Guid.Parse(npcId));

        // A rune id in the Arma category, an id of nothing, and a category the condition does not disable.
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, new() { ["Arma"] = [runaId.ToString()] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, new() { ["Arma"] = [Guid.NewGuid().ToString()] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, new() { ["Runa"] = [runaId.ToString()] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, new() { ["Inexistente"] = [] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, new() { ["Arma"] = [armaId.ToString()] }));

        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        (await LerAsync(participantId)).Desabilitacoes[CategoriaDesabilitavel.Arma].Should().Equal(armaId);
    }

    [Fact]
    public async Task The_snapshot_does_not_change_when_the_catalog_entry_is_edited_or_soft_deleted()
    {
        var gm = await RegisterGmAsync("CpGm7", "cp7@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Original", 3, "Runa"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null));
        var antes = await LerAsync(participantId);

        var editada = new SalvarCondicaoRequest("Editada", "Outra.", "Fogo", 9, [new CondicaoModificadorDto("Dano", null, "Fixo", 7, null)], ["Arma"]);
        (await _client.SendAsync(Authed(HttpMethod.Put, $"/api/condicoes/{condicao.Id}", gm, editada))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync(participantId)).Should().BeEquivalentTo(antes);

        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/condicoes/{condicao.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync(participantId)).Should().BeEquivalentTo(antes);
        var listada = (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.Should().ContainSingle().Subject;
        listada.Nome.Should().Be("Original");
    }

    [Fact]
    public async Task In_preparation_the_condition_is_kept_but_inactive_and_its_duration_counts_from_Turno_1()
    {
        var gm = await RegisterGmAsync("CpGm8", "cp8@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Preparada", 2));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null));

        var emPreparacao = (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.Should().ContainSingle().Subject;
        emPreparacao.Ativa.Should().BeFalse();
        emPreparacao.TurnosRestantes.Should().Be(2);

        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        var emAndamento = (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.Should().ContainSingle().Subject;
        emAndamento.Ativa.Should().BeTrue();
        emAndamento.TurnosRestantes.Should().Be(2);

        await PostAsync(gm, $"/api/encounters/{encounterId}/finish");
        var finalizado = (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.Should().ContainSingle().Subject;
        finalizado.Ativa.Should().BeFalse();
    }

    [Fact]
    public async Task TurnosRestantes_is_max_of_applied_turn_and_1_plus_duration_minus_current_round_and_null_without_duration()
    {
        var gm = await RegisterGmAsync("CpGm9", "cp9@teste.com", auditor: true);
        var comPrazo = await CriarNoCatalogoAsync(gm, Catalogo("Com prazo", 3));
        var semPrazo = await CriarNoCatalogoAsync(gm, Catalogo("Sem prazo", null));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(comPrazo.Id, null, null, null));
        await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(semPrazo.Id, null, null, null));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.EncounterParticipantCondicoes.Where(c => c.EncounterParticipantId == Guid.Parse(participantId)).Select(c => c.AplicadaNoTurno).ToListAsync()).Should().OnlyContain(t => t == 1);
        }
        var restantes = async () => (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.ToDictionary(c => c.Nome, c => c.TurnosRestantes);
        (await restantes()).Should().BeEquivalentTo(new Dictionary<string, int?> { ["Com prazo"] = 3, ["Sem prazo"] = null });

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            await db.Encounters.Where(e => e.Id == Guid.Parse(encounterId)).ExecuteUpdateAsync(s => s.SetProperty(e => e.CurrentRound, 3));
        }
        (await restantes()).Should().BeEquivalentTo(new Dictionary<string, int?> { ["Com prazo"] = 1, ["Sem prazo"] = null });

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            await db.Encounters.Where(e => e.Id == Guid.Parse(encounterId)).ExecuteUpdateAsync(s => s.SetProperty(e => e.CurrentRound, 9));
        }
        (await restantes()).Should().BeEquivalentTo(new Dictionary<string, int?> { ["Com prazo"] = 0, ["Sem prazo"] = null });
    }

    [Fact]
    public async Task Apply_and_remove_in_progress_are_logged_with_the_condition_name_and_nothing_is_logged_in_preparation()
    {
        var gm = await RegisterGmAsync("CpGm10", "cp10@teste.com", auditor: true);
        var antes = await CriarNoCatalogoAsync(gm, Catalogo("Antes"));
        var durante = await CriarNoCatalogoAsync(gm, Catalogo("Durante"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        var aplicadaAntes = await (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(antes.Id, null, null, null))).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>();
        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes/{aplicadaAntes!.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LogAsync(gm, encounterId)).Should().BeEmpty();

        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        var baseline = (await LogAsync(gm, encounterId)).Count;
        var aplicada = await (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(durante.Id, null, null, null))).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>();
        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes/{aplicada!.Id}", gm))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var log = (await LogAsync(gm, encounterId)).Skip(baseline).ToList();
        log.Select(e => (e.Tipo, e.Texto)).Should().Equal(("CondicaoAdicionada", "Durante"), ("CondicaoRemovida", "Durante"));
        var nome = (await ListarAsync(gm, encounterId)).Single().Nome;
        log.Should().OnlyContain(e => e.ParticipanteNome == nome);
        (await ListarAsync(gm, encounterId)).Single().CondicoesAplicadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Apply_and_remove_on_a_finished_encounter_return_409()
    {
        var gm = await RegisterGmAsync("CpGm11", "cp11@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Tarde"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        var aplicada = await (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null))).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>();
        await PostAsync(gm, $"/api/encounters/{encounterId}/start");
        await PostAsync(gm, $"/api/encounters/{encounterId}/finish");

        var outra = await CriarNoCatalogoAsync(gm, Catalogo("Outra"));
        (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(outra.Id, null, null, null))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var remover = await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes/{aplicada!.Id}", gm));
        remover.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await remover.Content.ReadAsStringAsync()).Should().Contain("Encontro finalizado.");
    }

    [Fact]
    public async Task Another_gm_gets_404_on_every_endpoint_and_an_unknown_applied_condition_is_404()
    {
        var gm = await RegisterGmAsync("CpGm12", "cp12@teste.com", auditor: true);
        var outro = await RegisterGmAsync("CpGm13", "cp13@teste.com");
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Alheia"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        var aplicada = await (await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null))).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>();
        var baseUrl = $"/api/encounters/{encounterId}/participants/{participantId}";

        (await AplicarAsync(outro, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(Authed(HttpMethod.Delete, $"{baseUrl}/condicoes/{aplicada!.Id}", outro))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(Authed(HttpMethod.Get, $"{baseUrl}/itens-desabilitaveis", outro))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(Authed(HttpMethod.Delete, $"{baseUrl}/condicoes/{Guid.NewGuid()}", gm))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/participants/{Guid.NewGuid()}/itens-desabilitaveis", gm))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Two_instances_of_one_npc_sheet_hold_different_conditions()
    {
        var gm = await RegisterGmAsync("CpGm14", "cp14@teste.com", auditor: true);
        var veneno = await CriarNoCatalogoAsync(gm, Catalogo("Veneno"));
        var lentidao = await CriarNoCatalogoAsync(gm, Catalogo("Lentidão"));
        var encounterId = await CreateEncounterAsync(gm);
        var npcId = await CreateNpcSheetAsync(gm);
        var primeiro = await AddNpcParticipantAsync(gm, encounterId, npcId);
        var segundo = await AddNpcParticipantAsync(gm, encounterId, npcId);

        await AplicarAsync(gm, encounterId, primeiro, new AplicarCondicaoRequest(veneno.Id, null, null, null));
        await AplicarAsync(gm, encounterId, segundo, new AplicarCondicaoRequest(lentidao.Id, null, null, null));
        // The same catalog entry is fine on the other instance.
        (await AplicarAsync(gm, encounterId, segundo, new AplicarCondicaoRequest(veneno.Id, null, null, null))).StatusCode.Should().Be(HttpStatusCode.Created);

        var lista = (await ListarAsync(gm, encounterId)).ToDictionary(p => p.Id);
        lista[primeiro].CondicoesAplicadas.Select(c => c.Nome).Should().Equal("Veneno");
        lista[segundo].CondicoesAplicadas.Select(c => c.Nome).Should().BeEquivalentTo("Lentidão", "Veneno");
    }

    [Fact]
    public async Task Removing_a_participant_removes_its_applied_conditions()
    {
        var gm = await RegisterGmAsync("CpGm15", "cp15@teste.com", auditor: true);
        var condicao = await CriarNoCatalogoAsync(gm, Catalogo("Some"));
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        await AplicarAsync(gm, encounterId, participantId, new AplicarCondicaoRequest(condicao.Id, null, null, null));

        (await _client.SendAsync(Authed(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}", gm))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.EncounterParticipantCondicoes.AnyAsync(c => c.EncounterParticipantId == Guid.Parse(participantId))).Should().BeFalse();
    }

    [Fact]
    public async Task Update_refuses_a_legacy_text_that_was_not_already_on_the_participant_but_allows_removing_one()
    {
        var gm = await RegisterGmAsync("CpGm16", "cp16@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.EncounterParticipantConditions.Add(new() { Id = Guid.NewGuid(), EncounterParticipantId = Guid.Parse(participantId), Texto = "Antiga 1" });
            db.EncounterParticipantConditions.Add(new() { Id = Guid.NewGuid(), EncounterParticipantId = Guid.Parse(participantId), Texto = "Antiga 2" });
            await db.SaveChangesAsync();
        }
        var url = $"/api/encounters/{encounterId}/participants/{participantId}";

        var nova = await _client.SendAsync(Authed(HttpMethod.Put, url, gm, new UpdateParticipantRequest(10, null, null, null, 3, ["Antiga 1", "Nova"])));
        nova.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await nova.Content.ReadAsStringAsync()).Should().Contain("Novas condições vêm do catálogo.");
        (await ListarAsync(gm, encounterId)).Single().Condicoes.Should().BeEquivalentTo("Antiga 1", "Antiga 2");

        var reduzir = await _client.SendAsync(Authed(HttpMethod.Put, url, gm, new UpdateParticipantRequest(10, null, null, null, 3, ["Antiga 2"])));
        reduzir.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListarAsync(gm, encounterId)).Single().Condicoes.Should().Equal("Antiga 2");
    }

    [Fact]
    public async Task ItensDesabilitaveis_of_an_npc_lists_the_sheet_rows_per_category()
    {
        var gm = await RegisterGmAsync("CpGm17", "cp17@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var npcId = await CreateNpcSheetAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId, npcId);
        var ids = await SemearNpcCompletoAsync(Guid.Parse(npcId));

        var response = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/participants/{participantId}/itens-desabilitaveis", gm));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = (await response.Content.ReadFromJsonAsync<ItensDesabilitaveisResponse>())!;
        corpo.Categorias.Keys.Should().BeEquivalentTo("Arma", "Armadura", "Escudo", "Artefato", "Magia", "Habilidade", "Runa", "Passiva");
        Par(corpo, "Arma").Should().Equal((ids["Arma"], "Espada"));
        // Only the slot that holds an item; the empty slot is not listed.
        Par(corpo, "Armadura").Should().Equal((ids["Armadura"], "Elmo"));
        Par(corpo, "Escudo").Should().Equal((ids["Escudo"], "Broquel"));
        Par(corpo, "Artefato").Should().Equal((ids["Artefato"], "Anel do Poder"));
        Par(corpo, "Magia").Should().Equal((ids["Magia"], "Bola de Fogo"));
        Par(corpo, "Habilidade").Should().Equal((ids["Habilidade"], "Golpe Forte"));
        Par(corpo, "Passiva").Should().Equal((ids["Passiva"], "Pele Grossa"));
        Par(corpo, "Runa").Should().Equal((ids["Runa"], "Runa do Fogo"));
    }

    [Fact]
    public async Task ItensDesabilitaveis_of_a_creature_uses_the_manual_weapon_name_and_has_no_runes()
    {
        var gm = await RegisterGmAsync("CpGm18", "cp18@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var criaturaId = await CreateCreatureSheetAsync(gm);
        var add = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gm, new AddParticipantRequest(null, null, criaturaId, 10)));
        var participantId = (await add.Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
        Guid natural, comItem;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var sheetId = Guid.Parse(criaturaId);
            var gmId = (await db.CreatureSheets.FindAsync(sheetId))!.GmId;
            var arma = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Lança", Peso = 1, Preco = 1 };
            db.Set<Arma>().Add(arma);
            await db.SaveChangesAsync();
            natural = Guid.NewGuid();
            comItem = Guid.NewGuid();
            db.CreatureWeapons.Add(new CreatureWeapon { Id = natural, CreatureSheetId = sheetId, ManualNome = "Mordida" });
            db.CreatureWeapons.Add(new CreatureWeapon { Id = comItem, CreatureSheetId = sheetId, ItemId = arma.Id, DurabilidadeAtual = 5 });
            await db.SaveChangesAsync();
        }

        var corpo = (await (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/participants/{participantId}/itens-desabilitaveis", gm))).Content.ReadFromJsonAsync<ItensDesabilitaveisResponse>())!;

        Par(corpo, "Arma").Should().BeEquivalentTo(new[] { (natural, "Mordida"), (comItem, "Lança") });
        Par(corpo, "Runa").Should().BeEmpty();
    }

    [Fact]
    public async Task ItensDesabilitaveis_is_empty_for_a_participant_whose_source_sheet_is_gone()
    {
        var gm = await RegisterGmAsync("CpGm19", "cp19@teste.com");
        var encounterId = await CreateEncounterAsync(gm);
        var npcId = await CreateNpcSheetAsync(gm);
        var participantId = await AddNpcParticipantAsync(gm, encounterId, npcId);
        await SemearNpcCompletoAsync(Guid.Parse(npcId));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            await db.NpcSheets.Where(s => s.Id == Guid.Parse(npcId)).ExecuteDeleteAsync();
        }

        var response = await _client.SendAsync(Authed(HttpMethod.Get, $"/api/encounters/{encounterId}/participants/{participantId}/itens-desabilitaveis", gm));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = (await response.Content.ReadFromJsonAsync<ItensDesabilitaveisResponse>())!;
        corpo.Categorias.Should().HaveCount(8).And.OnlyContain(c => c.Value.Count == 0);
    }

    private static List<(Guid, string)> Par(ItensDesabilitaveisResponse r, string categoria) =>
        r.Categorias[categoria].Select(i => (Guid.Parse(i.Id), i.Nome)).ToList();

    private async Task<(Guid Arma, Guid Runa)> SemearArmaERunaAsync(Guid npcSheetId)
    {
        var ids = await SemearNpcCompletoAsync(npcSheetId);
        return (ids["Arma"], ids["Runa"]);
    }

    // One row of each disableable kind on the NPC sheet, plus rows that must NOT be listed (empty armor slots, Racial spell).
    private async Task<Dictionary<string, Guid>> SemearNpcCompletoAsync(Guid npcSheetId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = (await db.NpcSheets.FindAsync(npcSheetId))!.GmId;
        var arma = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Espada", Peso = 1, Preco = 1 };
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Elmo", Peso = 1, Preco = 1 };
        var escudo = new Escudo { Id = Guid.NewGuid(), GmId = gmId, Nome = "Broquel", Peso = 1, Preco = 1 };
        var artefato = new Artefato { Id = Guid.NewGuid(), GmId = gmId, Nome = "Anel do Poder", Peso = 1, Preco = 1 };
        db.Set<Arma>().Add(arma);
        db.Set<Armadura>().Add(armadura);
        db.Set<Escudo>().Add(escudo);
        db.Set<Artefato>().Add(artefato);
        await db.SaveChangesAsync();

        var ids = new Dictionary<string, Guid>
        {
            ["Arma"] = Guid.NewGuid(), ["Escudo"] = Guid.NewGuid(), ["Artefato"] = Guid.NewGuid(),
            ["Magia"] = Guid.NewGuid(), ["Habilidade"] = Guid.NewGuid(), ["Passiva"] = Guid.NewGuid(), ["Runa"] = Guid.NewGuid(),
        };
        db.NpcWeapons.Add(new NpcWeapon { Id = ids["Arma"], NpcSheetId = npcSheetId, ItemId = arma.Id, DurabilidadeAtual = 5 });
        // Every slot already exists, empty, on a new sheet: fill one, leave the others empty.
        var slot = await db.NpcArmorSlots.SingleAsync(a => a.NpcSheetId == npcSheetId && a.Slot == RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Capacete);
        ids["Armadura"] = slot.Id;
        slot.ItemId = armadura.Id;
        slot.DurabilidadeAtual = 5;
        db.NpcShields.Add(new NpcShield { Id = ids["Escudo"], NpcSheetId = npcSheetId, ItemId = escudo.Id, DurabilidadeAtual = 5 });
        db.NpcArtifacts.Add(new NpcArtifact { Id = ids["Artefato"], NpcSheetId = npcSheetId, ArtifactItemId = artefato.Id });
        db.NpcRunes.Add(new NpcRune { Id = ids["Runa"], NpcSheetId = npcSheetId, Nome = "Runa do Fogo", Descricao = "Queima.", Grau = 1 });
        foreach (var (chave, nome, tipo) in new[] { ("Magia", "Bola de Fogo", SpellAbilityTipo.Magia), ("Habilidade", "Golpe Forte", SpellAbilityTipo.Habilidade), ("Passiva", "Pele Grossa", SpellAbilityTipo.Passiva) })
            db.NpcSpellAbilities.Add(new NpcSpellAbility { Id = ids[chave], NpcSheetId = npcSheetId, Nome = nome, Tipo = tipo, Descricao = "." });
        db.NpcSpellAbilities.Add(new NpcSpellAbility { Id = Guid.NewGuid(), NpcSheetId = npcSheetId, Nome = "Dom Racial", Tipo = SpellAbilityTipo.Racial, Descricao = "." });
        await db.SaveChangesAsync();
        return ids;
    }
}
