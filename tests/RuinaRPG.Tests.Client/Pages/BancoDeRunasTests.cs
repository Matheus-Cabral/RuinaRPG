using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.Runes;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class BancoDeRunasTests : MudBunitContext
{
    private readonly List<string> _queries = new();

    private IRenderedComponent<BancoDeRunas> RenderPage(params RuneBankEntryResponse[] entries)
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _queries.Add(request.RequestUri!.Query);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(entries) };
        }));
        return Render<BancoDeRunas>();
    }

    [Theory]
    [InlineData("Arcana")]
    [InlineData("Negra")]
    [InlineData("Nenhum")]
    public async Task The_Tipo_filter_is_sent_to_the_server(string valor)
    {
        var cut = RenderPage();
        await Task.Delay(50);

        var filtro = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");
        await cut.InvokeAsync(() => filtro.Instance.ValueChanged.InvokeAsync(valor));
        await Task.Delay(400); // past the filter debounce

        _queries.Last().Should().Contain($"tipo={valor}");
    }

    [Fact]
    public async Task With_all_tipos_selected_no_Tipo_filter_is_sent()
    {
        RenderPage();
        await Task.Delay(50);

        _queries.Single().Should().NotContain("tipo");
    }

    [Fact]
    public async Task The_Tipo_filter_offers_the_four_options()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<RuneBankEntryResponse>()) }));
        var cut = RenderWithPopover<BancoDeRunas>();
        await Task.Delay(50);

        OpenSelect(cut, "Tipo").Should().Equal("Todos", "Runa Arcana", "Runa Negra", "Sem tipo");
    }

    [Fact]
    public async Task The_list_shows_a_Tipo_column_with_the_label_of_each_entry()
    {
        var cut = RenderPage(
            new RuneBankEntryResponse("1", "Runa A", "d", 1, Tipo: "Arcana"),
            new RuneBankEntryResponse("2", "Runa N", "d", 1, Tipo: "Negra"),
            new RuneBankEntryResponse("3", "Runa S", "d", 1));
        await Task.Delay(50);

        cut.FindAll("th").Select(h => h.TextContent.Trim()).Should().Contain("Tipo");
        var linhas = cut.FindAll("tbody tr");
        linhas[0].TextContent.Should().Contain("Runa Arcana");
        linhas[1].TextContent.Should().Contain("Runa Negra");
        linhas[2].TextContent.Should().Contain("—");
    }
}
