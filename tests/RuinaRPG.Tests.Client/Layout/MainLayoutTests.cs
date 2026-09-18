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
