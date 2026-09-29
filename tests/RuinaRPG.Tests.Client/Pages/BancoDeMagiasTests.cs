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

        // Mesmo ícone e cor do Bestiário no menu lateral (NavMenu).
        cut.FindComponents<MudChip<string>>().Should().BeEmpty();
        var icone = cut.FindComponents<MudIcon>().Where(i => i.Instance.Icon == Icons.Material.Filled.Pets).Should().ContainSingle().Subject;
        icone.Instance.Color.Should().Be(Color.Primary);
        icone.Instance.Title.Should().Be("Magia/Habilidade de Criatura");
        cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Garras")).InnerHtml.Should().Contain("Magia/Habilidade de Criatura");
        cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Bola de Fogo")).InnerHtml.Should().NotContain("Magia/Habilidade de Criatura");
    }

    [Fact]
    public async Task The_Tipo_filter_offers_Passiva()
    {
        var cut = RenderPage();
        await Task.Delay(50);

        var filtro = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Tipo");
        await cut.InvokeAsync(() => filtro.Instance.ValueChanged.InvokeAsync("Passiva"));

        _queries.Last().Should().Contain("tipo=Passiva");
    }

    [Fact]
    public async Task A_passiva_row_renders_its_categoria_label_instead_of_grau_and_efeitos()
    {
        var passiva = new SpellAbilityEntryResponse(Guid.NewGuid().ToString(), "Pele de Pedra", "Passiva", 0, 0, 0, "", [], false, "Vocacional");
        var cut = RenderPage(passiva);
        await Task.Delay(50);

        var linha = cut.FindAll("tbody tr").Single(r => r.TextContent.Contains("Pele de Pedra"));
        linha.TextContent.Should().Contain("Passiva Vocacional");
    }
}
