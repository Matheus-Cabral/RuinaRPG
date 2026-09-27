using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class BancoDeMagiasTests : MudBunitContext
{
    private readonly List<string> _queries = new();

    private IRenderedComponent<BancoDeMagias> RenderPage(params SpellAbilityEntryResponse[] entries)
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _queries.Add(request.RequestUri!.Query);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(entries) };
        }));
        return Render<BancoDeMagias>();
    }

    private static SpellAbilityEntryResponse Entrada(string nome, bool deCriatura) =>
        new(Guid.NewGuid().ToString(), nome, "Habilidade", 1, 0, 0, "", [], deCriatura);

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task The_Criatura_filter_is_sent_to_the_server(string valor)
    {
        var cut = RenderPage();
        await Task.Delay(50);

        var filtro = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Criatura");
        await cut.InvokeAsync(() => filtro.Instance.ValueChanged.InvokeAsync(valor));

        _queries.Last().Should().Contain($"deCriatura={valor}");
    }

    [Fact]
    public async Task With_all_entries_selected_no_Criatura_filter_is_sent()
    {
        RenderPage();
        await Task.Delay(50);

        _queries.Single().Should().NotContain("deCriatura");
    }

    [Fact]
    public async Task Creature_entries_are_tagged_in_the_list()
    {
        var cut = RenderPage(Entrada("Garras", deCriatura: true), Entrada("Bola de Fogo", deCriatura: false));
        await Task.Delay(50);

        var chips = cut.FindComponents<MudChip<string>>();
        chips.Should().ContainSingle().Which.Instance.Text.Should().Be("Criatura");
        cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Garras")).TextContent.Should().Contain("Criatura");
    }
}
