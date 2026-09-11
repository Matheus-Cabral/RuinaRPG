using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class LivroDeRegrasTests : MudBunitContext
{
    // The page has no route guard (accessible to anonymous visitors, per "Requisitos - Livro de
    // Regras"), so its first breadcrumb can't always point at "painel" — that's a dead end for
    // anyone not logged in. It should point at the Landing Page instead when anonymous.
    //
    // AddAuthorization() (unlike NavMenuTests' manual IAuthorizationService double) also seeds a
    // real authenticated ClaimsPrincipal, which this page's direct
    // CascadingParameter<Task<AuthenticationState>> read needs — NavMenu only ever goes through
    // <AuthorizeView>, which a fake IAuthorizationService alone can satisfy.
    private void RegisterCommonServices()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        }));
    }

    [Fact]
    public void Anonymous_visitor_sees_Inicio_instead_of_Painel_in_the_breadcrumb()
    {
        RegisterCommonServices();
        AddAuthorization().SetNotAuthorized();

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<LivroDeRegras>());

        cut.FindAll("a").Should().Contain(a => a.TextContent.Trim() == "Início");
        cut.FindAll("a").Should().NotContain(a => a.TextContent.Trim() == "Painel");
    }

    [Fact]
    public void Authenticated_user_sees_Painel_in_the_breadcrumb()
    {
        RegisterCommonServices();
        AddAuthorization().SetAuthorized("Teste");

        var cut = Render<CascadingAuthenticationState>(p => p
            .AddChildContent<LivroDeRegras>());

        cut.FindAll("a").Should().Contain(a => a.TextContent.Trim() == "Painel");
        cut.FindAll("a").Should().NotContain(a => a.TextContent.Trim() == "Início");
    }
}
