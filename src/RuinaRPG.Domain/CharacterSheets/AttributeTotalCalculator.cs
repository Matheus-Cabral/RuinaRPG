namespace RuinaRPG.Domain.CharacterSheets;

public static class AttributeTotalCalculator
{
    public static int Total(int gasto, int bonus, bool temMaestria, int artefatos) =>
        temMaestria ? gasto + bonus + artefatos : gasto + bonus / 2 + artefatos;
}
