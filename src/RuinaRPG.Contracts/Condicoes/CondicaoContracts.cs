namespace RuinaRPG.Contracts.Condicoes;

/// <summary>Enums viajam como texto (o nome do enum), como no resto da API.</summary>
public record CondicaoModificadorDto(string Alvo, string? AlvoEspecifico, string TipoDeValor, int Valor, string? Momento);

public record CondicaoResponse(string Id, string Nome, string Descricao, string Icone, int? DuracaoPadraoTurnos,
    List<CondicaoModificadorDto> Modificadores, List<string> Desabilitacoes);

public record SalvarCondicaoRequest(string Nome, string? Descricao, string Icone, int? DuracaoPadraoTurnos,
    List<CondicaoModificadorDto> Modificadores, List<string> Desabilitacoes);
