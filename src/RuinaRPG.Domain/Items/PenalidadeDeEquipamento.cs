using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Domain.Items;

public sealed record PenalidadeDeAtributo(Atributo Atributo, int Valor);
public sealed record PenalidadeDeSubAtributo(SubAtributo SubAtributo, int Valor);
/// <summary><c>Pericia</c> é o Id da linha em Pericias, como em RequisitoDePericia.</summary>
public sealed record PenalidadeDePericia(int Pericia, int Valor);

/// <summary>
/// Penalidade de um equipamento (Arma, Armadura, Escudo, Artefato), aplicada enquanto a ficha não cumpre
/// os Requisitos dele. Valor é uma magnitude positiva, subtraída do alvo. Texto é só exibido. Gravado
/// como jsonb.
/// </summary>
public sealed record PenalidadeDeEquipamento
{
    public List<PenalidadeDeAtributo> Atributos { get; init; } = [];
    public List<PenalidadeDeSubAtributo> SubAtributos { get; init; } = [];
    public List<PenalidadeDePericia> Pericias { get; init; } = [];
    public string? Texto { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool EstaVazia => Atributos.Count == 0 && SubAtributos.Count == 0 && Pericias.Count == 0 && string.IsNullOrWhiteSpace(Texto);
}
