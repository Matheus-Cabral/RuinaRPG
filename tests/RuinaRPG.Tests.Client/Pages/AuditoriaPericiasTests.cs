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

public class AuditoriaPericiasTests : MudBunitContext
{
    // Mutable server-side state so writes are visible to the following GET, like the real API.
    private class Row
    {
        public int Id { get; set; }
        public string Chave { get; set; } = "";
        public string Nome { get; set; } = "";
        public string? Descricao { get; set; }
        public string? AtributoSugerido { get; set; }
        public bool DisponivelParaCriaturas { get; set; } = true;
        public bool Protegida { get; set; }
        public bool IsDeleted { get; set; }

        public PericiaAuditoriaResponse ToResponse() =>
            new(Id, Chave, Nome, Descricao, AtributoSugerido, DisponivelParaCriaturas, Protegida, IsDeleted);
    }

    private static List<Row> SeedRows() => new()
    {
        new() { Id = 7, Chave = "Atletismo", Nome = "Atletismo", Descricao = "Correr e saltar.", AtributoSugerido = "Forca" },
        new() { Id = 32, Chave = "Prontidao", Nome = "Prontidão", AtributoSugerido = "Instinto", Protegida = true },
    };

    private record Request(string Method, string Path, string? Body);

    private HttpClient CreateStatefulHttp(List<Row> rows, List<Request> log, string? postError = null, string? putError = null)
        => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var idx = Array.IndexOf(segments, "pericias");
            var rest = idx < 0 ? Array.Empty<string>() : segments[(idx + 1)..];

