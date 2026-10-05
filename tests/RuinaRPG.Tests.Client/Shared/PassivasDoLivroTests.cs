using Bunit;
using Bunit.Rendering;
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

    private static PassivaDoLivroResponse PassivaDe(string nome, string categoria, string? vocacao, string? classe = null) =>
        new(Guid.NewGuid().ToString(), nome, categoria, $"Desc {nome}", [], vocacao, classe);

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
        cut.FindComponents<MudSelect<string>>().Should().NotContain(c => c.Instance.Label == "Campanha");
        var texto = cut.Markup;
        texto.IndexOf("Passivas Livres").Should().BeLessThan(texto.IndexOf("Passivas Vocacionais"));
        texto.IndexOf("Passivas Vocacionais").Should().BeLessThan(texto.IndexOf("Passivas de Classe"));
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
        cut.FindComponents<MudSelect<string>>().Should().NotContain(c => c.Instance.Label == "Campanha");
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
        cut.Markup.Should().NotContain("Passivas Livres");
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

    private IRenderedComponent<ContainerFragment> RenderGm(params PassivaDoLivroResponse[] passivas)
    {
        Serve([], _ => passivas.ToList());
        return RenderWithPopover<PassivasDoLivro>((nameof(PassivasDoLivro.IsGm), true));
    }

    [Fact]
    public async Task Each_group_title_shows_how_many_passivas_it_has()
    {
        var root = RenderGm(Passiva("A", "Livre"), Passiva("B", "Livre"), Passiva("C", "Vocacional"));
        await Task.Delay(50);

        root.Markup.Should().Contain("Passivas Livres (2)").And.Contain("Passivas Vocacionais (1)").And.NotContain("Passivas de Classe");
    }

    [Fact]
    public async Task The_three_filters_are_offered_with_Todas_first_and_the_values_present()
    {
        var root = RenderGm(PassivaDe("A", "Livre", "Campeão", "Duelista"), PassivaDe("B", "Vocacional", "Caçador"));
        await Task.Delay(50);

        OpenSelect(root, "Categoria").Should().Equal("Todas", "Passiva Livre", "Passiva Vocacional", "Passiva de Classe");
        // Os popovers abertos antes continuam no markup; cada seletor acrescenta suas opções ao fim.
        OpenSelect(root, "Vocação").TakeLast(3).Should().Equal("Todas", "Caçador", "Campeão");
        OpenSelect(root, "Classe").TakeLast(2).Should().Equal("Todas", "Duelista");
    }

    [Fact]
    public async Task Choosing_a_category_leaves_only_that_group()
    {
        var root = RenderGm(Passiva("Ardor", "Livre"), Passiva("Brio", "Vocacional"));
        await Task.Delay(50);

        OpenSelect(root, "Categoria");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Passiva Vocacional").Click();
        await Task.Delay(50);

        root.Markup.Should().Contain("Passivas Vocacionais (1)").And.Contain("Brio");
        root.Markup.Should().NotContain("Passivas Livres").And.NotContain("Ardor");
    }

    [Fact]
    public async Task Each_passiva_is_a_collapsed_panel_whose_header_shows_name_and_requirements()
    {
        var root = RenderGm(Passiva("Ardor", "Livre", "Nível 10", "Força ≥ 4"), Passiva("Brio", "Livre"));
        await Task.Delay(50);

        var paineis = root.FindAll(".mud-expand-panel");
        paineis.Should().HaveCount(2);
        paineis.Should().OnlyContain(p => !p.ClassList.Contains("mud-panel-expanded"));
        var cabecalhos = root.FindAll(".mud-expand-panel-header").Select(h => h.TextContent).ToList();
        cabecalhos[0].Should().Contain("Ardor").And.Contain("Nível 10, Força ≥ 4");
        cabecalhos[1].Should().Contain("Brio").And.Contain("Sem requisitos");
    }

    [Fact]
    public async Task Filters_that_match_nothing_show_their_own_message_distinct_from_the_empty_bank()
    {
        var root = RenderGm(Passiva("Ardor", "Livre"));
        await Task.Delay(50);

        root.FindComponent<MudTextField<string>>().Find("input").Input("zzz");

        root.Markup.Should().Contain("Nenhuma Habilidade Passiva corresponde aos filtros.")
            .And.NotContain("Nenhuma Habilidade Passiva disponível.");
    }

    [Fact]
    public async Task Switching_campaign_keeps_the_filters_but_drops_a_vocacao_that_no_longer_exists()
    {
        Serve([Campanha("c1", "Campanha Um"), Campanha("c2", "Campanha Dois")],
            query => query.Contains("c2")
                ? [PassivaDe("Dois A", "Livre", "Caçador"), PassivaDe("Dois B", "Vocacional", "Caçador")]
                : [PassivaDe("Um A", "Livre", "Campeão"), PassivaDe("Um B", "Vocacional", "Campeão")]);
        var root = RenderWithPopover<PassivasDoLivro>((nameof(PassivasDoLivro.IsGm), false));
        await Task.Delay(50);

        OpenSelect(root, "Categoria");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Passiva Livre").Click();
        await Task.Delay(50);
        OpenSelect(root, "Vocação");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Campeão").Click();
        await Task.Delay(50);
        root.Markup.Should().Contain("Um A").And.NotContain("Um B");

        OpenSelect(root, "Campanha");
        root.FindAll(".mud-list-item").Single(li => li.TextContent.Trim() == "Campanha Dois").Click();
        await Task.Delay(50);

        // Categoria continua em Livre; a Vocação "Campeão" não existe mais e foi descartada.
        root.Markup.Should().Contain("Dois A").And.NotContain("Dois B").And.NotContain("corresponde aos filtros");
    }
}
