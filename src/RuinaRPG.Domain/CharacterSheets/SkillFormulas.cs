namespace RuinaRPG.Domain.CharacterSheets;

public static class SkillFormulas
{
    // The Histórico bonus (HistoricoBonusCalculator.For: 6 or 3, always a multiple of 3) counts as
    // Pericia points spent, not as raw points added to the Modificador — it's folded into the
    // division, not added after it. (gasto + historicoBonus) / 3 == gasto/3 + historicoBonus/3 for
    // any gasto, since historicoBonus is a multiple of 3 (floor((a + 3k)/3) = floor(a/3) + k).
    public static int Modificador(int gasto, int historicoBonus) => (gasto + historicoBonus) / 3;

    public static int Total(int modificador, int atributoTotal, int artefatos) => modificador + atributoTotal + artefatos;
}
