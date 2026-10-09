using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using RuinaRPG.Client.Layout;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace RuinaRPG.Tests.Client.Layout;

public class MainLayoutTests : MudBunitContext
{
    private const string BodyMarkup = "<div id=\"body-marker\">BODY</div>";
    private const string DrawerSelector = ".mud-drawer";
    private const string HamburgerSelector = "button[aria-label='Abrir menu de navegação']";

    // MudBlazor's real IBrowserViewportService talks to a JS ResizeObserver bUnit's JSInterop
    // (even in Loose mode) can't meaningfully drive — see FakeBrowserViewportService's own remarks.
    // Registering this fake after RegisterCommonServices (so it wins DI resolution over
    // MudBunitContext's AddMudServices()) lets a test declare the viewport up front; MainLayout's
    // own MudBreakpointProvider and the MudDrawer it renders both resolve the same instance.
    private FakeBrowserViewportService UseViewport(Breakpoint breakpoint)
    {
        var viewport = new FakeBrowserViewportService { CurrentBreakpoint = breakpoint };
        Services.AddScoped<IBrowserViewportService>(_ => viewport);
        return viewport;
    }

    private HttpClient DefaultHttp() => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, false)),
    });

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

    // Task brief "desktop mini drawer": at/above Breakpoint.Md the drawer collapses to icons-only
    // and expands on hover (MudDrawer Variant="Mini" OpenMiniOnHover="true"), and the AppBar's ☰
    // button — meaningless once the drawer is always visible — is hidden. Variant itself is never
    // toggled in C#: MudBlazor 9.9.0's MudDrawer natively downgrades a Mini drawer to behave like
    // Temporary below its own Breakpoint parameter (verified in the decompiled MudDrawer source:
    // EffectiveVariant/ShouldOpenDrawer/ShouldCloseDrawer/OnPointerEnterAsync), so these assertions
    // read the rendered "mud-drawer-mini"/"mud-drawer-temporary" class — the actually-observable
    // behavior — rather than the static Variant parameter, which never changes.
    [Fact]
    public async Task Desktop_viewport_renders_a_mini_drawer_and_hides_the_hamburger_button()
    {
        RegisterCommonServices(DefaultHttp());
        UseViewport(Breakpoint.Lg);

        var cut = RenderLayout();
        await Task.Delay(10);

        var drawer = cut.Find(DrawerSelector);
        drawer.ClassList.Should().Contain("mud-drawer-mini");
        drawer.ClassList.Should().NotContain("mud-drawer-temporary");
        cut.FindComponent<MudDrawer>().Instance.OpenMiniOnHover.Should().BeTrue();
        cut.FindAll(HamburgerSelector).Should().BeEmpty();
    }

    [Fact]
    public async Task Mobile_viewport_renders_a_temporary_drawer_and_shows_the_hamburger_button()
    {
        RegisterCommonServices(DefaultHttp());
        UseViewport(Breakpoint.Sm);

        var cut = RenderLayout();
        await Task.Delay(10);

        cut.Find(DrawerSelector).ClassList.Should().Contain("mud-drawer-temporary");
        cut.FindAll(HamburgerSelector).Should().HaveCount(1);
    }

    // Preserves "today's behavior" on mobile exactly, per the task brief: toggled open by the ☰
    // button, closed again on every navigation (MainLayout's own LocationChanged handler).
    [Fact]
    public async Task Mobile_drawer_still_closes_on_every_navigation()
    {
        RegisterCommonServices(DefaultHttp());
        UseViewport(Breakpoint.Sm);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("painel");

        var cut = RenderLayout();
        await Task.Delay(10);

        cut.Find(HamburgerSelector).Click();
        cut.Find(DrawerSelector).ClassList.Should().Contain("mud-drawer--open");

        await cut.InvokeAsync(() => navigation.NavigateTo("painel/jogadores"));

        cut.Find(DrawerSelector).ClassList.Should().Contain("mud-drawer--closed");
    }

    // The concern flagged in the task brief: MainLayout's own "close on every navigation" logic
    // (needed for mobile, since MudNavLink only notifies MudDrawer of same-tab <a> clicks, missing
    // e.g. browser back/forward) must not stomp on the desktop mini drawer's hover-driven Open
    // state. It's guarded to a no-op on desktop instead.
    [Fact]
    public async Task Desktop_drawer_expanded_by_hover_is_not_closed_by_a_navigation()
    {
        RegisterCommonServices(DefaultHttp());
        UseViewport(Breakpoint.Lg);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("painel");

        var cut = RenderLayout();
        await Task.Delay(10);

        cut.Find(DrawerSelector).PointerEnter();
        cut.Find(DrawerSelector).ClassList.Should().Contain("mud-drawer--open");

        await cut.InvokeAsync(() => navigation.NavigateTo("painel/jogadores"));

        cut.Find(DrawerSelector).ClassList.Should().Contain("mud-drawer--open");
    }

    private HttpClient HttpFor(string role, List<UnreadSecretNotesResponse>? unread = null) => FakeHttpMessageHandler.CreateClient(request =>
        request.RequestUri!.AbsolutePath.EndsWith("secret-notes/unread")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(unread ?? new()) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MeResponse("u1", "Nick", role, false, null, false)) });

    [Fact]
    public void Starts_the_notifier_for_a_Jogador()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        cut.WaitForAssertion(() => NotificationConnection.StartCount.Should().Be(1));
    }

    [Fact]
    public async Task Does_not_start_the_notifier_for_a_GM()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("GM"));
        RenderLayout();
        await Task.Delay(100);

        NotificationConnection.StartCount.Should().Be(0);
    }

    [Fact]
    public async Task A_received_note_shows_a_clickable_snackbar_and_plays_the_sound()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        cut.WaitForAssertion(() => NotificationConnection.IsActive.Should().BeTrue());

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("c1", "Ruína"));

        cut.WaitForAssertion(() => cut.Find(".mud-snackbar").TextContent.Should().Contain("Nova nota secreta em Ruína"));
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "ruinaNotificationSound.play");

        await cut.InvokeAsync(() => cut.Find(".mud-snackbar").Click());

        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("campanhas/c1/jogador?aba=notas");
    }

    [Fact]
    public async Task Snackbar_shows_a_campaign_name_with_markup_as_plain_text()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        cut.WaitForAssertion(() => NotificationConnection.IsActive.Should().BeTrue());

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("c1", "<b id=\"injected\">Ruína</b>"));

        cut.WaitForAssertion(() => cut.Find(".mud-snackbar").TextContent.Should().Contain("<b id=\"injected\">Ruína</b>"));
        cut.FindAll("#injected").Should().BeEmpty();
    }

    [Fact]
    public void App_bar_shows_the_unread_total_linking_to_the_single_campaign_with_unread_notes()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador", new() { new("c1", "Ruína", 3) }));
        var cut = RenderLayout();

        cut.WaitForAssertion(() =>
        {
            var indicator = cut.Find("[aria-label='Notas secretas não lidas']");
            indicator.GetAttribute("href").Should().Be("campanhas/c1/jogador?aba=notas");
            cut.Find(".rr-unread-indicator").TextContent.Should().Contain("3");
        });
    }

    [Fact]
    public void App_bar_indicator_links_to_Minhas_Campanhas_when_several_campaigns_have_unread_notes()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador", new() { new("c1", "Ruína", 1), new("c2", "Outra", 1) }));
        var cut = RenderLayout();

        cut.WaitForAssertion(() =>
            cut.Find("[aria-label='Notas secretas não lidas']").GetAttribute("href").Should().Be("minhas-campanhas"));
    }

    [Fact]
    public async Task App_bar_has_no_indicator_without_unread_notes()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        await Task.Delay(100);

        cut.FindAll("[aria-label='Notas secretas não lidas']").Should().BeEmpty();
    }
}
