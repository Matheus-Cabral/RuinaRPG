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
/// Task 5 ("Client — Rank in the Catálogo and Inquebrável on the sheets"): the "Combate" tab's
/// Armas/Armaduras/Escudos tables on the 3 sheets (Personagem, NPC, Criatura) now render
/// "Inquebrável" (no Durabilidade atual input) for an unbreakable item, and the ordinary
/// atual/max pair otherwise — the arsenal responses carry a trailing `Inquebravel` flag (Task 4).
/// Renders the full sheet page (its OnInitializedAsync loads dozens of endpoints regardless of
/// which tab is visible) with every endpoint but weapons/armor-slots/shields stubbed to an inert
/// default, then switches to the "Combate" tab to reach the tables under test.
/// </summary>
public class FichaArsenalDurabilidadeTests : MudBunitContext
{
    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "GM") }, "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = JsonContent.Create(body) };

    // Every GET the page's OnInitializedAsync fires that this test doesn't care about gets a
    // harmless default: a single-object endpoint (attributes/budget, sub-attributes,
    // modificador-de-dano, racial-ability, traits, racial-traits/pending, and the sheet root
    // itself) gets "{}" (an instance with every property defaulted — every markup site that reads
    // one of these is null-guarded with "is not null" in all 3 Ficha pages, so defaults render
    // nothing); everything else (every List<T> endpoint) gets "[]". level-up-notice gets 404 so
    // _levelUpBonuses is left at its default empty list instead of being overwritten with null.
    private static readonly string[] SingleObjectSuffixes =
    {
        "/attributes/budget", "/skills/budget", "/sub-attributes", "/modificador-de-dano",
        "/racial-ability", "/racial-traits/pending", "/traits",
    };

    private static HttpResponseMessage DefaultStub(HttpRequestMessage request)
    {
        if (request.Method != HttpMethod.Get)
            return new HttpResponseMessage(HttpStatusCode.OK);

        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/level-up-notice"))
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (Regex.IsMatch(path, "-sheets/[^/]+$"))
            return Json(HttpStatusCode.OK, new { }); // the sheet root itself (…/character-sheets/{id})
        if (SingleObjectSuffixes.Any(path.EndsWith))
            return Json(HttpStatusCode.OK, new { });
        return Json(HttpStatusCode.OK, new List<object>());
    }

    private HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage?> overrides)
    {
        var http = FakeHttpMessageHandler.CreateClient(request => overrides(request) ?? DefaultStub(request));
        Services.AddScoped(_ => http);
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());
        return http;
    }

    /// <summary>MudTabs only renders the active panel's content — switch to "Combate" (the 3rd tab
    /// on all 3 sheets) to reach the Armas/Armaduras/Escudos tables.</summary>
    private static async Task GoToCombateTabAsync<T>(IRenderedComponent<T> cut) where T : Microsoft.AspNetCore.Components.IComponent
    {
        var combateTab = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Combate");
        await cut.InvokeAsync(() => combateTab.Click());
    }

    private static object Weapon(bool inquebravel, string id = "weapon-1") => new
    {
        Id = id, ItemId = "item-1", Nome = "Espada Longa", TipoDeDano = "Cortante", Alcance = (int?)null,
        Dados = "1d8", Dano = (int?)null, Critico = "19-20", Rank = "D", Peso = 3m, IsEquipped = false,
        DurabilidadeAtual = inquebravel ? 0 : 40, DurabilidadeMaxima = inquebravel ? 0 : 80,
        ImageUrl = (string?)null, Descricao = (string?)null, Inquebravel = inquebravel,
    };

    private static object CreatureWeapon(bool inquebravel, string? itemId = "item-1", string id = "weapon-1") => new
    {
        Id = id, ItemId = itemId, Nome = "Garra", TipoDeDano = "Cortante", Dados = "1d6", Dano = (int?)null,
        Alcance = (int?)null, Critico = "19-20", Rank = "D", IsEquipped = false,
        DurabilidadeAtual = itemId is null ? (int?)null : inquebravel ? 0 : 40,
        DurabilidadeMaximo = itemId is null ? (int?)null : inquebravel ? 0 : 80,
        ImageUrl = (string?)null, Descricao = (string?)null, Inquebravel = inquebravel,
    };

    private static object ArmorSlot(bool inquebravel, string slot = "Superior") => new
    {
        Slot = slot, ItemId = "item-2", Nome = "Cota de Malha", Categoria = "Média", Defesa = (int?)2,
        RF = (int?)1, RM = (int?)0, Peso = (decimal?)8m,
        DurabilidadeAtual = inquebravel ? 0 : 30, DurabilidadeMaxima = inquebravel ? 0 : 60,
        ImageUrl = (string?)null, Descricao = (string?)null, Inquebravel = inquebravel,
    };

    private static object CreatureArmorSlot(bool inquebravel, string slot = "Superior") => new
    {
        Slot = slot, ItemId = "item-2", Nome = "Cota de Malha", Categoria = "Média", Defesa = (int?)2,
        RF = (int?)1, RM = (int?)0, Peso = (decimal?)8m,
        DurabilidadeAtual = inquebravel ? 0 : 30, DurabilidadeMaximo = inquebravel ? 0 : 60,
        ImageUrl = (string?)null, Descricao = (string?)null, Inquebravel = inquebravel,
    };

    private static object Shield(bool inquebravel, string id = "shield-1") => new
    {
        Id = id, ItemId = "item-3", Nome = "Broquel", Categoria = "Leve", BonusDefesa = (int?)1,
        Peso = 2m, IsEquipped = false,
        DurabilidadeAtual = inquebravel ? 0 : 15, DurabilidadeMaxima = inquebravel ? 0 : 30,
        ImageUrl = (string?)null, Descricao = (string?)null, Inquebravel = inquebravel,
    };

    // ---- FichaDePersonagem ----

    [Fact]
    public async Task Personagem_weapon_table_shows_Inquebravel_and_no_atual_input_for_an_unbreakable_weapon()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/weapons")) return Json(HttpStatusCode.OK, new[] { Weapon(inquebravel: true) });
            return null;
        });

        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        cut.Markup.Should().Contain(">Rank<");
        cut.Markup.Should().NotContain(">Tier<");
        var weaponSection = ExtractSection(cut.Markup, "Armas");
        weaponSection.Should().Contain("Inquebrável").And.NotContain("mud-input-slot");
    }

    [Fact]
    public async Task Personagem_weapon_table_shows_atual_over_max_for_a_breakable_weapon()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/weapons")) return Json(HttpStatusCode.OK, new[] { Weapon(inquebravel: false) });
            return null;
        });

        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        cut.Markup.Should().Contain("/ 80");
        cut.Markup.Should().NotContain("Inquebrável");
    }

    [Fact]
    public async Task Personagem_armor_slot_shows_Inquebravel_and_no_atual_input()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/armor-slots")) return Json(HttpStatusCode.OK, new[] { ArmorSlot(inquebravel: true) });
            return null;
        });

        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        var armorSection = ExtractSection(cut.Markup, "Armaduras");
        armorSection.Should().Contain("Inquebrável").And.NotContain("mud-input-slot");
    }

    [Fact]
    public async Task Personagem_shield_shows_Inquebravel_and_no_atual_input()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/shields")) return Json(HttpStatusCode.OK, new[] { Shield(inquebravel: true) });
            return null;
        });

        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        var shieldSection = ExtractSection(cut.Markup, "Escudos");
        shieldSection.Should().Contain("Inquebrável").And.NotContain("mud-input-slot");
    }

    // ---- FichaDeNpc ----

    [Fact]
    public async Task Npc_weapon_table_shows_Inquebravel_and_no_atual_input_for_an_unbreakable_weapon()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/weapons")) return Json(HttpStatusCode.OK, new[] { Weapon(inquebravel: true) });
            return null;
        });

        var cut = Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        var weaponSection = ExtractSection(cut.Markup, "Armas");
        weaponSection.Should().Contain("Inquebrável").And.NotContain("mud-input-slot");
    }

    [Fact]
    public async Task Npc_weapon_table_shows_atual_over_max_for_a_breakable_weapon()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/weapons")) return Json(HttpStatusCode.OK, new[] { Weapon(inquebravel: false) });
            return null;
        });

        var cut = Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        cut.Markup.Should().Contain("/ 80");
        cut.Markup.Should().NotContain("Inquebrável");
    }

    // ---- FichaDeCriatura ----

    [Fact]
    public async Task Criatura_weapon_table_shows_Inquebravel_for_an_unbreakable_item_weapon()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/weapons")) return Json(HttpStatusCode.OK, new[] { CreatureWeapon(inquebravel: true) });
            return null;
        });

        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        var weaponSection = ExtractSection(cut.Markup, "Armas");
        weaponSection.Should().Contain("Inquebrável").And.NotContain("mud-input-slot");
    }

    [Fact]
    public async Task Criatura_weapon_table_still_shows_Ataque_natural_when_there_is_no_ItemId()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/weapons")) return Json(HttpStatusCode.OK, new[] { CreatureWeapon(inquebravel: false, itemId: null) });
            return null;
        });

        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        var weaponSection = ExtractSection(cut.Markup, "Armas");
        weaponSection.Should().Contain("Ataque natural").And.NotContain("Inquebrável");
    }

    [Fact]
    public async Task Criatura_armor_slot_shows_Inquebravel_and_no_atual_input()
    {
        CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/armor-slots")) return Json(HttpStatusCode.OK, new[] { CreatureArmorSlot(inquebravel: true) });
            return null;
        });

        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await GoToCombateTabAsync(cut);

        var armorSection = ExtractSection(cut.Markup, "Armaduras");
        armorSection.Should().Contain("Inquebrável").And.NotContain("mud-input-slot");
    }

    /// <summary>Crude but effective for these assertions: cuts the markup down to the &lt;tbody&gt;
    /// of the first table following the given Section's card-header text — deliberately excluding
    /// that section's own "Adicionar ..." form (whose EntityPicker/CatalogoItemPicker autocomplete
    /// also renders a "mud-input-slot" input, which would otherwise make a "no atual input"
    /// assertion pass for the wrong reason) and any other table on the page.</summary>
    private static string ExtractSection(string markup, string sectionTitle)
    {
        var start = markup.IndexOf($">{sectionTitle}<", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"the '{sectionTitle}' section should be in the rendered markup");
        var bodyStart = markup.IndexOf("<tbody>", start, StringComparison.Ordinal);
        bodyStart.Should().BeGreaterThan(-1);
        var bodyEnd = markup.IndexOf("</tbody>", bodyStart, StringComparison.Ordinal);
        bodyEnd.Should().BeGreaterThan(-1);
        return markup[bodyStart..(bodyEnd + "</tbody>".Length)];
    }
}
