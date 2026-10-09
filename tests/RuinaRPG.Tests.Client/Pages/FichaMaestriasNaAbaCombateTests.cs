using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// A section "Maestrias" das 3 fichas (Personagem, NPC, Criatura) fica na aba "Combate", logo abaixo
/// de "Armas" e antes de "Armaduras" — e não mais na aba "Magias &amp; Habilidades". Renderiza a página
/// inteira com todos os endpoints em respostas inertes (mesmo arranjo de FichaArsenalDurabilidadeTests)
/// e troca de aba, já que o MudTabs só renderiza o painel ativo.
/// </summary>
public class FichaMaestriasNaAbaCombateTests : MudBunitContext
{
    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "GM") }, "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private static HttpResponseMessage Json(object body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static readonly string[] SingleObjectSuffixes =
    {
        "/attributes/budget", "/skills/budget", "/sub-attributes", "/modificador-de-dano",
        "/racial-ability", "/racial-traits/pending", "/traits",
    };

    private static HttpResponseMessage Stub(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Get)
            return new HttpResponseMessage(HttpStatusCode.OK);

        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/level-up-notice"))
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (Regex.IsMatch(path, "-sheets/[^/]+$") || SingleObjectSuffixes.Any(path.EndsWith))
            return Json(new { });
        return Json(new List<object>());
    }

    private async Task<IRenderedComponent<T>> RenderSheetAsync<T>(Action<ComponentParameterCollectionBuilder<T>> parameters) where T : IComponent
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(Stub));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());

        var cut = Render(parameters);
        await Task.Delay(100);
        return cut;
    }

    private static async Task GoToTabAsync<T>(IRenderedComponent<T> cut, string tab) where T : IComponent
    {
        var element = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == tab);
        await cut.InvokeAsync(() => element.Click());
    }

    private static List<string> SectionTitles<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.FindAll(".rr-section-title").Select(e => e.TextContent.Trim()).ToList();

    private static async Task AssertMaestriasFicaAbaixoDeArmasAsync<T>(IRenderedComponent<T> cut) where T : IComponent
    {
        await GoToTabAsync(cut, "Combate");
        var combate = SectionTitles(cut);
        combate.Should().Contain("Maestrias");
        combate.IndexOf("Maestrias").Should().Be(combate.IndexOf("Armas") + 1);
        combate.IndexOf("Armaduras").Should().Be(combate.IndexOf("Maestrias") + 1);
        cut.Markup.Should().Contain("Adicionar Maestria");

        await GoToTabAsync(cut, "Magias & Habilidades");
        SectionTitles(cut).Should().NotContain("Maestrias");
        cut.Markup.Should().NotContain("Adicionar Maestria");
    }

    [Fact]
    public async Task Personagem_shows_Maestrias_below_Armas_on_the_Combate_tab() =>
        await AssertMaestriasFicaAbaixoDeArmasAsync(await RenderSheetAsync<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1")));

    [Fact]
    public async Task Npc_shows_Maestrias_below_Armas_on_the_Combate_tab() =>
        await AssertMaestriasFicaAbaixoDeArmasAsync(await RenderSheetAsync<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1")));

    [Fact]
    public async Task Criatura_shows_Maestrias_below_Armas_on_the_Combate_tab() =>
        await AssertMaestriasFicaAbaixoDeArmasAsync(await RenderSheetAsync<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1")));
}
