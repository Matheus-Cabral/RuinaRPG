namespace RuinaRPG.Domain.CharacterSheets;

public static class LinhagemVarianteValidator
{
    private static readonly Dictionary<Linhagem, Variante[]> ValidPairs = new()
    {
        [Linhagem.Humano] = [Variante.Sinir, Variante.Laonir],
        [Linhagem.Phylauc] = [Variante.PhylacTai, Variante.EsPhylauc],
        [Linhagem.Nephrytes] = [Variante.Yavos, Variante.Koroanos],
        [Linhagem.Econos] = [Variante.Alora]
    };

    public static bool IsValidCombination(Linhagem linhagem, Variante variante) =>
        ValidPairs[linhagem].Contains(variante);
}
