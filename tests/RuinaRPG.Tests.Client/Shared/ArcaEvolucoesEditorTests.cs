using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.CharacterSheets;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ArcaEvolucoesEditorTests : MudBunitContext
{
    private readonly List<(HttpMethod Method, string Path, ArcaEvolucaoRequest? Body)> _log = new();

    private IRenderedComponent<ArcaEvolucoesEditor> RenderEditor(List<ArcaEvolucaoResponse> evolucoes, Action? onChanged = null, HttpStatusCode putStatus = HttpStatusCode.OK)
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            var body = request.Content is null ? null : request.Content.ReadFromJsonAsync<ArcaEvolucaoRequest>().GetAwaiter().GetResult();
            _log.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            if (request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new ArcaEvolucaoResponse(Guid.NewGuid(), body!.Nivel, body.Descricao)) };
            if (request.Method == HttpMethod.Put)
                return new HttpResponseMessage(putStatus) { Content = putStatus == HttpStatusCode.OK ? JsonContent.Create(new ArcaEvolucaoResponse(Guid.NewGuid(), body!.Nivel, body.Descricao)) : new StringContent("Descrição inválida.") };
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        Services.AddScoped(_ => http);
        return Render<ArcaEvolucoesEditor>(p => p
            .Add(c => c.Roll, 4)
            .Add(c => c.Evolucoes, evolucoes)
            .Add(c => c.OnChanged, () => onChanged?.Invoke()));
    }

    [Fact]
    public void Lists_each_evolucao_level_and_text()
    {
        var cut = RenderEditor(new() { new(Guid.NewGuid(), 3, "três"), new(Guid.NewGuid(), 8, "oito") });

        // The last numeric/text pair is the blank "new evolução" row.
        // Asserted through the rendered inputs: MudBlazor's analyzer (MUD0012) forbids reading .Instance.Value.
        cut.FindAll("input").Take(2).Select(i => i.GetAttribute("value")).Should().Equal("3", "8");
        cut.FindAll("textarea").Take(2).Select(t => t.TextContent.Trim()).Should().Equal("três", "oito");
    }

    [Fact]
    public async Task Adicionar_posts_a_new_evolucao_and_notifies()
    {
        var changed = false;
        var cut = RenderEditor(new(), () => changed = true);

        await cut.InvokeAsync(() => cut.FindComponents<MudNumericField<int>>().Last().Instance.ValueChanged.InvokeAsync(6));
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().Last().Instance.ValueChanged.InvokeAsync("nova"));
        cut.FindAll("button").Single(b => b.TextContent.Contains("Adicionar evolução")).Click();
        await Task.Delay(50);

        _log.Should().ContainSingle(l => l.Method == HttpMethod.Post && l.Path.EndsWith("/arcas/4/evolucoes") && l.Body!.Nivel == 6 && l.Body.Descricao == "nova");
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task Editing_a_text_puts_it()
    {
        var id = Guid.NewGuid();
        var cut = RenderEditor(new() { new(id, 3, "três") });

        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First().Instance.ValueChanged.InvokeAsync("três editado"));
        await Task.Delay(50);

        _log.Should().ContainSingle(l => l.Method == HttpMethod.Put && l.Path.EndsWith($"/arcas/4/evolucoes/{id}") && l.Body!.Descricao == "três editado" && l.Body.Nivel == 3);
    }

    [Fact]
    public async Task Remove_button_deletes_it()
    {
        var id = Guid.NewGuid();
        var cut = RenderEditor(new() { new(id, 3, "três") });

        cut.Find($"button[aria-label='Remover evolução do nível 3']").Click();
        await Task.Delay(50);

        _log.Should().ContainSingle(l => l.Method == HttpMethod.Delete && l.Path.EndsWith($"/arcas/4/evolucoes/{id}"));
    }

    [Fact]
    public async Task A_rejected_edit_shows_the_original_text_again()
    {
        var id = Guid.NewGuid();
        var evolucoes = new List<ArcaEvolucaoResponse> { new(id, 3, "três") };
        var cut = RenderEditor(evolucoes, putStatus: HttpStatusCode.BadRequest);

        cut.FindAll("textarea").First().Change("texto rejeitado");
        await Task.Delay(50);

        // Asserted through markup: MUD0012 forbids reading .Instance.Value.
        cut.WaitForAssertion(() => cut.FindAll("textarea").First().TextContent.Trim().Should().Be("três"));
        cut.Markup.Should().NotContain("texto rejeitado");
    }
}
