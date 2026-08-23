namespace RuinaRPG.Domain.CharacterSheets;

public static class ResourceMaximumCalculator
{
    public static int Vitalidade(int vigor, int statusDeClasseVida) => vigor * 2 + statusDeClasseVida;

    public static int Foco(int astucia, int statusDeClasseFoco) => astucia * 2 + statusDeClasseFoco;

    public static int Adrenalina(int artefatoBonus) => 10 + artefatoBonus;

    public static int Estresse() => 10;
}
