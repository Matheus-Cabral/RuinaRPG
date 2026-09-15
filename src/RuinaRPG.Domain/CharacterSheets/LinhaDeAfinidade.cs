namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Projeção pura de uma linha de Afinidades (2.c) — CharacterAffinity/NpcAffinity vivem em
/// Infrastructure/EF, então SubAttributeFormulas usa este record em vez de depender deles.
/// </summary>
public readonly record struct LinhaDeAfinidade(Elemento? Elemento, int? ElementoValor, SubElemento? SubElemento, int? SubElementoValor);
