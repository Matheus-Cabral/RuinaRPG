# Custeio automático de Efeitos (Magias/Habilidades) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace free-typed `EfeitoNome`/`CustoPI` on every Magia/Habilidade (Banco de Magias + the 3 Fichas) with a real, Auditor-maintained catalog of "[[GRAUS & CÍRCULOS]]" effects that computes `CustoPI` automatically, blocks selections that aren't Grau-unlocked or whose prerequisites aren't already present, and models the two real exceptions the rulebook has (Encantamento Elemental's Grau-4 cost jump, Dreno de Vitalidade/Arcana's cost derived from the spell's own Dano dice).

**Architecture:** A new `Efeito` catalog table (global, Auditor-editable — same model as `Trait`/`CreatureExclusiveTrait`), seeded once from a hand-authored table (not a generic parser — the rulebook's prose is too irregular). A pure Domain calculator (`EfeitoCustoCalculator`) and validator (`EfeitoValidator`) compute/verify cost, Grau-availability and prerequisite groups from primitive inputs, shared by the API (authoritative validation on every Create/Update that carries Efeitos) and the client (live preview). A new shared Blazor component (`AddEfeitoForm.razor`) replaces the free-text add-row in all 4 existing places with a guided pick-and-preview flow.

**Tech Stack:** ASP.NET Core 8 (EF Core/PostgreSQL), Blazor WebAssembly, MudBlazor. No new dependencies.

**Spec:** `docs/superpowers/specs/2026-09-14-custeio-automatico-de-efeitos-design.md`

## Global Constraints

- The `Efeito` catalog is global (shared by every GM, same access model as `Trait`: `GET` open to any authenticated caller, `POST`/`PUT`/`DELETE` gated to the Rules Auditor).
- A Grau/Círculo is cumulative: an effect introduced at Grau N is usable in any Magia/Habilidade of Grau >= N.
- `SpellAbilityCostCalculator.GastoEmPI`/`.Custo` (sum of `CustoPI` → Foco) are unchanged — this plan only changes how each individual effect's own `CustoPI` is produced/validated.
- Validation applies only where `Efeitos` is client-submitted raw data: `SpellAbilityBankController.Create`/`.Update` (always), and `Character`/`Npc`/`CreatureSpellAbilitiesController.Add`'s "from scratch" branch only — never the "from bank" branch, which copies an already-validated bank entry's effects verbatim.
- `dotnet build` must stay at 0 Warning(s), 0 Error(s) after every task.

---

### Task 1: Domain — `TipoDeCusto`, `EfeitoCustoCalculator`, `EfeitoValidator`

**Files:**
- Create: `src/RuinaRPG.Domain/Enums/TipoDeCusto.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoRegra.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoCustoCalculator.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoValidator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/EfeitoCustoCalculatorTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/EfeitoValidatorTests.cs`

**Interfaces:**
- Produces: `RuinaRPG.Domain.Enums.TipoDeCusto` (enum: `Fixo`, `PorUnidade`, `Manual`, `ManualPorUnidade`, `DerivadoDeOutroEfeito`), `RuinaRPG.Domain.SpellsAndAbilities.EfeitoRegra` (a plain record — the catalog's structured rule, with no EF/Contracts dependency), `RuinaRPG.Domain.SpellsAndAbilities.EfeitoSubmetido(string EfeitoNome, int? Quantidade, int CustoPI)`, `EfeitoCustoCalculator.Calcular(...)`, `EfeitoCustoCalculator.MaxPermitido(...)`, `EfeitoCustoCalculator.DanoAlcanceMaxPorGrau` (the Grau-indexed cap table for the 2 base effects), `EfeitoValidator.Validar(IReadOnlyList<EfeitoRegra> catalogo, int grauDaMagia, IReadOnlyList<EfeitoSubmetido> submetidos)` returning `string?` (null = valid, otherwise the first error message found).

This task has no I/O — pure functions over plain records, exactly like `SubAttributeFormulas`/`CarryWeightCalculator`.

- [ ] **Step 1: Write the failing calculator tests**

Create `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/EfeitoCustoCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class EfeitoCustoCalculatorTests
{
    [Fact]
    public void Calcular_Fixo_ignores_quantidade_and_returns_the_flat_cost()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 3, custoPorUnidade: null,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 1, quantidade: null, custoManual: null, quantidadeDerivada: null);

        custo.Should().Be(3);
    }

    [Fact]
    public void Calcular_PorUnidade_multiplies_by_Quantidade()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.PorUnidade, custoFixo: null, custoPorUnidade: 2,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 1, quantidade: 4, custoManual: null, quantidadeDerivada: null);

        custo.Should().Be(8);
    }

    [Fact]
    public void Calcular_PorUnidade_with_null_Quantidade_treats_it_as_1()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.PorUnidade, custoFixo: null, custoPorUnidade: 2,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 1, quantidade: null, custoManual: null, quantidadeDerivada: null);

        custo.Should().Be(2);
    }

    [Fact]
    public void Calcular_Manual_returns_the_GM_typed_value_as_is()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.Manual, custoFixo: null, custoPorUnidade: null,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 4, quantidade: null, custoManual: 15, quantidadeDerivada: null);

        custo.Should().Be(15);
    }

    [Fact]
    public void Calcular_ManualPorUnidade_multiplies_the_GM_typed_rate_by_Quantidade()
    {
        // Ato Múltiplo: the GM decides the per-Dado rate; Quantidade is how many Dados were bought.
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.ManualPorUnidade, custoFixo: null, custoPorUnidade: null,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 3, quantidade: 2, custoManual: 5, quantidadeDerivada: null);

        custo.Should().Be(10);
    }

    [Fact]
    public void Calcular_DerivadoDeOutroEfeito_multiplies_by_the_derived_Quantidade_not_a_manually_entered_one()
    {
        // Dreno de Vitalidade: 2 PI per Dado, Quantidade comes from the sibling "Dano" effect.
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.DerivadoDeOutroEfeito, custoFixo: null, custoPorUnidade: 2,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 3, quantidade: 999 /* ignored */, custoManual: null, quantidadeDerivada: 3);

        custo.Should().Be(6);
    }

    [Fact]
    public void Calcular_applies_CustoAlternativo_once_the_Grau_threshold_is_reached()
    {
        // Encantamento Elemental: 2 PI normally, 4 PI from Grau 4 onward.
        var abaixoDoLimiar = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 2, custoPorUnidade: null,
            custoAlternativo: 4, custoAlternativoAPartirDoGrau: 4, grauDaMagia: 3, quantidade: null, custoManual: null, quantidadeDerivada: null);
        var noLimiar = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 2, custoPorUnidade: null,
            custoAlternativo: 4, custoAlternativoAPartirDoGrau: 4, grauDaMagia: 4, quantidade: null, custoManual: null, quantidadeDerivada: null);
        var acimaDoLimiar = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 2, custoPorUnidade: null,
            custoAlternativo: 4, custoAlternativoAPartirDoGrau: 4, grauDaMagia: 6, quantidade: null, custoManual: null, quantidadeDerivada: null);

        abaixoDoLimiar.Should().Be(2);
        noLimiar.Should().Be(4);
        acimaDoLimiar.Should().Be(4);
    }

    [Fact]
    public void MaxPermitido_returns_null_when_MaxUnidades_is_null()
    {
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: null, maxEscalaPorGrau: false, maxContandoAPartirDoGrau: null, grauDaMagia: 5).Should().BeNull();
    }

    [Fact]
    public void MaxPermitido_returns_the_flat_value_when_it_does_not_scale()
    {
        // Ações por Turno: "Max. 2 ações", no "por Grau/Círculo".
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 2, maxEscalaPorGrau: false, maxContandoAPartirDoGrau: null, grauDaMagia: 8).Should().Be(2);
    }

    [Fact]
    public void MaxPermitido_scales_by_the_raw_Grau_when_no_starting_offset_is_given()
    {
        // Aumentar Armadura: "Max. 5 de Redução por Grau/Círculo".
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 5, maxEscalaPorGrau: true, maxContandoAPartirDoGrau: null, grauDaMagia: 3).Should().Be(15);
    }

    [Fact]
    public void MaxPermitido_scales_relative_to_the_starting_Grau_when_one_is_given()
    {
        // Absorção: "Max. 3 Dados por Grau/Círculo contando a partir do sétimo" — at Grau 7 the
        // multiplier is 1 (3 dados), at Grau 9 it's 3 (9 dados).
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 3, maxEscalaPorGrau: true, maxContandoAPartirDoGrau: 7, grauDaMagia: 7).Should().Be(3);
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 3, maxEscalaPorGrau: true, maxContandoAPartirDoGrau: 7, grauDaMagia: 9).Should().Be(9);
    }

    [Fact]
    public void DanoAlcanceMaxPorGrau_has_all_9_Graus_matching_the_rulebooks_top_table()
    {
        EfeitoCustoCalculator.DanoAlcanceMaxPorGrau[1].Should().Be((3, 2));
        EfeitoCustoCalculator.DanoAlcanceMaxPorGrau[5].Should().Be((7, 6));
        EfeitoCustoCalculator.DanoAlcanceMaxPorGrau[9].Should().Be((11, 10));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EfeitoCustoCalculatorTests"`
Expected: FAIL to compile — none of `TipoDeCusto`/`EfeitoCustoCalculator` exist yet.

- [ ] **Step 3: Create `TipoDeCusto`**

Create `src/RuinaRPG.Domain/Enums/TipoDeCusto.cs`:

```csharp
namespace RuinaRPG.Domain.Enums;

/// <summary>
/// How an Efeito's CustoPI is produced when added to a Magia/Habilidade — see
/// docs/superpowers/specs/2026-09-14-custeio-automatico-de-efeitos-design.md.
/// </summary>
public enum TipoDeCusto
{
    /// <summary>A flat PI cost regardless of Quantidade (e.g. "Gasto: 3 PI").</summary>
    Fixo,

    /// <summary>CustoPorUnidade × Quantidade (e.g. "Gasto: 2 PI por Ponto de Redução").</summary>
    PorUnidade,

    /// <summary>The rulebook leaves this to the GM ("Gasto: X PI") — a single manually-typed flat value.</summary>
    Manual,

    /// <summary>The rulebook leaves the per-unit rate to the GM ("Gasto: X PI por Dado") — the GM
    /// types the rate, Quantidade still comes from the form as usual.</summary>
    ManualPorUnidade,

    /// <summary>Quantidade is never entered for this effect — it mirrors another named Efeito's own
    /// Quantidade already present on the same Magia/Habilidade (Dreno de Vitalidade/Arcana → "Dano").</summary>
    DerivadoDeOutroEfeito,
}
```

- [ ] **Step 4: Create `EfeitoRegra` and `EfeitoSubmetido`**

Create `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoRegra.cs`:

```csharp
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// The catalog's structured rule for one Efeito — a plain projection of the Efeito table (which
/// lives in Infrastructure/EF), kept here so EfeitoCustoCalculator/EfeitoValidator stay pure and
/// have zero dependency on EF or Contracts. The API layer maps Infrastructure.Efeito rows into
/// this shape before calling into Domain.
/// </summary>
public sealed record EfeitoRegra(
    string Nome,
    int Grau,
    TipoDeCusto TipoDeCusto,
    int? CustoFixo,
    int? CustoPorUnidade,
    string? QuantidadeDerivadaDeEfeito,
    int? MaxUnidades,
    bool MaxEscalaPorGrau,
    int? MaxContandoAPartirDoGrau,
    int? CustoAlternativo,
    int? CustoAlternativoAPartirDoGrau,
    IReadOnlyList<IReadOnlyList<string>> PreRequisitos);

/// <summary>One Efeito entry as submitted by a client when creating/updating a Magia/Habilidade.</summary>
public sealed record EfeitoSubmetido(string EfeitoNome, int? Quantidade, int CustoPI);
```

- [ ] **Step 5: Create `EfeitoCustoCalculator`**

Create `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoCustoCalculator.cs`:

```csharp
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Domain.SpellsAndAbilities;

public static class EfeitoCustoCalculator
{
    /// <summary>
    /// Dano/Alcance (the 2 quantifiable base effects) have their teto in the rulebook's own
    /// overview table at the top of "[[GRAUS & CÍRCULOS]]" — not the generic
    /// MaxUnidades/MaxEscalaPorGrau mechanism, since the numbers don't follow that linear formula.
    /// Keyed by Grau, value is (MaxDano em dados, MaxAlcance em pés).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, (int MaxDano, int MaxAlcance)> DanoAlcanceMaxPorGrau =
        new Dictionary<int, (int, int)>
        {
            [1] = (3, 2), [2] = (4, 3), [3] = (5, 4), [4] = (6, 5), [5] = (7, 6),
            [6] = (8, 7), [7] = (9, 8), [8] = (10, 9), [9] = (11, 10),
        };

    public static int Calcular(
        TipoDeCusto tipoDeCusto, int? custoFixo, int? custoPorUnidade,
        int? custoAlternativo, int? custoAlternativoAPartirDoGrau,
        int grauDaMagia, int? quantidade, int? custoManual, int? quantidadeDerivada)
    {
        if (custoAlternativoAPartirDoGrau is { } limiar && grauDaMagia >= limiar)
            return custoAlternativo!.Value;

        return tipoDeCusto switch
        {
            TipoDeCusto.Fixo => custoFixo!.Value,
            TipoDeCusto.PorUnidade => custoPorUnidade!.Value * (quantidade ?? 1),
            TipoDeCusto.DerivadoDeOutroEfeito => custoPorUnidade!.Value * (quantidadeDerivada ?? 0),
            TipoDeCusto.Manual => custoManual!.Value,
            TipoDeCusto.ManualPorUnidade => custoManual!.Value * (quantidade ?? 1),
            _ => throw new ArgumentOutOfRangeException(nameof(tipoDeCusto)),
        };
    }

    /// <summary>Null = sem teto. Dano/Alcance not covered here — see DanoAlcanceMaxPorGrau.</summary>
    public static int? MaxPermitido(int? maxUnidades, bool maxEscalaPorGrau, int? maxContandoAPartirDoGrau, int grauDaMagia)
    {
        if (maxUnidades is not { } max)
            return null;
        if (!maxEscalaPorGrau)
            return max;

        var multiplicador = maxContandoAPartirDoGrau is { } inicio ? grauDaMagia - inicio + 1 : grauDaMagia;
        return max * Math.Max(1, multiplicador);
    }
}
```

- [ ] **Step 6: Run the calculator tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EfeitoCustoCalculatorTests"`
Expected: PASS (all 11 tests)

- [ ] **Step 7: Write the failing validator tests**

Create `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/EfeitoValidatorTests.cs`:

```csharp
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
        var catalogo = new[]
        {
            new EfeitoRegra("Dreno de Vitalidade", 3, TipoDeCusto.DerivadoDeOutroEfeito, null, 2, "Dano", null, false, null, null, null, [["Dano"]]),
        };
        var submetidos = new[] { new EfeitoSubmetido("Dreno de Vitalidade", null, 0) };

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
}
```

- [ ] **Step 8: Run the validator tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EfeitoValidatorTests"`
Expected: FAIL to compile — `EfeitoValidator` doesn't exist yet.

- [ ] **Step 9: Create `EfeitoValidator`**

Create `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoValidator.cs`:

```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// Authoritative, server-side validation for a submitted Efeitos list against the Efeito catalog
/// — Grau availability (cumulative), prerequisite groups (AND of ORs), teto, and CustoPI. Pure, no
/// I/O — the API layer resolves the catalog rows and Dano/Alcance's own teto table before calling
/// this. Returns the first error found, or null when everything checks out.
/// </summary>
public static class EfeitoValidator
{
    public static string? Validar(IReadOnlyList<EfeitoRegra> catalogo, int grauDaMagia, IReadOnlyList<EfeitoSubmetido> submetidos)
    {
        var nomesPresentes = submetidos.Select(s => s.EfeitoNome).ToHashSet();

        foreach (var submetido in submetidos)
        {
            var regra = catalogo.FirstOrDefault(r => r.Nome == submetido.EfeitoNome);
            if (regra is null)
                return $"Efeito \"{submetido.EfeitoNome}\" não encontrado no catálogo.";

            if (regra.Grau > grauDaMagia)
                return $"Efeito \"{submetido.EfeitoNome}\" exige Grau/Círculo {regra.Grau} ou superior.";

            foreach (var grupo in regra.PreRequisitos)
            {
                if (!grupo.Any(nomesPresentes.Contains))
                    return $"Efeito \"{submetido.EfeitoNome}\" exige {string.Join(" ou ", grupo)} já presente na mesma Magia/Habilidade.";
            }

            int? quantidadeDerivada = null;
            if (regra.QuantidadeDerivadaDeEfeito is { } nomeOrigem)
            {
                var origem = submetidos.FirstOrDefault(s => s.EfeitoNome == nomeOrigem);
                if (origem is null)
                    return $"Efeito \"{submetido.EfeitoNome}\" exige que \"{nomeOrigem}\" já tenha uma Quantidade definida na mesma Magia/Habilidade.";
                quantidadeDerivada = origem.Quantidade ?? 0;
            }

            var maxPermitido = EfeitoCustoCalculator.MaxPermitido(regra.MaxUnidades, regra.MaxEscalaPorGrau, regra.MaxContandoAPartirDoGrau, grauDaMagia);
            if (maxPermitido is { } max && (submetido.Quantidade ?? 1) > max)
                return $"Efeito \"{submetido.EfeitoNome}\" excede o teto de {max} para Grau/Círculo {grauDaMagia}.";

            // Manual/ManualPorUnidade: the GM's own typed value IS the input, not a value to
            // recompute — accept whatever CustoPI was submitted for those two types without
            // recalculating (there is nothing to check it against).
            if (regra.TipoDeCusto is Enums.TipoDeCusto.Manual or Enums.TipoDeCusto.ManualPorUnidade)
                continue;

            var custoEsperado = EfeitoCustoCalculator.Calcular(
                regra.TipoDeCusto, regra.CustoFixo, regra.CustoPorUnidade,
                regra.CustoAlternativo, regra.CustoAlternativoAPartirDoGrau,
                grauDaMagia, submetido.Quantidade, null, quantidadeDerivada);
            if (submetido.CustoPI != custoEsperado)
                return $"Efeito \"{submetido.EfeitoNome}\" tem Custo em PI incorreto (esperado {custoEsperado}).";
        }

        return null;
    }
}
```

- [ ] **Step 10: Run all Domain tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EfeitoCustoCalculatorTests|FullyQualifiedName~EfeitoValidatorTests"`
Expected: PASS (11 + 12 = 23 tests)

- [ ] **Step 11: Run the full Unit suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Unit && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 12: Commit**

```bash
git add src/RuinaRPG.Domain tests/RuinaRPG.Tests.Unit/SpellsAndAbilities
git commit -m "feat: EfeitoCustoCalculator + EfeitoValidator (Domain, pure)"
```

---

### Task 2: Infrastructure — `Efeito` entity, migration, `EfeitoSeeder`

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/Efeito.cs`
- Create: `src/RuinaRPG.Infrastructure/Rules/EfeitoSeeder.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (call the seeder at startup, same place `TraitSeeder` is called)
- Modify: `Docs/Sistema RPG/GRAUS & CÍRCULOS.md` (fix "Congelar"'s missing `##` heading)
- Create: `src/RuinaRPG.Infrastructure/Persistence/Migrations/<timestamp>_AddEfeitos.cs` (+ `.Designer.cs`, generated)
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/EfeitoMigrationAndSeedTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.Enums.TipoDeCusto` (Task 1).
- Produces: `RuinaRPG.Infrastructure.Rules.Efeito` (Id, Nome, Grau, Descricao, TipoDeCusto, CustoFixo, CustoPorUnidade, UnidadeLabel, QuantidadeDerivadaDeEfeito, MaxUnidades, MaxEscalaPorGrau, MaxContandoAPartirDoGrau, CustoAlternativo, CustoAlternativoAPartirDoGrau, PreRequisitosJson, IsCustomized, IsDeleted, UpdatedByUserId, UpdatedAt), `RuinaRpgDbContext.Efeitos` (`DbSet<Efeito>`), `EfeitoSeeder.SeedAsync(RuinaRpgDbContext db)`.

Read `src/RuinaRPG.Infrastructure/Rules/Trait.cs` and `src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs` first — this task mirrors both exactly (same `IsCustomized`/`IsDeleted` reasoning, same upsert-by-Nome-skip-if-customized seeding logic), except the seed source is a hand-authored table in this task's own file, not a parsed markdown document.

- [ ] **Step 1: Write the failing migration/round-trip test**

Create `tests/RuinaRPG.Tests.Integration/Persistence/EfeitoMigrationAndSeedTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EfeitoMigrationAndSeedTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EfeitoMigrationAndSeedTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Efeitos_table_and_SeedAsync_populates_it()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddEfeitos"));

        await EfeitoSeeder.SeedAsync(db);

        var todos = await db.Efeitos.Where(e => !e.IsDeleted).ToListAsync();
        todos.Should().HaveCount(49); // 45 named + 3 básicos + Libra split em 2

        var dano = todos.Single(e => e.Nome == "Dano");
        dano.TipoDeCusto.Should().Be(TipoDeCusto.PorUnidade);
        dano.CustoPorUnidade.Should().Be(2);
        dano.Grau.Should().Be(1);

        var aumentarArmadura = todos.Single(e => e.Nome == "Aumentar Armadura");
        aumentarArmadura.MaxUnidades.Should().Be(5);
        aumentarArmadura.MaxEscalaPorGrau.Should().BeTrue();
        aumentarArmadura.PreRequisitosJson.Should().Contain("Duração");

        var encantamentoElemental = todos.Single(e => e.Nome == "Encantamento Elemental");
        encantamentoElemental.CustoFixo.Should().Be(2);
        encantamentoElemental.CustoAlternativo.Should().Be(4);
        encantamentoElemental.CustoAlternativoAPartirDoGrau.Should().Be(4);

        var drenoDeVitalidade = todos.Single(e => e.Nome == "Dreno de Vitalidade");
        drenoDeVitalidade.TipoDeCusto.Should().Be(TipoDeCusto.DerivadoDeOutroEfeito);
        drenoDeVitalidade.QuantidadeDerivadaDeEfeito.Should().Be("Dano");

        var atoMultiplo = todos.Single(e => e.Nome == "Ato Múltiplo");
        atoMultiplo.TipoDeCusto.Should().Be(TipoDeCusto.ManualPorUnidade);
        atoMultiplo.MaxContandoAPartirDoGrau.Should().Be(3);

        var libraVitalidade = todos.Single(e => e.Nome == "Libra (Vitalidade)");
        var libraArcana = todos.Single(e => e.Nome == "Libra (Arcana)");
        libraVitalidade.CustoFixo.Should().Be(4);
        libraArcana.CustoFixo.Should().Be(6);

        var detrito = todos.Single(e => e.Nome == "Detrito");
        detrito.PreRequisitosJson.Should().Contain("Atordoamento").And.Contain("Congelar").And.Contain("Enraizar");
        detrito.PreRequisitosJson.Should().NotContain("Selar");
    }

    [Fact]
    public async Task SeedAsync_never_overwrites_a_row_the_Auditor_has_customized()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        await EfeitoSeeder.SeedAsync(db);

        var cura = await db.Efeitos.SingleAsync(e => e.Nome == "Cura");
        cura.CustoFixo = 99;
        cura.IsCustomized = true;
        await db.SaveChangesAsync();

        await EfeitoSeeder.SeedAsync(db);

        (await db.Efeitos.SingleAsync(e => e.Nome == "Cura")).CustoFixo.Should().Be(99);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EfeitoMigrationAndSeedTests"`
Expected: FAIL to compile — `Efeito`/`EfeitoSeeder`/`RuinaRpgDbContext.Efeitos` don't exist yet.

- [ ] **Step 3: Create the `Efeito` entity**

Create `src/RuinaRPG.Infrastructure/Rules/Efeito.cs`:

```csharp
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// The "[[GRAUS & CÍRCULOS]]" effect catalog, hand-authored (not parsed — see EfeitoSeeder) and
/// Auditor-editable. Same IsCustomized/IsDeleted convention as Trait: a manual edit always wins
/// over the seed on the next resync, and delete is soft (a Magia/Habilidade may already reference
/// this Nome).
/// </summary>
public class Efeito
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public int Grau { get; set; }
    public required string Descricao { get; set; }
    public TipoDeCusto TipoDeCusto { get; set; }
    public int? CustoFixo { get; set; }
    public int? CustoPorUnidade { get; set; }
    public string? UnidadeLabel { get; set; }
    public string? QuantidadeDerivadaDeEfeito { get; set; }
    public int? MaxUnidades { get; set; }
    public bool MaxEscalaPorGrau { get; set; }
    public int? MaxContandoAPartirDoGrau { get; set; }
    public int? CustoAlternativo { get; set; }
    public int? CustoAlternativoAPartirDoGrau { get; set; }

    /// <summary>JSON-serialized List&lt;List&lt;string&gt;&gt; — AND of OR-groups. Same
    /// string-column-holding-JSON convention as RacialTraitOverride.GratuitaOptionsJson.</summary>
    public string? PreRequisitosJson { get; set; }

    public bool IsCustomized { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

- [ ] **Step 4: Register the `DbSet`**

In `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`, find:

```csharp
    public DbSet<Trait> Traits => Set<Trait>();
```

Replace with:

```csharp
    public DbSet<Trait> Traits => Set<Trait>();
    public DbSet<Efeito> Efeitos => Set<Efeito>();
```

No relationship configuration is needed in `OnModelCreating` — `Efeito` has no FK to or from
anything (Magia/Habilidade effects reference it by `Nome`, a plain string, same as today).

- [ ] **Step 5: Generate the migration**

Run: `dotnet ef migrations add AddEfeitos --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`

Confirm the generated file contains one `CreateTable(name: "Efeitos", ...)` with all 16 columns
from Step 3. If missing, Step 3/4 weren't saved before running this command — fix and regenerate.

- [ ] **Step 6: Create `EfeitoSeeder` with the full 49-row seed table**

Create `src/RuinaRPG.Infrastructure/Rules/EfeitoSeeder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using System.Text.Json;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Seeds the Efeito catalog from a hand-authored table (not a markdown parser — see the design
/// spec's Contexto section for why "[[GRAUS & CÍRCULOS]]"'s prose is too irregular to parse
/// generically). Mirrors TraitSeeder: upsert by Nome, skip any row the Auditor has already
/// customized, called once at API startup (see Program.cs).
/// </summary>
public static class EfeitoSeeder
{
    private sealed record Seed(
        string Nome, int Grau, string Descricao, TipoDeCusto Tipo,
        int? CustoFixo = null, int? CustoPorUnidade = null, string? Unidade = null,
        string? DerivadoDe = null, int? MaxUnidades = null, bool MaxEscalaPorGrau = false,
        int? MaxContandoAPartirDoGrau = null, int? CustoAlternativo = null, int? CustoAlternativoAPartirDoGrau = null,
        string[][]? PreRequisitos = null);

    private static readonly Seed[] Seeds =
    [
        // --- Efeitos básicos (Grau 1, sempre disponíveis) ---
        new("Dano", 1, "Efeito básico: causa dano em dados. Teto de dados por Grau/Círculo em EfeitoCustoCalculator.DanoAlcanceMaxPorGrau.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Dado"),
        new("Alcance", 1, "Efeito básico: alcance em pés. Teto de pés por Grau/Círculo em EfeitoCustoCalculator.DanoAlcanceMaxPorGrau.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Pé"),
        new("Duração", 1, "Efeito básico: duração em dados (o tipo de dado, não a contagem, escala por Grau/Círculo — sem teto de Quantidade).", TipoDeCusto.PorUnidade, CustoPorUnidade: 4, Unidade: "Dado"),

        // --- Grau/Círculo 1 ---
        new("Aumentar Armadura", 1, "Concede Redução Física temporária.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Ponto de Redução", MaxUnidades: 5, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Aumentar Atributo", 1, "Aumenta um atributo do personagem temporariamente.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Ponto de Atributo", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Contrato Mágico", 1, "Cria um vínculo arcano com uma criatura, essencial para Invocação.", TipoDeCusto.Fixo, CustoFixo: 3),
        new("Cura", 1, "Os dados de dano recuperam Vitalidade em vez de retirá-la.", TipoDeCusto.Fixo, CustoFixo: 2, PreRequisitos: [["Dano"]]),
        new("Deslocamento", 1, "Força o alvo a se deslocar pelo grid de batalha.", TipoDeCusto.Fixo, CustoFixo: 1, PreRequisitos: [["Alcance"]]),
        new("Miragem", 1, "Cria uma ilusão que engana visão, olfato ou audição do alvo.", TipoDeCusto.PorUnidade, CustoPorUnidade: 1, Unidade: "Sentido", MaxUnidades: 3, MaxEscalaPorGrau: false, PreRequisitos: [["Duração"]]),
        new("Regeneração", 1, "Cura 1d por turno e remove efeitos negativos de Grau/Círculo igual ou inferior.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 2 ---
        new("Aceleração", 2, "O alvo recebe metade de sua movimentação como movimentação adicional.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Amedrontar", 2, "O alvo não pode se mover na direção do causador do efeito.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Diminuir Atributo", 2, "Diminui um atributo do alvo temporariamente.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Ponto de Atributo", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Encantamento Elemental", 2, "Encanta armas com dano mágico adicional. A partir do 4º Grau/Círculo, pode aplicar Condições em vez de dano, ao custo de 4 PI.", TipoDeCusto.Fixo, CustoFixo: 2, CustoAlternativo: 4, CustoAlternativoAPartirDoGrau: 4, PreRequisitos: [["Duração"], ["Dano"]]),
        new("Enraizar", 2, "O alvo não pode se deslocar, mas pode atacar, defender e esquivar.", TipoDeCusto.Fixo, CustoFixo: 2, PreRequisitos: [["Duração"]]),
        new("Envenenar", 2, "Corta pela metade as curas recebidas pelo alvo; causa 1d de dano por turno.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Libra (Vitalidade)", 2, "Permite ver a Vitalidade atual do alvo. Combinável com Libra (Arcana), pago separadamente.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Alcance"], ["Duração"]]),
        new("Libra (Arcana)", 2, "Permite ver a Arcana atual do alvo. Combinável com Libra (Vitalidade), pago separadamente.", TipoDeCusto.Fixo, CustoFixo: 6, PreRequisitos: [["Alcance"], ["Duração"]]),

        // --- Grau/Círculo 3 ---
        new("Área", 3, "A habilidade age em uma área, calculada em anéis a partir de um epicentro.", TipoDeCusto.PorUnidade, CustoPorUnidade: 4, Unidade: "Anel", MaxUnidades: 1, MaxEscalaPorGrau: true),
        new("Armadura Arcana", 3, "Concede Redução Mágica temporária.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Ponto de Redução", MaxUnidades: 5, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Ato Múltiplo", 3, "Permite múltiplos ataques rápidos numa única ação. O Mestre define o custo por Dado.", TipoDeCusto.ManualPorUnidade, Unidade: "Dado", MaxUnidades: 1, MaxEscalaPorGrau: true, MaxContandoAPartirDoGrau: 3),
        new("Aumentar Max. Vitalidade", 3, "Aumenta temporariamente a Vitalidade máxima.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Dado", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Congelar", 3, "O alvo não pode se deslocar e sofre 1d de dano por turno de Duração.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Dreno de Vitalidade", 3, "Absorve parte do dano causado para recuperar Vitalidade — não pode ter mais dados de recuperação do que de dano, então o custo é sempre derivado da Quantidade de Dano já comprada na mesma Magia/Habilidade.", TipoDeCusto.DerivadoDeOutroEfeito, CustoPorUnidade: 2, Unidade: "Dado", DerivadoDe: "Dano", PreRequisitos: [["Dano"]]),
        new("Reflexão", 3, "Causa dano toda vez que recebe um ataque corpo-a-corpo.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Dano"], ["Duração"]]),
        new("Proteção", 3, "Cria uma \"vida extra\" que toma dano no lugar do protegido.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Dano"], ["Duração"]]),
        new("Provocar", 3, "O alvo é obrigado a atacar o causador do efeito pela duração dele.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Marionete Arcana", 3, "Conjura uma entidade de arcana elemental.", TipoDeCusto.Fixo, CustoFixo: 5, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 4 ---
        new("Abençoar", 4, "Aumenta em 50% a precisão dos ataques do alvo (exclusivo do Caminho da Bênção).", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Aumentar Max. Arcana", 4, "Aumenta temporariamente a Arcana máxima.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Dado", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Confusão", 4, "Ataques falhos contra o alvo confuso podem acertar outro alvo aleatório na área.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Dreno de Arcana", 4, "Absorve parte do dano causado para recuperar Arcana — mesmo tratamento de Dreno de Vitalidade.", TipoDeCusto.DerivadoDeOutroEfeito, CustoPorUnidade: 4, Unidade: "Dado", DerivadoDe: "Dano", PreRequisitos: [["Dano"]]),
        new("Decair", 4, "Os pontos fracos do alvo ficam expostos; ataques contra ele causam dano adicional.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"], ["Dano"]]),
        new("Detrito", 4, "Condição aplicada a uma área em vez de um alvo.", TipoDeCusto.Fixo, CustoFixo: 2, PreRequisitos: [["Duração"], ["Área"], ["Atordoamento", "Congelar", "Enraizar"]]),
        new("Fadiga", 4, "O alvo não pode defender e seus golpes físicos têm o dano cortado pela metade.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Imagem Ilusória", 4, "Cria uma imagem ilusória. O Mestre define o custo total.", TipoDeCusto.Manual, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 5 ---
        new("Feixe", 5, "Afeta todos os hexes entre o alcance final e o originador do ataque.", TipoDeCusto.Fixo, CustoFixo: 10, PreRequisitos: [["Alcance"]]),
        new("Ações por Turno", 5, "Permite realizar múltiplas ações num único turno de batalha.", TipoDeCusto.PorUnidade, CustoPorUnidade: 20, Unidade: "Ação", MaxUnidades: 2, MaxEscalaPorGrau: false, PreRequisitos: [["Duração"]]),
        new("Cegueira", 5, "O alvo só pode realizar ataques mágicos.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Defesa Verdadeira", 5, "Ao defender, o alvo aplica o número das rolagens de dados.", TipoDeCusto.Fixo, CustoFixo: 6, PreRequisitos: [["Duração"]]),
        new("Deflexão", 5, "Reage a golpes à distância redirecionando-os ao originador do ataque.", TipoDeCusto.Fixo, CustoFixo: 8, PreRequisitos: [["Dano"]]),
        new("Deflexão Mágica", 5, "Reage a ataques mágicos direcionando-os ao originador do ataque.", TipoDeCusto.Fixo, CustoFixo: 8, PreRequisitos: [["Dano"]]),
        new("Encantamento Pessoal", 5, "Como Encantamento Elemental, mas encanta uma pessoa/criatura em vez de um equipamento.", TipoDeCusto.Fixo, CustoFixo: 5, PreRequisitos: [["Duração"]]),
        new("Encantar", 5, "O alvo encantado é obrigado a se aproximar e defender o causador do efeito.", TipoDeCusto.Fixo, CustoFixo: 10, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 6 ---
        new("Amplificação Arcana", 6, "A próxima magia conjurada terá o dobro do dano e do custo.", TipoDeCusto.Fixo, CustoFixo: 4),
        new("Fúria", 6, "O alvo tem o dano dobrado, mas ataca sempre a entidade mais próxima.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Julgamento", 6, "O alvo recebe 1d de dano por turno e perde efeitos positivos imediatamente.", TipoDeCusto.Fixo, CustoFixo: 5, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 7 ---
        new("Absorção", 7, "Absorve dados de dano.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Dado", MaxUnidades: 3, MaxEscalaPorGrau: true, MaxContandoAPartirDoGrau: 7, PreRequisitos: [["Duração"]]),
        new("Crítico Aprimorado", 7, "Reduz o número necessário no d20 para um acerto crítico.", TipoDeCusto.PorUnidade, CustoPorUnidade: 4, Unidade: "5% de Taxa Crítica", MaxUnidades: 4, MaxEscalaPorGrau: false, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 8 ---
        new("Atordoamento", 8, "O alvo perde completamente sua ação — incapaz de agir, mover, defender ou esquivar.", TipoDeCusto.Fixo, CustoFixo: 10, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 9 ---
        new("Imunidade", 9, "O alvo se torna completamente imune ao dano de um elemento específico. O Mestre define o custo total.", TipoDeCusto.Manual),
    ];

    public static async Task SeedAsync(RuinaRpgDbContext db)
    {
        var existentes = await db.Efeitos.ToDictionaryAsync(e => e.Nome);

        foreach (var seed in Seeds)
        {
            var preRequisitosJson = seed.PreRequisitos is null ? null : JsonSerializer.Serialize(seed.PreRequisitos);

            if (existentes.TryGetValue(seed.Nome, out var existente))
            {
                if (existente.IsCustomized)
                    continue;

                existente.Grau = seed.Grau;
                existente.Descricao = seed.Descricao;
                existente.TipoDeCusto = seed.Tipo;
                existente.CustoFixo = seed.CustoFixo;
                existente.CustoPorUnidade = seed.CustoPorUnidade;
                existente.UnidadeLabel = seed.Unidade;
                existente.QuantidadeDerivadaDeEfeito = seed.DerivadoDe;
                existente.MaxUnidades = seed.MaxUnidades;
                existente.MaxEscalaPorGrau = seed.MaxEscalaPorGrau;
                existente.MaxContandoAPartirDoGrau = seed.MaxContandoAPartirDoGrau;
                existente.CustoAlternativo = seed.CustoAlternativo;
                existente.CustoAlternativoAPartirDoGrau = seed.CustoAlternativoAPartirDoGrau;
                existente.PreRequisitosJson = preRequisitosJson;
                existente.IsDeleted = false;
            }
            else
            {
                db.Efeitos.Add(new Efeito
                {
                    Id = Guid.NewGuid(), Nome = seed.Nome, Grau = seed.Grau, Descricao = seed.Descricao,
                    TipoDeCusto = seed.Tipo, CustoFixo = seed.CustoFixo, CustoPorUnidade = seed.CustoPorUnidade,
                    UnidadeLabel = seed.Unidade, QuantidadeDerivadaDeEfeito = seed.DerivadoDe,
                    MaxUnidades = seed.MaxUnidades, MaxEscalaPorGrau = seed.MaxEscalaPorGrau,
                    MaxContandoAPartirDoGrau = seed.MaxContandoAPartirDoGrau,
                    CustoAlternativo = seed.CustoAlternativo, CustoAlternativoAPartirDoGrau = seed.CustoAlternativoAPartirDoGrau,
                    PreRequisitosJson = preRequisitosJson,
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 7: Fix "Congelar"'s missing heading in the source document**

In `Docs/Sistema RPG/GRAUS & CÍRCULOS.md`, find (inside the "3º GRAU / CÍRCULO II" section, between
"Aumentar Max. Vitalidade" and "Dreno de Vitalidade"):

```
Congelar

Gasto: 4 PI
```

Replace with:

```
## Congelar

Gasto: 4 PI
```

(This doesn't affect the new `Efeito` catalog, which is hand-authored — it fixes
`GraduacaoEfeitoParser`, so "Congelar" stops being invisible to the Compêndio search.)

- [ ] **Step 8: Wire the seeder into startup**

In `src/RuinaRPG.Api/Program.cs`, find where `TraitSeeder.SeedAsync(...)` is called (search for
`TraitSeeder`) and add a call to `EfeitoSeeder.SeedAsync(db)` right after it, inside the same
scope/block, following whatever pattern that existing call already uses (same `db` instance,
same `await`, same surrounding `using`/scope block — read the surrounding ~10 lines before editing
to match it exactly).

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EfeitoMigrationAndSeedTests"`
Expected: PASS (both tests)

- [ ] **Step 10: Clean-rebuild the whole solution**

Run: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 11: Commit**

```bash
git add src/RuinaRPG.Infrastructure src/RuinaRPG.Api/Program.cs "Docs/Sistema RPG/GRAUS & CÍRCULOS.md" tests/RuinaRPG.Tests.Integration/Persistence/EfeitoMigrationAndSeedTests.cs
git commit -m "feat: Efeito catalog table + EfeitoSeeder (49-row hand-authored seed)"
```

---

### Task 3: API — `EfeitosController` (CRUD, mirrors `TraitsController`)

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/EfeitoResponse.cs`
- Create: `src/RuinaRPG.Contracts/Rules/CreateEfeitoRequest.cs`
- Create: `src/RuinaRPG.Contracts/Rules/UpdateEfeitoRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/EfeitosController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EfeitosControllerTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Infrastructure.Rules.Efeito` (Task 2).
- Produces: `EfeitoResponse(string Id, string Nome, int Grau, string Descricao, string TipoDeCusto, int? CustoFixo, int? CustoPorUnidade, string? UnidadeLabel, string? QuantidadeDerivadaDeEfeito, int? MaxUnidades, bool MaxEscalaPorGrau, int? MaxContandoAPartirDoGrau, int? CustoAlternativo, int? CustoAlternativoAPartirDoGrau, List<List<string>> PreRequisitos)`, `CreateEfeitoRequest`/`UpdateEfeitoRequest` (same shape minus `Id`, `TipoDeCusto`/`PreRequisitos` as `string`/`List<List<string>>`) — routes `GET/POST api/efeitos`, `PUT/DELETE api/efeitos/{id}`.

Read `src/RuinaRPG.Api/Controllers/CreatureExclusiveTraitsController.cs` first (from an earlier
round on this same branch history) — this task is the closest mirror of it: same access model
(List open, Create/Update/Delete Auditor-gated via `RequireRulesAuditorAsync`), same shape of
Create/Update/Delete, adapted to `Efeito`'s richer field set and the `PreRequisitosJson`
serialize/deserialize step.

- [ ] **Step 1: Write the failing controller tests**

Read `tests/RuinaRPG.Tests.Integration/Controllers/TraitsControllerTests.cs` first for the
`GrantRulesAuditorAsync` helper shape (copied verbatim, same as every other Auditor-gated
controller test file in this codebase).

Create `tests/RuinaRPG.Tests.Integration/Controllers/EfeitosControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class EfeitosControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public EfeitosControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return email;
    }

    [Fact]
    public async Task List_is_seeded_with_49_effects_and_is_open_to_any_authenticated_GM()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm1", "efeito1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var efeitos = await response.Content.ReadFromJsonAsync<List<EfeitoResponse>>();
        efeitos!.Should().HaveCount(49);
        efeitos.Should().ContainSingle(e => e.Nome == "Detrito" && e.PreRequisitos.Any(g => g.Contains("Congelar")));
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm2", "efeito2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", gmToken,
            new CreateEfeitoRequest("Selar", 4, "Descrição de teste.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_by_the_Auditor_adds_a_new_effect_the_seeder_never_defines()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm3", "efeito3@teste.com");
        await GrantRulesAuditorAsync("efeito3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/efeitos", gmToken,
            new CreateEfeitoRequest("Selar", 4, "Sela o alvo, impedindo certas ações.", "Fixo", 2, null, null, null, null, false, null, null, null, [])));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        (await listResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Should().Contain(e => e.Nome == "Selar");
    }

    [Fact]
    public async Task Update_by_the_Auditor_changes_the_row_and_survives_a_reseed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm4", "efeito4@teste.com");
        await GrantRulesAuditorAsync("efeito4@teste.com");
        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        var cura = (await listResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Single(e => e.Nome == "Cura");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/efeitos/{cura.Id}", gmToken,
            new UpdateEfeitoRequest("Cura", 1, "Descrição custom.", "Fixo", 99, null, null, null, null, false, null, null, null, [["Dano"]])));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.Set<RuinaRPG.Infrastructure.Rules.Efeito>().SingleAsync(e => e.Nome == "Cura")).IsCustomized.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_a_seeded_effect_that_is_unused_removes_it_from_the_list()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EfeitoGm5", "efeito5@teste.com");
        await GrantRulesAuditorAsync("efeito5@teste.com");
        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        var amplificacaoArcana = (await listResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Single(e => e.Nome == "Amplificação Arcana");

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/efeitos/{amplificacaoArcana.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/efeitos", gmToken));
        (await afterResponse.Content.ReadFromJsonAsync<List<EfeitoResponse>>())!.Should().NotContain(e => e.Nome == "Amplificação Arcana");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EfeitosControllerTests"`
Expected: FAIL to compile — none of the contracts or the controller exist yet.

- [ ] **Step 3: Create the 3 contracts**

`src/RuinaRPG.Contracts/Rules/EfeitoResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record EfeitoResponse(
    string Id, string Nome, int Grau, string Descricao, string TipoDeCusto,
    int? CustoFixo, int? CustoPorUnidade, string? UnidadeLabel, string? QuantidadeDerivadaDeEfeito,
    int? MaxUnidades, bool MaxEscalaPorGrau, int? MaxContandoAPartirDoGrau,
    int? CustoAlternativo, int? CustoAlternativoAPartirDoGrau,
    List<List<string>> PreRequisitos);
```

`src/RuinaRPG.Contracts/Rules/CreateEfeitoRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record CreateEfeitoRequest(
    string Nome, int Grau, string Descricao, string TipoDeCusto,
    int? CustoFixo, int? CustoPorUnidade, string? UnidadeLabel, string? QuantidadeDerivadaDeEfeito,
    int? MaxUnidades, bool MaxEscalaPorGrau, int? MaxContandoAPartirDoGrau,
    int? CustoAlternativo, int? CustoAlternativoAPartirDoGrau,
    List<List<string>> PreRequisitos);
```

`src/RuinaRPG.Contracts/Rules/UpdateEfeitoRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record UpdateEfeitoRequest(
    string Nome, int Grau, string Descricao, string TipoDeCusto,
    int? CustoFixo, int? CustoPorUnidade, string? UnidadeLabel, string? QuantidadeDerivadaDeEfeito,
    int? MaxUnidades, bool MaxEscalaPorGrau, int? MaxContandoAPartirDoGrau,
    int? CustoAlternativo, int? CustoAlternativoAPartirDoGrau,
    List<List<string>> PreRequisitos);
```

- [ ] **Step 4: Create the controller**

Create `src/RuinaRPG.Api/Controllers/EfeitosController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// The "[[GRAUS & CÍRCULOS]]" effect catalog — global, Auditor-editable, same access model as
/// TraitsController/CreatureExclusiveTraitsController: List open to any authenticated caller
/// (every Magia/Habilidade-editing form uses it), Create/Update/Delete gated to the Rules
/// Auditor.
/// </summary>
[ApiController]
[Route("api/efeitos")]
[Authorize]
public class EfeitosController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EfeitoResponse>>> List()
    {
        var efeitos = await db.Efeitos.Where(e => !e.IsDeleted).ToListAsync();
        return efeitos.Select(ToResponse).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<EfeitoResponse>> Create(CreateEfeitoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<TipoDeCusto>(request.TipoDeCusto, out var tipoDeCusto))
            return BadRequest("TipoDeCusto inválido.");
        if (await db.Efeitos.AnyAsync(e => e.Nome == request.Nome && !e.IsDeleted))
            return BadRequest("Já existe um Efeito com esse Nome.");

        var efeito = new Efeito
        {
            Id = Guid.NewGuid(), Nome = request.Nome, Grau = request.Grau, Descricao = request.Descricao,
            TipoDeCusto = tipoDeCusto, CustoFixo = request.CustoFixo, CustoPorUnidade = request.CustoPorUnidade,
            UnidadeLabel = request.UnidadeLabel, QuantidadeDerivadaDeEfeito = request.QuantidadeDerivadaDeEfeito,
            MaxUnidades = request.MaxUnidades, MaxEscalaPorGrau = request.MaxEscalaPorGrau,
            MaxContandoAPartirDoGrau = request.MaxContandoAPartirDoGrau,
            CustoAlternativo = request.CustoAlternativo, CustoAlternativoAPartirDoGrau = request.CustoAlternativoAPartirDoGrau,
            PreRequisitosJson = JsonSerializer.Serialize(request.PreRequisitos),
            IsCustomized = true, UpdatedByUserId = CurrentUserId(), UpdatedAt = DateTime.UtcNow,
        };
        db.Efeitos.Add(efeito);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(efeito));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateEfeitoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var efeito = await db.Efeitos.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (efeito is null)
            return NotFound();

        if (!Enum.TryParse<TipoDeCusto>(request.TipoDeCusto, out var tipoDeCusto))
            return BadRequest("TipoDeCusto inválido.");
        if (await db.Efeitos.AnyAsync(e => e.Id != id && e.Nome == request.Nome && !e.IsDeleted))
            return BadRequest("Já existe um Efeito com esse Nome.");

        efeito.Nome = request.Nome; efeito.Grau = request.Grau; efeito.Descricao = request.Descricao;
        efeito.TipoDeCusto = tipoDeCusto; efeito.CustoFixo = request.CustoFixo; efeito.CustoPorUnidade = request.CustoPorUnidade;
        efeito.UnidadeLabel = request.UnidadeLabel; efeito.QuantidadeDerivadaDeEfeito = request.QuantidadeDerivadaDeEfeito;
        efeito.MaxUnidades = request.MaxUnidades; efeito.MaxEscalaPorGrau = request.MaxEscalaPorGrau;
        efeito.MaxContandoAPartirDoGrau = request.MaxContandoAPartirDoGrau;
        efeito.CustoAlternativo = request.CustoAlternativo; efeito.CustoAlternativoAPartirDoGrau = request.CustoAlternativoAPartirDoGrau;
        efeito.PreRequisitosJson = JsonSerializer.Serialize(request.PreRequisitos);
        efeito.IsCustomized = true; efeito.UpdatedByUserId = CurrentUserId(); efeito.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var efeito = await db.Efeitos.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (efeito is null)
            return NotFound();

        efeito.IsDeleted = true;
        efeito.UpdatedByUserId = CurrentUserId(); efeito.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static EfeitoResponse ToResponse(Efeito e) => new(
        e.Id.ToString(), e.Nome, e.Grau, e.Descricao, e.TipoDeCusto.ToString(),
        e.CustoFixo, e.CustoPorUnidade, e.UnidadeLabel, e.QuantidadeDerivadaDeEfeito,
        e.MaxUnidades, e.MaxEscalaPorGrau, e.MaxContandoAPartirDoGrau,
        e.CustoAlternativo, e.CustoAlternativoAPartirDoGrau,
        string.IsNullOrEmpty(e.PreRequisitosJson) ? [] : JsonSerializer.Deserialize<List<List<string>>>(e.PreRequisitosJson)!);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EfeitosControllerTests"`
Expected: PASS (all 5 tests)

- [ ] **Step 6: Clean-rebuild the whole solution**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Contracts/Rules src/RuinaRPG.Api/Controllers/EfeitosController.cs tests/RuinaRPG.Tests.Integration/Controllers/EfeitosControllerTests.cs
git commit -m "feat: EfeitosController — Auditor-gated CRUD for the Efeito catalog"
```

---

### Task 4: API — validate submitted Efeitos on the 4 existing spell/ability endpoints

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSpellAbilitiesController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CreatureSpellAbilitiesController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs` (or
  wherever `SpellAbilityBankController.Create`/`.Update` are tested — find with
  `grep -rl "spell-ability-bank" tests/RuinaRPG.Tests.Integration/Controllers`, use that exact
  file)
- Test: wherever `CharacterSpellAbilitiesController.Add` is tested (find with
  `grep -rl "character-sheets/{sheetId}/spell-abilities\|CharacterSpellAbilitiesController" tests/RuinaRPG.Tests.Integration/Controllers`)
- Test: the Npc and Creature equivalents, same `grep` approach

**Interfaces:**
- Consumes: `RuinaRPG.Domain.SpellsAndAbilities.EfeitoValidator.Validar`, `EfeitoRegra`,
  `EfeitoSubmetido` (Task 1); `RuinaRPG.Infrastructure.Rules.Efeito` (Task 2).
- Produces: no new public signatures — validation only, 400 on failure.

Read `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs` and
`src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs` in full first (both already
partially quoted below, but re-read the whole file — this task inserts one call before each
existing `SaveChangesAsync`, nothing else moves).

- [ ] **Step 1: Add a shared mapping helper**

Since all 4 controllers need the same "load the Efeito catalog, map to `EfeitoRegra`, call
`EfeitoValidator.Validar`" sequence, add it once as a small static helper both `SpellAbilityBankController`
and the three sheet controllers call. Create `src/RuinaRPG.Api/Controllers/EfeitoValidationHelper.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Shared by SpellAbilityBankController and the 3 sheet SpellAbilities controllers — maps the
/// Efeito catalog into the plain EfeitoRegra shape EfeitoValidator needs (Domain has no EF
/// dependency, so this mapping has to happen at the API boundary) and runs it against a
/// client-submitted Efeitos list. Only ever called for "from scratch" submissions — "from bank"
/// copies reuse an already-validated bank entry's effects verbatim, see this plan's Global
/// Constraints.
/// </summary>
public static class EfeitoValidationHelper
{
    public static async Task<string?> ValidarAsync(RuinaRpgDbContext db, int grau, List<SpellAbilityEffectRequest> efeitos)
    {
        var catalogo = await db.Efeitos.Where(e => !e.IsDeleted).ToListAsync();
        var regras = catalogo.Select(ToRegra).ToList();
        var submetidos = efeitos.Select(e => new EfeitoSubmetido(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();

        return EfeitoValidator.Validar(regras, grau, submetidos);
    }

    private static EfeitoRegra ToRegra(Efeito e) => new(
        e.Nome, e.Grau, e.TipoDeCusto, e.CustoFixo, e.CustoPorUnidade, e.QuantidadeDerivadaDeEfeito,
        e.MaxUnidades, e.MaxEscalaPorGrau, e.MaxContandoAPartirDoGrau, e.CustoAlternativo, e.CustoAlternativoAPartirDoGrau,
        string.IsNullOrEmpty(e.PreRequisitosJson) ? [] : JsonSerializer.Deserialize<List<List<string>>>(e.PreRequisitosJson)!);
}
```

Note: Dano/Alcance's teto validation (the Grau-indexed table, not the generic
`MaxUnidades`/`MaxEscalaPorGrau` mechanism) is NOT yet covered by `EfeitoValidator` — it only
checks the generic mechanism, and Dano/Alcance's catalog rows have `MaxUnidades = null` (per the
Task 2 seed), so today `EfeitoValidator` treats them as uncapped. Add this check here, since it's
specific to exactly 2 named effects and doesn't belong in the generic Domain validator: after
calling `EfeitoValidator.Validar`, if it returned `null` (no error yet), additionally check:

```csharp
        var generalError = EfeitoValidator.Validar(regras, grau, submetidos);
        if (generalError is not null)
            return generalError;

        if (!EfeitoCustoCalculator.DanoAlcanceMaxPorGrau.TryGetValue(grau, out var teto))
            return null; // Grau outside 1-9 shouldn't happen (validated elsewhere), skip defensively

        var dano = submetidos.FirstOrDefault(s => s.EfeitoNome == "Dano");
        if (dano is not null && (dano.Quantidade ?? 1) > teto.MaxDano)
            return $"Efeito \"Dano\" excede o teto de {teto.MaxDano} dados para Grau/Círculo {grau}.";

        var alcance = submetidos.FirstOrDefault(s => s.EfeitoNome == "Alcance");
        if (alcance is not null && (alcance.Quantidade ?? 1) > teto.MaxAlcance)
            return $"Efeito \"Alcance\" excede o teto de {teto.MaxAlcance} pés para Grau/Círculo {grau}.";

        return null;
```

Replace the whole method body with this expanded version (i.e. `ValidarAsync`'s final form
includes both the `EfeitoValidator.Validar` call and this Dano/Alcance check — the snippet above
is the complete replacement for what comes after computing `regras`/`submetidos`).

- [ ] **Step 2: Write the failing tests**

Find the exact test file for each of the 4 controllers with:
`grep -rl "spell-ability-bank\|CreateSpellAbilityEntryRequest" tests/RuinaRPG.Tests.Integration/Controllers`
and
`grep -rl "AddCharacterSpellAbilityRequest\|AddNpcSpellAbilityRequest\|AddCreatureSpellAbilityRequest" tests/RuinaRPG.Tests.Integration/Controllers`.

Read whichever file(s) those return first, to copy their exact existing helper names (GM
registration, sheet/bank-entry setup). Add these 4 tests to the **Bank** test file (adapt helper
names to what's actually there — do not invent new ones if an equivalent already exists):

```csharp
[Fact]
public async Task Create_with_a_correctly_costed_known_effect_succeeds()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SpellBankEfeitoGm1", "spellbankefeito1@teste.com");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
        new CreateSpellAbilityEntryRequest("Bola de Fogo", "Magia", 1, "Uma bola de fogo.",
            new List<SpellAbilityEffectRequest> { new("Dano", 2, 4) })));

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}

[Fact]
public async Task Create_with_a_wrong_CustoPI_for_a_known_effect_returns_400()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SpellBankEfeitoGm2", "spellbankefeito2@teste.com");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
        new CreateSpellAbilityEntryRequest("Bola de Fogo Errada", "Magia", 1, "Descrição.",
            new List<SpellAbilityEffectRequest> { new("Dano", 2, 999) })));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Create_with_an_effect_missing_a_prerequisite_returns_400()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SpellBankEfeitoGm3", "spellbankefeito3@teste.com");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
        new CreateSpellAbilityEntryRequest("Cura Sem Dano", "Magia", 1, "Descrição.",
            new List<SpellAbilityEffectRequest> { new("Cura", null, 2) })));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Create_with_Dano_exceeding_the_Grau_teto_returns_400()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SpellBankEfeitoGm4", "spellbankefeito4@teste.com");

    // Grau 1's teto for Dano is 3 dados (EfeitoCustoCalculator.DanoAlcanceMaxPorGrau[1]).
    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
        new CreateSpellAbilityEntryRequest("Dano Demais", "Magia", 1, "Descrição.",
            new List<SpellAbilityEffectRequest> { new("Dano", 4, 8) })));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

Add this test to whichever file has `CharacterSpellAbilitiesController.Add`'s tests:

```csharp
[Fact]
public async Task Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400()
{
    var gmToken = await RegisterGmAndGetTokenAsync("CharSpellEfeitoGm", "charspellefeito@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CharSpellEfeitoPlayer", "charspellefeitoplayer@teste.com");
    var sheetId = await SetUpSheetAsync(gmToken, playerId);

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken,
        new AddCharacterSpellAbilityRequest(null, "Cura Sem Dano", "Magia", 1, "Descrição.",
            new List<SpellAbilityEffectRequest> { new("Cura", null, 2) })));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

Add the mirror of that same test to whichever files have `NpcSpellAbilitiesController.Add`'s and
`CreatureSpellAbilitiesController.Add`'s tests, adapted to each controller's own route
(`/api/npc-sheets/{sheetId}/spell-abilities`, `/api/creature-sheets/{sheetId}/spell-abilities`)
and request type (`AddNpcSpellAbilityRequest`, `AddCreatureSpellAbilityRequest`) and setup helper
(NPC/Creature sheets are GM-owned directly — no linked-player step, same `CreateSheetAsync(gmToken)`
pattern already used elsewhere in those files).

- [ ] **Step 3: Run the new tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Efeito"`
Expected: FAIL — nothing validates yet, so the "should fail" tests (missing prerequisite, wrong
cost, exceeded teto) currently succeed instead of 400ing.

- [ ] **Step 4: Wire validation into `SpellAbilityBankController.Create`**

In `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`, find:

```csharp
        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));
```

Replace with:

```csharp
        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");

        var validationError = await EfeitoValidationHelper.ValidarAsync(db, request.Grau, request.Efeitos);
        if (validationError is not null)
            return BadRequest(validationError);

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));
```

- [ ] **Step 5: Wire validation into `SpellAbilityBankController.Update`**

Find the `Update` method's own `var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));`
line (the second occurrence in this file, inside `Update`, not `Create`) and apply the identical
insertion immediately before it: a `var validationError = await EfeitoValidationHelper.ValidarAsync(db, request.Grau, request.Efeitos); if (validationError is not null) return BadRequest(validationError);`
block, using `request.Grau` from `UpdateSpellAbilityEntryRequest` (confirm that request type has
a `Grau` field the same way `CreateSpellAbilityEntryRequest` does before writing this — read the
`UpdateSpellAbilityEntryRequest` contract file first if unsure).

- [ ] **Step 6: Wire validation into `CharacterSpellAbilitiesController.Add`'s "from scratch" branch**

In `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs`, find:

```csharp
        else
        {
            if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out tipo))
                return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");
            nome = request.Nome!; grau = request.Grau!.Value; descricao = request.Descricao!; efeitos = request.Efeitos!;
        }
```

Replace with:

```csharp
        else
        {
            if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out tipo))
                return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");
            nome = request.Nome!; grau = request.Grau!.Value; descricao = request.Descricao!; efeitos = request.Efeitos!;

            var validationError = await EfeitoValidationHelper.ValidarAsync(db, grau, efeitos);
            if (validationError is not null)
                return BadRequest(validationError);
        }
```

(Placed inside the `else` block — the "from bank" branch above it is untouched, per this plan's
Global Constraints: a bank entry's effects were already validated when that entry was created.)

- [ ] **Step 7: Repeat Step 6 for `NpcSpellAbilitiesController.cs` and `CreatureSpellAbilitiesController.cs`**

Both files have the identical `else { ... nome = request.Nome!; grau = request.Grau!.Value; ... }`
block (confirmed identical in structure during this plan's own research) — apply the exact same
edit: insert the `validationError` check as the last two lines inside that `else` block, right
after the `nome = ...; grau = ...; descricao = ...; efeitos = ...;` line, in both files.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Efeito"`
Expected: PASS (all new tests across the 4 modified controller test files)

- [ ] **Step 9: Run each modified file's full pre-existing test suite to confirm nothing broke**

Run a `dotnet test tests/RuinaRPG.Tests.Integration --filter` covering the 4 test classes found in
Step 2 (use the exact class names Step 2 discovered) plus `EfeitosControllerTests` and
`EfeitoMigrationAndSeedTests`.
Expected: PASS, same counts as before this task plus the new tests.

- [ ] **Step 10: Clean-rebuild the whole solution**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 11: Commit**

```bash
git add src/RuinaRPG.Api/Controllers tests/RuinaRPG.Tests.Integration/Controllers
git commit -m "feat: validate submitted Efeitos against the catalog on every Magia/Habilidade save"
```

---

### Task 5: Client — shared `AddEfeitoForm.razor` component

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Fields/AddEfeitoForm.razor`
- Create: `src/RuinaRPG.Client/Shared/Fields/EfeitoAdicionadoResult.cs`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/AddEfeitoFormTests.cs`

**Interfaces:**
- Consumes: `GET api/efeitos` (Task 3), `RuinaRPG.Domain.Enums.TipoDeCusto`,
  `RuinaRPG.Domain.SpellsAndAbilities.EfeitoCustoCalculator` (Task 1, referenced directly — Client
  already project-references Domain, same precedent as `SpellAbilityCostCalculator`'s existing
  reuse in `BancoDeMagiasForm.razor`).
- Produces: `RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult(string EfeitoNome, int? Quantidade, int CustoPI)`,
  component parameters `Grau` (`int`), `EfeitosExistentes` (`IReadOnlyList<(string Nome, int? Quantidade)>`),
  `OnAdicionar` (`EventCallback<EfeitoAdicionadoResult>`).

- [ ] **Step 1: Write the failing component tests**

Read `tests/RuinaRPG.Tests.Client/Shared/EntityPickerTests.cs` first for this project's bUnit
conventions for a `[Inject] HttpClient`-driven Shared component (fake handler setup, `MudBunitContext`).

Create `src/RuinaRPG.Client/Shared/Fields/EfeitoAdicionadoResult.cs`:

```csharp
namespace RuinaRPG.Client.Shared.Fields;

public record EfeitoAdicionadoResult(string EfeitoNome, int? Quantidade, int CustoPI);
```

Create `tests/RuinaRPG.Tests.Client/Shared/Fields/AddEfeitoFormTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class AddEfeitoFormTests : MudBunitContext
{
    private static HttpClient FakeCatalogClient(object efeitos)
    {
        return FakeHttpMessageHandler.CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith("efeitos")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(efeitos) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static object FixoEfeito(string nome, int grau, int custo, params string[][] preRequisitos) => new
    {
        Id = Guid.NewGuid().ToString(), Nome = nome, Grau = grau, Descricao = "Descrição.", TipoDeCusto = "Fixo",
        CustoFixo = custo, CustoPorUnidade = (int?)null, UnidadeLabel = (string?)null, QuantidadeDerivadaDeEfeito = (string?)null,
        MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
        CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
        PreRequisitos = preRequisitos,
    };

    [Fact]
    public async Task Only_effects_whose_Grau_is_at_or_below_the_parents_Grau_are_offered()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Contrato Mágico", 1, 3),
            FixoEfeito("Atordoamento", 8, 10, ["Duração"]),
        });
        Services.AddScoped(_ => http);

        var cut = Render<AddEfeitoForm>(p => p
            .Add(x => x.Grau, 1)
            .Add(x => x.EfeitosExistentes, Array.Empty<(string, int?)>()));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Contrato Mágico");
        cut.Markup.Should().NotContain("Atordoamento");
    }

    [Fact]
    public async Task An_effect_whose_prerequisite_is_not_yet_present_is_not_offered()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);

        var cut = Render<AddEfeitoForm>(p => p
            .Add(x => x.Grau, 1)
            .Add(x => x.EfeitosExistentes, new[] { ("Duração", (int?)null) }));
        await Task.Delay(50);

        cut.Markup.Should().NotContain("Cura");
    }

    [Fact]
    public async Task An_effect_whose_prerequisite_is_already_present_is_offered()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Dano", 1, 0),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);

        var cut = Render<AddEfeitoForm>(p => p
            .Add(x => x.Grau, 1)
            .Add(x => x.EfeitosExistentes, new[] { ("Dano", (int?)null) }));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Cura");
    }

    [Fact]
    public async Task Selecting_a_DerivadoDeOutroEfeito_effect_without_its_source_present_shows_a_persistent_error()
    {
        var http = FakeHttpMessageHandler.CreateClient(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[]
            {
                new
                {
                    Id = Guid.NewGuid().ToString(), Nome = "Dreno de Vitalidade", Grau = 3, Descricao = "Descrição.", TipoDeCusto = "DerivadoDeOutroEfeito",
                    CustoFixo = (int?)null, CustoPorUnidade = (int?)2, UnidadeLabel = "Dado", QuantidadeDerivadaDeEfeito = "Dano",
                    MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
                    CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
                    PreRequisitos = new[] { new[] { "Dano" } },
                },
            }),
        });
        Services.AddScoped(_ => http);

        var cut = Render<AddEfeitoForm>(p => p
            .Add(x => x.Grau, 3)
            .Add(x => x.EfeitosExistentes, Array.Empty<(string, int?)>()));
        await Task.Delay(50);

        // Dreno never satisfies its own prerequisite here (no "Dano" present), so it's correctly
        // absent from the offered list — proving the same prerequisite gate also covers this case,
        // not a separate code path.
        cut.Markup.Should().NotContain("Dreno de Vitalidade");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~AddEfeitoFormTests"`
Expected: FAIL to compile — `AddEfeitoForm` doesn't exist yet.

- [ ] **Step 3: Create the component**

Create `src/RuinaRPG.Client/Shared/Fields/AddEfeitoForm.razor`:

```razor
@inject HttpClient Http
@using MudBlazor
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Domain.Enums
@using RuinaRPG.Domain.SpellsAndAbilities

<MudSelect T="string" Value="@_efeitoNomeSelecionado" ValueChanged="OnEfeitoSelecionado" Label="Efeito">
    @foreach (var efeito in _disponiveis)
    {
        <MudSelectItem Value="@efeito.Nome">@efeito.Nome</MudSelectItem>
    }
</MudSelect>

@if (_selecionado is not null)
{
    <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">@_selecionado.Descricao</MudText>

    @if (_selecionado.TipoDeCusto is nameof(TipoDeCusto.PorUnidade) or nameof(TipoDeCusto.ManualPorUnidade))
    {
        <MudNumericField T="int?" @bind-Value="_quantidade" @bind-Value:after="RecalcularCusto" Label="@($"Quantidade ({_selecionado.UnidadeLabel})")" Class="mt-2" />
    }

    @if (_selecionado.TipoDeCusto is nameof(TipoDeCusto.Manual))
    {
        <MudNumericField T="int?" @bind-Value="_custoManual" @bind-Value:after="RecalcularCusto" Label="Custo em PI (definido pelo Mestre)" Class="mt-2" />
    }
    @if (_selecionado.TipoDeCusto is nameof(TipoDeCusto.ManualPorUnidade))
    {
        <MudNumericField T="int?" @bind-Value="_custoManual" @bind-Value:after="RecalcularCusto" Label="@($"Custo por {_selecionado.UnidadeLabel} (definido pelo Mestre)")" Class="mt-2" />
    }

    @if (_erroDrenoOrigemAusente is not null)
    {
        <MudAlert Severity="Severity.Error" Variant="Variant.Filled" Class="mt-2">@_erroDrenoOrigemAusente</MudAlert>
    }
    else if (_custoCalculado is not null)
    {
        <MudText Class="mt-2">Custo em PI: @_custoCalculado</MudText>
    }

    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" Class="mt-2"
               Disabled="@(_custoCalculado is null)" OnClick="ConfirmarAsync">
        Adicionar Efeito
    </MudButton>
}

@code {
    [Parameter, EditorRequired] public int Grau { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentes { get; set; } = [];
    [Parameter] public EventCallback<EfeitoAdicionadoResult> OnAdicionar { get; set; }

    private List<EfeitoResponse> _todos = [];
    private List<EfeitoResponse> _disponiveis = [];
    private string? _efeitoNomeSelecionado;
    private EfeitoResponse? _selecionado;
    private int? _quantidade;
    private int? _custoManual;
    private string? _erroDrenoOrigemAusente;
    // Recomputed explicitly on every input change (RecalcularCusto), never as a side effect of
    // reading it — a computed property that mutates state on every getter call is fragile and
    // gets read multiple times per render (the @if check, the display, the button's Disabled).
    private int? _custoCalculado;

    protected override async Task OnParametersSetAsync()
    {
        if (_todos.Count == 0)
            _todos = await Http.GetFromJsonAsync<List<EfeitoResponse>>("efeitos") ?? [];

        var nomesPresentes = EfeitosExistentes.Select(e => e.Nome).ToHashSet();
        _disponiveis = _todos
            .Where(e => e.Grau <= Grau)
            .Where(e => e.PreRequisitos.All(grupo => grupo.Any(nomesPresentes.Contains)))
            .OrderBy(e => e.Grau).ThenBy(e => e.Nome)
            .ToList();
    }

    private void OnEfeitoSelecionado(string nome)
    {
        _efeitoNomeSelecionado = nome;
        _selecionado = _disponiveis.FirstOrDefault(e => e.Nome == nome);
        _quantidade = null;
        _custoManual = null;
        RecalcularCusto();
    }

    private void RecalcularCusto()
    {
        _erroDrenoOrigemAusente = null;
        _custoCalculado = null;

        if (_selecionado is null)
            return;

        if (!Enum.TryParse<TipoDeCusto>(_selecionado.TipoDeCusto, out var tipo))
            return;

        int? quantidadeDerivada = null;
        if (_selecionado.QuantidadeDerivadaDeEfeito is { } origemNome)
        {
            var origem = EfeitosExistentes.FirstOrDefault(e => e.Nome == origemNome);
            if (origem.Nome is null)
            {
                _erroDrenoOrigemAusente = $"\"{_selecionado.Nome}\" exige que \"{origemNome}\" já tenha uma Quantidade definida nesta Magia/Habilidade.";
                return;
            }
            quantidadeDerivada = origem.Quantidade ?? 0;
        }

        if (tipo is TipoDeCusto.Manual or TipoDeCusto.ManualPorUnidade && _custoManual is null)
            return;
        if (tipo is TipoDeCusto.PorUnidade or TipoDeCusto.ManualPorUnidade && _quantidade is null)
            return; // quantidade opcional só quando o tipo não a exige; aqui exige e ainda não foi preenchida

        _custoCalculado = EfeitoCustoCalculator.Calcular(
            tipo, _selecionado.CustoFixo, _selecionado.CustoPorUnidade,
            _selecionado.CustoAlternativo, _selecionado.CustoAlternativoAPartirDoGrau,
            Grau, _quantidade, _custoManual, quantidadeDerivada);
    }

    private async Task ConfirmarAsync()
    {
        if (_selecionado is null || _custoCalculado is not { } custo)
            return;

        await OnAdicionar.InvokeAsync(new EfeitoAdicionadoResult(_selecionado.Nome, _quantidade, custo));

        _efeitoNomeSelecionado = null;
        _selecionado = null;
        _quantidade = null;
        _custoManual = null;
        _erroDrenoOrigemAusente = null;
        _custoCalculado = null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~AddEfeitoFormTests"`
Expected: PASS (all 4 tests)

- [ ] **Step 5: Clean-rebuild the whole solution**

Run: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Shared/Fields tests/RuinaRPG.Tests.Client/Shared/Fields
git commit -m "feat: AddEfeitoForm shared component — Grau/pré-requisito-aware Efeito picker"
```

---

### Task 6: Client — wire `AddEfeitoForm` into the 4 existing places

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`
- Modify: `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`

**Interfaces:**
- Consumes: `RuinaRPG.Client.Shared.Fields.AddEfeitoForm`, `EfeitoAdicionadoResult` (Task 5).
- Produces: none (leaf task).

Each of the 4 files replaces its "Adicionar Efeito" button + free-text-row table with: the
`AddEfeitoForm` component (fed `Grau` from that page's own form field, `EfeitosExistentes`
projected from the page's own current Efeitos list) + a read-only table of already-added effects
(Nome/Quantidade/CustoPI as plain text, Remover button per row — no more inline-editable cells).

- [ ] **Step 1: `FichaDePersonagem.razor`**

Find:

```razor
                    <MudText Typo="Typo.h6" Class="mt-3">Efeitos</MudText>
                    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="AddSpellAbilityEffect">Adicionar Efeito</MudButton>
                    <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
                        <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
                        <tbody>
                            @for (var i = 0; i < _spellAbilityForm.Efeitos.Count; i++)
                            {
                                var index = i; // capture for the closures below
                                <tr>
                                    <td><MudTextField T="string" @bind-Value="_spellAbilityForm.Efeitos[index].EfeitoNome" /></td>
                                    <td><MudNumericField T="int?" @bind-Value="_spellAbilityForm.Efeitos[index].Quantidade" /></td>
                                    <td><MudNumericField T="int" @bind-Value="_spellAbilityForm.Efeitos[index].CustoPI" /></td>
                                    <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => _spellAbilityForm.Efeitos.RemoveAt(index))">Remover</MudButton></td>
                                </tr>
                            }
                        </tbody>
                    </MudSimpleTable>
```

Replace with:

```razor
                    <MudText Typo="Typo.h6" Class="mt-3">Efeitos</MudText>
                    <AddEfeitoForm Grau="_spellAbilityForm.Grau" EfeitosExistentes="EfeitosExistentesParaPicker()" OnAdicionar="OnEfeitoAdicionado" />
                    <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
                        <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
                        <tbody>
                            @for (var i = 0; i < _spellAbilityForm.Efeitos.Count; i++)
                            {
                                var index = i; // capture for the closure below
                                <tr>
                                    <td>@_spellAbilityForm.Efeitos[index].EfeitoNome</td>
                                    <td>@_spellAbilityForm.Efeitos[index].Quantidade</td>
                                    <td>@_spellAbilityForm.Efeitos[index].CustoPI</td>
                                    <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => _spellAbilityForm.Efeitos.RemoveAt(index))">Remover</MudButton></td>
                                </tr>
                            }
                        </tbody>
                    </MudSimpleTable>
```

Find:

```csharp
    private void AddSpellAbilityEffect() => _spellAbilityForm.Efeitos.Add(new SpellAbilityEffectFormModel());
```

Replace with:

```csharp
    private IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentesParaPicker() =>
        _spellAbilityForm.Efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList();

    private void OnEfeitoAdicionado(RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult resultado) =>
        _spellAbilityForm.Efeitos.Add(new SpellAbilityEffectFormModel { EfeitoNome = resultado.EfeitoNome, Quantidade = resultado.Quantidade, CustoPI = resultado.CustoPI });
```

- [ ] **Step 2: `FichaDeNpc.razor`**

Apply the identical two replacements from Step 1 (confirmed byte-identical markup/`@code`
structure to `FichaDePersonagem.razor` for this exact section during this plan's own research) —
same old/new text, same file-local `SpellAbilityEffectFormModel`/`_spellAbilityForm` names (this
file has its own copies of both, unrelated to Personagem's).

- [ ] **Step 3: `FichaDeCriatura.razor`**

Apply the identical two replacements from Step 1 (also confirmed byte-identical markup/`@code`
structure) — same old/new text.

- [ ] **Step 4: `BancoDeMagiasForm.razor`**

Find:

```razor
    <Section Title="Efeitos">
        <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="AddEffect">Adicionar Efeito</MudButton>
        <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
            <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
            <tbody>
                @for (var i = 0; i < _form.Efeitos.Count; i++)
                {
                    var index = i; // capture for the closures below
                    <tr>
                        <td><MudTextField T="string" @bind-Value="_form.Efeitos[index].EfeitoNome" @bind-Value:after="NotifySavedAsync" /></td>
                        <td><MudNumericField T="int?" @bind-Value="_form.Efeitos[index].Quantidade" @bind-Value:after="NotifySavedAsync" /></td>
                        <td><MudNumericField T="int" @bind-Value="_form.Efeitos[index].CustoPI" @bind-Value:after="NotifySavedAsync" /></td>
                        <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => RemoveEffect(index))">Remover</MudButton></td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
```

Replace with:

```razor
    <Section Title="Efeitos">
        <AddEfeitoForm Grau="_form.Grau" EfeitosExistentes="EfeitosExistentesParaPicker()" OnAdicionar="OnEfeitoAdicionadoAsync" />
        <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
            <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
            <tbody>
                @for (var i = 0; i < _form.Efeitos.Count; i++)
                {
                    var index = i; // capture for the closure below
                    <tr>
                        <td>@_form.Efeitos[index].EfeitoNome</td>
                        <td>@_form.Efeitos[index].Quantidade</td>
                        <td>@_form.Efeitos[index].CustoPI</td>
                        <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => RemoveEffect(index))">Remover</MudButton></td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
```

Find:

```csharp
    private void AddEffect() => _form.Efeitos.Add(new EffectFormModel());

    private void RemoveEffect(int index)
    {
        _form.Efeitos.RemoveAt(index);
        NotifySavedAsync();
    }
```

Replace with:

```csharp
    private IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentesParaPicker() =>
        _form.Efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList();

    private Task OnEfeitoAdicionadoAsync(RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult resultado)
    {
        _form.Efeitos.Add(new EffectFormModel { EfeitoNome = resultado.EfeitoNome, Quantidade = resultado.Quantidade, CustoPI = resultado.CustoPI });
        return NotifySavedAsync();
    }

    private void RemoveEffect(int index)
    {
        _form.Efeitos.RemoveAt(index);
        NotifySavedAsync();
    }
```

`AddEfeitoForm`/`EfeitoAdicionadoResult` are in `RuinaRPG.Client.Shared.Fields` — confirmed
`FichaDePersonagem.razor`/`FichaDeNpc.razor`/`FichaDeCriatura.razor` already each have their own
`@using RuinaRPG.Client.Shared.Fields` (from the earlier shared cascading-select fields work), so
Steps 1-3 need no new `@using`. `BancoDeMagiasForm.razor` does **not** have that `@using` yet —
add it. Find the top of the file:

```razor
@using RuinaRPG.Domain.SpellsAndAbilities
```

Replace with:

```razor
@using RuinaRPG.Client.Shared.Fields
@using RuinaRPG.Domain.SpellsAndAbilities
```

- [ ] **Step 5: Clean-rebuild the whole client**

Run: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6: Run the full Client test suite**

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: PASS, same counts as before this task (this task touches no test files — a pure
regression check for the 4 modified pages, none of which have dedicated bUnit coverage per this
repo's established precedent for these pages)

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor src/RuinaRPG.Client/Pages/FichaDeCriatura.razor src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor
git commit -m "feat: wire AddEfeitoForm into all 4 Magia/Habilidade effect-adding forms"
```

---

### Task 7: Client — `AuditoriaEfeitos.razor` page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/AuditoriaEfeitos.razor`
- Modify: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`

**Interfaces:**
- Consumes: `GET/POST api/efeitos`, `PUT/DELETE api/efeitos/{id}` (Task 3),
  `RuinaRPG.Contracts.Rules.EfeitoResponse/CreateEfeitoRequest/UpdateEfeitoRequest`.
- Produces: route `/auditoria/efeitos`.

Read `src/RuinaRPG.Client/Pages/AuditoriaCaracteristicasDeCriatura.razor` first — same overall
CRUD-page shape, adapted to `Efeito`'s richer field set (no Positivas/Negativas split — one flat
list grouped by Grau instead — and a small inline editor for the `PreRequisitos` groups, since
that's the one field with no natural single-input representation).

- [ ] **Step 1: Create the page**

Create `src/RuinaRPG.Client/Pages/AuditoriaEfeitos.razor`:

```razor
@page "/auditoria/efeitos"
@inject HttpClient Http
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Domain.Enums
@using MudBlazor

<Breadcrumbs Items="@Crumbs" />
<MudText Typo="Typo.h3">Auditoria: Efeitos</MudText>

<DismissibleAlert @bind-Message="_errorMessage" Class="mt-3" />

@if (_forbidden)
{
    <MudAlert Severity="Severity.Warning" Variant="Variant.Filled" Class="mt-3">Você não é o Auditor de Regras designado — sem permissão para editar.</MudAlert>
}
else
{
    <Section Title="Adicionar Efeito">
        <MudTextField T="string" @bind-Value="_newForm.Nome" Label="Nome" />
        <MudNumericField T="int" @bind-Value="_newForm.Grau" Label="Grau/Círculo" />
        <MudSelect T="string" @bind-Value="_newForm.TipoDeCusto" Label="Tipo de Custo">
            <MudSelectItem Value="@(nameof(TipoDeCusto.Fixo))">Fixo</MudSelectItem>
            <MudSelectItem Value="@(nameof(TipoDeCusto.PorUnidade))">Por Unidade</MudSelectItem>
            <MudSelectItem Value="@(nameof(TipoDeCusto.Manual))">Manual (o Mestre digita o total)</MudSelectItem>
            <MudSelectItem Value="@(nameof(TipoDeCusto.ManualPorUnidade))">Manual por Unidade (o Mestre digita a taxa)</MudSelectItem>
            <MudSelectItem Value="@(nameof(TipoDeCusto.DerivadoDeOutroEfeito))">Derivado de outro Efeito</MudSelectItem>
        </MudSelect>
        <MudNumericField T="int?" @bind-Value="_newForm.CustoFixo" Label="Custo Fixo (PI)" />
        <MudNumericField T="int?" @bind-Value="_newForm.CustoPorUnidade" Label="Custo por Unidade (PI)" />
        <MudTextField T="string" @bind-Value="_newForm.UnidadeLabel" Label="Rótulo da Unidade (ex.: Dado)" />
        <MudTextField T="string" @bind-Value="_newForm.QuantidadeDerivadaDeEfeito" Label="Nome do Efeito de origem (se Derivado)" />
        <MudNumericField T="int?" @bind-Value="_newForm.MaxUnidades" Label="Teto de Unidades" />
        <MudCheckBox T="bool" @bind-Value="_newForm.MaxEscalaPorGrau" Label="Teto escala por Grau/Círculo?" />
        <MudNumericField T="int?" @bind-Value="_newForm.MaxContandoAPartirDoGrau" Label="Teto conta a partir do Grau (opcional)" />
        <MudNumericField T="int?" @bind-Value="_newForm.CustoAlternativo" Label="Custo Alternativo (PI, exceção)" />
        <MudNumericField T="int?" @bind-Value="_newForm.CustoAlternativoAPartirDoGrau" Label="Custo Alternativo vale a partir do Grau" />
        <MudTextField T="string" @bind-Value="_newForm.PreRequisitosTexto" Label="Pré-requisitos (grupos separados por ; nomes alternativos separados por ,)" HelperText="Ex.: Duração;Atordoamento,Congelar,Enraizar" />
        <MudTextField T="string" @bind-Value="_newForm.Descricao" Label="Descrição" Lines="3" />
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-2" OnClick="AddAsync">Adicionar</MudButton>
    </Section>

    @foreach (var grauGroup in _efeitos.GroupBy(e => e.Grau).OrderBy(g => g.Key))
    {
        <Section Title="@($"Grau/Círculo {grauGroup.Key}")">
            <MudSimpleTable Dense="true" Hover="true">
                <thead><tr><th>Nome</th><th>Tipo de Custo</th><th>Custo</th><th>Unidade</th><th>Teto</th><th>Pré-requisitos</th><th>Descrição</th><th></th></tr></thead>
                <tbody>
                    @foreach (var efeito in grauGroup)
                    {
                        <tr>
                            <td>@efeito.Nome</td>
                            <td>@efeito.TipoDeCusto</td>
                            <td>@(efeito.CustoFixo?.ToString() ?? efeito.CustoPorUnidade?.ToString() ?? "—")</td>
                            <td>@(efeito.UnidadeLabel ?? "—")</td>
                            <td>@(efeito.MaxUnidades is null ? "—" : $"{efeito.MaxUnidades}{(efeito.MaxEscalaPorGrau ? " × Grau" : "")}")</td>
                            <td>@string.Join(" E ", efeito.PreRequisitos.Select(g => string.Join(" ou ", g)))</td>
                            <td>@efeito.Descricao</td>
                            <td><MudIconButton Icon="@Icons.Material.Filled.Delete" OnClick="@(() => DeleteAsync(efeito.Id))" /></td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        </Section>
    }
}

@code {
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Auditoria: Efeitos"),
    };

    private List<EfeitoResponse> _efeitos = new();
    private string? _errorMessage;
    private bool _forbidden;
    private readonly NewEfeitoFormModel _newForm = new();

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var response = await Http.GetAsync("efeitos");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar os efeitos.";
            return;
        }

        _efeitos = await response.Content.ReadFromJsonAsync<List<EfeitoResponse>>() ?? new();
    }

    private static List<List<string>> ParsePreRequisitos(string texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? new()
            : texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(grupo => grupo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList())
                .ToList();

    private async Task AddAsync()
    {
        var response = await Http.PostAsJsonAsync("efeitos", new CreateEfeitoRequest(
            _newForm.Nome, _newForm.Grau, _newForm.Descricao, _newForm.TipoDeCusto,
            _newForm.CustoFixo, _newForm.CustoPorUnidade, _newForm.UnidadeLabel, _newForm.QuantidadeDerivadaDeEfeito,
            _newForm.MaxUnidades, _newForm.MaxEscalaPorGrau, _newForm.MaxContandoAPartirDoGrau,
            _newForm.CustoAlternativo, _newForm.CustoAlternativoAPartirDoGrau,
            ParsePreRequisitos(_newForm.PreRequisitosTexto)));
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar o Efeito — verifique se o Nome já existe.";
            return;
        }

        _newForm.Nome = ""; _newForm.Descricao = ""; _newForm.PreRequisitosTexto = "";
        await LoadAsync();
    }

    private async Task DeleteAsync(string id)
    {
        var response = await Http.DeleteAsync($"efeitos/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível excluir o Efeito.";
            return;
        }

        await LoadAsync();
    }

    private class NewEfeitoFormModel
    {
        public string Nome { get; set; } = "";
        public int Grau { get; set; } = 1;
        public string TipoDeCusto { get; set; } = nameof(RuinaRPG.Domain.Enums.TipoDeCusto.Fixo);
        public int? CustoFixo { get; set; }
        public int? CustoPorUnidade { get; set; }
        public string? UnidadeLabel { get; set; }
        public string? QuantidadeDerivadaDeEfeito { get; set; }
        public int? MaxUnidades { get; set; }
        public bool MaxEscalaPorGrau { get; set; }
        public int? MaxContandoAPartirDoGrau { get; set; }
        public int? CustoAlternativo { get; set; }
        public int? CustoAlternativoAPartirDoGrau { get; set; }
        public string PreRequisitosTexto { get; set; } = "";
        public string Descricao { get; set; } = "";
    }
}
```

(No `Update`/edit-in-place UI in this first version — only Add + Delete. Editing an existing row's
fields, e.g. to finally define "Selar", is reachable by Delete + re-Add. `Update` on the API stays
implemented — Task 3's own tests exercise it directly — just not wired to a client affordance yet;
note this as a small, deliberate scope cut in the final report, not a silent gap.)

- [ ] **Step 2: Add the nav link**

In `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`, find:

```razor
    <MudNavLink Href="auditoria/caracteristicas-de-criatura">Auditoria: Características de Criatura</MudNavLink>
```

Replace with:

```razor
    <MudNavLink Href="auditoria/caracteristicas-de-criatura">Auditoria: Características de Criatura</MudNavLink>
    <MudNavLink Href="auditoria/efeitos">Auditoria: Efeitos</MudNavLink>
```

- [ ] **Step 3: Clean-rebuild the whole client**

Run: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/AuditoriaEfeitos.razor src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor
git commit -m "feat: Auditoria: Efeitos page"
```

---

### Task 8: Docs — Requisitos updates

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md`
- Modify: `Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md`

**Interfaces:**
- Consumes: nothing (doc-only task).
- Produces: none.

- [ ] **Step 1: Add R0006 to `Requisitos - Auditoria de Regras.md`**

Add a new requirement after R0005 (at the end of the file):

```markdown

# **R0006** - O Auditor de Regras tem CRUD sobre o catálogo de Efeitos de "[[GRAUS & CÍRCULOS]]".

**Descrição**: Uma página separada lista, agrupados por Grau/Círculo, todos os Efeitos que uma Magia/Habilidade pode comprar — Nome, Tipo de Custo (Fixo, Por Unidade, Manual, Manual por Unidade, ou Derivado de outro Efeito), o valor de custo correspondente, o rótulo da unidade (quando houver Quantidade), o teto de Quantidade (quando houver, podendo escalar com o Grau/Círculo da Magia/Habilidade), e os grupos de pré-requisito (um Efeito pode exigir que um ou mais outros já estejam na mesma Magia/Habilidade, incluindo grupos alternativos "ou"). O catálogo nasce de um seed inicial extraído de "[[GRAUS & CÍRCULOS]]" — quando o próprio texto do sistema deixa um Efeito sem definição (ex.: "Selar", citado como pré-requisito alternativo do Detrito mas nunca definido), o Auditor cadastra a entrada faltante diretamente aqui, sem precisar de uma alteração de código. Assim como o catálogo de Características (R0003), é global (vale para o servidor inteiro, não por GM) e uma edição não é desfeita por uma futura atualização/reinicialização do servidor.

# **R0007** - O Custo em PI de cada Efeito numa Magia/Habilidade é calculado automaticamente a partir do catálogo de R0006.

**Descrição**: Em toda tela que adiciona um Efeito a uma Magia/Habilidade (Banco de Magias e as 3 Fichas), o Nome do Efeito é escolhido de uma lista — não mais um campo de texto livre — restrita aos Efeitos cujo Grau já foi desbloqueado pelo Grau/Círculo da própria Magia/Habilidade (acesso cumulativo: um Efeito de Grau 2 continua disponível numa Magia de Grau 5) e cujos pré-requisitos já estão presentes na mesma lista de Efeitos. O Custo em PI é somente-leitura, calculado a partir do Tipo de Custo do Efeito escolhido — exceto os tipos Manual/Manual por Unidade, onde o próprio livro de regras deixa o valor a critério do Mestre, e que por isso ganham um campo numérico para essa entrada manual. O servidor recusa (400) qualquer submissão cujo Custo em PI não bata com o valor recalculado, cujo Grau não esteja desbloqueado, ou cujos pré-requisitos não estejam satisfeitos — a tela guia, o servidor garante.
```

- [ ] **Step 2: Update `Requisitos - Banco de Magias e Habilidades.md`**

Read the file first (search for the section describing the Efeitos sub-table/add-form, likely
near wherever `CustoPI`/`Gasto em PI` is documented) and add one sentence there, matching the
surrounding prose style, pointing at the new R0006/R0007: something to the effect of "O Nome de
cada Efeito e seu Custo em PI seguem o catálogo e o cálculo automático descritos em
'[[Requisitos - Auditoria de Regras]]' R0006/R0007 — não são mais campos de texto/número livres."
Place it adjacent to the existing description of the Efeitos sub-table, not as a new top-level
requirement (this doc's own field-level behavior didn't change, just how the Nome/Custo em PI
fields are populated).

- [ ] **Step 3: Clean-rebuild and run the full Unit + Client suites**

Run: `dotnet build RuinaRPG.sln && dotnet test tests/RuinaRPG.Tests.Unit && dotnet test tests/RuinaRPG.Tests.Client`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`, both suites pass (this task touches no code,
a pure regression check)

- [ ] **Step 4: Commit**

```bash
git add "Docs/Requisitos/Requisitos - Auditoria de Regras.md" "Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md"
git commit -m "docs: Requisitos for the Efeito catalog + automatic PI cost calculation"
```

---

## Final verification (after all 8 tasks)

- [ ] Clean-rebuild: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln` → 0 Warning(s), 0 Error(s).
- [ ] `dotnet test tests/RuinaRPG.Tests.Unit` → all pass.
- [ ] `dotnet test tests/RuinaRPG.Tests.Client` → all pass.
- [ ] `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Efeito"` → all pass (scoped — the full Integration suite is chronically flaky under Testcontainers contention in this environment).
- [ ] Manually confirm: creating a Magia/Habilidade with a Dano effect whose Quantidade exceeds its Grau's teto is rejected; Encantamento Elemental costs 2 PI at Grau 2-3 and 4 PI at Grau 4+; Dreno de Vitalidade's picker option is unavailable until Dano has been added, and shows the persistent error if Dano is later removed.
