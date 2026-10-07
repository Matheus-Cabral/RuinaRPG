using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

/// <summary>
/// Covers Finding 3 of the autosave-on-blur final review: the "Detalhes" tab's SaveIfValidAsync
/// never validated, so clearing "Nome" and blurring auto-saved an empty campaign name.
/// </summary>
public class CampanhaDetalheTests : MudBunitContext
{
    private const string CampaignId = "campaign-1";

    private IRenderedComponent<CampanhaDetalhe> RenderDetalhesTab(Func<HttpRequestMessage, HttpResponseMessage> respond, decimal bonusDeCarga = 0m)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm-user");
        authContext.SetRoles("GM");

        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("campaigns"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null, bonusDeCarga) })
                };
            }

            if (request.Method == HttpMethod.Put)
                return respond(request);

            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CampanhaDetalhe>(p => p.Add(x => x.CampaignId, CampaignId));
        return cut;
    }

    private static void GoToDetalhesTab(IRenderedComponent<CampanhaDetalhe> cut)
    {
        var tabHeader = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Detalhes");
        cut.InvokeAsync(() => tabHeader.Click());
    }

    private static void GoToAnexosTab(IRenderedComponent<CampanhaDetalhe> cut)
    {
        var tabHeader = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == "Anexos");
        cut.InvokeAsync(() => tabHeader.Click());
    }

    /// <summary>
    /// Finding 7 of the final whole-branch review: the "Entrada do Banco de Magias" picker on the
    /// Anexos tab (used to publish a Banco entry, Passiva included, to the campaign) formatted every
    /// result as "Nome (Tipo, Grau N)" — for a Passiva (always Grau 0) that read as the meaningless
    /// "(Passiva, Grau 0)". It must show the Categoria instead, like the rest of the app does for
    /// Passivas (BancoDeMagias list, HabilidadesPassivasSection).
    /// </summary>
    [Fact]
    public async Task Anexos_tab_banco_picker_labels_a_passiva_by_categoria_not_grau()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm-user");
        authContext.SetRoles("GM");

        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("campaigns"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null) })
                };

            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("spell-ability-bank"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[]
                    {
                        new { Id = "p1", Nome = "Pele de Pedra", Tipo = "Passiva", Grau = 0, GastoEmPI = 0, Custo = 0, Descricao = "d", Efeitos = new List<object>(), DeCriatura = false, Categoria = (string?)"Vocacional" },
                        new { Id = "m1", Nome = "Bola de Fogo", Tipo = "Magia", Grau = 2, GastoEmPI = 3, Custo = 4, Descricao = "d", Efeitos = new List<object>(), DeCriatura = false, Categoria = (string?)null },
                    })
                };

            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<CampanhaDetalhe>(p => p.Add(x => x.CampaignId, CampaignId));
        await Task.Delay(100);

        GoToAnexosTab(cut);
        await Task.Delay(50);

        var picker = cut.FindComponents<EntityPicker>().Single(c => c.Instance.Placeholder == "Buscar magia/habilidade...");
        var results = await picker.Instance.SearchAsyncForTests("");

        results.Should().Contain(o => o.Id == "p1" && o.Label == "Pele de Pedra (Passiva Vocacional)");
        results.Should().Contain(o => o.Id == "m1" && o.Label == "Bola de Fogo (Magia, Grau 2)");
    }

    /// <summary>
    /// Renders the page for a GM whose every catalog holds two entries ("a1" already attached to the
    /// campaign, "a2" not), so each Anexos picker can be checked for what it hides.
    /// </summary>
    private IRenderedComponent<CampanhaDetalhe> RenderWithOneAttachmentOfEachKind()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm-user");
        authContext.SetRoles("GM");

        var catalog = new[]
        {
            new { Id = "a1", Nome = "Anexado", Tipo = "Magia", Grau = 1 },
            new { Id = "a2", Nome = "Livre", Tipo = "Magia", Grau = 1 },
        };
        var catalogPaths = new[] { "/items", "/spell-ability-bank", "/rune-bank", "/npc-sheets", "/creature-sheets" };

        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if (path.EndsWith("campaigns"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null) })
                };

            if (path.EndsWith("/attachments"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[] { "Item", "SpellAbilityBankEntry", "RuneBankEntry", "NpcSheet", "CreatureSheet" }
                        .Select(tipo => new CampaignAttachmentResponse($"att-{tipo}", tipo, "Anexado", false, null, null, null, null, null, "a1")))
                };

            if (catalogPaths.Any(path.EndsWith))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(catalog) };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
        });
        Services.AddScoped(_ => http);

        return Render<CampanhaDetalhe>(p => p.Add(x => x.CampaignId, CampaignId));
    }

    private static void GoToTab(IRenderedComponent<CampanhaDetalhe> cut, string text)
    {
        var tabHeader = cut.FindAll("div.mud-tab").Single(e => e.TextContent.Trim() == text);
        cut.InvokeAsync(() => tabHeader.Click());
    }

    /// <summary>
    /// Campanha R0006: um campo de busca da aba Anexos não lista o que já está anexado à campanha.
    /// </summary>
    [Theory]
    [InlineData("Buscar item...")]
    [InlineData("Buscar magia/habilidade...")]
    [InlineData("Buscar runa...")]
    [InlineData("Buscar ficha de NPC...")]
    [InlineData("Buscar ficha de Criatura...")]
    public async Task Anexos_tab_picker_hides_what_is_already_attached_to_the_campaign(string placeholder)
    {
        var cut = RenderWithOneAttachmentOfEachKind();
        await Task.Delay(100);

        GoToAnexosTab(cut);
        await Task.Delay(50);

        var picker = cut.FindComponents<EntityPicker>().Single(c => c.Instance.Placeholder == placeholder);
        var results = await picker.Instance.SearchAsyncForTests("");

        results.Select(o => o.Id).Should().Equal("a2");
    }

    /// <summary>
    /// O id anexado só esconde a entrada do MESMO tipo: um Item e uma Runa são registros de tabelas
    /// diferentes, então um anexo de Item nunca pode esconder uma Runa.
    /// </summary>
    [Fact]
    public async Task Anexos_tab_picker_ignores_attachments_of_another_kind()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm-user");
        authContext.SetRoles("GM");

        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("campaigns"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null) })
                };
            if (path.EndsWith("/attachments"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new[] { new CampaignAttachmentResponse("att-1", "Item", "Anexado", false, null, null, null, null, null, "a1") })
                };
            if (path.EndsWith("/rune-bank"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new { Id = "a1", Nome = "Runa", Grau = 1 } }) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
        });
        Services.AddScoped(_ => http);

        var cut = Render<CampanhaDetalhe>(p => p.Add(x => x.CampaignId, CampaignId));
        await Task.Delay(100);

        GoToAnexosTab(cut);
        await Task.Delay(50);

        var picker = cut.FindComponents<EntityPicker>().Single(c => c.Instance.Placeholder == "Buscar runa...");
        var results = await picker.Instance.SearchAsyncForTests("");

        results.Select(o => o.Id).Should().Equal("a1");
    }

    /// <summary>
    /// O campo "ficha existente" de Conceder Ficha reaproveita a busca de NPC/Criatura, mas conceder
    /// não é anexar: uma ficha já anexada à campanha continua podendo ser concedida.
    /// </summary>
    [Fact]
    public async Task Conceder_ficha_picker_still_lists_a_sheet_already_attached_to_the_campaign()
    {
        var cut = RenderWithOneAttachmentOfEachKind();
        await Task.Delay(100);

        GoToTab(cut, "Conceder Ficha");
        await Task.Delay(50);

        var picker = cut.FindComponents<EntityPicker>().Single(c => c.Instance.Placeholder.StartsWith("Buscar ficha existente"));
        var results = await picker.Instance.SearchAsyncForTests("");

        results.Select(o => o.Id).Should().Equal("a1", "a2");
    }

    /// <summary>
    /// Renders the Anexos tab of a campaign whose attachments are the given ones, recording every
    /// non-GET request the page makes.
    /// </summary>
    private async Task<(IRenderedComponent<CampanhaDetalhe> Cut, List<string> Escritas)> RenderAnexosTabAsync(params CampaignAttachmentResponse[] anexos)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("gm-user");
        authContext.SetRoles("GM");

        var escritas = new List<string>();
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method != HttpMethod.Get)
            {
                escritas.Add($"{request.Method} {path}");
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (path.EndsWith("campaigns"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<CampaignResponse> { new(CampaignId, "Campanha Original", "Descrição", null) })
                };
            if (path.EndsWith("/attachments"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(anexos) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
        }));

        var cut = Render<CampanhaDetalhe>(p => p.Add(x => x.CampaignId, CampaignId));
        await Task.Delay(100);
        GoToAnexosTab(cut);
        await Task.Delay(50);
        return (cut, escritas);
    }

    /// <summary>
    /// Campanha R0006: a aba Anexos mostra os anexos agrupados por tipo e filtráveis. Um NPC ou uma
    /// Criatura conta como "público" no filtro de visibilidade quando o Nome ou a Imagem é público.
    /// </summary>
    [Fact]
    public async Task Anexos_tab_hands_the_attachments_to_the_grouped_list_with_their_facets_and_visibility()
    {
        var facets = new AttachmentFacets(ItemTipo: "Arma", Subcategoria: "Espadas");
        var (cut, _) = await RenderAnexosTabAsync(
            new CampaignAttachmentResponse("a1", "Item", "Espada", true, null, null, null, null, "/images/e.png", "i1", facets),
            new CampaignAttachmentResponse("a2", "NpcSheet", "Ferreiro", null, false, true, null, null, null, "n1"),
            new CampaignAttachmentResponse("a3", "NpcSheet", "Espião", null, false, false, null, null, null, "n2"),
            new CampaignAttachmentResponse("a4", "CreatureSheet", "Lobo", null, null, null, true, false, null, "c1"));

        var lista = cut.FindComponent<AnexosAgrupados>().Instance;

        lista.FiltrarPorVisibilidade.Should().BeTrue();
        lista.Anexos.Should().BeEquivalentTo(new[]
        {
            new AnexoView("a1", "Item", "Espada", "/images/e.png", facets, true),
            new AnexoView("a2", "NpcSheet", "Ferreiro", null, null, true),
            new AnexoView("a3", "NpcSheet", "Espião", null, null, false),
            new AnexoView("a4", "CreatureSheet", "Lobo", null, null, true),
        });
    }

    [Fact]
    public async Task Anexos_tab_keeps_each_kind_of_visibility_toggle_in_the_row_of_its_attachment()
    {
        var (cut, escritas) = await RenderAnexosTabAsync(
            new CampaignAttachmentResponse("a1", "Item", "Espada", false, null, null, null, null, null, "i1"),
            new CampaignAttachmentResponse("a2", "NpcSheet", "Ferreiro", null, false, false, null, null, null, "n1"),
            new CampaignAttachmentResponse("a4", "CreatureSheet", "Lobo", null, null, null, false, false, null, "c1"));

        var caixas = cut.FindComponents<MudBlazor.MudCheckBox<bool>>();
        caixas.Select(c => c.Instance.Label).Should().Equal("Público", "Nome público", "Imagem pública", "Nome público", "Imagem pública");

        await cut.InvokeAsync(() => caixas[0].Instance.ValueChanged.InvokeAsync(true));
        await cut.InvokeAsync(() => caixas[2].Instance.ValueChanged.InvokeAsync(true));
        await cut.InvokeAsync(() => caixas[3].Instance.ValueChanged.InvokeAsync(true));

        escritas.Should().Equal(
            $"PUT /api/campaigns/{CampaignId}/attachments/a1/visibility",
            $"PUT /api/campaigns/{CampaignId}/attachments/a2/npc-visibility",
            $"PUT /api/campaigns/{CampaignId}/attachments/a4/creature-visibility");
    }

    [Fact]
    public async Task Anexos_tab_removes_the_attachment_of_the_row_whose_Remover_is_clicked()
    {
        var (cut, escritas) = await RenderAnexosTabAsync(
            new CampaignAttachmentResponse("a1", "Item", "Espada", false, null, null, null, null, null, "i1"),
            new CampaignAttachmentResponse("a2", "Item", "Corda", false, null, null, null, null, null, "i2"));

        // Em ordem alfabética: Corda (a2), depois Espada (a1).
        var remover = cut.FindAll(".anexo-acoes button").First();
        await cut.InvokeAsync(() => remover.Click());

        escritas.Should().Equal($"DELETE /api/campaigns/{CampaignId}/attachments/a2");
    }

    [Fact]
    public async Task Clearing_Nome_and_blurring_does_not_call_PUT()
    {
        var putCalled = false;
        var cut = RenderDetalhesTab(request =>
        {
            putCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await Task.Delay(100); // let OnInitializedAsync finish populating _detailsForm

        GoToDetalhesTab(cut);
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync(""));

        await Task.Delay(700); // past the 400ms debounce

        putCalled.Should().BeFalse("an empty Nome violates [Required(AllowEmptyStrings = false)] and must not reach the server");
    }

    [Fact]
    public async Task Setting_a_valid_Nome_and_blurring_calls_PUT()
    {
        var putCalled = false;
        var cut = RenderDetalhesTab(request =>
        {
            putCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await Task.Delay(100);

        GoToDetalhesTab(cut);
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Nova Campanha"));

        await Task.Delay(700);

        putCalled.Should().BeTrue("a non-empty Nome passes validation and the field blur must trigger the auto-save PUT");
    }

    [Fact]
    public async Task Detalhes_tab_shows_the_loaded_BonusDeCarga_with_its_info_popup()
    {
        var cut = RenderDetalhesTab(_ => new HttpResponseMessage(HttpStatusCode.OK), bonusDeCarga: -4m);
        await Task.Delay(100);

        GoToDetalhesTab(cut);
        await Task.Delay(50);

        cut.Markup.Should().Contain("Bônus de carga dos personagens");
        cut.FindAll("input").Select(i => i.GetAttribute("value")).Should().Contain("-4");
        cut.FindAll("button[title='Bônus de carga']").Should().HaveCount(1);
    }

    [Fact]
    public async Task Editing_the_BonusDeCarga_autosaves_with_the_new_value()
    {
        string? body = null;
        var cut = RenderDetalhesTab(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await Task.Delay(100);
        GoToDetalhesTab(cut);
        await Task.Delay(50);

        var field = cut.FindComponents<MudBlazor.MudNumericField<decimal>>().Single(c => c.Instance.Label == "Bônus de carga dos personagens");
        await cut.InvokeAsync(() => field.Instance.ValueChanged.InvokeAsync(12.5m));
        await Task.Delay(700);

        body.Should().NotBeNull("changing the bonus must trigger the autosave PUT");
        body.Should().Contain("\"bonusDeCarga\":12.5");
    }
}