            if (request.Method == HttpMethod.Get && rest.Length == 1 && rest[0] == "auditoria")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(rows.Select(r => r.ToResponse()).ToList()) };

            if (request.Method == HttpMethod.Get && rest.Length == 0)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(rows.Where(r => !r.IsDeleted)
                        .Select(r => new PericiaResponse(r.Id, r.Chave, r.Nome, r.Descricao, r.AtributoSugerido, r.DisponivelParaCriaturas, r.Protegida)).ToList())
                };

            log.Add(new Request(request.Method.Method, path, body));

            if (request.Method == HttpMethod.Post && rest.Length == 0)
            {
                if (postError is not null)
                    return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(postError) };
                var req = request.Content!.ReadFromJsonAsync<SalvarPericiaRequest>().GetAwaiter().GetResult()!;
                var row = new Row
                {
                    Id = rows.Max(r => r.Id) + 1, Chave = req.Nome, Nome = req.Nome, Descricao = req.Descricao,
                    AtributoSugerido = req.AtributoSugerido, DisponivelParaCriaturas = req.DisponivelParaCriaturas,
                };
                rows.Add(row);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(row.ToResponse()) };
            }

            if (rest.Length >= 1 && int.TryParse(rest[0], out var id))
            {
                var row = rows.Single(r => r.Id == id);
                if (request.Method == HttpMethod.Put)
                {
                    if (putError is not null)
                        return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(putError) };
                    var req = request.Content!.ReadFromJsonAsync<SalvarPericiaRequest>().GetAwaiter().GetResult()!;
                    row.Nome = req.Nome;
                    row.Descricao = req.Descricao;
                    row.AtributoSugerido = req.AtributoSugerido;
                    row.DisponivelParaCriaturas = req.DisponivelParaCriaturas;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(row.ToResponse()) };
                }
                if (request.Method == HttpMethod.Delete)
                {
                    row.IsDeleted = true;
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                if (request.Method == HttpMethod.Post && rest.Length == 2 && rest[1] == "restaurar")
                {
                    row.IsDeleted = false;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(row.ToResponse()) };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    // Inline <MudDialog>s only render through a MudDialogProvider in the tree (MainLayout has one).
    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider(HttpClient http)
    {
        Services.AddScoped(_ => http);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaPericias>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task Renders_active_rows_and_a_lock_instead_of_delete_for_protected()
    {
        var http = CreateStatefulHttp(SeedRows(), new());
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        cut.FindAll("button[aria-label='Remover Atletismo']").Should().HaveCount(1);
        cut.FindAll("button[aria-label='Remover Prontidão']").Should().BeEmpty();

        var prontidao = cut.FindAll("tbody tr").Single(tr => tr.QuerySelector("input")!.GetAttribute("value") == "Prontidão");
        prontidao.QuerySelector("[title='Usada em fórmulas — não pode ser removida']").Should().NotBeNull();
        prontidao.QuerySelector("[title] svg").Should().NotBeNull();
    }

    [Fact]
    public async Task Adding_posts_the_form_and_shows_the_new_row()
    {
        var log = new List<Request>();
        var http = CreateStatefulHttp(SeedRows(), log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        var nome = cut.FindComponents<MudTextField<string>>()[0]; // form row comes first
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Heráldica"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar")).Click();
        await Task.Delay(50);

        var post = log.Should().ContainSingle(r => r.Method == "POST").Subject;
        post.Path.Should().EndWith("pericias");
        System.Text.Json.JsonSerializer.Deserialize<SalvarPericiaRequest>(post.Body!,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.Nome.Should().Be("Heráldica");
        cut.FindAll("tbody tr input").Select(i => i.GetAttribute("value")).Should().Contain("Heráldica");
    }

    [Fact]
    public async Task Editing_the_description_puts_the_whole_row()
    {
        var log = new List<Request>();
        var http = CreateStatefulHttp(SeedRows(), log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        // Indexes 0/1 are the form row's Nome/Descrição; 2/3 are Atletismo's.
        var descricao = cut.FindComponents<MudTextField<string>>()[3];
        await cut.InvokeAsync(() => descricao.Instance.ValueChanged.InvokeAsync("Nova descrição"));
        await Task.Delay(50);

        var put = log.Should().ContainSingle(r => r.Method == "PUT").Subject;
        put.Path.Should().EndWith("pericias/7");
        var body = System.Text.Json.JsonSerializer.Deserialize<SalvarPericiaRequest>(put.Body!,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        body.Nome.Should().Be("Atletismo");
        body.Descricao.Should().Be("Nova descrição");
        body.AtributoSugerido.Should().Be("Forca");
        body.DisponivelParaCriaturas.Should().BeTrue();
    }

    [Fact]
    public async Task Removing_asks_for_confirmation_then_deletes_and_moves_it_to_Removidas()
    {
        var log = new List<Request>();
        var http = CreateStatefulHttp(SeedRows(), log);

        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        cut.Find("button[aria-label='Remover Atletismo']").Click();
        cut.Find(".mud-dialog-content").TextContent.Should().Contain("devolvidos em todas as fichas");
        log.Should().BeEmpty();

        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Remover")).Click();
        await Task.Delay(50);

        log.Should().ContainSingle(r => r.Method == "DELETE").Which.Path.Should().EndWith("pericias/7");
        cut.Markup.Should().Contain("Removidas");
        cut.Markup.Should().Contain("Restaurar");
        cut.FindAll("button[aria-label='Remover Atletismo']").Should().BeEmpty();
    }

    [Fact]
    public async Task Restaurar_posts_restore()
    {
        var rows = SeedRows();
        rows.Add(new Row { Id = 40, Chave = "Antiga", Nome = "Antiga", IsDeleted = true });
        var log = new List<Request>();
        var http = CreateStatefulHttp(rows, log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        cut.FindAll("button").First(b => b.TextContent.Contains("Restaurar")).Click();
        await Task.Delay(50);

        var post = log.Should().ContainSingle(r => r.Method == "POST").Subject;
        post.Path.Should().EndWith("pericias/40/restaurar");
    }

    [Fact]
    public async Task A_400_shows_the_message()
    {
        var http = CreateStatefulHttp(SeedRows(), new(), postError: "Já existe uma perícia ativa com esse nome.");
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        var nome = cut.FindComponents<MudTextField<string>>()[0];
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Atletismo"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar")).Click();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Já existe uma perícia ativa com esse nome.");
    }

    [Fact]
    public async Task Info_popups_explain_the_page()
    {
        var http = CreateStatefulHttp(SeedRows(), new());

        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        cut.Find("button[title='Como funciona a Auditoria de Perícias']").Click();

        cut.Markup.Should().Contain("Atributo sugerido só vem pré-selecionado");
    }

    private static SalvarPericiaRequest ParseBody(string body) =>
        System.Text.Json.JsonSerializer.Deserialize<SalvarPericiaRequest>(body,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

    [Fact]
    public async Task A_failed_PUT_shows_the_message_and_reverts_the_Nome_field()
    {
        var http = CreateStatefulHttp(SeedRows(), new(), putError: "Já existe uma perícia ativa com esse nome.");
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        // Type into the real <input> (Atletismo's Nome) so the text field holds the rejected text,
        // as in the browser; invoking ValueChanged directly would never touch the field's own state.
        cut.FindAll("tbody tr")[0].QuerySelector("input")!.Change("Rejeitado");
        await Task.Delay(50);

        cut.Markup.Should().Contain("Já existe uma perícia ativa com esse nome.");
        var valores = cut.FindAll("tbody tr input").Select(i => i.GetAttribute("value")).ToList();
        valores.Should().Contain("Atletismo");
        valores.Should().NotContain("Rejeitado");
    }

    [Fact]
    public async Task Choosing_the_dash_on_a_row_PUTs_a_null_Atributo()
    {
        var log = new List<Request>();
        var http = CreateStatefulHttp(SeedRows(), log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        var selects = cut.FindComponents<MudSelect<string>>(); // [0] form, [1] Atletismo
        await cut.InvokeAsync(() => selects[1].Instance.ValueChanged.InvokeAsync(""));
        await Task.Delay(50);

        var put = log.Should().ContainSingle(r => r.Method == "PUT").Subject;
        put.Path.Should().EndWith("pericias/7");
        ParseBody(put.Body!).AtributoSugerido.Should().BeNull();
    }

    [Fact]
    public async Task Adding_a_new_pericia_defaults_DisponivelParaCriaturas_to_false()
    {
        var log = new List<Request>();
        var http = CreateStatefulHttp(SeedRows(), log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        var nome = cut.FindComponents<MudTextField<string>>()[0];
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Padrão Falso"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar")).Click();
        await Task.Delay(50);

        var post = log.Should().ContainSingle(r => r.Method == "POST").Subject;
        ParseBody(post.Body!).DisponivelParaCriaturas.Should().BeFalse();
    }

    [Fact]
    public async Task Adding_with_the_dash_POSTs_a_null_Atributo()
    {
        var log = new List<Request>();
        var http = CreateStatefulHttp(SeedRows(), log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        var nome = cut.FindComponents<MudTextField<string>>()[0];
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Sem Atributo"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar")).Click();
        await Task.Delay(50);

        var post = log.Should().ContainSingle(r => r.Method == "POST").Subject;
        ParseBody(post.Body!).AtributoSugerido.Should().BeNull();
    }

    [Fact]
    public async Task The_Criaturas_checkbox_is_disabled_for_protected_rows_only()
    {
        var http = CreateStatefulHttp(SeedRows(), new());
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaPericias>();
        await Task.Delay(50);

        // [0] form checkbox, [1] Atletismo, [2] Prontidão
        var boxes = cut.FindComponents<MudCheckBox<bool>>();
        boxes[1].Instance.Disabled.Should().BeFalse();
        boxes[2].Instance.Disabled.Should().BeTrue();
    }
}
