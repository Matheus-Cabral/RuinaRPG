using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>Gerenciador de Encontros 1.5.0: fases, controle de vez, log do encontro (Gerenciador de Encontros R0001/R0006+).</summary>
public class GerenciadorDeEncontrosTests : MudBunitContext
{
    private sealed class TestHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    private static readonly string Pa = Icons.Material.Filled.PlayArrow;

    private string _status = "EmAndamento";
    private int _turno = 2;
    private string? _vezId = "p2";
    private List<EncounterLogEntryResponse> _log = new();
    private readonly List<string> _requests = new();
    private Func<HttpRequestMessage, Task<HttpResponseMessage>?>? _override;

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private List<EncounterParticipantResponse> _participants = Participants;

    private static readonly List<EncounterParticipantResponse> Participants = new()
    {
        new("p1", "Aldric", 15, 10, 5, 3, 3, false, new(), new()),
        new("p2", "Goblin", 12, 7, 0, 0, 2, false, new() { "Atordoado" }, new()),
        new("p3", "Lobo", 8, 9, 0, 0, 1, true, new(), new()),
    };

    private IRenderedComponent<ContainerFragment> RenderPage()
    {
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuth());
        Services.AddScoped(_ => new HttpClient(new TestHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath.Replace("/api/", "");
            _requests.Add($"{request.Method} {path}");
            if (_override?.Invoke(request) is { } task)
                return await task;
            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (path == "campaigns")
                return Json(new List<CampaignResponse> { new("c1", "Campanha", "d", null) });
            if (path == "campaigns/c1/encounters")
                return Json(new List<EncounterResponse> { new("e1", "Emboscada", _turno, 0, _status, _status == "Finalizado" ? DateTime.UtcNow : null, _vezId) });
            if (path == "encounters/e1/participants")
                return Json(_participants);
            if (path == "condicoes")
                return Json(new List<RuinaRPG.Contracts.Condicoes.CondicaoResponse>());
            if (path == "encounters/e1/log")
                return Json(_log);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        })) { BaseAddress = new Uri("http://localhost/api/") });

        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<GerenciadorDeEncontros>(2);
            builder.AddAttribute(3, "CampaignId", "c1");
            builder.AddAttribute(4, "EncounterId", "e1");
            builder.CloseComponent();
        });
    }

    private sealed class FakeAuth : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new System.Security.Claims.ClaimsPrincipal()));
    }

    private async Task<IRenderedComponent<ContainerFragment>> RenderLoadedAsync()
    {
        var cut = RenderPage();
        cut.WaitForState(() => cut.Markup.Contains("Emboscada"));
        await Task.Delay(50);
        return cut;
    }

    private static bool Has(IRenderedComponent<ContainerFragment> cut, string texto) =>
        cut.FindAll("button").Any(b => b.TextContent.Trim() == texto);

    private static AngleSharp.Dom.IElement Botao(IRenderedComponent<ContainerFragment> cut, string texto) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == texto);

    private static string[] TurnButtons = { "Vez anterior", "Próxima vez", "Finalizar encontro" };

    [Fact]
    public async Task Preparation_shows_Turno_0_the_chip_and_only_the_start_button()
    {
        _status = "Preparacao"; _turno = 0; _vezId = null;
        var cut = await RenderLoadedAsync();

        cut.Markup.Should().Contain("Emboscada — Turno 0");
        cut.Markup.Should().Contain("Preparação");
        cut.Markup.Should().NotContain("Rodada");
        Has(cut, "Iniciar encontro").Should().BeTrue();
        TurnButtons.Any(t => Has(cut, t)).Should().BeFalse();
        cut.FindAll("button[aria-label='Passar a vez para este']").Should().BeEmpty();
        cut.FindAll("button[aria-label='Remover do encontro']").Should().HaveCount(3);
        cut.Markup.Should().NotContain("Log do encontro");
        cut.FindAll("button[aria-label='Log do encontro']").Should().BeEmpty();
    }

    [Fact]
    public async Task In_progress_marks_the_participant_on_turn_and_offers_the_turn_controls()
    {
        var cut = await RenderLoadedAsync();

        cut.Markup.Should().Contain("Turno 2, vez de Goblin");
        cut.Markup.Should().Contain("Em andamento");
        var marcadas = cut.FindAll("tr.encontro-vez");
        marcadas.Should().ContainSingle();
        marcadas[0].TextContent.Should().Contain("Goblin");
        marcadas[0].InnerHtml.Should().Contain("M8 5v14l11-7z"); // o glifo de Icons.Material.Filled.PlayArrow
        Pa.Should().Contain("M8 5v14l11-7z");
        TurnButtons.All(t => Has(cut, t)).Should().BeTrue();
        Has(cut, "Iniciar encontro").Should().BeFalse();
        cut.FindAll("button[aria-label='Passar a vez para este']").Should().HaveCount(2);
        cut.FindAll("tr.encontro-vez button[aria-label='Passar a vez para este']").Should().BeEmpty();
    }

    [Fact]
    public async Task The_bar_says_nobody_has_the_turn_when_there_is_no_current_participant()
    {
        _vezId = null;
        var cut = await RenderLoadedAsync();

        cut.Markup.Should().Contain("Turno 2, ninguém com a vez");
        cut.FindAll("tr.encontro-vez").Should().BeEmpty();
    }

    [Fact]
    public async Task Proxima_vez_posts_advance_turn_and_disables_the_turn_buttons_while_pending()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>();
        _override = r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("advance-turn") ? gate.Task : null;
        var cut = await RenderLoadedAsync();

        var clique = cut.InvokeAsync(() => Botao(cut, "Próxima vez").Click());
        cut.WaitForAssertion(() => TurnButtons.All(t => Botao(cut, t).HasAttribute("disabled")).Should().BeTrue());
        _requests.Should().Contain("POST encounters/e1/advance-turn");

        gate.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await clique;
        cut.WaitForAssertion(() => TurnButtons.Any(t => Botao(cut, t).HasAttribute("disabled")).Should().BeFalse());
    }

    [Fact]
    public async Task Vez_anterior_posts_previous_turn_and_the_row_button_posts_turn_with_the_participant()
    {
        var cut = await RenderLoadedAsync();

        await cut.InvokeAsync(() => Botao(cut, "Vez anterior").Click());
        _requests.Should().Contain("POST encounters/e1/previous-turn");

        await cut.InvokeAsync(() => cut.FindAll("button[aria-label='Passar a vez para este']")[0].Click());
        _requests.Should().Contain("POST encounters/e1/turn/p1");
    }

    [Fact]
    public async Task Iniciar_encontro_posts_start()
    {
        _status = "Preparacao"; _turno = 0; _vezId = null;
        var cut = await RenderLoadedAsync();
        await cut.InvokeAsync(() => Botao(cut, "Iniciar encontro").Click());
        _requests.Should().Contain("POST encounters/e1/start");
    }

    [Fact]
    public async Task Finalizar_encontro_asks_for_confirmation_before_posting_finish()
    {
        var cut = await RenderLoadedAsync();

        _ = cut.InvokeAsync(() => Botao(cut, "Finalizar encontro").Click());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Finalizar o encontro? Ele passa a ser somente leitura; é possível reabrir depois."));
        _requests.Should().NotContain("POST encounters/e1/finish");

        Botao(cut, "Cancelar").Click();
        await Task.Delay(50);
        _requests.Should().NotContain("POST encounters/e1/finish");

        _ = cut.InvokeAsync(() => Botao(cut, "Finalizar encontro").Click());
        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog button").Any(b => b.TextContent.Trim() == "Finalizar").Should().BeTrue());
        cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Finalizar").Click();
        cut.WaitForAssertion(() => _requests.Should().Contain("POST encounters/e1/finish"));
    }

    [Fact]
    public async Task Removing_a_participant_asks_for_confirmation_before_deleting()
    {
        var cut = await RenderLoadedAsync();

        _ = cut.InvokeAsync(() => cut.FindAll("button[aria-label='Remover do encontro']")[0].Click());
        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Should().NotBeEmpty());
        _requests.Should().NotContain("DELETE encounters/e1/participants/p1");

        cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Remover").Click();
        cut.WaitForAssertion(() => _requests.Should().Contain("DELETE encounters/e1/participants/p1"));
    }

    [Fact]
    public async Task Finalizar_encontro_clicked_twice_opens_a_single_confirmation()
    {
        var cut = await RenderLoadedAsync();

        _ = cut.InvokeAsync(() => Botao(cut, "Finalizar encontro").Click());
        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Should().HaveCount(1));
        _ = cut.InvokeAsync(() => Botao(cut, "Finalizar encontro").Click());
        await Task.Delay(100);
        cut.FindAll(".mud-dialog").Should().HaveCount(1);

        cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Finalizar").Click();
        cut.WaitForAssertion(() => _requests.Count(r => r == "POST encounters/e1/finish").Should().Be(1));
    }

    [Fact]
    public async Task Remover_participante_clicked_twice_opens_a_single_confirmation_and_sends_one_request()
    {
        var cut = await RenderLoadedAsync();

        _ = cut.InvokeAsync(() => cut.FindAll("button[aria-label='Remover do encontro']")[0].Click());
        cut.WaitForAssertion(() => cut.FindAll(".mud-dialog").Should().HaveCount(1));
        _ = cut.InvokeAsync(() => cut.FindAll("button[aria-label='Remover do encontro']")[1].Click());
        await Task.Delay(100);
        cut.FindAll(".mud-dialog").Should().HaveCount(1);

        cut.FindAll(".mud-dialog button").First(b => b.TextContent.Trim() == "Remover").Click();
        cut.WaitForAssertion(() => _requests.Count(r => r.StartsWith("DELETE")).Should().Be(1));
    }

    [Fact]
    public async Task The_inline_edit_form_disappears_when_a_reload_finds_the_encounter_finished()
    {
        var cut = await RenderLoadedAsync();
        await cut.InvokeAsync(() => Botao(cut, "Editar").Click());
        cut.Markup.Should().Contain("Ações Restantes (0-3)");

        _status = "Finalizado"; _vezId = null;
        await cut.InvokeAsync(() => EncounterConnection.RaiseChangedAsync());

        cut.Markup.Should().NotContain("Ações Restantes (0-3)");
        Has(cut, "Cancelar").Should().BeFalse();
    }

    [Fact]
    public async Task Finished_is_read_only_with_Reabrir_and_the_log()
    {
        _status = "Finalizado"; _vezId = null;
        _log = new() { new(1, DateTime.UtcNow, 1, null, "Finalizado", null, null, null, null, null) };
        var cut = await RenderLoadedAsync();

        cut.Markup.Should().Contain("Finalizado");
        cut.Markup.Should().NotContain("Adicionar Participante");
        TurnButtons.Any(t => Has(cut, t)).Should().BeFalse();
        Has(cut, "Editar").Should().BeFalse();
        cut.FindAll("button[aria-label='Passar a vez para este']").Should().BeEmpty();
        cut.FindAll("button[aria-label='Remover do encontro']").Should().BeEmpty();
        cut.FindAll("button[aria-label='Log do encontro']").Should().ContainSingle();

        await cut.InvokeAsync(() => Botao(cut, "Reabrir").Click());
        _requests.Should().Contain("POST encounters/e1/reopen");
    }

    [Fact]
    public async Task The_inline_log_section_is_gone_and_a_Log_do_encontro_icon_opens_the_dialog()
    {
        var utc = new DateTime(2026, 10, 13, 15, 30, 0, DateTimeKind.Utc);
        _log = new() { new(1, utc, 1, "Aldric", "VezPassada", "Aldric", null, null, null, null, "p1") };
        var cut = await RenderLoadedAsync();

        cut.FindAll(".encontro-log-linha").Should().BeEmpty();
        cut.FindAll("h6").Select(h => h.TextContent.Trim()).Should().NotContain("Log do encontro");
        var botao = cut.FindAll("button[aria-label='Log do encontro']").Should().ContainSingle().Subject;

        await cut.InvokeAsync(() => botao.Click());

        cut.WaitForAssertion(() => cut.Find(".mud-dialog").TextContent.Should().Contain("Log do encontro — Emboscada").And.Contain("Vez de Aldric"));
        _requests.Should().Contain("GET encounters/e1/log");
    }

    [Fact]
    public async Task The_header_icon_opens_the_dialog_of_a_finished_encounter()
    {
        _status = "Finalizado"; _vezId = null;
        _log = new() { new(1, DateTime.UtcNow, 1, null, "Finalizado", null, null, null, null, null) };
        var cut = await RenderLoadedAsync();

        await cut.InvokeAsync(() => cut.Find("button[aria-label='Log do encontro']").Click());

        cut.WaitForAssertion(() => cut.Find(".mud-dialog").TextContent.Should().Contain("Log do encontro — Emboscada").And.Contain("Encontro finalizado"));
    }

    [Fact]
    public async Task An_API_error_on_a_turn_button_shows_the_message_in_the_alert()
    {
        _override = r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("previous-turn")
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Não há vez anterior.") })
            : null;
        var cut = await RenderLoadedAsync();

        await cut.InvokeAsync(() => Botao(cut, "Vez anterior").Click());

        cut.Markup.Should().Contain("Não há vez anterior.");
    }

    private static CondicaoAplicadaResponse Veneno(bool ativa = true, int? turnos = 3) => new("a1", "Veneno", "Corrói.", "Veneno", turnos, ativa);

    private static List<EncounterParticipantResponse> ComCondicoes(params CondicaoAplicadaResponse[] aplicadas) => new()
    {
        new("p1", "Aldric", 15, 10, 5, 3, 3, false, new(), new()),
        new("p2", "Goblin", 12, 7, 0, 0, 2, false, new() { "Atordoado" }, aplicadas.ToList()),
    };

    [Fact]
    public async Task A_catalog_condition_shows_as_a_chip_with_icon_name_and_remaining_turns()
    {
        _participants = ComCondicoes(Veneno(turnos: 3));
        var cut = await RenderLoadedAsync();

        var chip = cut.FindAll(".mud-chip").Single(c => c.TextContent.Contains("Veneno"));
        chip.TextContent.Should().Contain("3 turnos");
        chip.QuerySelector(".mud-chip-icon").Should().NotBeNull();
        chip.ClassList.Should().NotContain("condicao-inativa");
    }

    [Fact]
    public async Task A_condition_without_duration_shows_no_turns_and_an_inactive_one_is_muted()
    {
        _participants = ComCondicoes(Veneno(ativa: false, turnos: null));
        var cut = await RenderLoadedAsync();

        var chip = cut.FindAll(".mud-chip").Single(c => c.TextContent.Contains("Veneno"));
        chip.TextContent.Should().NotContain("turno");
        chip.ClassList.Should().Contain("condicao-inativa");
    }

    [Fact]
    public async Task Removing_a_catalog_chip_deletes_that_applied_condition_and_reloads()
    {
        _participants = ComCondicoes(Veneno());
        var cut = await RenderLoadedAsync();
        var antes = _requests.Count(r => r == "GET encounters/e1/participants");

        await cut.InvokeAsync(() => cut.FindAll(".mud-chip").Single(c => c.TextContent.Contains("Veneno")).QuerySelector(".mud-chip-close-button")!.Click());

        _requests.Should().Contain("DELETE encounters/e1/participants/p2/condicoes/a1");
        _requests.Count(r => r == "GET encounters/e1/participants").Should().Be(antes + 1);
    }

    [Fact]
    public async Task A_legacy_text_condition_is_a_plain_chip_whose_remove_sends_the_reduced_list()
    {
        _participants = ComCondicoes();
        string? corpo = null;
        _override = r =>
        {
            if (r.Method != HttpMethod.Put) return null;
            corpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };
        var cut = await RenderLoadedAsync();

        var chip = cut.FindAll(".mud-chip").Single(c => c.TextContent.Contains("Atordoado"));
        chip.QuerySelector(".mud-chip-icon").Should().BeNull("a legacy chip has no catalog icon");
        await cut.InvokeAsync(() => chip.QuerySelector(".mud-chip-close-button")!.Click());

        _requests.Should().Contain("PUT encounters/e1/participants/p2");
        System.Text.Json.JsonDocument.Parse(corpo!).RootElement.GetProperty("condicoes").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Removing_one_of_two_identical_legacy_tags_keeps_the_other()
    {
        _participants = new()
        {
            new("p2", "Goblin", 12, 7, 0, 0, 2, false, new() { "Atordoado", "Caído", "Atordoado" }, new()),
        };
        string? corpo = null;
        _override = r =>
        {
            if (r.Method != HttpMethod.Put) return null;
            corpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };
        var cut = await RenderLoadedAsync();

        var ultimo = cut.FindAll(".mud-chip").Last(c => c.TextContent.Contains("Atordoado"));
        await cut.InvokeAsync(() => ultimo.QuerySelector(".mud-chip-close-button")!.Click());

        System.Text.Json.JsonDocument.Parse(corpo!).RootElement.GetProperty("condicoes").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Atordoado", "Caído");
    }

    [Fact]
    public async Task The_edit_form_no_longer_has_the_free_text_conditions_field()
    {
        var cut = await RenderLoadedAsync();

        await cut.InvokeAsync(() => Botao(cut, "Editar").Click());

        cut.Markup.Should().Contain("Ações Restantes (0-3)").And.NotContain("Condições (substitui a lista atual)");
    }

    [Fact]
    public async Task Saving_the_edit_form_keeps_the_legacy_texts()
    {
        string? corpo = null;
        _override = r =>
        {
            if (r.Method != HttpMethod.Put) return null;
            corpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };
        var cut = await RenderLoadedAsync();
        await cut.InvokeAsync(() => cut.FindAll("button").Where(b => b.TextContent.Trim() == "Editar").ElementAt(1).Click());

        cut.FindAll("form").Last().Submit();

        cut.WaitForState(() => corpo is not null);
        System.Text.Json.JsonDocument.Parse(corpo!).RootElement.GetProperty("condicoes").EnumerateArray().Select(e => e.GetString()).Should().Equal("Atordoado");
    }

    [Fact]
    public async Task A_live_sourced_participant_edits_PV_PF_PA_and_the_save_sends_the_loaded_values()
    {
        string? corpo = null;
        _override = r =>
        {
            if (r.Method != HttpMethod.Put) return null;
            corpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };
        var cut = await RenderLoadedAsync();
        cut.Markup.Should().NotContain("somente leitura");

        // Lobo (p3) vem de uma ficha: PV 9, PF 0, PA 0.
        await cut.InvokeAsync(() => cut.FindAll("button").Where(b => b.TextContent.Trim() == "Editar").ElementAt(2).Click());
        cut.Markup.Should().NotContain("edite a ficha diretamente");
        var pv = cut.Find("#" + cut.FindAll("label").Single(l => l.TextContent.Trim() == "PV").GetAttribute("for"));
        pv.Change("4");
        cut.FindAll("form").Last().Submit();

        cut.WaitForState(() => corpo is not null);
        var json = System.Text.Json.JsonDocument.Parse(corpo!).RootElement;
        json.GetProperty("pv").GetInt32().Should().Be(4);
        json.GetProperty("pf").GetInt32().Should().Be(0);
        json.GetProperty("pa").GetInt32().Should().Be(0);
        json.GetProperty("pvCarregado").GetInt32().Should().Be(9);
        json.GetProperty("pfCarregado").GetInt32().Should().Be(0);
        json.GetProperty("paCarregado").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task The_info_popup_explains_that_PV_PF_PA_of_a_linked_sheet_change_the_sheet()
    {
        var cut = await RenderLoadedAsync();

        await cut.InvokeAsync(() => cut.Find("button[aria-label='PV, PF e PA no encontro']").Click());

        var texto = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        texto.Should().Contain("mudam na própria ficha").And.Contain("com as Condições ativas").And.Contain("na hora")
            .And.Contain("NPC ou Criatura do GM");
    }

    [Fact]
    public async Task A_reload_while_the_form_is_open_does_not_turn_the_untouched_values_into_edits()
    {
        string? corpo = null;
        _override = r =>
        {
            if (r.Method != HttpMethod.Put) return null;
            corpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        };
        var cut = await RenderLoadedAsync();
        await cut.InvokeAsync(() => cut.FindAll("button").Where(b => b.TextContent.Trim() == "Editar").ElementAt(2).Click());

        // Com o formulário aberto, a ficha do Lobo muda (um tique de Condição, o jogador salvou) e a página recarrega.
        _participants = Participants.Select(p => p.Id == "p3" ? p with { PV = 4, PF = 1, PA = 2 } : p).ToList();
        await cut.InvokeAsync(() => EncounterConnection.RaiseChangedAsync());
        cut.WaitForState(() => cut.Markup.Contains(">4<"));

        // O GM só muda a Iniciativa.
        var iniciativa = cut.Find("#" + cut.FindAll("label").Last(l => l.TextContent.Trim() == "Iniciativa").GetAttribute("for"));
        iniciativa.Change("20");
        cut.FindAll("form").Last().Submit();

        cut.WaitForState(() => corpo is not null);
        var json = System.Text.Json.JsonDocument.Parse(corpo!).RootElement;
        foreach (var (campo, carregado) in new[] { ("pv", "pvCarregado"), ("pf", "pfCarregado"), ("pa", "paCarregado") })
            json.GetProperty(campo).GetInt32().Should().Be(json.GetProperty(carregado).GetInt32(), $"{campo} não foi editado");
        json.GetProperty("iniciativa").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task The_participants_table_scrolls_sideways_and_keeps_each_row_on_one_line()
    {
        var cut = await RenderLoadedAsync();

        // MudSimpleTable põe a classe no seu contêiner, por fora da <table>; o CSS a pega pelos th/td de dentro.
        cut.FindAll(".encontro-tabela .encontro-sem-quebra table").Should().ContainSingle();
    }

    [Fact]
    public async Task A_failed_remove_shows_the_api_message()
    {
        _participants = ComCondicoes(Veneno());
        _override = r => r.Method == HttpMethod.Delete
            ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("Encontro finalizado.") })
            : null;
        var cut = await RenderLoadedAsync();

        await cut.InvokeAsync(() => cut.FindAll(".mud-chip").Single(c => c.TextContent.Contains("Veneno")).QuerySelector(".mud-chip-close-button")!.Click());

        cut.Markup.Should().Contain("Encontro finalizado.");
    }

    [Fact]
    public async Task Adicionar_condicao_is_offered_per_row_and_opens_the_dialog()
    {
        var cut = await RenderLoadedAsync();

        cut.FindAll("button").Count(b => b.TextContent.Trim() == "Adicionar condição").Should().Be(3);
        await cut.InvokeAsync(() => cut.FindAll("button").Where(b => b.TextContent.Trim() == "Adicionar condição").ElementAt(1).Click());

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Adicionar condição: Goblin"));
        _requests.Should().Contain("GET condicoes").And.Contain("GET encounters/e1/participants/p2/itens-desabilitaveis");
    }

    [Fact]
    public async Task Adicionar_condicao_is_not_offered_on_a_finished_encounter()
    {
        _status = "Finalizado"; _vezId = null;
        var cut = await RenderLoadedAsync();

        cut.Markup.Should().NotContain("Adicionar condição");
    }

    // ---------- Resumo da ficha (Gerenciador de Encontros R0014) ----------

    private static List<EncounterParticipantResponse> ComOrigens() => new()
    {
        new("p1", "Aldric", 15, 10, 5, 3, 3, true, new(), new(), "Personagem"),
        new("p2", "Goblin", 12, 7, 0, 0, 2, false, new(), new(), "Npc"),
        new("p3", "Lobo", 8, 9, 0, 0, 1, true, new(), new(), "Criatura"),
        new("p4", "Fantasma", 5, 1, 0, 0, 1, false, new(), new(), null),
    };

    [Fact]
    public async Task The_resumo_button_is_only_on_Npc_and_Criatura_rows()
    {
        _participants = ComOrigens();
        var cut = await RenderLoadedAsync();

        var botoes = cut.FindAll("button[aria-label='Resumo da ficha']");
        botoes.Should().HaveCount(2);
        botoes[0].InnerHtml.Should().Contain("M12 4.5C7 4.5"); // o glifo de Icons.Material.Filled.Visibility
        var linhas = cut.FindAll("tbody tr").Where(l => l.TextContent.Contains("Goblin") || l.TextContent.Contains("Lobo")).ToList();
        linhas.Should().OnlyContain(l => l.QuerySelector("button[aria-label='Resumo da ficha']") != null);
        cut.FindAll("tbody tr").Where(l => l.TextContent.Contains("Aldric") || l.TextContent.Contains("Fantasma"))
            .Should().OnlyContain(l => l.QuerySelector("button[aria-label='Resumo da ficha']") == null);
    }

    [Fact]
    public async Task The_resumo_button_is_still_there_on_a_finished_encounter()
    {
        _status = "Finalizado"; _vezId = null;
        _participants = ComOrigens();
        var cut = await RenderLoadedAsync();

        cut.FindAll("button[aria-label='Resumo da ficha']").Should().HaveCount(2);
        cut.FindAll("button[aria-label='Remover do encontro']").Should().BeEmpty();
    }

    [Fact]
    public async Task Clicking_the_resumo_button_opens_the_dialog_of_that_participant()
    {
        _participants = ComOrigens();
        _override = request => request.RequestUri!.AbsolutePath.EndsWith("/participants/p2/resumo")
            ? Task.FromResult(Json(new ResumoDeFichaResponse("f2", "Npc", "Goblin", null, 2, null, new() { new("Forca", new(5, 5)) }, new(), new(), new(), new(), new(), new(), new())))
            : null;
        var cut = await RenderLoadedAsync();

        await cut.InvokeAsync(() => cut.FindAll("button[aria-label='Resumo da ficha']")[0].Click());

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Resumo da ficha — Goblin"));
        _requests.Should().Contain("GET encounters/e1/participants/p2/resumo");
    }
}
