using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class HistoriaEditorTests : MudBunitContext
{
    private readonly List<(string Method, string Path, string Body)> _requests = new();

    private void RegisterApi(HttpStatusCode status, string responseBody = "") =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            lock (_requests) _requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "text/plain") };
        }));

    [Fact]
    public async Task A_change_in_the_editor_is_saved_to_SaveUrl()
    {
        RegisterApi(HttpStatusCode.NoContent);
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.Html, "<p>a</p>"));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>Nasceu em Alkeria</p>"));

        cut.WaitForAssertion(() =>
        {
            lock (_requests) _requests.Should().ContainSingle();
        }, TimeSpan.FromSeconds(3));
        var request = _requests.Single();
        request.Method.Should().Be("PUT");
        request.Path.Should().Be("/api/character-sheets/abc/historia");
        request.Body.Should().Contain("Nasceu em Alkeria");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Salvo às"), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task A_change_updates_the_bound_Html_immediately()
    {
        RegisterApi(HttpStatusCode.NoContent);
        string? bound = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "npc-sheets/abc/historia")
            .Add(x => x.HtmlChanged, (string? v) => bound = v));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>novo</p>"));

        bound.Should().Be("<p>novo</p>");
    }

    [Fact]
    public async Task A_400_from_the_api_is_reported_through_OnError_and_the_indicator_shows_the_error()
    {
        RegisterApi(HttpStatusCode.BadRequest, "A História pode ter no máximo 200.000 caracteres.");
        string? error = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.OnError, (string e) => error = e));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>longo demais</p>"));

        cut.WaitForAssertion(() => error.Should().Be("A História pode ter no máximo 200.000 caracteres."), TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Erro ao salvar"), TimeSpan.FromSeconds(3));
    }
}
