using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Pages;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class AuditoriaTabelaDeNiveisTests : MudBunitContext
{
    private static readonly Guid PontosId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid MaxPericiaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid FamaId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private record Request(string Method, string Path, string? Body);

    private class State
    {
        public List<ColunaDeNivelResponse> Colunas { get; set; } = new()
        {
            new(PontosId, "Pontos de Atributo", "Acumulativa", "PontosDeAtributo", true, 0),
            new(MaxPericiaId, "Máx. de Perícia", "PorNivel", "MaxPericia", true, 1),
            new(FamaId, "Fama", "PorNivel", null, false, 2),
        };
        public List<LinhaDeNivelResponse> Linhas { get; set; } = new()
        {
            new(1, null, new() { [PontosId] = 3, [MaxPericiaId] = 5, [FamaId] = null }),
            new(2, "Bônus do nível 2", new() { [PontosId] = 2, [MaxPericiaId] = null, [FamaId] = 1 }),
            new(3, null, new() { [PontosId] = 2, [MaxPericiaId] = 6, [FamaId] = null }),
        };
    }

    private static readonly System.Text.Json.JsonSerializerOptions Web = new(System.Text.Json.JsonSerializerDefaults.Web);

    private HttpClient CreateStatefulHttp(State state, List<Request> log, string? deleteUltimoError = null, string? putError = null)
        => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.IndexOf(segments, "tabela-de-niveis");
            var rest = idx < 0 ? Array.Empty<string>() : segments[(idx + 1)..];

            if (request.Method == HttpMethod.Get && rest.Length == 0)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new TabelaDeNiveisResponse(state.Colunas.ToList(), state.Linhas.ToList()))
                };

            log.Add(new Request(request.Method.Method, path, body));

            if (putError is not null && request.Method == HttpMethod.Put && (rest.Length == 3 || (rest.Length == 2 && rest[0] == "colunas" && rest[1] != "ordem")))
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(putError) };
            if (request.Method == HttpMethod.Put && rest.Length == 2 && rest[0] == "colunas" && rest[1] != "ordem")
            {
                var req = System.Text.Json.JsonSerializer.Deserialize<RenomearColunaDeNivelRequest>(body!, Web)!;
                var i = state.Colunas.FindIndex(c => c.Id == Guid.Parse(rest[1]));
                state.Colunas[i] = state.Colunas[i] with { Nome = req.Nome };
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Put && rest.Length == 3 && rest[1] == "valores")
            {
                var nivel = int.Parse(rest[0]);
                var req = System.Text.Json.JsonSerializer.Deserialize<AtualizarValorDeNivelRequest>(body!, Web)!;
                state.Linhas.Single(l => l.Nivel == nivel).Valores[Guid.Parse(rest[2])] = req.Valor;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Put && rest.Length == 2 && rest[1] == "outros-bonus")
            {
                var nivel = int.Parse(rest[0]);
                var req = System.Text.Json.JsonSerializer.Deserialize<AtualizarOutrosBonusRequest>(body!, Web)!;
                var i = state.Linhas.FindIndex(l => l.Nivel == nivel);
                state.Linhas[i] = state.Linhas[i] with { OutrosBonus = req.OutrosBonus };
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Put && rest.Length == 2 && rest[0] == "colunas" && rest[1] == "ordem")
            {
                var req = System.Text.Json.JsonSerializer.Deserialize<ReordenarColunasDeNivelRequest>(body!, Web)!;
                state.Colunas = req.Ids.Select((id, o) => state.Colunas.Single(c => c.Id == id) with { Ordem = o }).ToList();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Post && rest.Length == 1 && rest[0] == "niveis")
            {
                state.Linhas.Add(new(state.Linhas.Count + 1, null, state.Colunas.ToDictionary(c => c.Id, _ => (int?)null)));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Delete && rest.Length == 2 && rest[0] == "niveis" && rest[1] == "ultimo")
            {
                if (deleteUltimoError is not null)
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(deleteUltimoError) };
                state.Linhas.RemoveAt(state.Linhas.Count - 1);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Post && rest.Length == 1 && rest[0] == "colunas")
            {
                var req = System.Text.Json.JsonSerializer.Deserialize<CriarColunaDeNivelRequest>(body!, Web)!;
                var id = Guid.NewGuid();
                state.Colunas.Add(new(id, req.Nome, req.Tipo, null, false, state.Colunas.Count));
                state.Linhas = state.Linhas.Select(l => { l.Valores[id] = null; return l; }).ToList();
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
            if (request.Method == HttpMethod.Delete && rest.Length == 2 && rest[0] == "colunas")
            {
                state.Colunas.RemoveAll(c => c.Id == Guid.Parse(rest[1]));
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider(HttpClient http)
    {
        Services.AddScoped(_ => http);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaTabelaDeNiveis>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task Renders_one_row_per_level_and_one_column_per_coluna_plus_outros_bonus()
    {
        Services.AddScoped(_ => CreateStatefulHttp(new State(), new()));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.FindAll("tbody tr").Should().HaveCount(3);
        // thead: Nível + 3 colunas + Outros bônus
        cut.FindAll("thead th").Should().HaveCount(5);
        cut.Markup.Should().Contain("Outros bônus");
        var headerNames = cut.FindAll("thead textarea").Select(i => i.GetAttribute("value")).ToList();
        headerNames.Should().Equal("Pontos de Atributo", "Máx. de Perícia", "Fama");
        // Row 2: 3 numeric cells + outros bônus textarea
        var row2 = cut.FindAll("tbody tr")[1];
        row2.QuerySelectorAll("input").Select(i => i.GetAttribute("value")).Should().Equal("2", null, "1");
        row2.QuerySelector("textarea")!.TextContent.Should().Contain("Bônus do nível 2");
    }

    [Fact]
    public async Task Header_cell_has_the_name_field_on_line_one_and_the_actions_on_line_two()
    {
        Services.AddScoped(_ => CreateStatefulHttp(new State(), new()));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.Find(".tn-grid").Should().NotBeNull();
        var colunas = cut.FindAll("thead th.tn-col");
        colunas.Should().HaveCount(3);
        foreach (var th in colunas)
        {
            th.ClassList.Should().Contain("tn-th"); // sticky top (scoped css)
            var linhas = th.Children.Where(c => c.TagName == "DIV").ToList();
            linhas.Should().HaveCount(2);
            linhas[0].QuerySelector("textarea.mud-input-slot, textarea")!.Should().NotBeNull(); // multi-line name field
            linhas[0].QuerySelector("svg").Should().NotBeNull(); // type icon
            linhas[0].QuerySelector("button").Should().BeNull();
            linhas[1].ClassList.Should().Contain("tn-acoes");
            linhas[1].QuerySelectorAll("button").Length.Should().BeGreaterThanOrEqualTo(2); // move arrows (+ delete when custom)
        }
        colunas[0].QuerySelector("[data-sistema='true']").Should().NotBeNull();
        colunas[2].QuerySelector("button[aria-label='Remover coluna Fama']").Should().NotBeNull();
        cut.FindAll("tbody td.tn-cell").Should().NotBeEmpty();
        cut.Find("thead th.tn-corner").TextContent.Should().Be("Nível");
        cut.FindAll("tbody td.tn-first").Should().HaveCount(3);
    }

    [Fact]
    public async Task A_long_column_name_is_rendered_in_full_in_its_field()
    {
        var state = new State();
        state.Colunas[2] = new(FamaId, "Máx. Passivas De Classe Com Nome Muito Comprido", "PorNivel", null, false, 2);
        Services.AddScoped(_ => CreateStatefulHttp(state, new()));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.Find("textarea[aria-label='Nome da coluna Máx. Passivas De Classe Com Nome Muito Comprido']")
            .GetAttribute("value").Should().Be("Máx. Passivas De Classe Com Nome Muito Comprido");
    }

    [Fact]
    public async Task Renaming_a_column_never_sends_line_breaks()
    {
        var log = new List<Request>();
        Services.AddScoped(_ => CreateStatefulHttp(new State(), log));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.FindAll("thead textarea")[2].Change("Fama\nNova");
        await Task.Delay(50);

        log.Should().Contain(r => r.Method == "PUT" && r.Body!.Contains("Fama Nova") && !r.Body.Contains("\\n"));
    }

    [Fact]
    public async Task Editing_a_cell_puts_the_value()
    {
        var log = new List<Request>();
        Services.AddScoped(_ => CreateStatefulHttp(new State(), log));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        // Row for level 2, third numeric cell = Fama (currently 1)
        cut.FindAll("tbody tr")[1].QuerySelectorAll("input")[2].Change("7");
        await Task.Delay(50);

        var put = log.Should().ContainSingle(r => r.Method == "PUT").Subject;
        put.Path.Should().EndWith($"tabela-de-niveis/2/valores/{FamaId}");
        System.Text.Json.JsonSerializer.Deserialize<AtualizarValorDeNivelRequest>(put.Body!, Web)!.Valor.Should().Be(7);

        log.Clear();
        cut.FindAll("tbody tr")[1].QuerySelectorAll("input")[2].Change("");
        await Task.Delay(50);

        var clear = log.Should().ContainSingle(r => r.Method == "PUT").Subject;
        clear.Path.Should().EndWith($"tabela-de-niveis/2/valores/{FamaId}");
        System.Text.Json.JsonSerializer.Deserialize<AtualizarValorDeNivelRequest>(clear.Body!, Web)!.Valor.Should().BeNull();
    }

    [Fact]
    public async Task System_columns_show_a_lock_and_no_delete_button_custom_ones_can_be_removed_after_confirming()
    {
        var log = new List<Request>();
        var cut = RenderWithDialogProvider(CreateStatefulHttp(new State(), log));
        await Task.Delay(50);

        cut.FindAll("button[aria-label='Remover coluna Fama']").Should().HaveCount(1);
        cut.FindAll("button[aria-label='Remover coluna Pontos de Atributo']").Should().BeEmpty();
        cut.FindAll("button[aria-label='Remover coluna Máx. de Perícia']").Should().BeEmpty();
        cut.FindAll("thead [data-sistema='true']").Should().HaveCount(2);

        // Cancel: no DELETE
        cut.Find("button[aria-label='Remover coluna Fama']").Click();
        cut.Find(".mud-dialog-content").TextContent.Should().Contain("Remover a coluna Fama? Os valores dela em todos os níveis serão apagados.");
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Cancelar")).Click();
        await Task.Delay(50);
        log.Should().BeEmpty();

        // Confirm: DELETE
        cut.Find("button[aria-label='Remover coluna Fama']").Click();
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Remover")).Click();
        await Task.Delay(50);

        log.Should().ContainSingle(r => r.Method == "DELETE").Which.Path.Should().EndWith($"tabela-de-niveis/colunas/{FamaId}");
        cut.FindAll("thead th").Should().HaveCount(4);
    }

    [Fact]
    public async Task A_failed_cell_write_shows_the_message_and_reverts_the_input()
    {
        Services.AddScoped(_ => CreateStatefulHttp(new State(), new(), putError: "Valor inválido."));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.FindAll("tbody tr")[1].QuerySelectorAll("input")[2].Change("99");
        await Task.Delay(50);

        cut.Markup.Should().Contain("Valor inválido.");
        cut.FindAll("tbody tr")[1].QuerySelectorAll("input")[2].GetAttribute("value").Should().Be("1");
    }

    [Fact]
    public async Task A_failed_column_rename_shows_the_message_and_reverts_the_name()
    {
        Services.AddScoped(_ => CreateStatefulHttp(new State(), new(), putError: "Nome já existe."));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.FindAll("thead textarea")[2].Change("Rejeitado");
        await Task.Delay(50);

        cut.Markup.Should().Contain("Nome já existe.");
        var nomes = cut.FindAll("thead textarea").Select(i => i.GetAttribute("value")).ToList();
        nomes.Should().Contain("Fama");
        nomes.Should().NotContain("Rejeitado");
    }

    [Fact]
    public async Task The_Nova_coluna_dialog_has_its_own_info_popup()
    {
        var cut = RenderWithDialogProvider(CreateStatefulHttp(new State(), new()));
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Coluna")).Click();
        cut.Find(".mud-dialog button[title='Tipo da coluna']").Click();

        cut.Markup.Should().Contain("O tipo não pode ser mudado depois");
    }

    [Fact]
    public async Task Moving_a_column_right_puts_the_new_order()
    {
        var log = new List<Request>();
        Services.AddScoped(_ => CreateStatefulHttp(new State(), log));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.Find("button[aria-label='Mover coluna Pontos de Atributo para a direita']").Click();
        await Task.Delay(50);

        var put = log.Should().ContainSingle(r => r.Method == "PUT").Subject;
        put.Path.Should().EndWith("tabela-de-niveis/colunas/ordem");
        System.Text.Json.JsonSerializer.Deserialize<ReordenarColunasDeNivelRequest>(put.Body!, Web)!.Ids
            .Should().Equal(MaxPericiaId, PontosId, FamaId);
        cut.FindAll("thead textarea").Select(i => i.GetAttribute("value")).Should().Equal("Máx. de Perícia", "Pontos de Atributo", "Fama");
    }

    [Fact]
    public async Task Adding_a_column_posts_name_and_type()
    {
        var log = new List<Request>();
        var cut = RenderWithDialogProvider(CreateStatefulHttp(new State(), log));
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Coluna")).Click();
        cut.Find(".mud-dialog input[type='text']").Change("Renome");
        cut.FindAll(".mud-dialog input[type='radio']")[1].Click(); // Por nível
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Adicionar")).Click();
        await Task.Delay(50);

        var post = log.Should().ContainSingle(r => r.Method == "POST").Subject;
        post.Path.Should().EndWith("tabela-de-niveis/colunas");
        var body = System.Text.Json.JsonSerializer.Deserialize<CriarColunaDeNivelRequest>(post.Body!, Web)!;
        body.Nome.Should().Be("Renome");
        body.Tipo.Should().Be("PorNivel");
        cut.FindAll("thead textarea").Select(i => i.GetAttribute("value")).Should().Contain("Renome");
    }

    [Fact]
    public async Task Adicionar_nivel_posts_and_the_new_row_appears()
    {
        var log = new List<Request>();
        Services.AddScoped(_ => CreateStatefulHttp(new State(), log));

        var cut = Render<AuditoriaTabelaDeNiveis>();
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Nível") && b.TextContent.Contains("+")).Click();
        await Task.Delay(50);

        log.Should().ContainSingle(r => r.Method == "POST").Which.Path.Should().EndWith("tabela-de-niveis/niveis");
        cut.FindAll("tbody tr").Should().HaveCount(4);
    }

    [Fact]
    public async Task Remover_ultimo_nivel_confirms_then_deletes_and_shows_the_400_message()
    {
        var log = new List<Request>();
        var cut = RenderWithDialogProvider(CreateStatefulHttp(new State(), log, deleteUltimoError: "1 ficha(s) estão no nível 3."));
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Remover último nível")).Click();
        log.Should().BeEmpty();
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Remover")).Click();
        await Task.Delay(50);

        log.Should().ContainSingle(r => r.Method == "DELETE").Which.Path.Should().EndWith("tabela-de-niveis/niveis/ultimo");
        cut.Markup.Should().Contain("1 ficha(s) estão no nível 3.");
        cut.FindAll("tbody tr").Should().HaveCount(3);
    }

    [Fact]
    public async Task Info_popup_explains_the_column_types()
    {
        var cut = RenderWithDialogProvider(CreateStatefulHttp(new State(), new()));
        await Task.Delay(50);

        cut.Find("button[title='Como funciona a Tabela de Níveis']").Click();

        cut.Markup.Should().Contain("célula vazia repete o valor");
        cut.Markup.Should().Contain("usam os valores de Vida e Arcana do último nível dessas tabelas");
    }
}
