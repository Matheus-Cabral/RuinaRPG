using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ClickableItemNameTests : MudBunitContext
{
    // The inline <MudDialog> only renders its content through a MudDialogProvider present elsewhere
    // in the render tree — same idiom as InfoPopupTests.
    private IRenderedComponent<ContainerFragment> RenderName(
        List<string>? requisitos = null, List<string>? pendentes = null, List<string>? penalidade = null,
        string? outrasPenalidades = null, bool emUso = true) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ClickableItemName>(1);
            builder.AddAttribute(2, nameof(ClickableItemName.Nome), "Espada Longa");
            builder.AddAttribute(3, nameof(ClickableItemName.Descricao), "Uma espada.");
            builder.AddAttribute(4, nameof(ClickableItemName.Requisitos), requisitos);
            builder.AddAttribute(5, nameof(ClickableItemName.RequisitosPendentes), pendentes);
            builder.AddAttribute(6, nameof(ClickableItemName.Penalidade), penalidade);
            builder.AddAttribute(7, nameof(ClickableItemName.OutrasPenalidades), outrasPenalidades);
            builder.AddAttribute(8, nameof(ClickableItemName.EmUso), emUso);
            builder.CloseComponent();
        });

    [Fact]
    public void The_row_shows_only_the_name_when_the_requirements_are_met()
    {
        var cut = RenderName(requisitos: ["Vigor ≥ 8"], pendentes: [], penalidade: ["Força −2"], outrasPenalidades: "Lenta");

        cut.FindAll(".mud-chip").Should().BeEmpty();
        cut.Markup.Should().Contain("Espada Longa").And.NotContain("Vigor ≥ 8").And.NotContain("Força −2").And.NotContain("Lenta");
    }

    [Fact]
    public void The_row_shows_a_tag_and_no_detail_when_a_requirement_is_missing()
    {
        var cut = RenderName(requisitos: ["Vigor ≥ 8"], pendentes: ["Vigor ≥ 8"], penalidade: ["Força −2"]);

        cut.Find(".mud-chip").TextContent.Should().Contain("Requisito(s) não cumprido(s)");
        cut.Markup.Should().NotContain("Vigor ≥ 8").And.NotContain("Força −2");
    }

    [Fact]
    public void The_popup_shows_the_description_and_the_requirements_with_what_is_missing_and_the_penalty()
    {
        var cut = RenderName(requisitos: ["Vocação: Campeão", "Vigor ≥ 8"], pendentes: ["Vigor ≥ 8"], penalidade: ["Força −2"], outrasPenalidades: "Lenta");

        cut.Find("span").Click();

        cut.Find(".mud-dialog").TextContent.Should().Contain("Uma espada.").And.Contain("Falta: Vigor ≥ 8")
            .And.Contain("Penalidade aplicada: Força −2.").And.Contain("Outras penalidades (não aplicadas automaticamente): Lenta.");
    }

    [Fact]
    public void The_popup_shows_met_requirements_as_plain_information()
    {
        var cut = RenderName(requisitos: ["Vigor ≥ 8"], pendentes: [], penalidade: ["Força −2"]);

        cut.Find("span").Click();

        cut.Find(".mud-dialog").TextContent.Should().Contain("Requisitos: Vigor ≥ 8")
            .And.Contain("Penalidade se os requisitos não forem cumpridos: Força −2");
    }

    [Fact]
    public void The_popup_of_an_item_not_in_use_says_the_penalty_applies_when_equipped()
    {
        var cut = RenderName(requisitos: ["Vigor ≥ 8"], pendentes: ["Vigor ≥ 8"], penalidade: ["Força −2"], emUso: false);

        cut.Find("span").Click();

        cut.Find(".mud-dialog").TextContent.Should().Contain("Penalidade ao equipar: Força −2").And.NotContain("Penalidade aplicada");
    }

    [Fact]
    public void An_item_without_requirements_or_penalty_shows_just_the_description_in_the_popup()
    {
        var cut = RenderName();

        cut.FindAll(".mud-chip").Should().BeEmpty();
        cut.Find("span").Click();

        cut.Find(".mud-dialog").TextContent.Should().Contain("Uma espada.").And.NotContain("Requisitos").And.NotContain("Penalidade");
    }
}
