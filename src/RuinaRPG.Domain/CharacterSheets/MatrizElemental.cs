namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// A Matriz Elemental ("Matriz_Elemental.png"): o Sub-Elemento de uma linha de Afinidade é a
/// interseção das suas duas Essências Básicas. Fonte única da regra — usada pela validação da API,
/// pela migration que converteu as linhas antigas e pelas opções do cliente.
/// </summary>
public static class MatrizElemental
{
    // Pares de Elementos aparecem uma vez; Intersecao trata a simetria (Água + Ar = Gelo).
    private static readonly (Elemento Essencia1, EssenciaBasica Essencia2, SubElemento SubElemento)[] Tabela =
    [
        (Elemento.Ar, EssenciaBasica.Agua, SubElemento.Gelo),
        (Elemento.Ar, EssenciaBasica.Fogo, SubElemento.Raio),
        (Elemento.Agua, EssenciaBasica.Terra, SubElemento.Flora),
        (Elemento.Fogo, EssenciaBasica.Terra, SubElemento.Ferro),
        (Elemento.Ar, EssenciaBasica.Alma, SubElemento.Prever),
        (Elemento.Agua, EssenciaBasica.Alma, SubElemento.Purificar),
        (Elemento.Fogo, EssenciaBasica.Vida, SubElemento.Curar),
        (Elemento.Terra, EssenciaBasica.Vida, SubElemento.Aprimorar),
        (Elemento.Ar, EssenciaBasica.Mundano, SubElemento.Ecomancia),
        (Elemento.Agua, EssenciaBasica.Mundano, SubElemento.Hemomancia),
        (Elemento.Fogo, EssenciaBasica.Mundano, SubElemento.Necromancia),
        (Elemento.Terra, EssenciaBasica.Mundano, SubElemento.Invocacao),
    ];

    public static SubElemento? Intersecao(Elemento essencia1, EssenciaBasica essencia2)
    {
        foreach (var (e1, e2, sub) in Tabela)
        {
            if (e1 == essencia1 && e2 == essencia2)
                return sub;
            // Simetria dos pares de Elementos: (Agua, Ar) acha a linha (Ar, Agua).
            if (e2 <= EssenciaBasica.Terra && (int)e2 == (int)essencia1 && (int)e1 == (int)essencia2)
                return sub;
        }
        return null;
    }

    public static IReadOnlyList<EssenciaBasica> OpcoesSegundaEssencia(Elemento essencia1) =>
        Enum.GetValues<EssenciaBasica>().Where(e2 => Intersecao(essencia1, e2) is not null).ToList();

    public static EssenciaBasica? SegundaEssenciaQueProduz(Elemento essencia1, SubElemento subElemento) =>
        Enum.GetValues<EssenciaBasica>().Cast<EssenciaBasica?>().FirstOrDefault(e2 => Intersecao(essencia1, e2!.Value) == subElemento);
}
