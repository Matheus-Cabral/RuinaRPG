using Blazored.LocalStorage;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Services;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class PainelTests : MudBunitContext
{
    private static object Resumo(string id, string nome, bool cemiterio, params string[] motivos) => new
    {
        Id = id, Nome = nome, Nivel = 3, CampaignId = "c1", CampanhaNome = "Ruína", NoCemiterio = cemiterio, MotivosDoCemiterio = motivos,
    };

    private IRenderedComponent<Painel> RenderComoJogador(params object[] fichas)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("jogador");
        authContext.SetRoles("Jogador");
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith("auth/me")
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { Id = "u", Nickname = "Ana", Email = "a@x.com", Role = "Jogador", IsRulesAuditor = false })
                }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(fichas) }));
        return Render<Painel>();
    }

    [Fact]
    public void Painel_separa_as_fichas_do_cemiterio()
    {
        var cut = RenderComoJogador(Resumo("f1", "Viva", false), Resumo("f2", "Morta", true, "Morte"));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid=fichas-vivas]").TextContent.Should().Contain("Viva").And.NotContain("Morta");
            var cemiterio = cut.Find("[data-testid=cemiterio]").TextContent;
            cemiterio.Should().Contain("Cemitério").And.Contain("Morta").And.Contain("Nível 3").And.Contain("Ruína")
                .And.Contain("Personagem morto");
        });
    }

    [Fact]
    public void Painel_sem_ficha_no_cemiterio_nao_mostra_a_secao()
    {
        var cut = RenderComoJogador(Resumo("f1", "Viva", false));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Viva"));
        cut.FindAll("[data-testid=cemiterio]").Should().BeEmpty();
    }

    [Fact]
    public void Mensagem_de_nenhuma_ficha_considera_so_as_vivas()
    {
        var cut = RenderComoJogador(Resumo("f2", "Morta", true, "Morte"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Você ainda não tem nenhuma ficha de personagem");
            cut.Find("[data-testid=cemiterio]").TextContent.Should().Contain("Morta");
        });
    }
}
