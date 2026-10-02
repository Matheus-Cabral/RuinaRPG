namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Limite de Carga (Requisitos - Ficha de Personagem 2.b) plus the "mochila" mechanic: an
/// Inventário item with a Capacidade Extra raises the max without adding its own weight to
/// PesoAtual (see docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md).
/// </summary>
public static class CarryWeightCalculator
{
    /// <summary>
    /// <paramref name="bonusDeCarga"/> is the Campanha's Bônus de carga (may be negative); NPCs and
    /// Criaturas pass 0. The result never goes below 0.
    /// </summary>
    public static decimal PesoMaximo(int forca, int vigor, decimal capacidadeExtraTotal, decimal bonusDeCarga = 0m) =>
        Math.Max(0m, Math.Floor((forca + vigor) / 2m) + capacidadeExtraTotal + bonusDeCarga);

    public static bool CountsTowardPesoAtual(decimal? capacidadeExtra) =>
        capacidadeExtra is null or 0;
}
