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

    private static SpellAbilityEntryResponse Passiva(string nome, string categoria, string? vocacao = null, string? classe = null) =>
        new(Guid.NewGuid().ToString(), nome, "Passiva", 0, 0, 0, "", [], false, categoria, new RequisitosDePassivaDto(Vocacao: vocacao, Classe: classe));

    private static async Task EscolherAsync(IRenderedComponent<BancoDeMagias> cut, string label, string valor)
    {
        var filtro = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == label);
        await cut.InvokeAsync(() => filtro.Instance.ValueChanged.InvokeAsync(valor));
    }

    private static IEnumerable<string> Opcoes(IRenderedComponent<BancoDeMagias> cut, string label) =>
        cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == label)
            .FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value!).Distinct();

    private static IEnumerable<string> NomesListados(IRenderedComponent<BancoDeMagias> cut) =>
        cut.FindAll("tbody tr").Select(r => r.QuerySelector("td")!.TextContent.Trim());

    [Fact]
    public async Task The_Categoria_filter_keeps_only_the_passivas_of_that_categoria()
    {
        var cut = RenderPage(Entrada("Bola de Fogo", deCriatura: false), Passiva("Pele de Pedra", "Vocacional"), Passiva("Olho Vivo", "Livre"));
        await Task.Delay(50);

        await EscolherAsync(cut, "Categoria", "Vocacional");

        NomesListados(cut).Should().Equal("Pele de Pedra");
    }

    [Fact]
    public async Task The_Vocacao_and_Classe_filters_offer_only_the_values_present_in_the_list()
    {
        var cut = RenderPage(Passiva("A", "Vocacional", "Campeao"), Passiva("B", "DeClasse", "Campeao", " Duelista "), Passiva("C", "Livre"));
        await Task.Delay(50);

        Opcoes(cut, "Vocação").Should().Equal("", "Campeão");
        Opcoes(cut, "Classe").Should().Equal("", "Duelista");
    }

    [Fact]
    public async Task The_Vocacao_and_Classe_filters_combine()
    {
        var cut = RenderPage(
            Passiva("Golpe Firme", "Vocacional", "Campeao"), Passiva("Golpe Rápido", "Vocacional", "Cacador"),
            Passiva("Golpe Duplo", "DeClasse", "Campeao", "Duelista"), Entrada("Garras", deCriatura: false));
        await Task.Delay(50);

        await EscolherAsync(cut, "Vocação", "Campeão");
        NomesListados(cut).Should().Equal("Golpe Firme", "Golpe Duplo");

        await EscolherAsync(cut, "Classe", "Duelista");
        NomesListados(cut).Should().Equal("Golpe Duplo");
    }

    [Fact]
    public async Task The_passiva_filters_do_not_hit_the_server()
    {
        var cut = RenderPage(Passiva("A", "Vocacional", "Campeao"));
        await Task.Delay(50);

        await EscolherAsync(cut, "Categoria", "Vocacional");
        await EscolherAsync(cut, "Vocação", "Campeão");

        _queries.Should().ContainSingle();
    }
}
