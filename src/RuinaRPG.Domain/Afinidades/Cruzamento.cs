namespace RuinaRPG.Domain.Afinidades;

/// <summary>Requisito de um Sub-Elemento: mínimo de pontos em cada um dos dois elementos que o geram.</summary>
public sealed record Cruzamento(int ElementoA, int MinimoA, int ElementoB, int MinimoB, int SubElemento);
