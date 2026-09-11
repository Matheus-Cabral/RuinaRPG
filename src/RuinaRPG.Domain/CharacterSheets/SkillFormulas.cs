namespace RuinaRPG.Domain.CharacterSheets;

public static class SkillFormulas
{
    public static int Modificador(int gasto) => gasto / 3;

    public static int Total(int modificador, int atributoTotal, int artefatos) => modificador + atributoTotal + artefatos;
}
