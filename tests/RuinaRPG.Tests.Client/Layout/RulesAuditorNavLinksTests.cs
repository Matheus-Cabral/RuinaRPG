using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Layout;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Tests.Client.Shared;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace RuinaRPG.Tests.Client.Layout;

/// <summary>
/// Item 2 of the "ajustes-ui-historico" UI-tweaks brief: the 6 auditor-only nav links move inside
/// a collapsible MudNavGroup titled "Auditoria", drop their "Auditoria: " prefix, and the group
/// auto-expands whenever the current URL is under auditoria/ — including on navigation while the
/// component stays mounted, since it lives in MainLayout's drawer for the whole session.
/// </summary>
public class RulesAuditorNavLinksTests : MudBunitContext
{
    private static readonly (string Text, string Href)[] ExpectedLinks =
    {
        ("Livro de Regras", "auditoria/livro-de-regras"),
        ("Características", "auditoria/caracteristicas"),
        ("Características de Criatura", "auditoria/caracteristicas-de-criatura"),
        ("Efeitos", "auditoria/efeitos"),
        ("Históricos", "auditoria/historicos"),
        ("Equipagem", "auditoria/equipagem"),
    };

    private static HttpClient AuditorHttp() => FakeHttpMessageHandler.CreateClient(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MeResponse("u1", "Auditor1", "GM", true, null, false)) });

    private static HttpClient NonAuditorHttp() => FakeHttpMessageHandler.CreateClient(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, false)) });

    // MudBlazor's analyzer (MUD0012) disallows reading a MudNavGroup's [Parameter] Expanded
    // directly from outside the component, and GetState<T> isn't public — so this asserts through
    // the group's own rendered toggle button, which carries its expanded/collapsed state as
    // aria-expanded (standard ARIA disclosure-widget markup MudNavGroup already emits).
    private static bool IsExpanded(IRenderedComponent<RulesAuditorNavLinks> cut) =>
        cut.Find("button[aria-label='Toggle Auditoria']").GetAttribute("aria-expanded") == "true";

    [Fact]
    public async Task Renders_nothing_for_a_non_rules_auditor()
    {
        Services.AddScoped(_ => NonAuditorHttp());

        var cut = Render<RulesAuditorNavLinks>();
        await Task.Delay(50);

        cut.Markup.Should().BeEmpty();
    }

    [Fact]
    public async Task Renders_a_MudNavGroup_titled_Auditoria_with_the_6_links_and_new_texts_in_order()
    {
        Services.AddScoped(_ => AuditorHttp());

        var cut = Render<RulesAuditorNavLinks>();
        await Task.Delay(50);

        var group = cut.FindComponent<MudNavGroup>();
        group.Instance.Title.Should().Be("Auditoria");

        var anchors = cut.FindAll("a");
        anchors.Select(a => a.TextContent.Trim()).Should().BeEquivalentTo(
            ExpectedLinks.Select(l => l.Text), options => options.WithStrictOrdering());
        anchors.Select(a => a.GetAttribute("href")).Should().BeEquivalentTo(
            ExpectedLinks.Select(l => l.Href), options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Group_is_collapsed_when_the_current_URL_is_not_under_auditoria()
    {
        Services.AddScoped(_ => AuditorHttp());
        Services.GetRequiredService<NavigationManager>().NavigateTo("painel");

        var cut = Render<RulesAuditorNavLinks>();
        await Task.Delay(50);

        IsExpanded(cut).Should().BeFalse();
    }

    [Fact]
    public async Task Group_is_expanded_when_the_current_URL_starts_under_auditoria()
    {
        Services.AddScoped(_ => AuditorHttp());
        Services.GetRequiredService<NavigationManager>().NavigateTo("auditoria/equipagem");

        var cut = Render<RulesAuditorNavLinks>();
        await Task.Delay(50);

        IsExpanded(cut).Should().BeTrue();
    }

    [Fact]
    public async Task Group_expands_on_a_later_navigation_into_auditoria_since_the_component_persists_in_the_drawer()
    {
        Services.AddScoped(_ => AuditorHttp());
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("painel");

        var cut = Render<RulesAuditorNavLinks>();
        await Task.Delay(50);
        IsExpanded(cut).Should().BeFalse();

        await cut.InvokeAsync(() => navigation.NavigateTo("auditoria/historicos"));
        await Task.Delay(50);

        IsExpanded(cut).Should().BeTrue();
    }

    [Fact]
    public async Task Group_collapses_again_on_a_later_navigation_away_from_auditoria()
    {
        Services.AddScoped(_ => AuditorHttp());
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("auditoria/efeitos");

        var cut = Render<RulesAuditorNavLinks>();
        await Task.Delay(50);
        IsExpanded(cut).Should().BeTrue();

        await cut.InvokeAsync(() => navigation.NavigateTo("painel"));
        await Task.Delay(50);

        IsExpanded(cut).Should().BeFalse();
    }
}
