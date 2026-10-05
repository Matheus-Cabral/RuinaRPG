using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class RequisitosDeEquipamentoAvisoTests : MudBunitContext
{
    [Fact]
    public void Renders_nothing_for_an_item_without_requirements()
    {
        Render<RequisitosDeEquipamentoAviso>(p => p.Add(x => x.Requisitos, new List<string>())).Markup.Trim().Should().BeEmpty();
        Render<RequisitosDeEquipamentoAviso>().Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void Met_requirements_are_shown_as_plain_information_without_a_warning()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p
            .Add(x => x.Requisitos, ["Vigor ≥ 8"]).Add(x => x.Pendentes, []).Add(x => x.Penalidade, ["Força −2"]));

        cut.Markup.Should().Contain("Requisitos: Vigor ≥ 8").And.Contain("Penalidade se os requisitos não forem cumpridos: Força −2")
            .And.NotContain("Requisitos não cumpridos").And.NotContain("Outras penalidades");
        cut.FindAll(".mud-icon-root").Should().BeEmpty();
    }

    [Fact]
    public void Met_requirements_without_numeric_lines_do_not_mention_a_penalty()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p
            .Add(x => x.Requisitos, ["Vigor ≥ 8"]).Add(x => x.Pendentes, []).Add(x => x.Penalidade, []));

        cut.Markup.Should().Contain("Requisitos: Vigor ≥ 8").And.NotContain("Penalidade");
    }

    [Fact]
    public void Met_requirements_still_show_the_free_text_as_not_applied()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p
            .Add(x => x.Requisitos, ["Vigor ≥ 8"]).Add(x => x.Pendentes, []).Add(x => x.Penalidade, [])
            .Add(x => x.OutrasPenalidades, "-10 Reflexo"));

        cut.Markup.Should().Contain("Outras penalidades (não aplicadas automaticamente): -10 Reflexo.");
    }

    [Fact]
    public void Pending_requirements_show_the_warning_what_is_missing_and_the_applied_penalty()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p
            .Add(x => x.Requisitos, ["Vocação: Campeão", "Vigor ≥ 8"]).Add(x => x.Pendentes, ["Vigor ≥ 8"]).Add(x => x.Penalidade, ["Força −2", "Reflexos −1"]));

        cut.Markup.Should().Contain("Requisitos não cumpridos").And.Contain("Falta: Vigor ≥ 8").And.Contain("Penalidade aplicada: Força −2, Reflexos −1.")
            .And.NotContain("Outras penalidades");
        cut.FindAll(".mud-icon-root").Should().HaveCount(1);
    }

    [Fact]
    public void Pending_requirements_with_free_text_show_it_apart_and_not_as_applied()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p
            .Add(x => x.Requisitos, ["Vigor ≥ 8"]).Add(x => x.Pendentes, ["Vigor ≥ 8"]).Add(x => x.Penalidade, ["Força −2"])
            .Add(x => x.OutrasPenalidades, "Lenta"));

        cut.Markup.Should().Contain("Penalidade aplicada: Força −2.").And.Contain("Outras penalidades (não aplicadas automaticamente): Lenta.");
        cut.Markup.Should().NotContain("Força −2, Lenta");
    }

    [Fact]
    public void Pending_requirements_without_any_penalty_say_so()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p.Add(x => x.Requisitos, ["Vigor ≥ 8"]).Add(x => x.Pendentes, ["Vigor ≥ 8"]).Add(x => x.Penalidade, []));

        cut.Markup.Should().Contain("Falta: Vigor ≥ 8").And.Contain("Sem penalidade definida");
    }

    [Fact]
    public void Pending_requirements_with_only_free_text_do_not_say_no_penalty_is_defined()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p.Add(x => x.Requisitos, ["Vigor ≥ 8"]).Add(x => x.Pendentes, ["Vigor ≥ 8"]).Add(x => x.Penalidade, [])
            .Add(x => x.OutrasPenalidades, "Lenta"));

        cut.Markup.Should().NotContain("Sem penalidade definida").And.NotContain("Penalidade aplicada").And.Contain("Outras penalidades (não aplicadas automaticamente): Lenta.");
    }

    [Fact]
    public void Without_requirements_the_penalty_is_shown_as_not_applied_and_without_a_warning()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p
            .Add(x => x.Requisitos, []).Add(x => x.Penalidade, ["Reflexos −1"]).Add(x => x.OutrasPenalidades, "Barulhenta"));

        cut.Markup.Should().Contain("Penalidade (sem requisitos definidos, não é aplicada): Reflexos −1.")
            .And.Contain("Outras penalidades (não aplicadas automaticamente): Barulhenta.");
        cut.FindAll(".mud-icon-root").Should().BeEmpty();
    }

    [Fact]
    public void Without_requirements_only_free_text_shows_just_the_text_line()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p.Add(x => x.OutrasPenalidades, "Barulhenta"));

        cut.Markup.Should().Contain("Outras penalidades (não aplicadas automaticamente): Barulhenta.").And.NotContain("sem requisitos definidos");
    }
}
