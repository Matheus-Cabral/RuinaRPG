namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Takes the two nullable Pericia fields directly (not the Historico entity itself, which lives in
/// RuinaRPG.Infrastructure — Domain cannot reference it) — both null means no Histórico is chosen.
/// </summary>
public static class HistoricoBonusCalculator
{
    public static int For(Pericia pericia, Pericia? periciaMaisSeis, Pericia? periciaMaisTres) =>
        pericia == periciaMaisSeis ? 6 : pericia == periciaMaisTres ? 3 : 0;
}
