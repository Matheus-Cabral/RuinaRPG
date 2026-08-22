namespace RuinaRPG.Domain.CharacterSheets;

public static class RacialAbilityLookup
{
    private static readonly Dictionary<Variante, RacialAbility> Abilities = new()
    {
        [Variante.Sinir] = new("Racial (Arca)", "Role 1d18 na tabela de Arcas."),
        [Variante.Laonir] = new("Racial (Arca)", "Role 1d18 na tabela de Arcas."),
        [Variante.PhylacTai] = new("Racial (Lei da Selva)", "Recupera uma quantidade de PV igual ao Foco (PF) gasto em habilidades e magias."),
        [Variante.EsPhylauc] = new("Racial (Lei da Selva)", "Recupera uma quantidade de PV igual ao Foco (PF) gasto em habilidades e magias."),
        [Variante.Yavos] = new("Racial (Sobre Voo)", "Passiva. Capacidade de voo livre."),
        [Variante.Koroanos] = new("Racial (Sobre Voo)", "Passiva. Capacidade de voo livre."),
        [Variante.Alora] = new("Racial (Amplificador Místico)", "Ao usar qualquer item ou artefato de origem holística, o Alóra pode aumentar a escala do dado da rolagem em +1 degrau (ex: de d6 para d8, de d10 para d12).")
    };

    public static RacialAbility For(Variante variante) => Abilities[variante];
}
