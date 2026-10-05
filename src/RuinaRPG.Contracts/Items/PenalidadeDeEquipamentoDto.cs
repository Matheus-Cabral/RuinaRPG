namespace RuinaRPG.Contracts.Items;

/// <summary>Uma linha de penalidade: Alvo é o nome do enum (Atributo, SubAtributo) ou a Chave de uma Perícia; Valor ≥ 1, subtraído.</summary>
public record PenalidadeLinhaDto(string Alvo, int Valor);

/// <summary>Penalidade de um equipamento enquanto os Requisitos não são cumpridos. Listas vazias/nulas e Texto em branco = sem penalidade.</summary>
public record PenalidadeDeEquipamentoDto(
    List<PenalidadeLinhaDto>? Atributos = null,
    List<PenalidadeLinhaDto>? SubAtributos = null,
    List<PenalidadeLinhaDto>? Pericias = null,
    string? Texto = null);
