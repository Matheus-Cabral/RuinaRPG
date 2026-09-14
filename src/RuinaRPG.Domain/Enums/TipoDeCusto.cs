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
