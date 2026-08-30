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
        // anonymous/unauthenticated state — only Início, Entrar and Cadastrar should render.
        Services.AddAuthorizationCore();
        Services.AddSingleton<IAuthorizationService>(new BunitAuthorizationService(AuthorizationState.Unauthorized));
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
        Services.AddScoped(_ => new HttpClient());

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<NavMenu>());

        cut.FindAll("a").Count.Should().Be(3);
        cut.FindAll("div[tabindex]").Count.Should().Be(0);
    }
}
