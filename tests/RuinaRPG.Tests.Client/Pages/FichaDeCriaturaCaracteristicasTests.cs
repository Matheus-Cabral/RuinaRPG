using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>Ficha de Criaturas 5.d: o seletor lista só o catálogo de criatura; o Auditor cadastra pela ficha.</summary>
public class FichaDeCriaturaCaracteristicasTests : MudBunitContext
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

    private void RegisterHttp(bool auditor)
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            _requests.Add($"{request.Method} {path}");
            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (path.EndsWith("/auth/me"))
                return Json(new MeResponse("u-1", "Mestre", "GM", auditor, null, false));
            if (path.EndsWith("/level-up-notice"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Regex.IsMatch(path, "-sheets/[^/]+$"))
                return Json(new { });
            return SingleObjectSuffixes.Any(path.EndsWith) ? Json(new { }) : Json(new List<object>());
        }));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());
    }

    [Fact]
    public async Task The_new_characteristic_button_only_renders_for_the_rules_auditor()
    {
        RegisterHttp(auditor: true);
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindAll("button:contains('Nova característica de criatura')").Should().HaveCount(1);
    }

    [Fact]
    public async Task The_new_characteristic_button_is_hidden_from_a_gm_who_is_not_the_auditor()
    {
        RegisterHttp(auditor: false);
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindAll("button:contains('Nova característica de criatura')").Should().BeEmpty();
    }

    [Fact]
    public async Task The_characteristic_picker_searches_only_the_creature_catalog()
    {
        RegisterHttp(auditor: false);
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        _requests.Clear();

        var picker = cut.FindComponents<EntityPicker>().Single(p => p.Instance.Placeholder == "Buscar característica...");
        await cut.InvokeAsync(() => picker.Instance.SearchAsyncForTests("regen"));

        _requests.Should().Contain(r => r.Contains("/creature-exclusive-traits"));
        _requests.Should().NotContain(r => r.EndsWith("/traits") && !r.Contains("-sheets/"));
    }
}
