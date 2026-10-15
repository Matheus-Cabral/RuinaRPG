using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class AplicarCondicaoDialogTests : MudBunitContext
{
    private static readonly CondicaoResponse Veneno = new("c1", "Veneno", "Corrói a vitalidade.", "Veneno", 3,
        new()
        {
            new("Atributo", "Vigor", "Fixo", -2, null),
            new("RecursoAtual", "Vitalidade", "Fixo", -5, "ACadaVez"),
        },
        new() { "Arma" });

    private static readonly CondicaoResponse Bencao = new("c2", "Bênção", "", "Bencao", null,
        new() { new("Atributo", "Forca", "Fixo", 1, null) }, new());

    private static readonly CondicaoResponse Treino = new("c3", "Treino", "", "Bencao", null,
        new() { new("Pericia", null, "Fixo", 1, null), new("Maestria", null, "Fixo", 2, null), new("Maestria", "luta", "Fixo", 1, null) }, new());

    private readonly List<string> _requests = new();
    private AplicarCondicaoRequest? _corpo;
    private HttpResponseMessage? _respostaDoPost;
    private bool _itensFalham;

    private async Task<(IRenderedComponent<ContainerFragment> Cut, IDialogReference Dialog)> AbrirAsync()
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath.Replace("/api/", "");
            _requests.Add($"{request.Method} {path}");
            if (request.Method == HttpMethod.Post)
            {
                _corpo = request.Content!.ReadFromJsonAsync<AplicarCondicaoRequest>().GetAwaiter().GetResult();
                return _respostaDoPost ?? new HttpResponseMessage(HttpStatusCode.OK);
            }
            if (path == "condicoes")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<CondicaoResponse> { Veneno, Bencao, Treino }) };
            if (path == "encounters/e1/participants/p1/itens-desabilitaveis" && _itensFalham)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            if (path == "encounters/e1/participants/p1/itens-desabilitaveis")
            {
                var vazia = new List<ItemDesabilitavelDto>();
                var categorias = new[] { "Arma", "Armadura", "Escudo", "Artefato", "Magia", "Habilidade", "Runa", "Passiva" }
                    .ToDictionary(c => c, _ => vazia);
                categorias["Arma"] = new() { new("w1", "Espada"), new("w2", "Adaga") };
                var maestrias = new List<MaestriaDaFichaDto> { new("m1", "Espadas", "atletismo"), new("m2", "Lanças", "atletismo"), new("m3", "Socos", "luta") };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ItensDesabilitaveisResponse(categorias, maestrias)) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) };
        }));

        var cut = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
        });
        var dialogs = Services.GetRequiredService<IDialogService>();
        IDialogReference? reference = null;
        await cut.InvokeAsync(async () => reference = await dialogs.ShowAsync<AplicarCondicaoDialog>("Adicionar condição",
            new DialogParameters<AplicarCondicaoDialog> { { x => x.EncounterId, "e1" }, { x => x.ParticipantId, "p1" } }));
        cut.WaitForAssertion(() => _requests.Should().Contain("GET encounters/e1/participants/p1/itens-desabilitaveis"));
        return (cut, reference!);
    }

    private static async Task EscolherAsync(IRenderedComponent<ContainerFragment> cut, CondicaoResponse condicao)
    {
        var autocomplete = cut.FindComponent<MudAutocomplete<CondicaoResponse>>();
        await cut.InvokeAsync(() => autocomplete.Instance.ValueChanged.InvokeAsync(condicao));
    }

    [Fact]
    public async Task Loads_the_catalog_and_the_participants_rows_when_opened()
    {
        await AbrirAsync();

        _requests.Should().Contain("GET condicoes").And.Contain("GET encounters/e1/participants/p1/itens-desabilitaveis");
    }

    [Fact]
    public async Task Choosing_a_condition_shows_its_description_duration_and_lines()
    {
        var (cut, _) = await AbrirAsync();

        await EscolherAsync(cut, Veneno);

        cut.Markup.Should().Contain("Corrói a vitalidade.");
        cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Duração (turnos)").Find("input").GetAttribute("value").Should().Be("3");
        cut.Markup.Should().Contain("Vigor −2").And.Contain("a cada vez");
        cut.FindComponents<MudNumericField<int>>().Select(c => c.Find("input").GetAttribute("value")).Should().Equal("-2", "-5");
    }

    [Fact]
    public async Task Apply_sends_the_default_duration_and_no_overrides_when_nothing_was_changed()
    {
        var (cut, dialog) = await AbrirAsync();
        await EscolherAsync(cut, Veneno);

        cut.Find("button:contains('Aplicar')").Click();
        var resultado = await dialog.Result;

        _requests.Should().Contain("POST encounters/e1/participants/p1/condicoes");
        _corpo.Should().Be(new AplicarCondicaoRequest("c1", 3, null, null));
        resultado!.Canceled.Should().BeFalse();
    }

    [Fact]
    public async Task Apply_sends_the_adjusted_values_by_line_index_the_duration_and_the_chosen_items()
    {
        var (cut, dialog) = await AbrirAsync();
        await EscolherAsync(cut, Veneno);

        cut.FindComponents<MudNumericField<int>>()[1].Find("input").Change("-8");
        cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Duração (turnos)").Find("input").Change("5");
        cut.FindComponents<MudSwitch<bool>>().Single(c => c.Instance.Label == "Toda a categoria").Find("input").Change(false);
        var itens = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Itens desabilitados: Armas");
        await cut.InvokeAsync(() => itens.Instance.SelectedValuesChanged.InvokeAsync(new[] { "w2" }));
        cut.Find("button:contains('Aplicar')").Click();
        await dialog.Result;

        _corpo!.CondicaoId.Should().Be("c1");
        _corpo.DuracaoTurnos.Should().Be(5);
        _corpo.ValoresAjustados.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = -8 });
        _corpo.ItensDesabilitados.Should().BeEquivalentTo(new Dictionary<string, List<string>> { ["Arma"] = new() { "w2" } });
    }

    [Fact]
    public async Task Clearing_the_duration_sends_none()
    {
        var (cut, dialog) = await AbrirAsync();
        await EscolherAsync(cut, Veneno);

        cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Duração (turnos)").Find("input").Change("");
        cut.Find("button:contains('Aplicar')").Click();
        await dialog.Result;

        _corpo!.DuracaoTurnos.Should().BeNull();
    }

    [Fact]
    public async Task Choosing_specific_items_without_picking_any_is_refused_without_a_request()
    {
        var (cut, _) = await AbrirAsync();
        await EscolherAsync(cut, Veneno);
        cut.FindComponents<MudSwitch<bool>>().Single(c => c.Instance.Label == "Toda a categoria").Find("input").Change(false);

        cut.Find("button:contains('Aplicar')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Escolha ao menos um item de Armas"));
        _requests.Should().NotContain("POST encounters/e1/participants/p1/condicoes");
    }

    [Fact]
    public async Task A_duplicate_is_refused_with_the_api_message_and_the_dialog_stays_open()
    {
        _respostaDoPost = new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("Esta condição já está neste participante.") };
        var (cut, _) = await AbrirAsync();
        await EscolherAsync(cut, Bencao);

        cut.Find("button:contains('Aplicar')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Esta condição já está neste participante."));
        cut.FindAll("button:contains('Aplicar')").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Apply_is_disabled_until_a_condition_is_chosen()
    {
        var (cut, _) = await AbrirAsync();

        cut.Find("button:contains('Aplicar')").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Sem_duracao_sends_the_flag_with_no_duration_and_disables_the_duration_field()
    {
        var (cut, dialog) = await AbrirAsync();
        await EscolherAsync(cut, Veneno);

        cut.FindComponents<MudCheckBox<bool>>().Single(c => c.Instance.Label == "Sem duração").Find("input").Change(true);
        cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Duração (turnos)").Find("input").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button:contains('Aplicar')").Click();
        await dialog.Result;

        _corpo.Should().Be(new AplicarCondicaoRequest("c1", null, null, null, true));
    }

    [Fact]
    public async Task Without_sem_duracao_the_flag_is_false()
    {
        var (cut, dialog) = await AbrirAsync();
        await EscolherAsync(cut, Veneno);

        cut.Find("button:contains('Aplicar')").Click();
        await dialog.Result;

        _corpo!.SemDuracao.Should().BeFalse();
    }

    [Fact]
    public async Task When_the_participants_rows_fail_to_load_the_error_shows_and_a_disabling_condition_cannot_be_applied()
    {
        _itensFalham = true;
        var (cut, _) = await AbrirAsync();
        await EscolherAsync(cut, Veneno);

        cut.Markup.Should().Contain("Não foi possível carregar os itens da ficha deste participante.");
        cut.FindComponents<MudSwitch<bool>>().Should().BeEmpty();
        cut.Find("button:contains('Aplicar')").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task When_the_participants_rows_fail_to_load_a_condition_that_disables_nothing_can_still_be_applied()
    {
        _itensFalham = true;
        var (cut, _) = await AbrirAsync();
        await EscolherAsync(cut, Bencao);

        cut.Find("button:contains('Aplicar')").HasAttribute("disabled").Should().BeFalse();
    }

    // ---- Maestria restrita na aplicação ----

    private static IRenderedComponent<MudSwitch<bool>> TodasAsMaestrias(IRenderedComponent<ContainerFragment> cut, int linha) =>
        cut.FindComponents<MudSwitch<bool>>().Single(c => c.Instance.Label == $"Todas as Maestrias (linha {linha})");

    [Fact]
    public async Task A_Maestria_line_defaults_to_every_mastery_and_sends_no_choice()
    {
        var (cut, dialog) = await AbrirAsync();
        await EscolherAsync(cut, Treino);

        ((AngleSharp.Html.Dom.IHtmlInputElement)TodasAsMaestrias(cut, 2).Find("input")).IsChecked.Should().BeTrue();
        cut.Find("button:contains('Aplicar')").Click();
        await dialog.Result;

        _corpo!.MaestriasEscolhidas.Should().BeNull();
    }

    [Fact]
    public async Task Choosing_specific_masteries_sends_them_by_line_index()
    {
        var (cut, dialog) = await AbrirAsync();
        await EscolherAsync(cut, Treino);

        TodasAsMaestrias(cut, 2).Find("input").Change(false);
        var select = cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Maestrias da linha 2");
        await cut.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync(new[] { "m2" }));
        cut.Find("button:contains('Aplicar')").Click();
        await dialog.Result;

        _corpo!.MaestriasEscolhidas.Should().BeEquivalentTo(new Dictionary<int, List<string>> { [1] = new() { "m2" } });
    }

    [Fact]
    public async Task A_line_of_one_Pericia_lists_only_that_Pericias_masteries()
    {
        var (cut, _) = await AbrirAsync();
        await EscolherAsync(cut, Treino);

        TodasAsMaestrias(cut, 2).Find("input").Change(false);
        TodasAsMaestrias(cut, 3).Find("input").Change(false);

        cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Maestrias da linha 2")
            .FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Equal("m1", "m2", "m3");
        cut.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == "Maestrias da linha 3")
            .FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Equal("m3");
    }

    [Fact]
    public async Task Specific_masteries_without_picking_any_is_refused_without_a_request()
    {
        var (cut, _) = await AbrirAsync();
        await EscolherAsync(cut, Treino);
        TodasAsMaestrias(cut, 2).Find("input").Change(false);

        cut.Find("button:contains('Aplicar')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Escolha ao menos uma Maestria na linha 2"));
        _requests.Should().NotContain("POST encounters/e1/participants/p1/condicoes");
    }
}
