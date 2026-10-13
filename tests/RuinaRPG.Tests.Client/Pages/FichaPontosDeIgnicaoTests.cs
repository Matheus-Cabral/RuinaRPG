using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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
/// Ficha de Personagem 1.b e Ficha de NPC R0005: a ficha exibe só os Pontos de Ignição atuais, e o
/// campo de bônus manual é largo o bastante para o rótulo.
/// </summary>
public class FichaPontosDeIgnicaoTests : MudBunitContext
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
                return Json(new { PontosDeIgnicaoAtual = 12, PontosDeIgnicaoTotal = 40 });
            if (path.EndsWith("/sub-attributes"))
                return Json(new { DefesaNatural = 15, EficienciaElemental = 3, DanoElemental = 2 });
            return SingleObjectSuffixes.Any(path.EndsWith) ? Json(new { }) : Json(new List<object>());
        }));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());
    }

    [Fact]
    public async Task Personagem_no_longer_shows_the_Pontos_de_Ignicao_Total()
    {
        RegisterHttp();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);

        cut.Markup.Should().NotContain("Pontos de Ignição Total");
        cut.FindComponents<MudNumericField<int?>>().Should().Contain(c => c.Instance.Label == "Pontos de Ignição Atual");
    }

    [Fact]
    public async Task Npc_no_longer_has_the_Pontos_de_Ignicao_Total_field()
    {
        RegisterHttp();
        var cut = Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);

        cut.FindComponents<MudNumericField<int?>>().Should().NotContain(c => c.Instance.Label == "Pontos de Ignição Total");
        cut.FindComponents<MudNumericField<int?>>().Should().Contain(c => c.Instance.Label == "Pontos de Ignição Atual");
    }

    [Fact]
    public async Task The_manual_bonus_field_is_wide_enough_for_its_label_and_matches_the_xp_field()
    {
        RegisterHttp();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);

        var bonus = cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Adicionar/subtrair PI (bônus manual)");
        var xp = cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Adicionar/subtrair XP");
        bonus.Instance.Style.Should().Be("max-width:22rem");
        xp.Instance.Style.Should().Be("max-width:22rem");
    }
}
