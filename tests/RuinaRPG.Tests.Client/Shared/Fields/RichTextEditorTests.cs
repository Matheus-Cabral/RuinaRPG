using Bunit;
using FluentAssertions;
using Microsoft.JSInterop;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class RichTextEditorTests : MudBunitContext
{
    [Fact]
    public void First_render_creates_the_js_editor_with_the_initial_html()
    {
        Render<RichTextEditor>(p => p.Add(x => x.Html, "<p>Era uma vez</p>"));

        var create = JSInterop.VerifyInvoke("ruinaRichText.create");
        create.Arguments[2].Should().Be("<p>Era uma vez</p>");
    }

    [Fact]
    public void A_null_Html_creates_an_empty_editor()
    {
        Render<RichTextEditor>(p => p.Add(x => x.Html, (string?)null));

        JSInterop.VerifyInvoke("ruinaRichText.create").Arguments[2].Should().Be("");
    }

    [Fact]
    public async Task A_change_reported_by_js_raises_HtmlChanged_then_OnCommit()
    {
        var calls = new List<string>();
        var cut = Render<RichTextEditor>(p => p
            .Add(x => x.Html, "<p>a</p>")
            .Add(x => x.HtmlChanged, (string? v) => calls.Add($"changed:{v}"))
            .Add(x => x.OnCommit, (string? v) => calls.Add($"commit:{v}")));

        await cut.InvokeAsync(() => cut.Instance.OnEditorChanged("<p>ab</p>"));

        calls.Should().Equal("changed:<p>ab</p>", "commit:<p>ab</p>");
    }

    [Fact]
    public void Re_rendering_with_a_different_Html_does_not_touch_the_js_editor()
    {
        // Review Focus: a sheet reload (autosave of another field) must never push older text into
        // an editor the player is typing in — the editor only takes content when it is created.
        var cut = Render<RichTextEditor>(p => p.Add(x => x.Html, "<p>a</p>"));

        cut.Render(p => p.Add(x => x.Html, "<p>versão antiga do servidor</p>"));

        JSInterop.Invocations.Should().ContainSingle(i => i.Identifier.StartsWith("ruinaRichText."));
    }

    [Fact]
    public async Task Disposing_destroys_the_js_editor()
    {
        var cut = Render<RichTextEditor>();

        await DisposeComponentsAsync();

        JSInterop.VerifyInvoke("ruinaRichText.destroy");
        _ = cut;
    }

    [Fact]
    public void A_failed_js_create_shows_an_error_instead_of_the_editor()
    {
        JSInterop.SetupVoid("ruinaRichText.create", _ => true).SetException(new JSException("falhou"));

        var cut = Render<RichTextEditor>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Não foi possível carregar o editor"));
    }

    [Fact]
    public async Task Disposing_while_create_is_still_pending_destroys_the_editor_once_it_exists()
    {
        var plan = JSInterop.SetupVoid("ruinaRichText.create", _ => true);
        Render<RichTextEditor>();

        await DisposeComponentsAsync();
        // create's continuation resumes on the renderer's dispatcher; a second dispatcher hop lets it run.
        await Renderer.Dispatcher.InvokeAsync(plan.SetVoidResult);
        await Renderer.Dispatcher.InvokeAsync(() => { });

        JSInterop.VerifyInvoke("ruinaRichText.destroy");
    }
}
