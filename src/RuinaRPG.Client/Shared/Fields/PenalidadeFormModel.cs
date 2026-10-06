using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Client.Shared.Fields;

public class PenalidadeLinha
{
    public string Alvo { get; set; } = "";
    public int Valor { get; set; } = 1;
}

/// <summary>Estado editável da seção Penalidade de um equipamento; nada preenchido vira null no DTO.</summary>
public class PenalidadeFormModel
{
    public List<PenalidadeLinha> Atributos { get; set; } = new();
    public List<PenalidadeLinha> SubAtributos { get; set; } = new();
    public List<PenalidadeLinha> Pericias { get; set; } = new();
    public string Texto { get; set; } = "";

    public static PenalidadeFormModel FromDto(PenalidadeDeEquipamentoDto? dto) => dto is null ? new() : new()
    {
        Atributos = Linhas(dto.Atributos), SubAtributos = Linhas(dto.SubAtributos), Pericias = Linhas(dto.Pericias),
        Texto = dto.Texto ?? "",
    };

    public PenalidadeDeEquipamentoDto? ToDto()
    {
        var atributos = Dtos(Atributos);
        var subAtributos = Dtos(SubAtributos);
        var pericias = Dtos(Pericias);
        var texto = string.IsNullOrWhiteSpace(Texto) ? null : Texto.Trim();
        return atributos.Count == 0 && subAtributos.Count == 0 && pericias.Count == 0 && texto is null
            ? null
            : new(atributos, subAtributos, pericias, texto);
    }

    private static List<PenalidadeLinha> Linhas(List<PenalidadeLinhaDto>? items) =>
        (items ?? []).Select(i => new PenalidadeLinha { Alvo = i.Alvo, Valor = i.Valor }).ToList();
    private static List<PenalidadeLinhaDto> Dtos(List<PenalidadeLinha> linhas) =>
        linhas.Where(l => !string.IsNullOrWhiteSpace(l.Alvo)).Select(l => new PenalidadeLinhaDto(l.Alvo, l.Valor)).ToList();
}
