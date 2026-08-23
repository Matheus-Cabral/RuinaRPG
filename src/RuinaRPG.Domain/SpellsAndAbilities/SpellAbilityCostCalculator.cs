namespace RuinaRPG.Domain.SpellsAndAbilities;

public static class SpellAbilityCostCalculator
{
    public static int GastoEmPI(IEnumerable<int> efeitoCustosPI) => efeitoCustosPI.Sum();

    public static int Custo(int gastoEmPI) => (int)Math.Ceiling(gastoEmPI * 1.25);
}
