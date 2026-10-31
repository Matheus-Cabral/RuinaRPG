using Microsoft.AspNetCore.Components;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// Ficha de Personagem 4.b (e NPC/Criatura): só as entradas montadas na ficha têm o botão Editar, e
/// adicionar/editar/remover traz o saldo de Pontos de Ignição de volta para o formulário.
/// </summary>
public class FichaMagiasEdicaoTests : MudBunitContext
{
    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "GM") }, "test"))));
    }

    private static readonly string[] SingleObjectSuffixes =
    {
        "/attributes/budget", "/skills/budget", "/afinidades", "/sub-attributes", "/modificador-de-dano",
        "/racial-ability", "/racial-traits/pending", "/traits",
    };

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static object Entrada(string id, string nome, bool criadaNaFicha, string tipo = "Magia") => new
    {
        Id = id, Nome = nome, Tipo = tipo, Grau = 1, Descricao = "d", GastoEmPI = 8, Custo = 10,
        Efeitos = new List<object>(), CriadaNaFicha = criadaNaFicha,
    };

    private readonly List<string> _requests = new();
    private int _leiturasDaFicha;
    private bool _releiturasFalham;

    private void RegisterHttp()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            _requests.Add($"{request.Method} {path}");
            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (path.EndsWith("/auth/me") || path.EndsWith("/level-up-notice"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Regex.IsMatch(path, "-sheets/[^/]+$"))
            {
                var pi = ++_leiturasDaFicha == 1 ? 30 : 16;
                if (_releiturasFalham && _leiturasDaFicha > 1)
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                return Json(new { PontosDeIgnicaoAtual = pi, PontosDeIgnicaoTotal = 40, PontosDeIgnicao = pi });
            }
            if (path.EndsWith("/spell-abilities"))
                return Json(new List<object> { Entrada("m1", "Montada", true), Entrada("b1", "Do Banco", false), Entrada("r1", "Racial montada", true, "Racial") });
            if (path.EndsWith("/sub-attributes"))
                return Json(new { DefesaNatural = 15, EficienciaElemental = 3, DanoElemental = 2 });
            return SingleObjectSuffixes.Any(path.EndsWith) ? Json(new { }) : Json(new List<object>());
        }));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());
    }

    private IRenderedComponent<ComponentBase> RenderPagina(Type pagina) => pagina switch
    {
        _ when pagina == typeof(FichaDePersonagem) => Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1")),
        _ when pagina == typeof(FichaDeNpc) => Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1")),
        _ => Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1")),
    };

    private static async Task AbrirAbaAsync(IRenderedComponent<ComponentBase> cut, string nome)
    {
        await cut.InvokeAsync(() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == nome).Click());
        await Task.Delay(50);
    }

    [Theory]
    [InlineData(typeof(FichaDePersonagem))]
    [InlineData(typeof(FichaDeNpc))]
    [InlineData(typeof(FichaDeCriatura))]
    public async Task Only_entries_built_on_the_sheet_have_the_Editar_button(Type pagina)
    {
        RegisterHttp();
        var cut = RenderPagina(pagina);
        await Task.Delay(100);
        await AbrirAbaAsync(cut, "Magias & Habilidades");

        var linhas = cut.FindAll("tr").Where(tr => tr.TextContent.Contains("Montada") || tr.TextContent.Contains("Do Banco")).ToList();
        linhas.Single(tr => tr.TextContent.Contains("Montada")).QuerySelectorAll("button").Should().Contain(b => b.TextContent.Contains("Editar"));
        linhas.Single(tr => tr.TextContent.Contains("Do Banco")).QuerySelectorAll("button").Should().NotContain(b => b.TextContent.Contains("Editar"));
        cut.FindAll("tr").Single(tr => tr.TextContent.Contains("Racial montada")).QuerySelectorAll("button")
            .Should().NotContain(b => b.TextContent.Contains("Editar"), "só Magia e Habilidade são editáveis");
    }

    [Theory]
    [InlineData(typeof(FichaDePersonagem), "character-sheets", "Pontos de Ignição Atual")]
    [InlineData(typeof(FichaDeNpc), "npc-sheets", "Pontos de Ignição Atual")]
    [InlineData(typeof(FichaDeCriatura), "creature-sheets", "Pontos de Ignição")]
    public async Task Removing_a_spell_reloads_the_Pontos_de_Ignicao_shown_on_the_sheet(Type pagina, string rota, string rotulo)
    {
        RegisterHttp();
        var cut = RenderPagina(pagina);
        await Task.Delay(100);
        await AbrirAbaAsync(cut, "Magias & Habilidades");

        await cut.InvokeAsync(() => cut.FindAll("tr").Single(tr => tr.TextContent.Contains("Montada")).QuerySelectorAll("button").Single(b => b.TextContent.Contains("Remover")).Click());
        await Task.Delay(100);
        await AbrirAbaAsync(cut, "Informações Básicas");

        _requests.Count(r => r == $"GET /api/{rota}/sheet-1").Should().BeGreaterThan(1);
        cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == rotulo).Find("input").GetAttribute("value").Should().Be("16");
    }

    [Theory]
    [InlineData(typeof(FichaDePersonagem), "character-sheets", "Pontos de Ignição Atual")]
    [InlineData(typeof(FichaDeNpc), "npc-sheets", "Pontos de Ignição Atual")]
    [InlineData(typeof(FichaDeCriatura), "creature-sheets", "Pontos de Ignição")]
    public async Task A_failed_reload_after_removing_a_spell_leaves_the_field_as_it_was(Type pagina, string rota, string rotulo)
    {
        // A remoção já deu certo: a releitura do saldo falhar não pode virar erro da operação.
        _releiturasFalham = true;
        RegisterHttp();
        var cut = RenderPagina(pagina);
        await Task.Delay(100);
        await AbrirAbaAsync(cut, "Magias & Habilidades");

        await cut.InvokeAsync(() => cut.FindAll("tr").Single(tr => tr.TextContent.Contains("Montada")).QuerySelectorAll("button").Single(b => b.TextContent.Contains("Remover")).Click());
        await Task.Delay(100);
        await AbrirAbaAsync(cut, "Informações Básicas");

        _requests.Count(r => r == $"GET /api/{rota}/sheet-1").Should().BeGreaterThan(1);
        cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == rotulo).Find("input").GetAttribute("value").Should().Be("30");
    }
}
