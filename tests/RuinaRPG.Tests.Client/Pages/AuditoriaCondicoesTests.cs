using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class AuditoriaCondicoesTests : MudBunitContext
{
    private static readonly PericiaResponse[] Pericias =
    [
        new(1, "Atletismo", "Atletismo", null, null, true, false),
        new(2, "Furtividade", "Furtividade", null, null, true, false)
    ];

    private readonly List<(HttpMethod Method, string Path, string Body)> _calls = new();
    private List<CondicaoResponse> _catalogo = new();
    private bool _auditor = true;
    private Task? _meGate;
    private bool _meFalha;

    private sealed class AsyncHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
    private Func<HttpMethod, HttpResponseMessage?> _escrita = _ => null;

    private static CondicaoResponse Veneno() => new("c1", "Veneno", "Perde vida", "Veneno", 3,
        [new CondicaoModificadorDto("RecursoTotal", "Vitalidade", "Fixo", 10, null)], ["Arma"]);

    private IRenderedComponent<ContainerFragment> RenderPage()
    {
        var http = new HttpClient(new AsyncHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/auth/me"))
            {
                if (_meGate is not null)
                    await _meGate;
                if (_meFalha)
                    throw new HttpRequestException("sem rede");
            }
            var body = request.Content is null ? "" : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (request.Method != HttpMethod.Get)
                _calls.Add((request.Method, path, body));
            if (request.Method == HttpMethod.Get && path.EndsWith("/auth/me"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MeResponse("u1", "Usuario", "GM", _auditor, null, false)) };
            if (ElementosDeTeste.EhCatalogo(request))
                return ElementosDeTeste.Resposta();
            if (request.Method == HttpMethod.Get && path.EndsWith("/pericias"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Pericias) };
            if (request.Method == HttpMethod.Get && path.EndsWith("/condicoes"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_catalogo) };
            return _escrita(request.Method) ?? new HttpResponseMessage(HttpStatusCode.OK);
        })) { BaseAddress = new Uri("http://localhost/api/") };
        Services.AddScoped(_ => http);

        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaCondicoes>(2);
            builder.CloseComponent();
        });
    }

    private static async Task<IRenderedComponent<ContainerFragment>> AbrirNovaAsync(IRenderedComponent<ContainerFragment> cut)
    {
        await Task.Delay(50);
        cut.FindAll("button").First(b => b.TextContent.Contains("Nova condição")).Click();
        return cut;
    }

    private static void AdicionarLinha(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar linha")).Click();

    private static IRenderedComponent<MudSelect<string>> Select(IRenderedComponent<ContainerFragment> cut, string label, int indice = 0) =>
        cut.FindComponents<MudSelect<string>>().Where(c => c.Instance.Label == label).ElementAt(indice);

    private static async Task Escolher(IRenderedComponent<ContainerFragment> cut, string label, int indice, string valor)
    {
        var select = Select(cut, label, indice);
        await cut.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(valor));
    }

    // O valor que o select guarda (no bUnit os itens só existem com o popover aberto, então o input mostra o valor, não o rótulo); ler o parâmetro Value de fora é proibido pelo analisador do MudBlazor.
    private static string Escolha(IRenderedComponent<ContainerFragment> cut, string label, int indice = 0) =>
        Select(cut, label, indice).Find("input").GetAttribute("value") ?? "";

    private static List<string> Opcoes(IRenderedComponent<ContainerFragment> cut, string label, int indice = 0)
    {
        var controle = Select(cut, label, indice).Find(".mud-input-control");
        controle.MouseDown();
        var textos = cut.FindAll(".mud-list-item").Select(li => li.TextContent.Trim()).ToList();
        controle.MouseDown(); // fecha, para o próximo select abrir limpo
        return textos;
    }

    private static async Task Digitar(IRenderedComponent<ContainerFragment> cut, string label, int indice, int valor)
    {
        var campo = cut.FindComponents<MudNumericField<int>>().Where(c => c.Instance.Label == label).ElementAt(indice);
        await cut.InvokeAsync(() => campo.Instance.ValueChanged.InvokeAsync(valor));
    }

    [Fact]
    public async Task Title_is_Auditoria_Condicoes_and_the_info_popup_says_it_is_not_the_Efeitos_of_a_Magia()
    {
        var cut = RenderPage();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Auditoria: Condições");
        cut.Find("button[aria-label='O que são as Condições']").Click();
        var texto = TextNormalization.Collapse(cut.Find(".mud-dialog-content").TextContent);
        texto.Should().Contain("durante um encontro").And.Contain("Efeitos").And.Contain("em andamento")
            .And.Contain("Valor atual de um recurso (dano ou cura por vez)").And.Contain("Total (máximo) de um recurso")
            .And.Contain("Modificador de dano dos ataques").And.Contain("não a Vitalidade dela")
            .And.Contain("painel Ícone");
    }

    [Fact]
    public async Task The_icon_picker_is_a_collapsed_panel_whose_header_shows_the_chosen_icon()
    {
        var cut = await AbrirNovaAsync(RenderPage());

        var cabecalho = cut.Find(".mud-expand-panel-header");
        TextNormalization.Collapse(cabecalho.TextContent).Should().Contain("Ícone").And.Contain("Marcado");
        cut.Find(".mud-expand-panel").ClassList.Should().NotContain("mud-panel-expanded", "o painel começa fechado");

        cabecalho.Click();
        cut.Find(".mud-expand-panel").ClassList.Should().Contain("mud-panel-expanded");
        cut.Find("button[aria-label='Fogo']").Click();

        TextNormalization.Collapse(cut.Find(".mud-expand-panel-header").TextContent).Should().Contain("Fogo").And.NotContain("Marcado");
        cut.Find(".mud-expand-panel").ClassList.Should().Contain("mud-panel-expanded", "escolher um ícone mantém o painel aberto");
    }

    [Fact]
    public async Task A_403_on_save_shows_the_forbidden_alert_instead_of_the_page()
    {
        _escrita = _ => new HttpResponseMessage(HttpStatusCode.Forbidden);
        var cut = await AbrirNovaAsync(RenderPage());
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First(c => c.Instance.Label == "Nome").Instance.ValueChanged.InvokeAsync("X"));
        var categorias = Select(cut, "Categorias desabilitadas");
        await cut.InvokeAsync(() => categorias.Instance.SelectedValuesChanged.InvokeAsync(new[] { "Arma" }));
        cut.FindAll("button").First(b => b.TextContent.Contains("Salvar")).Click();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Você não é o Auditor de Regras designado");
        cut.FindAll("button").Where(b => b.TextContent.Contains("Nova condição")).Should().BeEmpty();
    }

    [Fact]
    public async Task The_especifico_select_follows_the_Alvo()
    {
        var cut = await AbrirNovaAsync(RenderPage());
        AdicionarLinha(cut);

        // Alvo padrão: Atributo.
        var atributos = Opcoes(cut, "Específico");
        atributos.Should().HaveCount(8).And.Contain(["Força", "Astúcia", "Influência"]);

        await Escolher(cut, "Alvo", 0, "Maestria");
        Opcoes(cut, "Específico").Should().Equal("Todas", "Atletismo", "Furtividade");

        await Escolher(cut, "Alvo", 0, "DanoElemental");
        // Os elementos do catálogo que não são Caminho, na ordem do catálogo (os ids).
        Opcoes(cut, "Específico").Should().Equal(["Todos", .. ElementosDeTeste.Semeados.Where(e => e.Tipo != "Caminho").Select(e => e.Nome)]);
    }

    [Fact]
    public async Task Momento_appears_only_for_the_current_value_of_a_resource()
    {
        var cut = await AbrirNovaAsync(RenderPage());
        AdicionarLinha(cut);
        cut.FindComponents<MudSelect<string>>().Where(c => c.Instance.Label == "Momento").Should().BeEmpty();

        await Escolher(cut, "Alvo", 0, "RecursoAtual");
        Escolha(cut, "Momento").Should().Be("AoAplicar");
        Escolha(cut, "Específico").Should().Be("Vitalidade");

        await Escolher(cut, "Alvo", 0, "RecursoTotal");
        cut.FindComponents<MudSelect<string>>().Where(c => c.Instance.Label == "Momento").Should().BeEmpty();
    }

    [Fact]
    public async Task Adding_two_lines_and_saving_posts_the_expected_body()
    {
        var cut = await AbrirNovaAsync(RenderPage());
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First(c => c.Instance.Label == "Nome").Instance.ValueChanged.InvokeAsync("Veneno forte"));
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First(c => c.Instance.Label == "Descrição").Instance.ValueChanged.InvokeAsync("Corrói"));
        cut.Find(".mud-expand-panel-header").Click();
        cut.Find("button[aria-label='Fogo']").Click();
        await cut.InvokeAsync(() => cut.FindComponent<MudNumericField<int?>>().Instance.ValueChanged.InvokeAsync(3));

        AdicionarLinha(cut);
        AdicionarLinha(cut);
        await Escolher(cut, "Específico", 0, "Forca");
        await Digitar(cut, "Valor", 0, -2);
        await Escolher(cut, "Alvo", 1, "RecursoAtual");
        await Escolher(cut, "Específico", 1, "Vitalidade");
        await Escolher(cut, "Tipo", 1, "Percentual");
        await Digitar(cut, "Valor", 1, -10);
        await Escolher(cut, "Momento", 0, "ACadaVez");
        var cats = Select(cut, "Categorias desabilitadas");
        await cut.InvokeAsync(() => cats.Instance.SelectedValuesChanged.InvokeAsync(new[] { "Arma", "Escudo" }));

        cut.FindAll("button").First(b => b.TextContent.Contains("Salvar")).Click();
        await Task.Delay(50);

        var post = _calls.Single(c => c.Method == HttpMethod.Post);
        post.Path.Should().EndWith("/condicoes");
        var body = System.Text.Json.JsonSerializer.Deserialize<SalvarCondicaoRequest>(post.Body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        body.Nome.Should().Be("Veneno forte");
        body.Descricao.Should().Be("Corrói");
        body.Icone.Should().Be("Fogo");
        body.DuracaoPadraoTurnos.Should().Be(3);
        body.Modificadores.Should().Equal(
            new CondicaoModificadorDto("Atributo", "Forca", "Fixo", -2, null),
            new CondicaoModificadorDto("RecursoAtual", "Vitalidade", "Percentual", -10, "ACadaVez"));
        body.Desabilitacoes.Should().BeEquivalentTo("Arma", "Escudo");
    }

    [Fact]
    public async Task A_general_choice_is_sent_as_null()
    {
        var cut = await AbrirNovaAsync(RenderPage());
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First(c => c.Instance.Label == "Nome").Instance.ValueChanged.InvokeAsync("Lento"));
        AdicionarLinha(cut);
        await Escolher(cut, "Alvo", 0, "Pericia");
        await Digitar(cut, "Valor", 0, -1);

        cut.FindAll("button").First(b => b.TextContent.Contains("Salvar")).Click();
        await Task.Delay(50);

        var body = System.Text.Json.JsonSerializer.Deserialize<SalvarCondicaoRequest>(_calls.Single().Body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        body.Modificadores.Should().ContainSingle().Which.AlvoEspecifico.Should().BeNull();
    }

    [Fact]
    public async Task A_400_shows_the_API_message_and_keeps_the_form_open()
    {
        _escrita = _ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Já existe uma condição com esse nome.") };
        var cut = await AbrirNovaAsync(RenderPage());
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First(c => c.Instance.Label == "Nome").Instance.ValueChanged.InvokeAsync("Veneno"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Salvar")).Click();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Já existe uma condição com esse nome.");
        cut.FindComponents<MudTextField<string>>().Any(c => c.Instance.Label == "Nome").Should().BeTrue();
    }

    [Fact]
    public async Task The_list_shows_icon_name_summary_and_default_duration()
    {
        _catalogo = [Veneno()];
        var cut = RenderPage();
        await Task.Delay(50);

        var linha = cut.Find("tbody tr");
        linha.TextContent.Should().Contain("Veneno").And.Contain("Vitalidade total +10").And.Contain("Desabilita: Armas").And.Contain("3");
        linha.InnerHtml.Should().Contain("mud-icon-root");
    }

    [Fact]
    public async Task Editing_loads_the_entry_and_saving_puts_it()
    {
        _catalogo = [Veneno()];
        var cut = RenderPage();
        await Task.Delay(50);

        cut.Find("button[aria-label='Editar Veneno']").Click();
        cut.FindComponents<MudTextField<string>>().First(c => c.Instance.Label == "Nome").Find("input").GetAttribute("value").Should().Be("Veneno");
        Escolha(cut, "Alvo").Should().Be("RecursoTotal");
        Escolha(cut, "Específico").Should().Be("Vitalidade");

        cut.FindAll("button").First(b => b.TextContent.Contains("Salvar")).Click();
        await Task.Delay(50);

        var put = _calls.Single(c => c.Method == HttpMethod.Put);
        put.Path.Should().EndWith("/condicoes/c1");
    }

    [Fact]
    public async Task Deleting_asks_for_confirmation_then_deletes()
    {
        _catalogo = [Veneno()];
        var cut = RenderPage();
        await Task.Delay(50);

        cut.Find("button[aria-label='Excluir Veneno']").Click();
        _calls.Should().BeEmpty();
        cut.Find(".mud-dialog-content").TextContent.Should().Contain("Veneno").And.Contain("não altera");
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Excluir")).Click();
        await Task.Delay(50);

        _calls.Should().ContainSingle(c => c.Method == HttpMethod.Delete && c.Path.EndsWith("/condicoes/c1"));
    }

    [Fact]
    public async Task A_user_who_is_not_the_Auditor_sees_the_forbidden_notice_and_no_form_edit_or_delete()
    {
        _auditor = false;
        _catalogo = [Veneno()];
        var cut = RenderPage();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Você não é o Auditor de Regras designado");
        cut.FindAll("button").Where(b => b.TextContent.Contains("Nova condição")).Should().BeEmpty();
        cut.FindAll("button[aria-label='Editar Veneno']").Should().BeEmpty();
        cut.FindAll("button[aria-label='Excluir Veneno']").Should().BeEmpty();
    }

    [Fact]
    public async Task A_line_whose_Pericia_was_removed_shows_a_flagged_option_and_a_warning_and_keeps_the_key()
    {
        _catalogo = [new("c2", "Lento", "", "Lentidao", null,
            [new CondicaoModificadorDto("Pericia", "Apagada", "Fixo", -1, null)], [])];
        var cut = RenderPage();
        await Task.Delay(50);

        cut.Find("button[aria-label='Editar Lento']").Click();

        Escolha(cut, "Específico").Should().Be("Apagada");
        Opcoes(cut, "Específico").Should().Equal("Todas", "Atletismo", "Furtividade", "(Perícia removida)");
        cut.Markup.Should().Contain("Esta Perícia foi removida do catálogo; escolha outra.");

        cut.FindAll("button").First(b => b.TextContent.Contains("Salvar")).Click();
        await Task.Delay(50);
        _calls.Single(c => c.Method == HttpMethod.Put).Body.Should().Contain("\"Apagada\"");
    }

    [Fact]
    public async Task A_line_with_an_existing_Pericia_shows_no_removed_warning()
    {
        _catalogo = [new("c2", "Lento", "", "Lentidao", null,
            [new CondicaoModificadorDto("Pericia", "Atletismo", "Fixo", -1, null)], [])];
        var cut = RenderPage();
        await Task.Delay(50);

        cut.Find("button[aria-label='Editar Lento']").Click();

        cut.Markup.Should().NotContain("foi removida do catálogo");
    }

    [Fact]
    public async Task While_the_Auditor_check_is_pending_no_write_control_is_rendered_and_the_Auditor_then_gets_the_page()
    {
        var gate = new TaskCompletionSource();
        _meGate = gate.Task;
        _catalogo = [Veneno()];
        var cut = RenderPage();
        await Task.Delay(50);

        cut.FindAll("button").Where(b => b.TextContent.Contains("Nova condição")).Should().BeEmpty();
        cut.FindAll("button[aria-label='Editar Veneno']").Should().BeEmpty();
        cut.FindAll("button[aria-label='Excluir Veneno']").Should().BeEmpty();
        cut.FindComponents<MudTextField<string>>().Should().BeEmpty();
        cut.Markup.Should().NotContain("Você não é o Auditor");

        gate.SetResult();
        await Task.Delay(100);

        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Nova condição"));
    }

    [Fact]
    public async Task While_the_check_is_pending_and_then_answers_non_Auditor_only_the_forbidden_notice_remains()
    {
        _auditor = false;
        var gate = new TaskCompletionSource();
        _meGate = gate.Task;
        var cut = RenderPage();
        await Task.Delay(50);
        cut.FindAll("button").Where(b => b.TextContent.Contains("Nova condição")).Should().BeEmpty();

        gate.SetResult();
        await Task.Delay(100);

        cut.Markup.Should().Contain("Você não é o Auditor de Regras designado");
        cut.FindAll("button").Where(b => b.TextContent.Contains("Nova condição")).Should().BeEmpty();
    }

    [Fact]
    public async Task A_failing_Auditor_check_does_not_crash_the_page_and_the_list_still_loads()
    {
        _meFalha = true;
        _catalogo = [Veneno()];
        var cut = RenderPage();
        await Task.Delay(100);

        cut.Markup.Should().Contain("Veneno");
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Nova condição"));
    }
}
