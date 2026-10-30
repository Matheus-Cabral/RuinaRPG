using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// Ficha de Personagem 2.b: as três fichas usam a mesma seção Sub-Atributos (aba Combate), com o
/// bloco de Defesa; Personagem e NPC têm também o bloco de Afinidade Elemental, e por isso a
/// Afinidade deixa de ser um campo de Informações Básicas neles. A Criatura não tem Eficiência/Dano
/// Elemental, então a Afinidade dela continua em Informações Básicas. Escolher a Afinidade (salva e relê os
/// sub-atributos) fica em AbaCombateTests.Changing_afinidade_saves_reloads_the_tabs_and_shows_the_new_efficiency.
/// </summary>
public class FichaSubAtributosTests : MudBunitContext
{
    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "GM") }, "test"))));
    }

    private static readonly string[] SingleObjectSuffixes =
    {
        "/attributes/budget", "/skills/budget", "/sub-attributes", "/modificador-de-dano",
        "/racial-ability", "/racial-traits/pending", "/traits",
    };

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    // Same inert stubbing as FichaArsenalDurabilidadeTests, plus a sheet whose Afinidade is Fogo.
    private readonly List<string> _requests = new();

    private void RegisterHttp()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            _requests.Add($"{request.Method} {path}");
            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (path.EndsWith("/auth/me"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (path.EndsWith("/level-up-notice"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Regex.IsMatch(path, "-sheets/[^/]+$"))
                return Json(new { Afinidade = 2 });
            if (path.EndsWith("/sub-attributes"))
                return Json(new { DefesaNatural = 15, EficienciaElemental = 3, DanoElemental = 2 });
            return SingleObjectSuffixes.Any(path.EndsWith) ? Json(new { }) : Json(new List<object>());
        }));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());
    }

    private const string Combate = "Combate";

    private static void AssertAfinidadeNosSubAtributos(IRenderedComponent<SubAtributosSection> secao)
    {
        secao.Instance.ComAfinidade.Should().BeTrue();
        secao.Instance.Afinidade.Should().Be(2);
        secao.Find(".subatributo-eficiencia .subatributo-valor").TextContent.Trim().Should().Be("3");
        secao.Find(".subatributo-dano .subatributo-valor").TextContent.Trim().Should().Be("2");
    }

    private IRenderedComponent<ComponentBase> RenderPagina(Type pagina) => pagina switch
    {
        _ when pagina == typeof(FichaDePersonagem) => Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1")),
        _ when pagina == typeof(FichaDeNpc) => Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1")),
        _ => Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1")),
    };

    [Theory]
    [InlineData(typeof(FichaDePersonagem))]
    [InlineData(typeof(FichaDeNpc))]
    public async Task The_Afinidade_is_in_the_sub_attributes_not_in_the_basic_info(Type pagina)
    {
        RegisterHttp();
        var cut = RenderPagina(pagina);
        await Task.Delay(100);
        cut.FindComponents<AfinidadeSelect>().Should().BeEmpty("a Afinidade saiu de Informações Básicas");

        await cut.InvokeAsync(() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == Combate).Click());
        await Task.Delay(50);

        AssertAfinidadeNosSubAtributos(cut.FindComponent<SubAtributosSection>());
    }

    [Fact]
    public async Task Criatura_keeps_the_Afinidade_in_the_basic_info_and_has_no_Afinidade_block()
    {
        RegisterHttp();
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindComponents<AfinidadeSelect>().Should().ContainSingle();

        await cut.InvokeAsync(() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == Combate).Click());
        await Task.Delay(50);

        var secao = cut.FindComponent<SubAtributosSection>();
        secao.Instance.ComAfinidade.Should().BeFalse();
        secao.FindAll(".subatributos-defesa").Should().ContainSingle();
        secao.FindAll(".subatributos-bloco-afinidade").Should().BeEmpty();
    }
}
