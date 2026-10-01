namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Takes the two nullable Perícia ids directly (not the Historico entity itself, which lives in
/// RuinaRPG.Infrastructure — Domain cannot reference it) — both null means no Histórico is chosen.
/// </summary>
public static class HistoricoBonusCalculator
{
    public static int For(int periciaId, int? periciaMaisSeisId, int? periciaMaisTresId) =>
        periciaId == periciaMaisSeisId ? 6 : periciaId == periciaMaisTresId ? 3 : 0;
}
