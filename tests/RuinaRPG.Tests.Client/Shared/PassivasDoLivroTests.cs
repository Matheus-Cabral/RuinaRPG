using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Rules;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class PassivasDoLivroTests : MudBunitContext
{
    private readonly List<string> _requests = new();

    private static PassivaDoLivroResponse Passiva(string nome, string categoria, params string[] requisitos) =>
        new(Guid.NewGuid().ToString(), nome, categoria, $"Desc {nome}", requisitos.ToList());

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>campaigns/mine devolve <paramref name="campanhas"/>; rulebook/passivas devolve o que <paramref name="passivas"/> der para a query string.</summary>
    private void Serve(List<CampaignResponse> campanhas, Func<string, List<PassivaDoLivroResponse>> passivas) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(request.RequestUri!.PathAndQuery);
            return request.RequestUri.AbsolutePath.EndsWith("campaigns/mine") ? Json(campanhas) : Json(passivas(request.RequestUri.Query));
        }));

    private static CampaignResponse Campanha(string id, string nome) => new(id, nome, "", null);

    [Fact]
    public async Task Gm_sees_the_bank_grouped_by_category_with_requisitos_and_no_campaign_selector()
    {
        Serve([], _ => [Passiva("Zelo", "DeClasse"), Passiva("Ardor", "Livre", "Nível 10", "Força ≥ 4"), Passiva("Brio", "Vocacional")]);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);

        _requests.Should().Equal("/api/rulebook/passivas");
        cut.FindComponents<MudSelect<string>>().Should().BeEmpty();
        var texto = cut.Markup;
        texto.IndexOf("Passiva Livre").Should().BeLessThan(texto.IndexOf("Passiva Vocacional"));
        texto.IndexOf("Passiva Vocacional").Should().BeLessThan(texto.IndexOf("Passiva de Classe"));
        texto.Should().Contain("Ardor").And.Contain("Desc Ardor").And.Contain("Nível 10, Força ≥ 4");
        texto.Should().Contain("Sem requisitos");
    }

    [Fact]
    public async Task Jogador_with_one_campaign_loads_it_directly_without_a_selector()
    {
        Serve([Campanha("c1", "Campanha Um")], _ => [Passiva("Ardor", "Livre")]);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, false));
        await Task.Delay(50);

        _requests.Should().Equal("/api/campaigns/mine", "/api/rulebook/passivas?campaignId=c1");
        cut.FindComponents<MudSelect<string>>().Should().BeEmpty();
        cut.Markup.Should().Contain("Ardor");
    }

    [Fact]
    public async Task Jogador_with_several_campaigns_gets_a_selector_that_switches_the_list()
    {
        Serve([Campanha("c1", "Campanha Um"), Campanha("c2", "Campanha Dois")],
            query => query.Contains("c2") ? [Passiva("Da Dois", "Livre")] : [Passiva("Da Um", "Livre")]);

        var root = RenderWithPopover<PassivasDoLivro>((nameof(PassivasDoLivro.IsGm), false));
        await Task.Delay(50);

        root.Markup.Should().Contain("Da Um").And.NotContain("Da Dois");
        OpenSelect(root, "Campanha").Should().Equal("Campanha Um", "Campanha Dois");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Campanha Dois").Click();
        await Task.Delay(50);

        root.Markup.Should().Contain("Da Dois").And.NotContain("Da Um");
        _requests.Should().Contain("/api/rulebook/passivas?campaignId=c2");
    }

    [Fact]
    public async Task Jogador_without_campaigns_sees_the_empty_state_and_requests_no_passivas()
    {
        Serve([], _ => []);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, false));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Você ainda não participa de nenhuma campanha.");
        _requests.Should().Equal("/api/campaigns/mine");
    }

    [Fact]
    public async Task Empty_list_shows_the_empty_state()
    {
        Serve([], _ => []);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Nenhuma Habilidade Passiva disponível.");
    }

    [Fact]
    public async Task Search_filters_by_name_ignoring_case_and_hides_empty_groups()
    {
        Serve([], _ => [Passiva("Ardor", "Livre"), Passiva("Pele de Pedra", "Vocacional")]);

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);
        cut.FindComponent<MudTextField<string>>().Find("input").Input("PELE");

        cut.Markup.Should().Contain("Pele de Pedra").And.NotContain("Ardor");
        cut.Markup.Should().NotContain("Passiva Livre");
    }

    [Fact]
    public async Task A_failed_load_shows_an_error_message()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, true));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Não foi possível carregar as Habilidades Passivas.");
        cut.Markup.Should().NotContain("Nenhuma Habilidade Passiva disponível.")
            .And.NotContain("Você ainda não participa de nenhuma campanha.");
    }

    [Fact]
    public async Task Jogador_whose_campaigns_request_fails_sees_the_error_and_no_empty_state()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var cut = Render<PassivasDoLivro>(p => p.Add(x => x.IsGm, false));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Não foi possível carregar as Habilidades Passivas.");
        cut.Markup.Should().NotContain("Nenhuma Habilidade Passiva disponível.")
            .And.NotContain("Você ainda não participa de nenhuma campanha.");
    }

    [Fact]
    public async Task A_failed_campaign_switch_drops_the_previous_list_and_a_later_success_shows_the_new_one()
    {
        var falhar = true;
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("campaigns/mine"))
                return Json(new List<CampaignResponse> { Campanha("c1", "Campanha Um"), Campanha("c2", "Campanha Dois") });
            if (request.RequestUri.Query.Contains("c2"))
                return falhar ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Json(new List<PassivaDoLivroResponse> { Passiva("Da Dois", "Livre") });
            return Json(new List<PassivaDoLivroResponse> { Passiva("Da Um", "Livre") });
        }));

        var root = RenderWithPopover<PassivasDoLivro>((nameof(PassivasDoLivro.IsGm), false));
        await Task.Delay(50);
        root.Markup.Should().Contain("Da Um");

        OpenSelect(root, "Campanha");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Campanha Dois").Click();
        await Task.Delay(50);

        root.Markup.Should().Contain("Não foi possível carregar as Habilidades Passivas.");
        root.Markup.Should().NotContain("Da Um")
            .And.NotContain("Nenhuma Habilidade Passiva disponível.")
            .And.NotContain("Você ainda não participa de nenhuma campanha.");

        falhar = false;
        OpenSelect(root, "Campanha");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Campanha Um").Click();
        await Task.Delay(50);
        OpenSelect(root, "Campanha");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Campanha Dois").Click();
        await Task.Delay(50);

        root.Markup.Should().Contain("Da Dois");
    }
}
