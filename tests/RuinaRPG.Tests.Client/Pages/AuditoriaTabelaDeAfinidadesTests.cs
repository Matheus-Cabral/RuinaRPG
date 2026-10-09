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

public class AuditoriaTabelaDeAfinidadesTests : MudBunitContext
{
    private static readonly Guid Id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private record Request(string Method, string Path, string? Body);

    private static List<LinhaDaTabelaDeAfinidadesResponse> SeedRows() => new()
    {
        new(Id1, 3, 10, 5),
        new(Id2, 8, 20, 12),
    };

    // Mutable list so POST/PUT/DELETE are reflected by the following GET, like the real API.
    private static HttpClient CreateHttp(List<LinhaDaTabelaDeAfinidadesResponse> rows, List<Request> log,
        HttpStatusCode? postStatus = null, string? postError = null)
        => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(rows.OrderBy(r => r.Afinidade).ToList()) };

            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            log.Add(new Request(request.Method.Method, path, body));

            if (request.Method == HttpMethod.Post)
            {
                if (postStatus is not null)
                    return new HttpResponseMessage(postStatus.Value) { Content = new StringContent(postError ?? "") };
                var req = request.Content!.ReadFromJsonAsync<SalvarLinhaDaTabelaDeAfinidadesRequest>().GetAwaiter().GetResult()!;
                var row = new LinhaDaTabelaDeAfinidadesResponse(Guid.NewGuid(), req.Afinidade, req.Eficiencia, req.Dano);
                rows.Add(row);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(row) };
            }

            var id = Guid.Parse(path.Split('/').Last());
            var index = rows.FindIndex(r => r.Id == id);
            if (request.Method == HttpMethod.Put)
            {
                var req = request.Content!.ReadFromJsonAsync<SalvarLinhaDaTabelaDeAfinidadesRequest>().GetAwaiter().GetResult()!;
                rows[index] = new LinhaDaTabelaDeAfinidadesResponse(id, req.Afinidade, req.Eficiencia, req.Dano);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (request.Method == HttpMethod.Delete)
            {
                rows.RemoveAt(index);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

    // The Afinidade of each table row is an editable field, so read it from the input's value.
    private static List<string?> Afinidades(IRenderedComponent<AuditoriaTabelaDeAfinidades> cut) =>
        cut.FindAll("tbody tr td:first-child input").Select(i => i.GetAttribute("value")).ToList();

    // Inline <MudDialog>s only render through a MudDialogProvider in the tree (MainLayout has one).
    private IRenderedComponent<ContainerFragment> RenderWithDialogProvider(HttpClient http)
    {
        Services.AddScoped(_ => http);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AuditoriaTabelaDeAfinidades>(1);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task Lists_the_rows_in_the_order_received()
    {
        var http = CreateHttp(SeedRows(), new());
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaTabelaDeAfinidades>();
        await Task.Delay(50);

        Afinidades(cut).Should().Equal("3", "8");
        cut.FindAll("button[aria-label='Excluir linha da Afinidade 3']").Should().HaveCount(1);
        cut.FindAll("button[aria-label='Excluir linha da Afinidade 8']").Should().HaveCount(1);
    }

    [Fact]
    public async Task Adicionar_linha_posts_the_three_values_and_reloads()
    {
        var rows = SeedRows();
        var log = new List<Request>();
        var http = CreateHttp(rows, log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaTabelaDeAfinidades>();
        await Task.Delay(50);

        var form = cut.FindComponents<MudNumericField<int>>();
        await cut.InvokeAsync(() => form[0].Instance.ValueChanged.InvokeAsync(5));
        await cut.InvokeAsync(() => form[1].Instance.ValueChanged.InvokeAsync(15));
        await cut.InvokeAsync(() => form[2].Instance.ValueChanged.InvokeAsync(9));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar linha")).Click();
        await Task.Delay(50);

        var post = log.Should().ContainSingle(r => r.Method == "POST").Subject;
        post.Path.Should().EndWith("tabela-de-afinidades");
        var body = System.Text.Json.JsonSerializer.Deserialize<SalvarLinhaDaTabelaDeAfinidadesRequest>(post.Body!,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        body.Should().Be(new SalvarLinhaDaTabelaDeAfinidadesRequest(5, 15, 9));
        Afinidades(cut).Should().Equal("3", "5", "8");
        cut.FindAll("input")[0].GetAttribute("value").Should().Be("0");
    }

    [Fact]
    public async Task Editing_the_Eficiencia_of_a_row_PUTs_the_three_values()
    {
        var log = new List<Request>();
        var http = CreateHttp(SeedRows(), log);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaTabelaDeAfinidades>();
        await Task.Delay(50);

        // Fields 0-2 are the "Nova linha" form; the first row's Afinidade/Eficiência/Dano follow.
        var eficiencia = cut.FindComponents<MudNumericField<int>>()[4];
        await cut.InvokeAsync(() => eficiencia.Instance.ValueChanged.InvokeAsync(14));
        await Task.Delay(50);

        var put = log.Should().ContainSingle(r => r.Method == "PUT").Subject;
        put.Path.Should().EndWith($"tabela-de-afinidades/{Id1}");
        var body = System.Text.Json.JsonSerializer.Deserialize<SalvarLinhaDaTabelaDeAfinidadesRequest>(put.Body!,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        body.Should().Be(new SalvarLinhaDaTabelaDeAfinidadesRequest(3, 14, 5));
    }

    [Fact]
    public async Task Excluir_asks_for_confirmation_then_deletes()
    {
        var log = new List<Request>();
        var http = CreateHttp(SeedRows(), log);
        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        cut.Find("button[aria-label='Excluir linha da Afinidade 8']").Click();
        log.Should().BeEmpty();
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Excluir")).Click();
        await Task.Delay(50);

        log.Should().ContainSingle(r => r.Method == "DELETE").Which.Path.Should().EndWith($"tabela-de-afinidades/{Id2}");
        cut.FindAll("button[aria-label='Excluir linha da Afinidade 8']").Should().BeEmpty();
    }

    [Fact]
    public async Task Excluir_then_cancelar_does_not_call_the_api()
    {
        var log = new List<Request>();
        var http = CreateHttp(SeedRows(), log);
        var cut = RenderWithDialogProvider(http);
        await Task.Delay(50);

        cut.Find("button[aria-label='Excluir linha da Afinidade 8']").Click();
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains("Cancelar")).Click();
        await Task.Delay(50);

        log.Should().BeEmpty();
        cut.FindAll("button[aria-label='Excluir linha da Afinidade 8']").Should().HaveCount(1);
    }

    [Fact]
    public async Task A_409_on_POST_shows_the_response_body()
    {
        var http = CreateHttp(SeedRows(), new(), HttpStatusCode.Conflict, "Já existe uma linha para a Afinidade 5.");
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaTabelaDeAfinidades>();
        await Task.Delay(50);
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar linha")).Click();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Já existe uma linha para a Afinidade 5.");
    }

    [Fact]
    public async Task A_403_shows_the_no_permission_alert()
    {
        var http = CreateHttp(SeedRows(), new(), HttpStatusCode.Forbidden);
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaTabelaDeAfinidades>();
        await Task.Delay(50);
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar linha")).Click();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Você não é o Auditor de Regras designado");
    }

    [Fact]
    public async Task An_empty_table_shows_the_empty_text()
    {
        var http = CreateHttp(new(), new());
        Services.AddScoped(_ => http);

        var cut = Render<AuditoriaTabelaDeAfinidades>();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Nenhuma linha cadastrada — Eficiência e Dano Elemental valem 0 em todas as fichas.");
    }
}
