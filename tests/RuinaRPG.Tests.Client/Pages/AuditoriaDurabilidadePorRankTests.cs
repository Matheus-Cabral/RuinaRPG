using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class AuditoriaDurabilidadePorRankTests : MudBunitContext
{
    // Mutable so PUT can mutate it and a following GET reflects the change — mirrors what the
    // real API does (see DurabilidadesPorRankControllerTests).
    private class Row
    {
        public string Rank { get; set; } = "";
        public int? Durabilidade { get; set; }
        public bool Inquebravel { get; set; }
    }

    private static List<Row> SeedRows() => new()
    {
        new() { Rank = "F", Durabilidade = 20, Inquebravel = false },
        new() { Rank = "E", Durabilidade = 45, Inquebravel = false },
        new() { Rank = "D", Durabilidade = 80, Inquebravel = false },
        new() { Rank = "C", Durabilidade = 125, Inquebravel = false },
        new() { Rank = "B", Durabilidade = 180, Inquebravel = false },
        new() { Rank = "A", Durabilidade = 245, Inquebravel = false },
        new() { Rank = "S", Durabilidade = null, Inquebravel = true },
        new() { Rank = "SS", Durabilidade = null, Inquebravel = true },
    };

    // Mirrors the real controller's validation (see DurabilidadesPorRankController.Update): a PUT
    // that isn't Inquebravel and has no Durabilidade >= 1 is rejected with 400 and never mutates
    // the row — needed so the "revert on a failed PUT" behavior can be exercised faithfully.
    private HttpClient CreateStatefulHttp(List<Row> rows, List<(string Rank, UpdateDurabilidadePorRankRequest Body)> putLog)
        => FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("durabilidades-por-rank"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(rows) };

            if (request.Method == HttpMethod.Put && request.RequestUri!.AbsolutePath.Contains("durabilidades-por-rank/"))
            {
                var rank = request.RequestUri!.AbsolutePath.Split('/').Last();
                var body = request.Content!.ReadFromJsonAsync<UpdateDurabilidadePorRankRequest>().GetAwaiter().GetResult()!;
                putLog.Add((rank, body));

                if (!body.Inquebravel && (body.Durabilidade is null || body.Durabilidade < 1))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                        { Content = new StringContent("Informe a durabilidade (mínimo 1) ou marque Inquebrável.") };

                var row = rows.Single(r => r.Rank == rank);
                row.Inquebravel = body.Inquebravel;
                row.Durabilidade = body.Inquebravel ? null : body.Durabilidade;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    // Info popups' inline <MudDialog> only renders its content through a MudDialogProvider present
    // elsewhere in the render tree (the real app has one in MainLayout) — same idiom as
    // ChangelogDialogTests/AuditoriaCaracteristicasTests.
    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider(HttpClient http)
    {
        Services.AddScoped(_ => http);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaDurabilidadePorRank>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task Page_renders_8_rows_labeled_F_through_SS()
    {
        var rows = SeedRows();
        var http = CreateStatefulHttp(rows, new());
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaDurabilidadePorRank>();
        await Task.Delay(50);

        var ranks = cut.FindAll("tbody tr td:first-child").Select(td => td.TextContent.Trim()).ToList();
        ranks.Should().Equal("F", "E", "D", "C", "B", "A", "S", "SS");
    }

    [Fact]
    public async Task Toggling_Inquebravel_on_a_row_disables_its_number_field_and_PUTs_null_true()
    {
        var rows = SeedRows();
        var putLog = new List<(string, UpdateDurabilidadePorRankRequest)>();
        var http = CreateStatefulHttp(rows, putLog);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaDurabilidadePorRank>();
        await Task.Delay(50);

        // Rows render F, E, D, C, B, A, S, SS in order — C is index 3.
        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[3];
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));
        await Task.Delay(50);

        var (rank, body) = putLog.Should().ContainSingle().Subject;
        rank.Should().Be("C");
        body.Durabilidade.Should().BeNull();
        body.Inquebravel.Should().BeTrue();

        var numeric = cut.FindComponents<MudNumericField<int?>>()[3];
        numeric.Instance.Disabled.Should().BeTrue();
    }

    [Fact]
    public async Task Editing_a_number_PUTs_it()
    {
        var rows = SeedRows();
        var putLog = new List<(string, UpdateDurabilidadePorRankRequest)>();
        var http = CreateStatefulHttp(rows, putLog);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaDurabilidadePorRank>();
        await Task.Delay(50);

        var numeric = cut.FindComponents<MudNumericField<int?>>()[3]; // C
        await cut.InvokeAsync(() => numeric.Instance.ValueChanged.InvokeAsync(130));
        await Task.Delay(50);

        var (rank, body) = putLog.Should().ContainSingle().Subject;
        rank.Should().Be("C");
        body.Durabilidade.Should().Be(130);
        body.Inquebravel.Should().BeFalse();
    }

    [Fact]
    public async Task Unchecking_Inquebravel_on_a_row_with_no_number_sends_no_PUT_and_enables_the_field()
    {
        var rows = SeedRows();
        var putLog = new List<(string, UpdateDurabilidadePorRankRequest)>();
        var http = CreateStatefulHttp(rows, putLog);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaDurabilidadePorRank>();
        await Task.Delay(50);

        // Rows render F, E, D, C, B, A, S, SS in order — S (Durabilidade null, Inquebravel true
        // as seeded) is index 6.
        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[6];
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(false));
        await Task.Delay(50);

        putLog.Should().BeEmpty();

        var numeric = cut.FindComponents<MudNumericField<int?>>()[6];
        numeric.Instance.Disabled.Should().BeFalse();
        // MudBlazor's analyzer (MUD0012) disallows reading a component's own [Parameter] state
        // (Value) directly — assert through the rendered <input>'s value attribute instead, same
        // idiom as EstrelaSelectTests.
        cut.FindAll("tbody tr")[6].QuerySelector("input")!.GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Unchecking_Inquebravel_then_typing_a_number_PUTs_it()
    {
        var rows = SeedRows();
        var putLog = new List<(string, UpdateDurabilidadePorRankRequest)>();
        var http = CreateStatefulHttp(rows, putLog);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaDurabilidadePorRank>();
        await Task.Delay(50);

        var checkbox = cut.FindComponents<MudCheckBox<bool>>()[6]; // S
        await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(false));
        await Task.Delay(50);
        putLog.Should().BeEmpty();

        var numeric = cut.FindComponents<MudNumericField<int?>>()[6];
        await cut.InvokeAsync(() => numeric.Instance.ValueChanged.InvokeAsync(300));
        await Task.Delay(50);

        var (rank, body) = putLog.Should().ContainSingle().Subject;
        rank.Should().Be("S");
        body.Durabilidade.Should().Be(300);
        body.Inquebravel.Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_PUT_reverts_the_row_to_the_last_saved_value_and_keeps_the_error_visible()
    {
        var rows = SeedRows();
        var putLog = new List<(string, UpdateDurabilidadePorRankRequest)>();
        var http = CreateStatefulHttp(rows, putLog);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaDurabilidadePorRank>();
        await Task.Delay(50);

        var numeric = cut.FindComponents<MudNumericField<int?>>()[3]; // C, seeded at 125
        await cut.InvokeAsync(() => numeric.Instance.ValueChanged.InvokeAsync(0));
        await Task.Delay(50);

        var (rank, body) = putLog.Should().ContainSingle().Subject;
        rank.Should().Be("C");
        body.Durabilidade.Should().Be(0);

        cut.Markup.Should().Contain("Informe a durabilidade (mínimo 1) ou marque Inquebrável.");

        cut.FindAll("tbody tr")[3].QuerySelector("input")!.GetAttribute("value").Should().Be("125");
    }

    [Fact]
    public async Task Info_popup_has_the_exact_title_and_help_text()
    {
        var rows = SeedRows();
        var http = CreateStatefulHttp(rows, new());

        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        var infoButton = cut.Find("button[title='Como funciona a Durabilidade por Rank']");
        infoButton.GetAttribute("aria-label").Should().Be("Como funciona a Durabilidade por Rank");

        infoButton.Click();

        var content = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        content.Should().Be(TextNormalization.Collapse(
            "A durabilidade máxima de Armas, Armaduras e Escudos vem do Rank do item, conforme esta tabela. Marque Inquebrável para que os itens daquele Rank nunca quebrem — a ficha mostra 'Inquebrável' e não controla a durabilidade atual. Itens sem Rank não têm durabilidade. Ao reduzir um valor, a durabilidade atual das fichas que passar do novo máximo é limitada a ele; ao aumentar, ela não é recarregada."));
    }
}
