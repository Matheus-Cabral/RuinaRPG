using Bunit;
using FluentAssertions;
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
/// Elemental, então a Afinidade dela continua em Informações Básicas.
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
            if (path.EndsWith("/level-up-notice"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Regex.IsMatch(path, "-sheets/[^/]+$"))
                return Json(new { Afinidade = "Fogo" });
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
        secao.Instance.Afinidade.Should().Be("Fogo");
        secao.Find(".subatributo-eficiencia .subatributo-valor").TextContent.Trim().Should().Be("3");
        secao.Find(".subatributo-dano .subatributo-valor").TextContent.Trim().Should().Be("2");
    }

    [Fact]
    public async Task Personagem_has_the_Afinidade_in_the_sub_attributes_not_in_the_basic_info()
    {
        RegisterHttp();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindComponents<AfinidadeSelect>().Should().BeEmpty("a Afinidade saiu de Informações Básicas");

        await cut.InvokeAsync(() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == Combate).Click());
        await Task.Delay(50);

        AssertAfinidadeNosSubAtributos(cut.FindComponent<SubAtributosSection>());
    }

    [Fact]
    public async Task Npc_has_the_Afinidade_in_the_sub_attributes_not_in_the_basic_info()
    {
        RegisterHttp();
        var cut = Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindComponents<AfinidadeSelect>().Should().BeEmpty("a Afinidade saiu de Informações Básicas");

        await cut.InvokeAsync(() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == Combate).Click());
        await Task.Delay(50);

        AssertAfinidadeNosSubAtributos(cut.FindComponent<SubAtributosSection>());
    }

    [Fact]
    public async Task Criatura_keeps_the_Afinidade_in_the_basic_info_and_has_only_the_Defesa_block()
    {
        RegisterHttp();
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindComponents<AfinidadeSelect>().Should().ContainSingle();

        await cut.InvokeAsync(() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == Combate).Click());
        await Task.Delay(50);

        var secao = cut.FindComponent<SubAtributosSection>();
        secao.Instance.ComAfinidade.Should().BeFalse();
        secao.FindAll(".subatributos-bloco-defesa").Should().ContainSingle();
        secao.FindAll(".subatributos-bloco-afinidade").Should().BeEmpty();
    }

    // Trocar a Afinidade muda a Eficiência e o Dano Elemental, que vêm do servidor — a ficha salva e
    // recarrega os sub-atributos, como já faz com a Cobertura.
    [Fact]
    public async Task Choosing_an_Afinidade_saves_the_sheet_and_reloads_the_sub_attributes()
    {
        RegisterHttp();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await cut.InvokeAsync(() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == Combate).Click());
        await Task.Delay(50);
        _requests.Clear();

        await cut.InvokeAsync(() => cut.FindComponent<AfinidadeSelect>().Instance.ValueChanged.InvokeAsync("Gelo"));
        await Task.Delay(100);

        _requests.Should().Contain("PUT /api/character-sheets/sheet-1");
        _requests.Should().Contain("GET /api/character-sheets/sheet-1/sub-attributes");
    }
}
