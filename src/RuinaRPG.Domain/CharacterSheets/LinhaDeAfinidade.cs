namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Projeção pura de uma linha de Afinidades (2.c) — CharacterAffinity/NpcAffinity vivem em
/// Infrastructure/EF, então SubAttributeFormulas usa este record em vez de depender deles.
/// </summary>
public readonly record struct LinhaDeAfinidade(
    Elemento? Elemento, int? ElementoValor, SubElemento? SubElemento, int? SubElementoValor,
    EssenciaBasica? SegundaEssencia, int? SegundaEssenciaValor, int? Experiencia)
{
    // Sem parâmetros opcionais: as linhas são montadas dentro de expression trees do EF.
    public LinhaDeAfinidade(
        Elemento? elemento, int? elementoValor, SubElemento? subElemento, int? subElementoValor,
        EssenciaBasica? segundaEssencia, int? segundaEssenciaValor)
        : this(elemento, elementoValor, subElemento, subElementoValor, segundaEssencia, segundaEssenciaValor, null)
    {
    }

    public LinhaDeAfinidade(Elemento? elemento, int? elementoValor, SubElemento? subElemento, int? subElementoValor)
        : this(elemento, elementoValor, subElemento, subElementoValor, null, null, null)
    {
    }
}
