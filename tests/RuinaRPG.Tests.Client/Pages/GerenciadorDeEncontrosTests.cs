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
                return Json(Participants);
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
        cut.Markup.Should().Contain("Log do encontro");
        cut.Markup.Should().Contain("Encontro finalizado");

        await cut.InvokeAsync(() => Botao(cut, "Reabrir").Click());
        _requests.Should().Contain("POST encounters/e1/reopen");
    }

    [Fact]
    public async Task The_log_renders_a_heading_per_Turno_and_vez_with_each_line_and_its_local_time()
    {
        var utc = new DateTime(2026, 10, 13, 15, 30, 0, DateTimeKind.Utc);
        _log = new()
        {
            new(1, utc, 1, "Aldric", "VezPassada", "Aldric", null, null, null, null),
            new(2, utc, 1, "Aldric", "CampoAlterado", "Goblin", "PV", "7", "3", null),
            new(3, utc, 1, "Goblin", "VezPassada", "Goblin", null, null, null, null),
        };
        var cut = await RenderLoadedAsync();

        var hora = TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).ToString("HH:mm");
        cut.FindAll("h6").Select(h => h.TextContent.Trim()).Should().Contain("Turno 1");
        cut.FindAll("h6").Select(h => h.TextContent.Trim()).Should().Contain("Log do encontro");
        cut.Markup.Should().Contain("Vez de Aldric").And.Contain("Goblin — PV: 7 → 3");
        cut.FindAll(".encontro-log-linha").Should().HaveCount(3);
        cut.FindAll(".encontro-log-linha")[1].TextContent.Should().Contain(hora).And.Contain("Goblin — PV: 7 → 3");
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

    [Fact]
    public async Task A_ParticipantsChanged_message_reloads_the_log()
    {
        var cut = await RenderLoadedAsync();
        _log = new() { new(1, DateTime.UtcNow, 2, "Goblin", "VezPassada", "Goblin", null, null, null, null) };

        await cut.InvokeAsync(() => EncounterConnection.RaiseChangedAsync());

        cut.FindAll(".encontro-log-linha").Should().HaveCount(1);
        EncounterConnection.JoinedEncounterId.Should().Be("e1");
    }
}
