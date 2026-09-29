using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Client.Shared.Fields;

public class RequisitoMinimoLinha
{
    public string Alvo { get; set; } = "";
    public int Minimo { get; set; }
}

/// <summary>Estado editável da seção Requisitos de uma Passiva; texto vazio vira nulo no DTO.</summary>
public class RequisitosFormModel
{
    public int? Nivel { get; set; }
    public string? Vocacao { get; set; }
    public string? Classe { get; set; }
    public string? Linhagem { get; set; }
    public string? Variante { get; set; }
    public int? Graduacao { get; set; }
    public bool CoracaoDeMana { get; set; }
    public string? Afinidade { get; set; }
    public string? Estrela { get; set; }
    public string? HistoricoId { get; set; }
    public List<RequisitoMinimoLinha> Atributos { get; set; } = new();
    public List<RequisitoMinimoLinha> SubAtributos { get; set; } = new();
    public List<RequisitoMinimoLinha> Pericias { get; set; } = new();

    public static RequisitosFormModel FromDto(RequisitosDePassivaDto? dto) => dto is null ? new() : new()
    {
        Nivel = dto.Nivel, Vocacao = dto.Vocacao, Classe = dto.Classe, Linhagem = dto.Linhagem, Variante = dto.Variante,
        Graduacao = dto.Graduacao, CoracaoDeMana = dto.CoracaoDeMana == true, Afinidade = dto.Afinidade, Estrela = dto.Estrela,
        HistoricoId = dto.HistoricoId,
        Atributos = Linhas(dto.Atributos), SubAtributos = Linhas(dto.SubAtributos), Pericias = Linhas(dto.Pericias),
    };

    public RequisitosDePassivaDto ToDto() => new(
        Nivel, Blank(Vocacao), Blank(Classe), Blank(Linhagem), Blank(Variante), Graduacao, CoracaoDeMana ? true : null,
        Blank(Afinidade), Blank(Estrela), Blank(HistoricoId), Dtos(Atributos), Dtos(SubAtributos), Dtos(Pericias));

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static List<RequisitoMinimoLinha> Linhas(List<RequisitoMinimoDto>? items) =>
        (items ?? []).Select(i => new RequisitoMinimoLinha { Alvo = i.Alvo, Minimo = i.Minimo }).ToList();
    private static List<RequisitoMinimoDto> Dtos(List<RequisitoMinimoLinha> linhas) =>
        linhas.Where(l => !string.IsNullOrWhiteSpace(l.Alvo)).Select(l => new RequisitoMinimoDto(l.Alvo, l.Minimo)).ToList();
}
