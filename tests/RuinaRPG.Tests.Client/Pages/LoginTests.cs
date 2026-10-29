using Blazored.LocalStorage;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Services;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>Login e Cadastro R0002: a mensagem de recusa do login.</summary>
public class LoginTests : MudBunitContext
{
    private IRenderedComponent<Login> RenderRespondendo(string corpoDoErro)
    {
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent(corpoDoErro) }));
        return Render<Login>();
    }

    private static void Entrar(IRenderedComponent<Login> cut)
    {
        cut.FindAll("input")[0].Change("Ana");
        cut.FindAll("input")[1].Change("Senha!123");
        cut.Find("form").Submit();
    }

    [Fact]
    public void Conta_desativada_mostra_o_texto_do_servidor()
    {
        var cut = RenderRespondendo(TextosDaConta.ContaDesativada);

        Entrar(cut);

        cut.WaitForAssertion(() => cut.FindComponent<DismissibleAlert>().Instance.Message.Should().Be(TextosDaConta.ContaDesativada));
    }

    [Fact]
    public void Credenciais_invalidas_mantem_a_mensagem_de_sempre()
    {
        var cut = RenderRespondendo("Credenciais inválidas.");

        Entrar(cut);

        cut.WaitForAssertion(() => cut.FindComponent<DismissibleAlert>().Instance.Message.Should().Be("Nickname ou senha inválidos."));
    }
}
