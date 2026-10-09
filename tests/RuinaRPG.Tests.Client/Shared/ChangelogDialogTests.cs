using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ChangelogDialogTests : MudBunitContext
{
    // ChangelogDialog's inline <MudDialog> only renders its content through a MudDialogProvider
    // present elsewhere in the render tree (the real app has one in MainLayout) — bUnit's TestContext
    // starts with none, so every test renders one alongside the dialog via this composite fragment.
    private IRenderedComponent<ContainerFragment> RenderDialog(string version, EventCallback onDismissed, bool isRulesAuditor = false) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChangelogDialog>(1);
            builder.AddAttribute(2, nameof(ChangelogDialog.Version), version);
            builder.AddAttribute(3, nameof(ChangelogDialog.OnDismissed), onDismissed);
            builder.AddAttribute(4, nameof(ChangelogDialog.IsRulesAuditor), isRulesAuditor);
            builder.CloseComponent();
        });

    [Fact]
    public void Rendering_the_dialog_does_not_invoke_OnDismissed()
    {
        var dismissedCount = 0;

        RenderDialog("1.2.0", EventCallback.Factory.Create(this, () => dismissedCount++));

        dismissedCount.Should().Be(0);
    }

    [Fact]
    public void Clicking_Fechar_invokes_OnDismissed_exactly_once()
    {
        var dismissedCount = 0;
        var cut = RenderDialog("1.2.0", EventCallback.Factory.Create(this, () => dismissedCount++));

        cut.Find("button:contains('Fechar')").Click();

        dismissedCount.Should().Be(1);
    }

    [Fact]
    public void Renders_the_Version_parameter_in_the_title_not_a_hardcoded_literal()
    {
        var cut = RenderDialog("9.9.9", EventCallback.Factory.Create(this, () => { }));

        cut.Markup.Should().Contain("Novidades da Versão 9.9.9");
        cut.Markup.Should().NotContain("1.4.3");
    }

    [Fact]
    public void Rendered_list_contains_the_section_labels_of_1_4_4()
    {
        var cut = RenderDialog("1.4.4", EventCallback.Factory.Create(this, () => { }));

        cut.Markup.Should().Contain("Afinidade Elemental:").And.Contain("Sub-Atributos:").And.Contain("Características:")
            .And.Contain("Anexos da campanha:").And.Contain("Catálogo de Itens:").And.Contain("Banco de Magias e Habilidades:")
            .And.Contain("Tabela de Afinidades").And.Contain("Características Negativas");
    }

    [Fact]
    public void The_1_4_3_text_is_gone()
    {
        var cut = RenderDialog("1.4.4", EventCallback.Factory.Create(this, () => { }), isRulesAuditor: true);

        cut.Markup.Should().NotContain("Disciplina").And.NotContain("Kits de Equipagem");
    }

    [Fact]
    public void A_user_who_is_not_an_auditor_does_not_see_the_auditor_block()
    {
        var cut = RenderDialog("1.4.4", EventCallback.Factory.Create(this, () => { }));

        cut.Markup.Should().NotContain("Para Auditores");
    }

    [Fact]
    public void An_auditor_sees_the_auditor_block_after_the_general_list()
    {
        var cut = RenderDialog("1.4.4", EventCallback.Factory.Create(this, () => { }), isRulesAuditor: true);

        var texto = cut.Markup;
        texto.Should().Contain("Para Auditores");
        texto.IndexOf("Para Auditores").Should().BeGreaterThan(texto.IndexOf("Banco de Magias e Habilidades:"));
        texto.IndexOf("Tabela de Afinidades:").Should().BeGreaterThan(texto.IndexOf("Para Auditores"));
    }
}
