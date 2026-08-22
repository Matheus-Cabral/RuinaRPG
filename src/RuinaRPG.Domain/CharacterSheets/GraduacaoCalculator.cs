using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

public static class GraduacaoCalculator
{
    public static int Compute(Vocacao vocacao, int eapAtual, bool possuiCoracaoDeMana, IReadOnlyList<CirculoGrauPorEap> tabela)
    {
        var usaGraumSempre = vocacao is Vocacao.Campeao or Vocacao.Cacador;
        if (!usaGraumSempre && !possuiCoracaoDeMana)
            return 0;

        var highestMet = 0;
        foreach (var row in tabela.OrderBy(r => r.CirculoOuGrau))
        {
            if (!int.TryParse(row.EapAbsoluto, out var threshold))
                continue; // "Max." rows aren't a real numeric threshold to compare against

            if (eapAtual >= threshold)
                highestMet = row.CirculoOuGrau;
        }

        return highestMet;
    }
}
