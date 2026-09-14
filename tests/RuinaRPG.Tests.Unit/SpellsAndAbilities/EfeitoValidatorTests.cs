using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class EfeitoValidatorTests
{
    private static EfeitoRegra Fixo(string nome, int grau, int custo, params string[][] preRequisitos) =>
        new(nome, grau, TipoDeCusto.Fixo, custo, null, null, null, false, null, null, null, preRequisitos);

    private static EfeitoRegra PorUnidade(string nome, int grau, int custoPorUnidade, int? maxUnidades = null, bool maxEscalaPorGrau = false, params string[][] preRequisitos) =>
        new(nome, grau, TipoDeCusto.PorUnidade, null, custoPorUnidade, null, maxUnidades, maxEscalaPorGrau, null, null, null, preRequisitos);

    private static EfeitoRegra Manual(string nome, int grau, params string[][] preRequisitos) =>
        new(nome, grau, TipoDeCusto.Manual, null, null, null, null, false, null, null, null, preRequisitos);

    private static EfeitoRegra ManualPorUnidade(string nome, int grau, params string[][] preRequisitos) =>
        new(nome, grau, TipoDeCusto.ManualPorUnidade, null, null, null, null, false, null, null, null, preRequisitos);

    [Fact]
    public void Validar_accepts_a_correctly_costed_Fixo_effect_with_no_prerequisites()
    {
        var catalogo = new[] { Fixo("Contrato Mágico", 1, 3) };
        var submetidos = new[] { new EfeitoSubmetido("Contrato Mágico", null, 3) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 1, submetidos).Should().BeNull();
    }

    [Fact]
    public void Validar_rejects_an_effect_not_in_the_catalog()
    {
        var catalogo = Array.Empty<EfeitoRegra>();
        var submetidos = new[] { new EfeitoSubmetido("Inexistente", null, 1) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 1, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_rejects_an_effect_whose_Grau_is_above_the_spells_own_Grau()
    {
        var catalogo = new[] { Fixo("Atordoamento", 8, 10) };
        var submetidos = new[] { new EfeitoSubmetido("Atordoamento", null, 10) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 5, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_accepts_an_effect_whose_Grau_is_below_the_spells_own_Grau_cumulative_access()
    {
        var catalogo = new[] { Fixo("Aceleração", 2, 3, ["Duração"]), Fixo("Duração", 1, 0) };
        var submetidos = new[] { new EfeitoSubmetido("Duração", 1, 0), new EfeitoSubmetido("Aceleração", null, 3) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 6, submetidos).Should().BeNull();
    }

    [Fact]
    public void Validar_rejects_an_effect_missing_a_single_group_prerequisite()
    {
        var catalogo = new[] { Fixo("Cura", 1, 2, ["Dano"]) };
        var submetidos = new[] { new EfeitoSubmetido("Cura", null, 2) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 1, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_accepts_an_OR_group_prerequisite_satisfied_by_any_one_alternative()
    {
        // Detrito requires Duração AND Área AND (Atordoamento OR Congelar OR Enraizar).
        var catalogo = new[]
        {
            Fixo("Detrito", 4, 2, ["Duração"], ["Área"], ["Atordoamento", "Congelar", "Enraizar"]),
            Fixo("Duração", 1, 0), Fixo("Área", 3, 4), Fixo("Congelar", 3, 4),
        };
        var submetidos = new[]
        {
            new EfeitoSubmetido("Duração", null, 0), new EfeitoSubmetido("Área", null, 4),
            new EfeitoSubmetido("Congelar", null, 4), new EfeitoSubmetido("Detrito", null, 2),
        };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 4, submetidos).Should().BeNull();
    }

    [Fact]
    public void Validar_rejects_an_OR_group_prerequisite_when_none_of_the_alternatives_are_present()
    {
        var catalogo = new[]
        {
            Fixo("Detrito", 4, 2, ["Duração"], ["Área"], ["Atordoamento", "Congelar", "Enraizar"]),
            Fixo("Duração", 1, 0), Fixo("Área", 3, 4),
        };
        var submetidos = new[]
        {
            new EfeitoSubmetido("Duração", null, 0), new EfeitoSubmetido("Área", null, 4),
            new EfeitoSubmetido("Detrito", null, 2),
        };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 4, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_rejects_a_wrong_CustoPI_for_a_PorUnidade_effect()
    {
        var catalogo = new[] { PorUnidade("Aumentar Armadura", 1, 2, 5, true, ["Duração"]), Fixo("Duração", 1, 0) };
        var submetidos = new[] { new EfeitoSubmetido("Duração", null, 0), new EfeitoSubmetido("Aumentar Armadura", 3, 999 /* should be 6 */) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 1, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_rejects_a_Quantidade_exceeding_the_scaled_teto()
    {
        // Aumentar Armadura at Grau 2: teto = 5 × 2 = 10.
        var catalogo = new[] { PorUnidade("Aumentar Armadura", 1, 2, 5, true, ["Duração"]), Fixo("Duração", 1, 0) };
        var submetidos = new[] { new EfeitoSubmetido("Duração", null, 0), new EfeitoSubmetido("Aumentar Armadura", 11, 22) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 2, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_accepts_a_DerivadoDeOutroEfeito_effect_whose_cost_matches_the_sibling_Quantidade()
    {
        var catalogo = new[]
        {
            new EfeitoRegra("Dreno de Vitalidade", 3, TipoDeCusto.DerivadoDeOutroEfeito, null, 2, "Dano", null, false, null, null, null, [["Dano"]]),
            Fixo("Dano", 1, 0),
        };
        var submetidos = new[] { new EfeitoSubmetido("Dano", 3, 0), new EfeitoSubmetido("Dreno de Vitalidade", null, 6) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 3, submetidos).Should().BeNull();
    }

    [Fact]
    public void Validar_rejects_a_DerivadoDeOutroEfeito_effect_when_the_sibling_is_missing()
    {
        // PreRequisitos is deliberately empty (not ["Dano"]) so this test actually reaches the
        // QuantidadeDerivadaDeEfeito/sibling-lookup branch instead of being rejected earlier by
        // the ordinary prerequisite check.
        var catalogo = new[]
        {
            new EfeitoRegra("Dreno de Vitalidade", 3, TipoDeCusto.DerivadoDeOutroEfeito, null, 2, "Dano", null, false, null, null, null, []),
        };
        var submetidos = new[] { new EfeitoSubmetido("Dreno de Vitalidade", null, 0) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 3, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_rejects_a_DerivadoDeOutroEfeito_effect_whose_derived_Quantidade_exceeds_the_teto()
    {
        // Before the fix, the teto check used (submetido.Quantidade ?? 1), which is always 1 for a
        // DerivadoDeOutroEfeito effect (its own Quantidade is never submitted) — so the teto check
        // effectively never fired for this type. It must check the derived Quantidade instead.
        var catalogo = new[]
        {
            new EfeitoRegra("Dreno de Vitalidade", 3, TipoDeCusto.DerivadoDeOutroEfeito, null, 2, "Dano", 2, false, null, null, null, [["Dano"]]),
            Fixo("Dano", 1, 0),
        };
        // CustoPI (10) is deliberately correct for Quantidade=5 (2 × 5) so the cost check alone
        // would not reject this — only the teto check (2) against the derived Quantidade (5) can.
        var submetidos = new[] { new EfeitoSubmetido("Dano", 5, 0), new EfeitoSubmetido("Dreno de Vitalidade", null, 10) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 3, submetidos).Should().NotBeNull();
    }

    [Fact]
    public void Validar_applies_CustoAlternativo_at_the_right_Grau_when_checking_submitted_cost()
    {
        var encantamentoElemental = new EfeitoRegra("Encantamento Elemental", 2, TipoDeCusto.Fixo, 2, null, null, null, false, null, 4, 4, [["Duração"], ["Dano"]]);
        var catalogo = new[] { encantamentoElemental, Fixo("Duração", 1, 0), Fixo("Dano", 1, 0) };
        var submetidosGrau3 = new[] { new EfeitoSubmetido("Duração", null, 0), new EfeitoSubmetido("Dano", null, 0), new EfeitoSubmetido("Encantamento Elemental", null, 2) };
        var submetidosGrau4 = new[] { new EfeitoSubmetido("Duração", null, 0), new EfeitoSubmetido("Dano", null, 0), new EfeitoSubmetido("Encantamento Elemental", null, 4) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 3, submetidosGrau3).Should().BeNull();
        EfeitoValidator.Validar(catalogo, grauDaMagia: 4, submetidosGrau4).Should().BeNull();
        // Grau 4 submitting the pre-Grau-4 cost (2) must fail — the exception has kicked in.
        var submetidosErrados = new[] { new EfeitoSubmetido("Duração", null, 0), new EfeitoSubmetido("Dano", null, 0), new EfeitoSubmetido("Encantamento Elemental", null, 2) };
        EfeitoValidator.Validar(catalogo, grauDaMagia: 4, submetidosErrados).Should().NotBeNull();
    }

    [Fact]
    public void Validar_accepts_any_submitted_CustoPI_for_a_Manual_effect_without_recomputing()
    {
        // The GM's typed value IS the input for TipoDeCusto.Manual — Validar must not recompute or
        // reject it, no matter what was submitted (here, an arbitrary 999).
        var catalogo = new[] { Manual("Ritual Especial", 1) };
        var submetidos = new[] { new EfeitoSubmetido("Ritual Especial", null, 999) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 1, submetidos).Should().BeNull();
    }

    [Fact]
    public void Validar_accepts_any_submitted_CustoPI_for_a_ManualPorUnidade_effect_without_recomputing()
    {
        // Same as Manual, but with a Quantidade also present — TipoDeCusto.ManualPorUnidade's rate
        // is GM-typed, so Validar must accept the submitted CustoPI as-is here too.
        var catalogo = new[] { ManualPorUnidade("Ato Múltiplo", 1) };
        var submetidos = new[] { new EfeitoSubmetido("Ato Múltiplo", 4, 777) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 1, submetidos).Should().BeNull();
    }

    [Fact]
    public void Validar_still_enforces_Grau_and_prerequisites_for_Manual_effects()
    {
        // The skip-recompute path must only skip the cost check, not the earlier Grau/prerequisite
        // gates.
        var catalogo = new[] { Manual("Ritual Especial", 5, ["Duração"]) };
        var submetidos = new[] { new EfeitoSubmetido("Ritual Especial", null, 999) };

        EfeitoValidator.Validar(catalogo, grauDaMagia: 5, submetidos).Should().NotBeNull();
    }
}
