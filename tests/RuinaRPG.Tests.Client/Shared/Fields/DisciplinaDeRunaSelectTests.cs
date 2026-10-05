using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class DisciplinaDeRunaSelectTests : MudBunitContext
{
    [Fact]
    public void Offers_the_four_disciplinas_and_no_blank_option()
    {
        var root = RenderWithPopover<DisciplinaDeRunaSelect>((nameof(DisciplinaDeRunaSelect.Value), ""));

        OpenSelect(root, "Disciplina").Should().Equal("Adição", "Alteração", "Emissão", "Manifestação");
    }

    [Fact]
    public void The_info_popup_lists_each_disciplina_with_its_description()
    {
        // The InfoPopup's dialog renders through a MudDialogProvider (see InfoPopupTests).
        var root = Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<DisciplinaDeRunaSelect>(1);
            builder.CloseComponent();
        });

        root.Find("button[aria-label='Disciplina da Runa']").Click();

        root.Markup.Should().Contain("Influencia o corpo do usuário")
            .And.Contain("Influencia objetos inanimados")
            .And.Contain("Influencia alvos inanimados")
            .And.Contain("Manifesta a aura do usuário");
    }
}
