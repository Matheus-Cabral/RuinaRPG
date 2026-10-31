namespace RuinaRPG.Domain.Afinidades;

/// <summary>Requisito de um Sub-Elemento: mínimo de pontos em cada um dos dois elementos que o geram.</summary>
public sealed record Cruzamento(int ElementoA, int MinimoA, int ElementoB, int MinimoB, int SubElemento)
{
    /// <summary>
    /// O mesmo cruzamento com o menor id em <see cref="ElementoA"/> (como o banco grava o par simétrico); cada mínimo
    /// acompanha o seu elemento.
    /// </summary>
    public Cruzamento Ordenado() => ElementoA <= ElementoB ? this : new(ElementoB, MinimoB, ElementoA, MinimoA, SubElemento);

    /// <summary>Se os dois cruzamentos ligam o mesmo par de elementos, em qualquer ordem.</summary>
    public bool MesmoPar(Cruzamento outro) =>
        (ElementoA == outro.ElementoA && ElementoB == outro.ElementoB) || (ElementoA == outro.ElementoB && ElementoB == outro.ElementoA);
}
