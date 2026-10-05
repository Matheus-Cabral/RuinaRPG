using System.Net;
using System.Net.Http.Json;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Items;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class PenalidadesAtivasAlertTests : MudBunitContext
{
    private void Serve(List<PenalidadeAtivaResponse> ativas, List<string>? requests = null) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            requests?.Add(request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ativas) };
        }));

    [Fact]
    public async Task Renders_nothing_when_no_penalty_is_active()
    {
        Serve([]);
        var cut = Render<PenalidadesAtivasAlert>(p => p.Add(x => x.Url, "character-sheets/1/equipment-penalties"));
        await Task.Delay(50);

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task Lists_each_item_with_what_is_missing_and_its_penalty()
    {
        Serve([new("Cota", ["Vigor ≥ 12"], ["Reflexos −10"]), new("Elmo", ["Vigor ≥ 9"], [])]);
        var cut = Render<PenalidadesAtivasAlert>(p => p.Add(x => x.Url, "character-sheets/1/equipment-penalties"));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Penalidades de equipamento ativas")
            .And.Contain("Cota").And.Contain("Vigor ≥ 12").And.Contain("Reflexos −10")
            .And.Contain("Elmo").And.Contain("sem penalidade definida");
    }

    [Fact]
    public async Task An_item_with_free_text_lists_it_after_the_applied_penalty_as_not_applied()
    {
        Serve([new("Cota", ["Vigor ≥ 12"], ["Reflexos −10"], "Barulhenta"), new("Elmo", ["Vigor ≥ 9"], [], "Aperta")]);
        var cut = Render<PenalidadesAtivasAlert>(p => p.Add(x => x.Url, "character-sheets/1/equipment-penalties"));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Reflexos −10; outras penalidades (não aplicadas automaticamente): Barulhenta")
            .And.Contain("sem penalidade definida; outras penalidades (não aplicadas automaticamente): Aperta");
    }

    [Fact]
    public async Task Reloads_when_Versao_changes()
    {
        var requests = new List<string>();
        Serve([], requests);
        var cut = Render<PenalidadesAtivasAlert>(p => p.Add(x => x.Url, "character-sheets/1/equipment-penalties").Add(x => x.Versao, 0));
        await Task.Delay(50);

        cut.Render(p => p.Add(x => x.Versao, 1));
        await Task.Delay(50);

        requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_failed_request_renders_nothing_instead_of_breaking_the_sheet()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var cut = Render<PenalidadesAtivasAlert>(p => p.Add(x => x.Url, "character-sheets/1/equipment-penalties"));
        await Task.Delay(50);

        cut.Markup.Trim().Should().BeEmpty();
    }
}
