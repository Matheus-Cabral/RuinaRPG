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

        cut.Markup.Should().Contain("Requisitos: Vigor ≥ 8").And.NotContain("Requisitos não cumpridos");
        cut.FindAll(".mud-icon-root").Should().BeEmpty();
    }

    [Fact]
    public void Pending_requirements_show_the_warning_what_is_missing_and_the_penalty()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p
            .Add(x => x.Requisitos, ["Vocação: Campeão", "Vigor ≥ 8"]).Add(x => x.Pendentes, ["Vigor ≥ 8"]).Add(x => x.Penalidade, ["Força −2", "Lenta"]));

        cut.Markup.Should().Contain("Requisitos não cumpridos").And.Contain("Falta: Vigor ≥ 8").And.Contain("Penalidade: Força −2, Lenta");
        cut.FindAll(".mud-icon-root").Should().HaveCount(1);
    }

    [Fact]
    public void Pending_requirements_without_a_penalty_say_so()
    {
        var cut = Render<RequisitosDeEquipamentoAviso>(p => p.Add(x => x.Requisitos, ["Vigor ≥ 8"]).Add(x => x.Pendentes, ["Vigor ≥ 8"]).Add(x => x.Penalidade, []));

        cut.Markup.Should().Contain("Falta: Vigor ≥ 8").And.Contain("Sem penalidade definida");
    }
}
