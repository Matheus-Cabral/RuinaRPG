using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration.SheetLogs;

/// <summary>Campanha R0016 / Técnico R0012: o interceptor grava o log de ficha no mesmo salvamento da alteração.</summary>
public partial class SheetLogRecordingTests : IClassFixture<BlankDatabaseFixture>, IAsyncLifetime
{
    private readonly BlankDatabaseFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SheetLogRecordingTests(BlankDatabaseFixture postgres) => _postgres = postgres;

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

    private static string Rotulo<T>(string propriedade) => SheetLogMap.Entidades[typeof(T)].Campos[propriedade];
    private static string Secao<T>() => SheetLogMap.Entidades[typeof(T)].Secao;

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<HttpResponseMessage> SendOkAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var response = await _client.SendAsync(AuthedRequest(method, url, token, body));
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} respondeu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private async Task<Guid> MeIdAsync(string token) =>
        Guid.Parse((await (await SendOkAsync(HttpMethod.Get, "/api/auth/me", token)).Content.ReadFromJsonAsync<MeResponse>())!.Id);

    private async Task<(string Token, Guid Id)> RegisterGmAsync(string nickname)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, $"{nickname.ToLowerInvariant()}@teste.com", "Senha!123", "Senha!123"));
        var token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        return (token, await MeIdAsync(token));
    }

    private async Task<(string Token, Guid Id)> RegisterJogadorAsync(string gmToken, string nickname)
    {
        var codeResponse = await SendOkAsync(HttpMethod.Post, "/api/invite-codes", gmToken);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, $"{nickname.ToLowerInvariant()}@teste.com", "Senha!123", "Senha!123", code));
        var token = (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        return (token, await MeIdAsync(token));
    }

    private sealed record Cenario(string GmToken, Guid GmId, string PlayerToken, Guid PlayerId, string PlayerNickname, string CampaignId, Guid SheetId);

    /// <summary>Um GM, um jogador ligado a ele, uma campanha com o jogador e a Ficha de Personagem dele.</summary>
    private async Task<Cenario> CenarioAsync(string sufixo)
    {
        var (gmToken, gmId) = await RegisterGmAsync($"LogGm{sufixo}");
        var playerNickname = $"LogJogador{sufixo}";
        var (playerToken, playerId) = await RegisterJogadorAsync(gmToken, playerNickname);
        var campaign = await SendOkAsync(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest($"Campanha {sufixo}", ""));
        var campaignId = (await campaign.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId.ToString()));
        var sheet = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId.ToString()));
        var sheetId = Guid.Parse((await sheet.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id);
        return new Cenario(gmToken, gmId, playerToken, playerId, playerNickname, campaignId, sheetId);
    }

    private static UpdateCharacterSheetRequest ValidUpdate() => new(
        null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, "Marcado pela Ruína",
        true, 749, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, 0, null, null, 0, null, null, 0);

    private static UpdateNpcSheetRequest ValidNpcUpdate() => new(
        null, "Sentinela da Ruína", "Humano", "Sinir", "Campeao", "Duelista", null, "Guardiã do Portal",
        5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100, null, null, 0, null, null, 0);

    private Task PutSheetAsync(string token, Guid sheetId, UpdateCharacterSheetRequest request) =>
        SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{sheetId}", token, request);

    private async Task<CharacterSheetResponse> GetSheetAsync(string token, Guid sheetId) =>
        (await (await SendOkAsync(HttpMethod.Get, $"/api/character-sheets/{sheetId}", token)).Content.ReadFromJsonAsync<CharacterSheetResponse>())!;

    private async Task<List<SheetLogEntry>> LogAsync(Func<SheetLogEntry, bool> daFicha)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return (await db.SheetLogEntries.AsNoTracking().OrderBy(l => l.OccurredAt).ToListAsync()).Where(daFicha).ToList();
    }

    private Task<List<SheetLogEntry>> LogDaFichaAsync(Guid sheetId) => LogAsync(l => l.CharacterSheetId == sheetId);

    /// <summary>As linhas que a ação gravou: o log da ficha depois dela, menos o que já estava lá.</summary>
    private async Task<List<SheetLogEntry>> NovasAsync(Guid sheetId, Func<Task> acao)
    {
        var antes = (await LogDaFichaAsync(sheetId)).Select(l => l.Id).ToHashSet();
        await acao();
        return (await LogDaFichaAsync(sheetId)).Where(l => !antes.Contains(l.Id)).ToList();
    }

    [Fact]
    public async Task Creating_a_sheet_logs_a_single_Criou_line()
    {
        var c = await CenarioAsync("Criou");

        var log = await LogDaFichaAsync(c.SheetId);

        var linha = log.Should().ContainSingle().Subject;
        linha.Acao.Should().Be(SheetLogAcao.Criou);
        linha.Secao.Should().Be(Secao<CharacterSheet>());
        linha.ActorUserId.Should().Be(c.GmId);
        linha.ActorNickname.Should().Be("LogGmCriou");
        linha.ActorIsGm.Should().BeTrue();
        linha.NpcSheetId.Should().BeNull();
        linha.CreatureSheetId.Should().BeNull();
        linha.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_player_edit_logs_the_field_with_before_and_after()
    {
        var c = await CenarioAsync("Jogador");
        await PutSheetAsync(c.GmToken, c.SheetId, ValidUpdate());
        var guardada = (await GetSheetAsync(c.GmToken, c.SheetId)).VitalidadeAtual;
        guardada.Should().BeGreaterThan(0);

        var novas = await NovasAsync(c.SheetId, () =>
            PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { VitalidadeAtual = guardada - 1 }));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Acao.Should().Be(SheetLogAcao.Alterou);
        linha.Campo.Should().Be(Rotulo<CharacterSheet>("VitalidadeAtual"));
        linha.ValorAntes.Should().Be(guardada.ToString());
        linha.ValorDepois.Should().Be((guardada - 1).ToString());
        linha.Categoria.Should().Be(SheetLogCategoria.RecursoVFA);
        linha.ItemNome.Should().BeNull();
        linha.ActorUserId.Should().Be(c.PlayerId);
        linha.ActorNickname.Should().Be(c.PlayerNickname);
        linha.ActorIsGm.Should().BeFalse();
    }

    [Fact]
    public async Task A_gm_edit_is_flagged_ActorIsGm()
    {
        var c = await CenarioAsync("GmEdita");

        var novas = await NovasAsync(c.SheetId, () => PutSheetAsync(c.GmToken, c.SheetId, ValidUpdate()));

        novas.Should().NotBeEmpty();
        novas.Should().OnlyContain(l => l.ActorIsGm && l.ActorUserId == c.GmId);
        // Enum com o nome que a tela mostra, e não o do membro.
        var vocacao = novas.Single(l => l.Campo == Rotulo<CharacterSheet>("Vocacao"));
        vocacao.ValorAntes.Should().Be("—");
        vocacao.ValorDepois.Should().Be("Campeão");
        novas.Single(l => l.Campo == Rotulo<CharacterSheet>("Nome")).Categoria.Should().Be(SheetLogCategoria.Geral);
        // Todas as linhas de um salvamento carregam o mesmo instante.
        novas.Select(l => l.OccurredAt).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Saving_the_same_values_again_logs_nothing()
    {
        var c = await CenarioAsync("Repetido");
        await PutSheetAsync(c.GmToken, c.SheetId, ValidUpdate());

        var novas = await NovasAsync(c.SheetId, async () =>
        {
            await PutSheetAsync(c.GmToken, c.SheetId, ValidUpdate());
            await PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate());
        });

        novas.Should().BeEmpty();
    }

    [Fact]
    public async Task Estresse_Atual_gets_its_own_categoria()
    {
        var c = await CenarioAsync("Estresse");
        await PutSheetAsync(c.GmToken, c.SheetId, ValidUpdate());
        var guardado = (await GetSheetAsync(c.GmToken, c.SheetId)).EstresseAtual;
        guardado.Should().BeGreaterThan(0);

        var novas = await NovasAsync(c.SheetId, () =>
            PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { EstresseAtual = guardado - 1 }));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Campo.Should().Be(Rotulo<CharacterSheet>("EstresseAtual"));
        linha.Categoria.Should().Be(SheetLogCategoria.Estresse);
    }

    [Fact]
    public async Task A_long_text_is_logged_without_its_content()
    {
        var c = await CenarioAsync("Historia");

        var novas = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/historia", c.PlayerToken, new UpdateHistoriaRequest("<p>Nasceu na Ruína.</p>")));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Acao.Should().Be(SheetLogAcao.Alterou);
        linha.Campo.Should().Be(SheetLogMap.RotulosDeTextoLongo["Historia"]);
        linha.ValorAntes.Should().BeNull();
        linha.ValorDepois.Should().BeNull();
    }

    [Fact]
    public async Task Adding_and_removing_a_list_item_logs_Adicionou_and_Removeu_with_its_name()
    {
        var c = await CenarioAsync("Lista");
        string afeicaoId = null!;

        var aoAdicionar = await NovasAsync(c.SheetId, async () =>
        {
            var response = await SendOkAsync(HttpMethod.Post, $"/api/character-sheets/{c.SheetId}/affections", c.PlayerToken, new AddCharacterAffectionRequest("Rival", -2));
            afeicaoId = (await response.Content.ReadFromJsonAsync<CharacterAffectionResponse>())!.Id;
        });
        var aoRemover = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Delete, $"/api/character-sheets/{c.SheetId}/affections/{afeicaoId}", c.PlayerToken));

        var adicionou = aoAdicionar.Should().ContainSingle().Subject;
        adicionou.Acao.Should().Be(SheetLogAcao.Adicionou);
        adicionou.Secao.Should().Be(Secao<CharacterAffection>());
        adicionou.ItemNome.Should().Be("Rival");
        adicionou.Campo.Should().BeNull();
        var removeu = aoRemover.Should().ContainSingle().Subject;
        removeu.Acao.Should().Be(SheetLogAcao.Removeu);
        removeu.Secao.Should().Be(Secao<CharacterAffection>());
        removeu.ItemNome.Should().Be("Rival");
    }

    private static AddCharacterSpellAbilityRequest BolaDeFogo() => new(
        null, "Bola de Fogo", "Magia", 3, "Uma explosão de fogo.",
        [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)]);

    private async Task<string> AddBolaDeFogoAsync(Cenario c)
    {
        var response = await SendOkAsync(HttpMethod.Post, $"/api/character-sheets/{c.SheetId}/spell-abilities", c.GmToken, BolaDeFogo());
        return (await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!.Id;
    }

    [Fact]
    public async Task A_spell_built_on_the_sheet_logs_one_line_and_its_effects_are_folded_in()
    {
        var c = await CenarioAsync("Magia");
        await SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/pontos-de-ignicao", c.GmToken, 30);

        var novas = await NovasAsync(c.SheetId, () => AddBolaDeFogoAsync(c));

        novas.Should().HaveCount(2);
        var adicionou = novas.Single(l => l.Acao == SheetLogAcao.Adicionou);
        adicionou.Secao.Should().Be(Secao<CharacterSpellAbility>());
        adicionou.ItemNome.Should().Be("Bola de Fogo");
        var saldo = novas.Single(l => l.Acao == SheetLogAcao.Alterou);
        saldo.Campo.Should().Be(Rotulo<CharacterSheet>("PontosDeIgnicaoAtual"));
        saldo.ValorAntes.Should().Be("30");
        saldo.ValorDepois.Should().Be("16");
    }

    [Fact]
    public async Task Editing_a_spell_logs_its_changed_fields_and_one_line_for_the_effects()
    {
        var c = await CenarioAsync("MagiaEdita");
        await SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/pontos-de-ignicao", c.GmToken, 30);
        var id = await AddBolaDeFogoAsync(c);

        // O Dano cai de 4 para 3: o Gasto em PI vai de 14 para 12 e os 2 PI voltam para o saldo da ficha.
        var novas = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/spell-abilities/{id}", c.GmToken,
                new UpdateSheetSpellAbilityRequest("Bola de Gelo", "Magia", 3, "Uma explosão de fogo.",
                    [new SpellAbilityEffectRequest("Dano", 3, 6), new SpellAbilityEffectRequest("Alcance", 2, 6)])));

        novas.Should().OnlyContain(l => l.Acao == SheetLogAcao.Alterou);
        var daMagia = novas.Where(l => l.Secao == Secao<CharacterSpellAbility>()).ToList();
        daMagia.Should().OnlyContain(l => l.ItemNome == "Bola de Fogo");
        var nome = daMagia.Single(l => l.Campo == Rotulo<CharacterSpellAbility>("Nome"));
        nome.ValorAntes.Should().Be("Bola de Fogo");
        nome.ValorDepois.Should().Be("Bola de Gelo");
        var gasto = daMagia.Single(l => l.Campo == Rotulo<CharacterSpellAbility>("GastoEmPI"));
        gasto.ValorAntes.Should().Be("14");
        gasto.ValorDepois.Should().Be("12");
        var efeitos = daMagia.Single(l => l.Campo == "Efeitos");
        efeitos.ValorAntes.Should().BeNull();
        efeitos.ValorDepois.Should().BeNull();
        // Nenhuma linha por Efeito: fora Nome, Gasto em PI, Custo e Efeitos, só o saldo de PI da ficha.
        daMagia.Select(l => l.Campo).Should().BeSubsetOf(new[] { nome.Campo, gasto.Campo, Rotulo<CharacterSpellAbility>("Custo"), "Efeitos" });
        var saldo = novas.Except(daMagia).Should().ContainSingle().Subject;
        saldo.Campo.Should().Be(Rotulo<CharacterSheet>("PontosDeIgnicaoAtual"));
        saldo.ValorAntes.Should().Be("16");
        saldo.ValorDepois.Should().Be("18");
    }

    [Fact]
    public async Task Editing_a_spell_without_touching_its_effects_logs_no_line_for_them()
    {
        // O controller troca os Efeitos por inteiro a cada edição; trocar por Efeitos iguais não é uma alteração.
        var c = await CenarioAsync("MagiaMesmosEfeitos");
        await SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/pontos-de-ignicao", c.GmToken, 30);
        var id = await AddBolaDeFogoAsync(c);

        var novas = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/spell-abilities/{id}", c.GmToken,
                new UpdateSheetSpellAbilityRequest("Bola de Gelo", "Magia", 3, "Uma explosão de fogo.", BolaDeFogo().Efeitos!)));

        novas.Should().ContainSingle().Which.Campo.Should().Be(Rotulo<CharacterSpellAbility>("Nome"));
    }

    [Fact]
    public async Task An_attribute_row_change_names_the_attribute()
    {
        var c = await CenarioAsync("Atributo");

        var novas = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Put, $"/api/character-sheets/{c.SheetId}/attributes/Forca", c.PlayerToken, new UpdateCharacterAttributeRequest(1, 0, false)));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Acao.Should().Be(SheetLogAcao.Alterou);
        linha.Secao.Should().Be(Secao<CharacterAttribute>());
        linha.ItemNome.Should().Be(SheetLogValues.Render(Atributo.Forca)).And.Be("Força");
        linha.Campo.Should().Be(Rotulo<CharacterAttribute>("Gasto"));
        linha.ValorAntes.Should().Be("0");
        linha.ValorDepois.Should().Be("1");
    }

    [Fact]
    public async Task A_reference_value_is_rendered_by_name()
    {
        var c = await CenarioAsync("Historico");
        await PutSheetAsync(c.GmToken, c.SheetId, ValidUpdate());
        Guid historicoId; string historicoNome;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var historico = await db.Historicos.AsNoTracking().OrderBy(h => h.Nome).FirstAsync(h => !h.IsDeleted);
            (historicoId, historicoNome) = (historico.Id, historico.Nome);
        }

        var novas = await NovasAsync(c.SheetId, () =>
            PutSheetAsync(c.PlayerToken, c.SheetId, ValidUpdate() with { HistoricoId = historicoId.ToString() }));

        var linha = novas.Should().ContainSingle().Subject;
        linha.Campo.Should().Be(Rotulo<CharacterSheet>("HistoricoId"));
        linha.ValorAntes.Should().Be("—");
        linha.ValorDepois.Should().Be(historicoNome);
    }

    [Fact]
    public async Task A_non_granted_npc_is_not_logged_and_a_granted_one_is()
    {
        var c = await CenarioAsync("Npc");
        var doGm = await SendOkAsync(HttpMethod.Post, "/api/npc-sheets", c.GmToken);
        var doGmId = Guid.Parse((await doGm.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id);
        var concessao = await SendOkAsync(HttpMethod.Post, $"/api/campaigns/{c.CampaignId}/grants", c.GmToken, new GrantSheetRequest(c.PlayerId.ToString(), "Npc", null));
        var concedidoId = Guid.Parse((await concessao.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId);

        await SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{doGmId}", c.GmToken, ValidNpcUpdate());
        await SendOkAsync(HttpMethod.Put, $"/api/npc-sheets/{concedidoId}", c.PlayerToken, ValidNpcUpdate());

        (await LogAsync(l => l.NpcSheetId == doGmId)).Should().BeEmpty();
        var log = await LogAsync(l => l.NpcSheetId == concedidoId);
        // A concessão é uma linha só (Criou, do GM), sem uma linha por atributo/perícia semeada com a ficha.
        var criou = log.Where(l => l.ActorUserId == c.GmId).Should().ContainSingle().Subject;
        criou.Acao.Should().Be(SheetLogAcao.Criou);
        criou.ActorIsGm.Should().BeTrue();
        var doJogador = log.Where(l => l.ActorUserId == c.PlayerId).ToList();
        doJogador.Should().NotBeEmpty();
        doJogador.Should().OnlyContain(l => l.Acao == SheetLogAcao.Alterou && !l.ActorIsGm && l.CharacterSheetId == null && l.CreatureSheetId == null);
        doJogador.Should().Contain(l => l.Campo == Rotulo<NpcSheet>("Nome") && l.ValorDepois == "Sentinela da Ruína");
    }

    [Fact]
    public async Task The_character_diary_is_not_logged()
    {
        var c = await CenarioAsync("Diario");

        var novas = await NovasAsync(c.SheetId, () =>
            SendOkAsync(HttpMethod.Post, $"/api/character-sheets/{c.SheetId}/diary", c.PlayerToken, new CreateDiaryEntryRequest("Hoje caímos numa emboscada.", [])));

        novas.Should().BeEmpty();
    }

    private sealed class FixedActor(Guid userId) : ICurrentActor
    {
        public Guid? UserId => userId;
    }

    [Fact]
    public async Task A_failed_save_leaves_no_log()
    {
        var c = await CenarioAsync("Falha");
        await using var comAtor = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<ICurrentActor>(_ => new FixedActor(c.GmId))));

        // Controle: neste mesmo arranjo, um salvamento que dá certo grava o log.
        var doControle = await NovasAsync(c.SheetId, async () =>
        {
            await using var scope = comAtor.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.CharacterSheets.SingleAsync(s => s.Id == c.SheetId)).Nome = "Salvo";
            await db.SaveChangesAsync();
        });
        doControle.Should().ContainSingle().Which.ValorDepois.Should().Be("Salvo");

        var daFalha = await NovasAsync(c.SheetId, async () =>
        {
            await using var scope = comAtor.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.CharacterSheets.SingleAsync(s => s.Id == c.SheetId)).Nome = "Nunca Salvo";
            // Viola a FK para CharacterSheets: o salvamento inteiro falha.
            db.CharacterAttributes.Add(new CharacterAttribute { Id = Guid.NewGuid(), CharacterSheetId = Guid.NewGuid(), Atributo = Atributo.Vigor });

            var salvar = () => db.SaveChangesAsync();

            await salvar.Should().ThrowAsync<DbUpdateException>();
        });

        daFalha.Should().BeEmpty();
        (await GetSheetAsync(c.GmToken, c.SheetId)).Nome.Should().Be("Salvo");
    }

    /// <summary>Um ator cuja leitura do usuário falha: faz a montagem das linhas do log lançar sem código de teste na produção.</summary>
    private sealed class BrokenActor : ICurrentActor
    {
        public Guid? UserId => throw new InvalidOperationException("falha simulada ao montar o log");
    }

    [Fact]
    public async Task A_fault_while_building_the_log_does_not_fail_the_save()
    {
        var c = await CenarioAsync("Defeito");
        await using var comDefeito = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<ICurrentActor, BrokenActor>()));

        var novas = await NovasAsync(c.SheetId, async () =>
        {
            await using var scope = comDefeito.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.CharacterSheets.SingleAsync(s => s.Id == c.SheetId)).Nome = "Salvo Sem Log";
            await db.SaveChangesAsync();
        });

        novas.Should().BeEmpty();
        (await GetSheetAsync(c.GmToken, c.SheetId)).Nome.Should().Be("Salvo Sem Log");
    }

    [Fact]
    public async Task Retrying_a_failed_save_writes_each_log_row_once()
    {
        var c = await CenarioAsync("Retry");
        await using var comAtor = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<ICurrentActor>(_ => new FixedActor(c.GmId))));

        var novas = await NovasAsync(c.SheetId, async () =>
        {
            await using var scope = comAtor.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.CharacterSheets.SingleAsync(s => s.Id == c.SheetId)).Nome = "Depois da Falha";
            var ruim = new CharacterAttribute { Id = Guid.NewGuid(), CharacterSheetId = Guid.NewGuid(), Atributo = Atributo.Vigor };
            db.CharacterAttributes.Add(ruim);
            var salvar = () => db.SaveChangesAsync();
            await salvar.Should().ThrowAsync<DbUpdateException>();

            db.CharacterAttributes.Remove(ruim);
            await db.SaveChangesAsync();
        });

        novas.Should().ContainSingle().Which.ValorDepois.Should().Be("Depois da Falha");
    }

    [Fact]
    public async Task A_write_without_an_authenticated_user_logs_nothing()
    {
        var c = await CenarioAsync("SemAtor");

        var novas = await NovasAsync(c.SheetId, async () =>
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.CharacterSheets.SingleAsync(s => s.Id == c.SheetId)).Nome = "Alterado pelo servidor";
            await db.SaveChangesAsync();
        });

        novas.Should().BeEmpty();
        (await GetSheetAsync(c.GmToken, c.SheetId)).Nome.Should().Be("Alterado pelo servidor");
    }
}
