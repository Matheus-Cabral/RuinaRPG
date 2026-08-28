namespace RuinaRPG.Domain.CreatureSheets;

public static class XpAwardCalculator
{
    public static int Kill(int experienciaAtual) => (int)Math.Floor(experienciaAtual * 0.15);

    public static int Assistencia(int experienciaAtual) => (int)Math.Floor(experienciaAtual * 0.12);
}
