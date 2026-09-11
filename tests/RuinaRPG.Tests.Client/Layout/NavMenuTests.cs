using Blazored.LocalStorage;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Layout;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Layout;

public class NavMenuTests : MudBunitContext
{
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
}
