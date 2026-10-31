using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// Condições (1.5.0) nas três fichas: o alerta das Condições ativas, o chip "Desabilitado por" nas linhas
/// afetadas, a Afinidade com Condições, o dano elemental e a recarga ao vivo (CondicoesAlteradas). Renderiza a
/// página inteira com os endpoints de lista inertes, como FichaArsenalDurabilidadeTests.
/// </summary>
public class FichaCondicoesTests : MudBunitContext
{
    private sealed class FakeAuthStateProvider(string papel) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, papel) }, "test"))));
    }

    private string _papel = "GM";

    private readonly List<string> _requests = new();
    private Func<HttpRequestMessage, HttpResponseMessage?> _overrides = _ => null;
    private List<CondicaoAtivaNaFichaResponse> _ativas = new();

    private static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private static readonly string[] SingleObjectSuffixes =
    {
        "/attributes/budget", "/skills/budget", "/afinidades", "/sub-attributes", "/modificador-de-dano", "/racial-ability", "/racial-traits/pending", "/traits",
    };

    private int Contagem(string sufixo) => _requests.Count(r => r.StartsWith("GET ") && r.EndsWith(sufixo));

    private HttpResponseMessage Stub(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        _requests.Add($"{request.Method} {path}");
        if (_overrides(request) is { } custom)
            return custom;
        if (request.Method != HttpMethod.Get)
            return new HttpResponseMessage(HttpStatusCode.OK);
        if (path.EndsWith("/condicoes-ativas"))
            return Json(_ativas);
        if (path.EndsWith("/auth/me") || path.EndsWith("/level-up-notice"))
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (Regex.IsMatch(path, "-sheets/[^/]+$") || SingleObjectSuffixes.Any(path.EndsWith))
            return Json(new { });
        return Json(new List<object>());
    }

    private void Prepare(Func<HttpRequestMessage, HttpResponseMessage?>? overrides = null)
    {
        _overrides = overrides ?? (_ => null);
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(Stub));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider(_papel));
    }

    private static async Task GoToTabAsync<T>(IRenderedComponent<T> cut, string tab) where T : IComponent
    {
        var header = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == tab);
        await cut.InvokeAsync(() => header.Click());
    }

    private async Task<IRenderedComponent<FichaDePersonagem>> RenderPersonagemAsync()
    {
        // MainLayout is what opens the notification connection in the app; here the test plays that part.
        await Services.GetRequiredService<AvisosDoHub>().StartAsync();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        return cut;
    }

    private async Task<IRenderedComponent<ComponentBase>> RenderPaginaAsync(Type pagina)
    {
        IRenderedComponent<ComponentBase> cut = pagina switch
        {
            _ when pagina == typeof(FichaDePersonagem) => Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1")),
            _ when pagina == typeof(FichaDeNpc) => Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1")),
            _ => Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1")),
        };
        await Task.Delay(100);
        return cut;
    }

    private static readonly CondicaoAtivaNaFichaResponse Atordoado = new("Atordoado", "Não consegue agir direito.", "Atordoado", 2, new() { "Vigor −2" });

    // ---- alerta ----

    [Fact]
    public async Task Personagem_shows_the_active_conditions_alert_at_the_top()
    {
        _ativas = new() { Atordoado };
        Prepare();

        var cut = await RenderPersonagemAsync();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Condições ativas").And.Contain("Não consegue agir direito.").And.Contain("Vigor −2").And.Contain("2 turnos"));
        _requests.Should().Contain("GET /api/character-sheets/sheet-1/condicoes-ativas");
        cut.Markup.IndexOf("Condições ativas", StringComparison.Ordinal).Should().BeLessThan(cut.Markup.IndexOf("Informações Básicas", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Personagem_has_no_alert_without_active_conditions()
    {
        Prepare();

        var cut = await RenderPersonagemAsync();

        cut.Markup.Should().NotContain("Condições ativas");
    }

    [Theory]
    [InlineData(typeof(FichaDeNpc), "npc-sheets")]
    [InlineData(typeof(FichaDeCriatura), "creature-sheets")]
    public async Task A_granted_sheet_shows_the_alert_but_a_GM_owned_one_does_not_even_ask(Type pagina, string rota)
    {
        _ativas = new() { Atordoado };
        Prepare(r => r.RequestUri!.AbsolutePath.EndsWith($"{rota}/sheet-1") ? Json(new { OwnerId = "u1" }) : null);
        var granted = await RenderPaginaAsync(pagina);
        granted.WaitForAssertion(() => granted.Markup.Should().Contain("Condições ativas"));

        _requests.Clear();
        _overrides = _ => null;
        var gmOwned = await RenderPaginaAsync(pagina);

        gmOwned.Markup.Should().NotContain("Condições ativas");
        _requests.Should().NotContain(r => r.EndsWith("/condicoes-ativas"));
    }

    // ---- chips de "Desabilitado por" ----

    [Theory]
    [InlineData(typeof(FichaDePersonagem))]
    [InlineData(typeof(FichaDeNpc))]
    [InlineData(typeof(FichaDeCriatura))]
    public async Task A_disabled_weapon_row_shows_the_chip_and_an_enabled_one_does_not(Type pagina)
    {
        Prepare(r => r.RequestUri!.AbsolutePath.EndsWith("/weapons")
            ? Json(new object[]
            {
                new { Id = "w1", ItemId = "i1", Nome = "Espada Longa", Dados = "1d8", DesabilitadoPor = "Atordoado" },
                new { Id = "w2", ItemId = "i2", Nome = "Adaga", Dados = "1d4" },
            })
            : null);
        await Services.GetRequiredService<AvisosDoHub>().StartAsync();
        var cut = await RenderPaginaAsync(pagina);
        await GoToTabAsync(cut, "Combate");

        var linhas = cut.FindAll("tr").Where(t => t.TextContent.Contains("Espada Longa") || t.TextContent.Contains("Adaga")).ToList();
        linhas.Single(t => t.TextContent.Contains("Espada Longa")).TextContent.Should().Contain("Desabilitado por: Atordoado");
        linhas.Single(t => t.TextContent.Contains("Adaga")).TextContent.Should().NotContain("Desabilitado por");
    }

    [Fact]
    public async Task A_disabled_spell_row_shows_the_chip()
    {
        Prepare(r => r.RequestUri!.AbsolutePath.EndsWith("/spell-abilities")
            ? Json(new object[] { new { Id = "s1", Nome = "Bola de Fogo", Tipo = "Magia", Grau = 1, Descricao = "Queima.", Efeitos = new List<object>(), DesabilitadoPor = "Silenciado" } })
            : null);
        var cut = await RenderPersonagemAsync();
        await GoToTabAsync(cut, "Magias & Habilidades");

        cut.FindAll("tr").Single(t => t.TextContent.Contains("Bola de Fogo") && !t.TextContent.Contains("Adicionar")).TextContent.Should().Contain("Desabilitado por: Silenciado");
    }

    [Fact]
    public async Task A_disabled_passive_and_a_disabled_rune_show_the_chip()
    {
        Prepare(r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/spell-abilities"))
                return Json(new object[] { new { Id = "p1", Nome = "Pele de Ferro", Tipo = "Passiva", Grau = 1, Descricao = "Dura.", Efeitos = new List<object>(), DesabilitadoPor = "Enfraquecido" } });
            if (path.EndsWith("/runes"))
                return Json(new object[] { new { Id = "r1", Nome = "Runa do Vento", Descricao = "Sopra.", Grau = 1, DesabilitadoPor = "Silenciado" } });
            return null;
        });
        var cut = await RenderPersonagemAsync();
        await GoToTabAsync(cut, "Magias & Habilidades");

        cut.FindAll("tr").Single(t => t.TextContent.Contains("Pele de Ferro")).TextContent.Should().Contain("Desabilitado por: Enfraquecido");
        cut.FindAll("tr").Single(t => t.TextContent.Contains("Runa do Vento")).TextContent.Should().Contain("Desabilitado por: Silenciado");
    }

    [Fact]
    public async Task Armor_shield_and_artifact_rows_show_the_chip()
    {
        Prepare(r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/armor-slots"))
                return Json(new object[] { new { Slot = "Superior", ItemId = "a1", Nome = "Cota de Malha", DesabilitadoPor = "Atordoado" } });
            if (path.EndsWith("/shields"))
                return Json(new object[] { new { Id = "sh1", ItemId = "i3", Nome = "Broquel", DesabilitadoPor = "Atordoado" } });
            if (path.EndsWith("/artifacts"))
                return Json(new object[] { new { Id = "af1", ArtifactItemId = "i4", Nome = "Amuleto", TipoDeAlvo = "Atributo", Alvo = "Vigor", Valor = 1, DesabilitadoPor = "Silenciado" } });
            return null;
        });
        var cut = await RenderPersonagemAsync();
        await GoToTabAsync(cut, "Combate");
        cut.FindAll("tr").Single(t => t.TextContent.Contains("Cota de Malha")).TextContent.Should().Contain("Desabilitado por: Atordoado");
        cut.FindAll("tr").Single(t => t.TextContent.Contains("Broquel")).TextContent.Should().Contain("Desabilitado por: Atordoado");

        await GoToTabAsync(cut, "Posses");
        cut.FindAll("tr").Single(t => t.TextContent.Contains("Amuleto")).TextContent.Should().Contain("Desabilitado por: Silenciado");
    }

    // ---- afinidade e dano elemental ----

    private static HttpResponseMessage AfinidadesComFogo(int pontosComCondicoes) => Json(new
    {
        Gasto = 2, Maximo = 6, SecaoVisivel = true,
        Elementos = new[] { new { Id = 2, Nome = "Fogo", Tipo = "Base", Teto = (int?)null, Pontos = 2, Experiencia = (int?)null, Bloqueado = false, Motivo = (string?)null, PontosComCondicoes = pontosComCondicoes } },
    });

    // Ficha de Personagem R0009: o valor exibido do elemento muda com as Condições; o que o − e o + mexem (e a barra
    // mostra) continua sendo os pontos guardados.
    [Fact]
    public async Task The_affinity_shows_the_adjusted_value_but_the_points_stay_the_stored_ones()
    {
        Prepare(r => r.RequestUri!.AbsolutePath.EndsWith("/afinidades") ? AfinidadesComFogo(5) : null);
        var cut = await RenderPersonagemAsync();
        await GoToTabAsync(cut, "Atributos & Perícias");

        var linha = cut.Find("button[aria-label='Pôr 1 ponto em Fogo']").Closest("tr")!;
        linha.TextContent.Should().Contain("com Condições: 5");
        linha.QuerySelector("div[role=img]")!.GetAttribute("aria-label").Should().Be("2 de 6 pontos");
    }

    [Fact]
    public async Task The_affinity_shows_no_adjusted_value_when_nothing_changed_it()
    {
        Prepare(r => r.RequestUri!.AbsolutePath.EndsWith("/afinidades") ? AfinidadesComFogo(2) : null);
        var cut = await RenderPersonagemAsync();
        await GoToTabAsync(cut, "Atributos & Perícias");

        cut.Markup.Should().NotContain("com Condições");
    }

    [Fact]
    public async Task Elemental_damage_from_conditions_shows_only_for_the_non_zero_elements()
    {
        // A chave é o id do elemento no catálogo (Fogo = 2, Água = 1, Ar = 3); o nome vem do catálogo.
        Prepare(r => ElementosDeTeste.EhCatalogo(r) ? ElementosDeTeste.Resposta()
            : r.RequestUri!.AbsolutePath.EndsWith("/modificador-de-dano")
            ? Json(new { Cortante = 1, Perfurante = 0, Contundente = 0, Arcano = 0, DanoElemental = new Dictionary<int, int> { [2] = 3, [1] = 0, [3] = -2 } })
            : null);
        var cut = await RenderPersonagemAsync();
        await GoToTabAsync(cut, "Combate");

        cut.Markup.Should().Contain("Dano de Fogo: +3").And.Contain("Dano de Ar: −2").And.NotContain("Dano de Água");
    }

    // ---- recarga ao vivo ----

    // O dono e o GM que tem a ficha aberta recebem o aviso (Condições, ou PV/PF/PA editados no encontro).
    [Theory]
    [InlineData("GM")]
    [InlineData("Jogador")]
    public async Task A_notification_for_this_sheet_reloads_the_sheet_and_the_alert_without_saving(string papel)
    {
        _papel = papel;
        Prepare();
        var cut = await RenderPersonagemAsync();
        var armas = Contagem("/weapons");
        var alertas = Contagem("/condicoes-ativas");
        _requests.Clear();
        _ativas = new() { Atordoado };

        await cut.InvokeAsync(() => NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification("character", "sheet-1")));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Condições ativas"));
        Contagem("/weapons").Should().Be(1);
        Contagem("/condicoes-ativas").Should().Be(1);
        Contagem("/sheet-1").Should().Be(1);
        _requests.Should().NotContain(r => r.StartsWith("PUT ") || r.StartsWith("POST ") || r.StartsWith("DELETE "));
        armas.Should().Be(1);
        alertas.Should().Be(1);
    }

    [Theory]
    [InlineData("character", "outra")]
    [InlineData("npc", "sheet-1")]
    public async Task A_notification_for_another_sheet_is_ignored(string kind, string sheetId)
    {
        Prepare();
        var cut = await RenderPersonagemAsync();
        _requests.Clear();

        await cut.InvokeAsync(() => NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification(kind, sheetId)));
        await Task.Delay(50);

        _requests.Should().BeEmpty();
    }

    [Fact]
    public async Task With_a_field_focused_the_sheet_reload_waits_for_focus_to_leave_but_the_alert_updates()
    {
        JSInterop.Setup<bool>("ruinaFoco.campoEmEdicao").SetResult(true);
        Prepare();
        var cut = await RenderPersonagemAsync();
        _requests.Clear();
        _ativas = new() { Atordoado };

        await cut.InvokeAsync(() => NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification("character", "sheet-1")));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Condições ativas"));
        Contagem("/weapons").Should().Be(0);
        Contagem("/sheet-1").Should().Be(0);

        JSInterop.Setup<bool>("ruinaFoco.campoEmEdicao").SetResult(false);
        await cut.InvokeAsync(() => cut.Find("div.ficha-condicoes-raiz").TriggerEventAsync("onfocusout", new Microsoft.AspNetCore.Components.Web.FocusEventArgs()));

        cut.WaitForAssertion(() => Contagem("/weapons").Should().Be(1), TimeSpan.FromSeconds(5));
        Contagem("/sheet-1").Should().Be(1);
    }

    [Fact]
    public async Task Disposing_the_page_stops_listening()
    {
        Prepare();
        var cut = await RenderPersonagemAsync();
        _requests.Clear();

        await DisposeComponentsAsync();
        await NotificationConnection.RaiseCondicoesAlteradasAsync(new CondicoesAlteradasNotification("character", "sheet-1"));
        await Task.Delay(50);

        _requests.Should().BeEmpty();
    }
}
