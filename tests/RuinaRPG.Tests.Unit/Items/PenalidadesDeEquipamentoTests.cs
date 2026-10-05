using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.Items;

public class PenalidadesDeEquipamentoTests
{
    private static FichaParaRequisitos Ficha(int vigor = 5, int forca = 5) => new(
        TemIdentidadeDePersonagem: true, Nivel: 1, Vocacao: null, Classe: null, Linhagem: null, Variante: null,
        Graduacao: 0, PossuiCoracaoDeMana: false, Afinidade: null, Estrela: null, HistoricoId: null,
        Atributos: new Dictionary<Atributo, int> { [Atributo.Vigor] = vigor, [Atributo.Forca] = forca },
        SubAtributos: new Dictionary<SubAtributo, int>(),
        Pericias: new Dictionary<int, int?> { [7] = 3 });

    private static RequisitosDePassiva ExigeVigor(int minimo) => new() { Atributos = [new(Atributo.Vigor, minimo)] };
    private static PenalidadeDeEquipamento MenosForca(int valor) => new() { Atributos = [new(Atributo.Forca, valor)] };
    private static string Nome(int id) => $"Perícia {id}";

    [Fact]
    public void An_item_that_meets_its_requirements_is_not_active()
    {
        var emUso = new[] { new EquipamentoEmUso("Cota", ExigeVigor(5), MenosForca(2)) };

        PenalidadesDeEquipamento.Ativas(emUso, Ficha(vigor: 5), Nome).Should().BeEmpty();
    }

    [Fact]
    public void An_item_with_a_pending_requirement_is_active_with_its_pendencias_and_penalty()
    {
        var emUso = new[] { new EquipamentoEmUso("Cota", ExigeVigor(8), MenosForca(2)) };

        var ativa = PenalidadesDeEquipamento.Ativas(emUso, Ficha(vigor: 5), Nome).Single();

        ativa.Nome.Should().Be("Cota");
        ativa.Pendencias.Should().Equal("Vigor ≥ 8");
        ativa.Penalidade!.Atributos.Should().Equal(new PenalidadeDeAtributo(Atributo.Forca, 2));
    }

    [Fact]
    public void An_item_without_requirements_is_never_active_even_with_a_penalty()
    {
        var emUso = new[] { new EquipamentoEmUso("Elmo", null, MenosForca(2)) };

        PenalidadesDeEquipamento.Ativas(emUso, Ficha(), Nome).Should().BeEmpty();
    }

    [Fact]
    public void An_unmet_item_without_a_penalty_is_still_reported_so_the_sheet_can_warn()
    {
        var emUso = new[] { new EquipamentoEmUso("Elmo", ExigeVigor(9), null) };

        PenalidadesDeEquipamento.Ativas(emUso, Ficha(), Nome).Single().Penalidade.Should().BeNull();
    }

    [Fact]
    public void Modifiers_are_negative_and_add_up_across_items()
    {
        var ativas = new[]
        {
            new PenalidadeAtiva("A", ["x"], new() { Atributos = [new(Atributo.Forca, 2)], SubAtributos = [new(SubAtributo.Movimentacao, 1)] }),
            new PenalidadeAtiva("B", ["x"], new() { Atributos = [new(Atributo.Forca, 3)], Pericias = [new(7, 4)] }),
        };

        var modificadores = PenalidadesDeEquipamento.ComoModificadores(ativas, id => id == 7 ? "reflexos" : null);

        ArtifactBonusCalculator.Sum(modificadores, TipoDeAlvo.Atributo, "Forca").Should().Be(-5);
        ArtifactBonusCalculator.Sum(modificadores, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao).Should().Be(-1);
        ArtifactBonusCalculator.Sum(modificadores, TipoDeAlvo.Pericia, "reflexos").Should().Be(-4);
    }

    [Fact]
    public void A_penalty_on_a_removed_pericia_produces_no_modifier()
    {
        var ativas = new[] { new PenalidadeAtiva("A", ["x"], new() { Pericias = [new(99, 4)] }) };

        PenalidadesDeEquipamento.ComoModificadores(ativas, _ => null).Should().BeEmpty();
    }

    [Fact]
    public void A_null_penalty_produces_no_modifier() =>
        PenalidadesDeEquipamento.ComoModificadores([new PenalidadeAtiva("A", ["x"], null)], _ => null).Should().BeEmpty();

    [Fact]
    public void Descrever_spells_the_lines_then_the_free_text_and_skips_removed_pericias()
    {
        var penalidade = new PenalidadeDeEquipamento
        {
            Atributos = [new(Atributo.Forca, 2)],
            SubAtributos = [new(SubAtributo.ReducaoFisica, 1)],
            Pericias = [new(7, 4), new(99, 1)],
            Texto = "  Desvantagem em furtividade  ",
        };

        PenalidadesDeEquipamento.Descrever(penalidade, id => id == 7 ? "Reflexos" : null)
            .Should().Equal("Força −2", "Redução Física −1", "Reflexos −4", "Desvantagem em furtividade");
    }

    [Fact]
    public void Descrever_of_null_is_empty_and_EstaVazia_ignores_blank_text()
    {
        PenalidadesDeEquipamento.Descrever(null, _ => null).Should().BeEmpty();
        new PenalidadeDeEquipamento { Texto = "   " }.EstaVazia.Should().BeTrue();
        new PenalidadeDeEquipamento { Texto = "algo" }.EstaVazia.Should().BeFalse();
    }
}
