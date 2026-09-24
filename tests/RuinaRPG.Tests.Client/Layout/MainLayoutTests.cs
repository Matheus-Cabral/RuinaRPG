using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Layout;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace RuinaRPG.Tests.Client.Layout;

public class MainLayoutTests : MudBunitContext
{
    private const string BodyMarkup = "<div id=\"body-marker\">BODY</div>";

    private void RegisterCommonServices(HttpClient http)
    {
        // NavMenu (rendered unconditionally inside MainLayout's drawer) needs its own dependency
        // graph, same as NavMenuTests. Unauthorized keeps it on the simple "not logged in" nav
        // links, so it never itself calls auth/me (RulesAuditorNavLinks is nested behind
        // AuthorizeView Roles="GM") — this test only cares about MainLayout's own gating.
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationService>(new BunitAuthorizationService(AuthorizationState.Unauthorized));
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        Services.AddScoped(_ => http);
    }

    private IRenderedComponent<CascadingAuthenticationState> RenderLayout() =>
        Render<CascadingAuthenticationState>(p => p
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<MainLayout>(0);
                builder.AddAttribute(1, nameof(MainLayout.Body), (RenderFragment)(b => b.AddMarkupContent(0, BodyMarkup)));
                builder.CloseComponent();
            }));

    [Fact]
    public void Renders_Body_when_auth_me_reports_MustChangePassword_false()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, false)),
        });
        RegisterCommonServices(http);

        var cut = RenderLayout();

        cut.Markup.Should().Contain("body-marker");
    }

    // Item 4 of the "ajustes-ui-historico" UI-tweaks brief: cap the desktop page body at a centered
    // 1280px (MudBlazor's MaxWidth.Large) so it isn't full-bleed wide on large monitors — smaller
    // screens are unaffected since MudContainer only ever narrows, never widens, past its MaxWidth.
    //
    // Fix round 1: Landing ("/") has a deliberately full-bleed hero (Landing.razor.css ~line 21,
    // referenced from Login.razor.css ~line 4 too) — it's the one route excluded from the
    // MudContainer. Every non-root route still gets MaxWidth.Large, so these two facts now
    // navigate to a non-root path first; the Landing-specific behavior gets its own facts below.
    [Fact]
    public void Wraps_Body_in_a_MudContainer_with_MaxWidth_Large_on_a_non_Landing_route()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, false)),
        });
        RegisterCommonServices(http);
        Services.GetRequiredService<NavigationManager>().NavigateTo("painel");

        var cut = RenderLayout();

        var container = cut.Find(".mud-container");
        container.ClassList.Should().Contain("mud-container-maxwidth-lg");
        container.TextContent.Should().Contain("BODY");
    }

    [Fact]
    public void Wraps_the_forced_password_form_in_the_same_MudContainer_on_a_non_Landing_route()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, true)),
        });
        RegisterCommonServices(http);
        Services.GetRequiredService<NavigationManager>().NavigateTo("painel");

        var cut = RenderLayout();

        var container = cut.Find(".mud-container");
        container.ClassList.Should().Contain("mud-container-maxwidth-lg");
        container.TextContent.Should().Contain("Defina uma nova senha");
    }

    [Fact]
    public void Does_not_wrap_Body_in_a_MudContainer_on_the_Landing_route()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, false)),
        });
        RegisterCommonServices(http);
        Services.GetRequiredService<NavigationManager>().NavigateTo("");

        var cut = RenderLayout();

        cut.FindAll(".mud-container").Should().BeEmpty();
        cut.Markup.Should().Contain("body-marker");
    }

    [Fact]
    public void Does_not_wrap_the_forced_password_form_in_a_MudContainer_on_the_Landing_route()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, true)),
        });
        RegisterCommonServices(http);
        Services.GetRequiredService<NavigationManager>().NavigateTo("");

        var cut = RenderLayout();

        cut.FindAll(".mud-container").Should().BeEmpty();
        cut.Markup.Should().Contain("Defina uma nova senha");
    }

    [Fact]
    public async Task The_MudContainer_exemption_updates_on_navigation_without_remounting()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, false)),
        });
        RegisterCommonServices(http);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("");

        var cut = RenderLayout();
        cut.FindAll(".mud-container").Should().BeEmpty();

        await cut.InvokeAsync(() => navigation.NavigateTo("painel"));

        cut.Find(".mud-container").ClassList.Should().Contain("mud-container-maxwidth-lg");

        await cut.InvokeAsync(() => navigation.NavigateTo(""));

        cut.FindAll(".mud-container").Should().BeEmpty();
    }

    [Fact]
    public void Renders_Body_when_the_caller_is_unauthenticated()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        RegisterCommonServices(http);

        var cut = RenderLayout();

        cut.Markup.Should().Contain("body-marker");
    }

    [Fact]
    public void Renders_the_forced_password_form_instead_of_Body_when_MustChangePassword_is_true()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, true)),
        });
        RegisterCommonServices(http);

        var cut = RenderLayout();

        cut.Markup.Should().NotContain("body-marker");
        cut.Markup.Should().Contain("Defina uma nova senha");
    }

    [Fact]
    public void Completing_the_forced_form_reveals_Body()
    {
        var http = FakeHttpMessageHandler.CreateClient(req => req.RequestUri!.ToString().Contains("change-password")
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, true)) });
        RegisterCommonServices(http);

        var cut = RenderLayout();
        cut.Markup.Should().NotContain("body-marker");

        cut.Find("button:contains('Salvar nova senha')").Click();

        cut.Markup.Should().Contain("body-marker");
        cut.Markup.Should().NotContain("Defina uma nova senha");
    }
}
