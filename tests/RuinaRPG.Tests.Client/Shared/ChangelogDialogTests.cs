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
    public void Rendered_list_contains_the_section_labels_of_2_2_0()
    {
        var cut = RenderDialog("2.2.0", EventCallback.Factory.Create(this, () => { }));

        cut.Markup.Should().Contain("Afinidades por elemento:").And.Contain("Sub-Elementos e Caminhos:")
            .And.Contain("Vocações marciais:").And.Contain("Fichas existentes:").And.Contain("Correções e melhorias:")
            .And.Contain("Distribuídos X/Y");
    }

    [Fact]
    public void The_2_1_0_text_is_gone()
    {
        var cut = RenderDialog("2.2.0", EventCallback.Factory.Create(this, () => { }), isRulesAuditor: true);

        cut.Markup.Should().NotContain("Cemitério de fichas:").And.NotContain("Remover jogador da campanha:")
            .And.NotContain("Contas de jogador:").And.NotContain("Peso da Criatura:");
    }

    // Um diálogo por teste: dois MudDialogProvider no mesmo contexto compartilham o IDialogService.
    [Fact]
    public void The_auditor_block_is_hidden_from_non_auditors()
    {
        var comum = RenderDialog("2.2.0", EventCallback.Factory.Create(this, () => { }));

        comum.Markup.Should().NotContain("Para Auditores").And.NotContain("Auditoria → Afinidades");
    }

    [Fact]
    public void The_auditor_block_shows_the_new_Auditoria_de_Afinidades()
    {
        var auditor = RenderDialog("2.2.0", EventCallback.Factory.Create(this, () => { }), isRulesAuditor: true);

        auditor.Markup.Should().Contain("Para Auditores").And.Contain("Auditoria → Afinidades")
            .And.Contain("Totais por Círculo").And.Contain("Elementos").And.Contain("Cruzamentos").And.Contain("Visibilidade");
    }
}
