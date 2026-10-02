using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

public static class GraduacaoCalculator
{
    public static int Compute(Vocacao vocacao, int eapAtual, bool possuiCoracaoDeMana, IReadOnlyList<CirculoGrauPorEap> tabela)
    {
        var marcial = vocacao is Vocacao.Campeao or Vocacao.Cacador;
        if (!marcial && !possuiCoracaoDeMana)
            return 0;

        var highestMet = 0;
        foreach (var row in tabela.OrderBy(r => r.CirculoOuGrau))
        {
            if (!int.TryParse(row.EapAbsoluto, out var threshold))
                continue; // "Max." rows aren't a real numeric threshold to compare against

            if (eapAtual >= threshold)
                highestMet = row.CirculoOuGrau;
        }

        // Vocações marciais nunca ficam abaixo do Grau 1, mesmo com a EAP abaixo do primeiro limiar.
        return marcial ? Math.Max(highestMet, 1) : highestMet;
    }
}
