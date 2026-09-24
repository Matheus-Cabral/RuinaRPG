using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Layout;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Tests.Client.Shared;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Layout;

public class NavMenuTests : MudBunitContext
{
    // Every MudNavLink/MudNavGroup in the drawer carries a Material icon (task brief "sidebar
    // icons"), same visual language as the Landing feature cards (Icons.Material.Filled.*,
    // Color.Primary). This reads each link's own icon + icon color off the MudNavLink component
    // instance, paired with its rendered text (".mud-nav-link-text" is the div MudNavLink itself
    // wraps ChildContent in, regardless of which of its two render branches — <a> vs <div onclick>
    // for "Sair" — is taken).
    private static (string Text, string? Icon, Color IconColor) Describe(IRenderedComponent<MudNavLink> link) =>
        (link.Find(".mud-nav-link-text").TextContent.Trim(), link.Instance.Icon, link.Instance.IconColor);

    [Fact]
    public void Unauthenticated_NavLinks_render_as_real_anchors_not_click_divs()
    {
        // No token is ever stored in the fake local storage backing this test, so the real
        // AuthStateService/TokenAuthenticationStateProvider pipeline naturally resolves to the
        // anonymous/unauthenticated state — Início, Entrar, Cadastrar and Livro de Regras (the
        // rulebook is readable without authentication, per "Requisitos - Livro de Regras") render.
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationService>(new BunitAuthorizationService(AuthorizationState.Unauthorized));
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        Services.AddScoped(_ => new HttpClient());

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<NavMenu>());

        cut.FindAll("a").Count.Should().Be(4);
        cut.FindAll("div[tabindex]").Count.Should().Be(0);
        cut.FindAll("a").Should().Contain(a => a.TextContent.Trim() == "Livro de Regras");
    }

    [Fact]
    public void Authenticated_NavLinks_show_Painel_instead_of_Inicio()
    {
        // A logged-in user (GM or Jogador — this fake authorization double doesn't discriminate
        // by role, only by overall Authorized/Unauthorized state) should see "Painel" as their
        // home link in the sidebar, not the anonymous "Início" landing-page link.
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationService>(new BunitAuthorizationService(AuthorizationState.Authorized));
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        Services.AddScoped(_ => new HttpClient());

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<NavMenu>());

        cut.FindAll("a").Should().NotContain(a => a.TextContent.Trim() == "Início");
        cut.FindAll("a").Should().Contain(a => a.TextContent.Trim() == "Painel");
    }

    [Fact]
    public void Unauthenticated_NavLinks_have_the_expected_icons()
    {
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationService>(new BunitAuthorizationService(AuthorizationState.Unauthorized));
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        Services.AddScoped(_ => new HttpClient());

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<NavMenu>());

        cut.FindComponents<MudNavLink>().Select(Describe).Should().BeEquivalentTo(new[]
        {
            ("Início", Icons.Material.Filled.Home, Color.Primary),
            ("Entrar", Icons.Material.Filled.Login, Color.Primary),
            ("Cadastrar", Icons.Material.Filled.PersonAddAlt1, Color.Primary),
            ("Livro de Regras", Icons.Material.Filled.MenuBook, Color.Primary),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Authenticated_GM_NavLinks_have_the_expected_icons()
    {
        // Unlike the blanket BunitAuthorizationService double above, AddAuthorization().SetRoles(...)
        // actually evaluates the "Roles" an AuthorizeView declares, so this is the only way to reach
        // the GM-only block. RulesAuditorNavLinks (nested inside it) calls its own auth/me — answering
        // with IsRulesAuditor=false keeps it rendering nothing, so it doesn't perturb this ordered list
        // (its own icons are covered by RulesAuditorNavLinksTests).
        AddAuthorization().SetAuthorized("gm-user").SetRoles("GM");
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new MeResponse("u1", "Gm1", "GM", false, null, false)),
        }));

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<NavMenu>());

        cut.FindComponents<MudNavLink>().Select(Describe).Should().BeEquivalentTo(new[]
        {
            ("Painel", Icons.Material.Filled.Dashboard, Color.Primary),
            ("Convidar Jogador", Icons.Material.Filled.PersonAdd, Color.Primary),
            ("Jogadores", Icons.Material.Filled.Group, Color.Primary),
            ("Catálogo de Itens", Icons.Material.Filled.Inventory2, Color.Primary),
            ("NPCs do GM", Icons.Material.Filled.Person, Color.Primary),
            ("Bestiário do GM", Icons.Material.Filled.Pets, Color.Primary),
            ("Banco de Magias e Habilidades", Icons.Material.Filled.AutoStories, Color.Primary),
            ("Banco de Runas", Icons.Material.Filled.Diamond, Color.Primary),
            ("Habilidades Raciais", Icons.Material.Filled.Diversity3, Color.Primary),
            ("Campanhas", Icons.Material.Filled.Groups, Color.Primary),
            ("Livro de Regras", Icons.Material.Filled.MenuBook, Color.Primary),
            ("Sair", Icons.Material.Filled.Logout, Color.Primary),
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Authenticated_Jogador_NavLinks_have_the_expected_icons()
    {
        AddAuthorization().SetAuthorized("player-user").SetRoles("Jogador");
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped(_ => new HttpClient());

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<NavMenu>());

        cut.FindComponents<MudNavLink>().Select(Describe).Should().BeEquivalentTo(new[]
        {
            ("Painel", Icons.Material.Filled.Dashboard, Color.Primary),
            ("Minhas Campanhas", Icons.Material.Filled.Groups, Color.Primary),
            ("Livro de Regras", Icons.Material.Filled.MenuBook, Color.Primary),
            ("Sair", Icons.Material.Filled.Logout, Color.Primary),
        }, options => options.WithStrictOrdering());
    }
}
