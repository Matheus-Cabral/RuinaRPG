using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

public sealed record RequisitoDeAtributo(Atributo Atributo, int Minimo);
public sealed record RequisitoDeSubAtributo(SubAtributo SubAtributo, int Minimo);
public sealed record RequisitoDePericia(Pericia Pericia, int Minimo);

/// <summary>
/// Requisitos de uma Passiva (Banco de Magias e Habilidades). Todo campo nulo — ou lista vazia — não
/// faz parte dos requisitos; os preenchidos precisam ser todos cumpridos. Gravado como jsonb.
/// </summary>
public sealed record RequisitosDePassiva
{
    public int? Nivel { get; init; }
    public Vocacao? Vocacao { get; init; }
    public string? Classe { get; init; }
    public Linhagem? Linhagem { get; init; }
    public Variante? Variante { get; init; }
    public int? Graduacao { get; init; }
    public bool? CoracaoDeMana { get; init; }
    public AfinidadeElemental? Afinidade { get; init; }
    public Estrela? Estrela { get; init; }
    public Guid? HistoricoId { get; init; }
    public List<RequisitoDeAtributo> Atributos { get; init; } = [];
    public List<RequisitoDeSubAtributo> SubAtributos { get; init; } = [];
    public List<RequisitoDePericia> Pericias { get; init; } = [];
}
