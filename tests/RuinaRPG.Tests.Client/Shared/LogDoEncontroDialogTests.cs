using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Encounters;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class LogDoEncontroDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();
    private static long _seq;

    private static EncounterLogEntryResponse Linha(DateTime utc, int turno = 1, string vez = "Aldric", string tipo = "VezPassada",
        string? campo = null, string? antes = null, string? depois = null) =>
        new(++_seq, DateTime.SpecifyKind(utc, DateTimeKind.Utc), turno, vez, tipo, vez, campo, antes, depois, null, "p-" + vez);

    private void RegisterHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(request.RequestUri!.PathAndQuery);
            return respond(request);
        }));

    private static HttpResponseMessage Json(List<EncounterLogEntryResponse> itens) => new(HttpStatusCode.OK) { Content = JsonContent.Create(itens) };

    private async Task<IRenderedComponent<MudDialogProvider>> AbrirAsync()
    {
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        await cut.InvokeAsync(async () => await dialogs.ShowAsync<LogDoEncontroDialog>(null,
            new DialogParameters<LogDoEncontroDialog> { { x => x.EncounterId, "e1" }, { x => x.NomeDoEncontro, "Emboscada" } }));
        return cut;
    }

    private static DateTime LocalParaUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local);

    [Fact]
    public async Task On_open_loads_the_log_and_groups_by_day_then_Turno_then_vez()
    {
        RegisterHttp(_ => Json(new()
        {
            Linha(LocalParaUtc(new DateTime(2026, 10, 12, 10, 0, 0)), turno: 1, vez: "Aldric"),
            Linha(LocalParaUtc(new DateTime(2026, 10, 13, 10, 0, 0)), turno: 2, vez: "Goblin"),
            Linha(LocalParaUtc(new DateTime(2026, 10, 13, 10, 5, 0)), turno: 2, vez: "Goblin", tipo: "CampoAlterado", campo: "PV", antes: "7", depois: "3"),
        }));

        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Goblin — PV: 7 → 3"));
        _requests.Should().ContainSingle().Which.Should().Be("/api/encounters/e1/log");
        cut.Markup.Should().Contain("Log do encontro — Emboscada");
        var dias = cut.FindAll(".mud-typography-subtitle1").Select(h => h.TextContent.Trim()).Where(t => t.Contains("/2026")).ToList();
        dias.Should().Equal("terça-feira, 13/10/2026", "segunda-feira, 12/10/2026");
        cut.FindAll("h6").Select(h => h.TextContent.Trim()).Should().Contain(new[] { "Turno 1", "Turno 2" });
        cut.Markup.Should().Contain("Vez de Goblin");
        cut.FindAll(".encontro-log-linha")[0].TextContent.Should().Contain("10:00");
    }

    [Fact]
    public async Task The_period_filter_re_filters_the_loaded_lines_without_another_request()
    {
        RegisterHttp(_ => Json(new()
        {
            Linha(LocalParaUtc(new DateTime(2026, 10, 12, 10, 0, 0)), tipo: "CondicaoAdicionada"),
            Linha(LocalParaUtc(new DateTime(2026, 10, 13, 10, 0, 0)), tipo: "Iniciado"),
        }));
        var cut = await AbrirAsync();
        cut.WaitForAssertion(() => cut.FindAll(".encontro-log-linha").Should().HaveCount(2));

        var picker = cut.FindComponent<MudDateRangePicker>();
        await cut.InvokeAsync(() => picker.Instance.DateRangeChanged.InvokeAsync(new DateRange(new DateTime(2026, 10, 13), new DateTime(2026, 10, 13))));

        cut.WaitForAssertion(() => cut.FindAll(".encontro-log-linha").Should().ContainSingle());
        cut.Markup.Should().Contain("Encontro iniciado");
        _requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Each_checkbox_hides_only_its_resource_lines_and_never_the_event_lines()
    {
        var t = LocalParaUtc(new DateTime(2026, 10, 13, 10, 0, 0));
        RegisterHttp(_ => Json(new()
        {
            Linha(t, tipo: "CampoAlterado", campo: "PV", antes: "9", depois: "8"),
            Linha(t, tipo: "RecursoDaFicha", campo: "Estresse", antes: "1", depois: "2"),
            Linha(t, tipo: "CampoAlterado", campo: "Iniciativa", antes: "5", depois: "6"),
            Linha(t, tipo: "CondicaoAdicionada"),
        }));
        var cut = await AbrirAsync();
        cut.WaitForAssertion(() => cut.FindAll(".encontro-log-linha").Should().HaveCount(4));

        cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Vitalidade, Foco e Adrenalina").Find("input").Change(false);
        cut.WaitForAssertion(() => cut.FindAll(".encontro-log-linha").Should().HaveCount(3));
        cut.Markup.Should().NotContain("PV: 9 → 8").And.Contain("Estresse: 1 → 2").And.Contain("Iniciativa: 5 → 6").And.Contain("recebeu a condição");

        cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Estresse").Find("input").Change(false);
        cut.WaitForAssertion(() => cut.FindAll(".encontro-log-linha").Should().HaveCount(2));
        cut.Markup.Should().NotContain("Estresse: 1 → 2").And.Contain("Iniciativa: 5 → 6").And.Contain("recebeu a condição");

        cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Estresse").Find("input").Change(true);
        cut.WaitForAssertion(() => cut.FindAll(".encontro-log-linha").Should().HaveCount(3));
        _requests.Should().ContainSingle();
    }

    [Fact]
    public async Task An_empty_log_shows_the_empty_message()
    {
        RegisterHttp(_ => Json(new()));

        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Nenhum evento registrado neste período."));
    }

    [Fact]
    public async Task A_failed_load_shows_the_error_and_no_empty_message()
    {
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Não foi possível carregar o log do encontro."));
        cut.Markup.Should().NotContain("Nenhum evento registrado");
    }
}
