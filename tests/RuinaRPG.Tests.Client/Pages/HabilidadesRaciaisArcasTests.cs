using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>Dado da Tabela de Arcas na página de Habilidades Raciais (Requisitos - Habilidades Raciais R0002).</summary>
public class HabilidadesRaciaisArcasTests : MudBunitContext
{
    private readonly List<(HttpMethod Method, string Path, int? Dado)> _log = new();
    private int _dadoNoServidor = 20;

    private IRenderedComponent<ContainerFragment> RenderPage()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            int? dado = null;
            if (request.Method == HttpMethod.Put && path.EndsWith("/arcas/dado"))
            {
                dado = request.Content!.ReadFromJsonAsync<ArcaDadoRequest>().GetAwaiter().GetResult()!.Dado;
                _dadoNoServidor = dado.Value;
                _log.Add((request.Method, path, dado));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            _log.Add((request.Method, path, null));
            if (path.EndsWith("/arcas/dado"))
                return Json(new ArcaDadoResponse(_dadoNoServidor));
            if (path.EndsWith("/arcas"))
                return Json(Enumerable.Range(1, _dadoNoServidor).Select(r => new ArcaEntryResponse(r, null, null, new())).ToList());
            if (path.EndsWith("/racial-abilities"))
                return Json(new List<RacialAbilityEntryResponse>());
            return Json(new List<RacialTraitSlotsEntryResponse>());
        }));

        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<HabilidadesRaciais>(2);
            builder.CloseComponent();
        });
    }

    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static IRenderedComponent<MudSelect<int>> DadoSelect(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponents<MudSelect<int>>().Single(c => c.Instance.Label == "Dado da tabela");

    private IEnumerable<int?> PutsDeDado => _log.Where(l => l.Method == HttpMethod.Put).Select(l => l.Dado);

    [Fact]
    public async Task The_select_offers_the_six_dice()
    {
        var cut = RenderPage();
        await Task.Delay(50);

        DadoSelect(cut).Find(".mud-input-control").MouseDown();

        cut.FindAll(".mud-list-item").Select(li => li.TextContent.Trim()).Should().Equal("D6", "D8", "D10", "D12", "D20", "D100");
    }

    [Fact]
    public async Task The_table_has_one_row_per_face_of_the_current_die()
    {
        _dadoNoServidor = 12;
        var cut = RenderPage();
        await Task.Delay(50);

        cut.FindAll("tbody tr").Count.Should().Be(12 * 2); // uma linha da Arca e uma do painel de evoluções
    }

    [Fact]
    public async Task Choosing_a_bigger_die_puts_it_and_reloads_the_table_without_asking()
    {
        var cut = RenderPage();
        await Task.Delay(50);

        await cut.InvokeAsync(() => DadoSelect(cut).Instance.ValueChanged.InvokeAsync(100));
        await Task.Delay(50);

        PutsDeDado.Should().Equal(100);
        cut.FindAll(".mud-dialog").Should().BeEmpty();
        cut.FindAll("tbody tr").Count.Should().Be(100 * 2);
    }

    [Fact]
    public async Task Choosing_a_smaller_die_asks_for_confirmation_and_cancel_sends_nothing()
    {
        var cut = RenderPage();
        await Task.Delay(50);

        _ = cut.InvokeAsync(() => DadoSelect(cut).Instance.ValueChanged.InvokeAsync(6));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Mudar para D6? As Arcas acima de 6 somem da tabela, mas ficam guardadas com suas evoluções e voltam se você escolher um dado maior."));
        PutsDeDado.Should().BeEmpty();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Cancelar").Click();
        await Task.Delay(50);

        PutsDeDado.Should().BeEmpty();
        cut.FindAll("tbody tr").Count.Should().Be(20 * 2);
    }

    [Fact]
    public async Task Confirming_the_smaller_die_puts_it_and_reloads_the_table()
    {
        var cut = RenderPage();
        await Task.Delay(50);

        _ = cut.InvokeAsync(() => DadoSelect(cut).Instance.ValueChanged.InvokeAsync(6));
        cut.WaitForAssertion(() => cut.FindAll("button").Should().Contain(b => b.TextContent.Trim() == "Mudar"));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Mudar").Click();
        await Task.Delay(100);

        PutsDeDado.Should().Equal(6);
        cut.FindAll("tbody tr").Count.Should().Be(6 * 2);
    }

    [Fact]
    public async Task The_evolucoes_editor_is_only_mounted_once_its_panel_is_expanded()
    {
        var cut = RenderPage();
        await Task.Delay(50);

        cut.FindComponents<RuinaRPG.Client.Shared.ArcaEvolucoesEditor>().Should().BeEmpty();

        var painel = cut.FindComponents<MudExpansionPanel>().First();
        await cut.InvokeAsync(() => painel.Instance.ExpandedChanged.InvokeAsync(true));

        cut.FindComponents<RuinaRPG.Client.Shared.ArcaEvolucoesEditor>().Should().HaveCount(1);
    }
}
