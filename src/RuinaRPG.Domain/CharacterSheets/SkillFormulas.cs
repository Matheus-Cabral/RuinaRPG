namespace RuinaRPG.Domain.CharacterSheets;

public static class SkillFormulas
{
    public static int Modificador(int gasto, int historicoBonus) => gasto / 3 + historicoBonus;

    public static int Total(int modificador, int atributoTotal, int artefatos) => modificador + atributoTotal + artefatos;
}
