using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Domain.SpellsAndAbilities;

public static class EfeitoCustoCalculator
{
    /// <summary>
    /// Dano/Alcance (the 2 quantifiable base effects) have their teto in the rulebook's own
    /// overview table at the top of "[[GRAUS & CÍRCULOS]]" — not the generic
    /// MaxUnidades/MaxEscalaPorGrau mechanism, since the numbers don't follow that linear formula.
    /// Keyed by Grau, value is (MaxDano em dados, MaxAlcance em pés).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, (int MaxDano, int MaxAlcance)> DanoAlcanceMaxPorGrau =
        new Dictionary<int, (int, int)>
        {
            [1] = (3, 2), [2] = (4, 3), [3] = (5, 4), [4] = (6, 5), [5] = (7, 6),
            [6] = (8, 7), [7] = (9, 8), [8] = (10, 9), [9] = (11, 10),
        };

    public static int Calcular(
        TipoDeCusto tipoDeCusto, int? custoFixo, int? custoPorUnidade,
        int? custoAlternativo, int? custoAlternativoAPartirDoGrau,
        int grauDaMagia, int? quantidade, int? custoManual, int? quantidadeDerivada)
    {
        if (custoAlternativoAPartirDoGrau is { } limiar && grauDaMagia >= limiar)
            return custoAlternativo!.Value;

        return tipoDeCusto switch
        {
            TipoDeCusto.Fixo => custoFixo!.Value,
            TipoDeCusto.PorUnidade => custoPorUnidade!.Value * (quantidade ?? 1),
            TipoDeCusto.DerivadoDeOutroEfeito => custoPorUnidade!.Value * (quantidadeDerivada ?? 0),
            TipoDeCusto.Manual => custoManual!.Value,
            TipoDeCusto.ManualPorUnidade => custoManual!.Value * (quantidade ?? 1),
            _ => throw new ArgumentOutOfRangeException(nameof(tipoDeCusto)),
        };
    }

    /// <summary>Null = sem teto. Dano/Alcance not covered here — see DanoAlcanceMaxPorGrau.</summary>
    public static int? MaxPermitido(int? maxUnidades, bool maxEscalaPorGrau, int? maxContandoAPartirDoGrau, int grauDaMagia)
    {
        if (maxUnidades is not { } max)
            return null;
        if (!maxEscalaPorGrau)
            return max;

        var multiplicador = maxContandoAPartirDoGrau is { } inicio ? grauDaMagia - inicio + 1 : grauDaMagia;
        return max * Math.Max(1, multiplicador);
    }
}
