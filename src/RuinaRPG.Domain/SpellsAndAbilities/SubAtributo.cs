namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>Os sub-atributos (2.b) que o servidor calcula em /sub-attributes e que podem ser requisito
/// de uma Passiva. Adrenalina fica de fora: é máximo de recurso (1.c), não sub-atributo.</summary>
public enum SubAtributo
{
    Iniciativa,
    Movimentacao,
    EsquivaNatural,
    DefesaNatural,
    ReducaoFisica,
    ReducaoMagica
}
