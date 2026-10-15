using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.SheetLogs;
using System.Net;
using System.Net.Http.Json;
using System.Web;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class LogDaFichaDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();

    private static SheetLogEntryResponse Linha(string id, DateTime utc, string ator = "Ana", bool gm = false, string campo = "Vitalidade Atual") =>
        new(id, DateTime.SpecifyKind(utc, DateTimeKind.Utc), ator, gm, "Recursos", "Alterou", "Informações Básicas", campo, null, "30", "25");

    private static HttpResponseMessage Pagina(List<SheetLogEntryResponse> itens, string? cursor = null) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new SheetLogPageResponse(itens, cursor)) };

    private void RegisterHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(request.RequestUri!.PathAndQuery);
            return respond(request);
        }));

    private async Task<IRenderedComponent<MudDialogProvider>> AbrirAsync()
    {
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        await cut.InvokeAsync(async () => await dialogs.ShowAsync<LogDaFichaDialog>(null,
            new DialogParameters<LogDaFichaDialog> { { x => x.Kind, "character" }, { x => x.SheetId, "s1" }, { x => x.NomeDaFicha, "Aria" } }));
        return cut;
    }

    private static NameValueCollection_ Query(string pathAndQuery) => new(HttpUtility.ParseQueryString(pathAndQuery[(pathAndQuery.IndexOf('?') + 1)..]));

    private sealed class NameValueCollection_
    {
        private readonly System.Collections.Specialized.NameValueCollection _q;
        public NameValueCollection_(System.Collections.Specialized.NameValueCollection q) => _q = q;
        public string? this[string key] => _q[key];
    }

    [Fact]
    public async Task On_open_requests_the_first_page_with_both_filters_on_and_renders_days_lines_and_gm_chip()
    {
        RegisterHttp(_ => Pagina(new()
        {
            Linha("2", new DateTime(2026, 10, 12, 15, 0, 0), "Mestre", gm: true, campo: "Foco Atual"),
            Linha("1", new DateTime(2026, 10, 10, 15, 0, 0)),
        }));

        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Foco Atual: 30 → 25"));
        _requests.Should().ContainSingle();
        _requests[0].Should().StartWith("/api/sheet-logs/character/s1?").And.Contain("recursos=true").And.Contain("estresse=true")
            .And.NotContain("de=").And.NotContain("ate=").And.NotContain("antesDe=");
        cut.Markup.Should().Contain("Log da ficha — Aria");
        cut.FindAll(".mud-typography-subtitle1").Count(h => h.TextContent.Contains("/2026")).Should().Be(2);
        cut.Markup.Should().Contain("Vitalidade Atual: 30 → 25");
        cut.FindAll(".mud-chip").Count(c => c.TextContent.Trim() == "GM").Should().Be(1);
    }

    [Fact]
    public async Task An_automatic_line_shows_the_automatico_chip_next_to_the_actor_and_the_others_do_not()
    {
        RegisterHttp(_ => Pagina(new()
        {
            Linha("2", new DateTime(2026, 10, 12, 15, 0, 0), "Mestre", gm: true) with { Automatico = true },
            Linha("1", new DateTime(2026, 10, 12, 14, 0, 0), "Mestre", gm: true, campo: "Foco Atual"),
        }));

        var cut = await AbrirAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Foco Atual: 30 → 25"));
        cut.FindAll(".mud-chip").Count(c => c.TextContent.Trim() == "automático").Should().Be(1);
        var linhaAutomatica = cut.FindAll(".mud-chip").Single(c => c.TextContent.Trim() == "automático").Closest("div.d-flex")!;
        linhaAutomatica.TextContent.Should().Contain("Mestre").And.Contain("Vitalidade Atual: 30 → 25");
    }

    [Fact]
    public async Task Unchecking_the_filters_reloads_the_list_without_those_lines()
    {
        RegisterHttp(r => r.RequestUri!.Query.Contains("recursos=false")
            ? Pagina(new() { Linha("9", new DateTime(2026, 10, 12, 15, 0, 0), campo: "Raça") })
            : Pagina(new() { Linha("1", new DateTime(2026, 10, 12, 15, 0, 0)) }));
        var cut = await AbrirAsync();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Vitalidade Atual"));

        cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Vitalidade, Foco e Adrenalina").Find("input").Change(false);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Raça: 30 → 25"));
        cut.Markup.Should().NotContain("Vitalidade Atual");
        Query(_requests.Last())["recursos"].Should().Be("false");
        Query(_requests.Last())["estresse"].Should().Be("true");

        cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Estresse").Find("input").Change(false);

        cut.WaitForAssertion(() => Query(_requests.Last())["estresse"].Should().Be("false"));
    }

    [Fact]
    public async Task Picking_a_date_range_sends_the_local_day_bounds_as_utc()
    {
        RegisterHttp(_ => Pagina(new()));
        var cut = await AbrirAsync();
        cut.WaitForAssertion(() => _requests.Should().HaveCount(1));
        var primeiro = new DateTime(2026, 10, 5);
        var ultimo = new DateTime(2026, 10, 7);

        var picker = cut.FindComponent<MudDateRangePicker>();
        await cut.InvokeAsync(() => picker.Instance.DateRangeChanged.InvokeAsync(new DateRange(primeiro, ultimo)));

        var esperadoDe = TimeZoneInfo.ConvertTimeToUtc(primeiro, TimeZoneInfo.Local);
        var esperadoAte = TimeZoneInfo.ConvertTimeToUtc(ultimo.AddDays(1), TimeZoneInfo.Local);
        cut.WaitForAssertion(() => _requests.Should().HaveCount(2));
        var consulta = Query(_requests.Last());
        DateTime.Parse(consulta["de"]!, null, System.Globalization.DateTimeStyles.RoundtripKind).Should().Be(esperadoDe);
        DateTime.Parse(consulta["ate"]!, null, System.Globalization.DateTimeStyles.RoundtripKind).Should().Be(esperadoAte);
        consulta["de"].Should().EndWith("Z");
        consulta["ate"].Should().EndWith("Z");
    }

    [Fact]
    public async Task Carregar_mais_appends_the_next_page_and_disappears_on_the_last_one()
    {
        RegisterHttp(r => r.RequestUri!.Query.Contains("antesDe=")
            ? Pagina(new() { Linha("1", new DateTime(2026, 10, 10, 15, 0, 0), campo: "Foco Atual") })
            : Pagina(new() { Linha("2", new DateTime(2026, 10, 12, 15, 0, 0)) }, "cursor/+1="));
        var cut = await AbrirAsync();
        cut.WaitForAssertion(() => cut.Find("button:contains('Carregar mais')"));

        cut.Find("button:contains('Carregar mais')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Foco Atual: 30 → 25"));
        Query(_requests.Last())["antesDe"].Should().Be("cursor/+1=");
        cut.Markup.Should().Contain("Vitalidade Atual: 30 → 25");
        cut.FindAll("button:contains('Carregar mais')").Should().BeEmpty();
    }

    [Fact]
    public async Task An_empty_result_says_so()
    {
        RegisterHttp(_ => Pagina(new()));
        var cut = await AbrirAsync();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Nenhuma alteração registrada neste período."));
        cut.FindAll("button:contains('Carregar mais')").Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_request_shows_an_error_and_no_list()
    {
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var cut = await AbrirAsync();
        cut.WaitForAssertion(() => cut.FindAll(".mud-alert").Should().NotBeEmpty());
        cut.Markup.Should().NotContain("Nenhuma alteração registrada");
        cut.Markup.Should().NotContain("→");
    }
}
