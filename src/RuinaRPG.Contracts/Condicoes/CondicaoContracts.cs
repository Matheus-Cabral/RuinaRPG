namespace RuinaRPG.Contracts.Condicoes;

/// <summary>Enums viajam como texto (o nome do enum), como no resto da API.</summary>
public record CondicaoModificadorDto(string Alvo, string? AlvoEspecifico, string TipoDeValor, int Valor, string? Momento);

public record CondicaoResponse(string Id, string Nome, string Descricao, string Icone, int? DuracaoPadraoTurnos,
    List<CondicaoModificadorDto> Modificadores, List<string> Desabilitacoes);

public record SalvarCondicaoRequest(string Nome, string? Descricao, string Icone, int? DuracaoPadraoTurnos,
    List<CondicaoModificadorDto> Modificadores, List<string> Desabilitacoes);

/// <summary>
/// Uma Condição ativa numa ficha, para quem pode editá-la: <c>Linhas</c> diz, em texto, o que ela muda;
/// <c>TurnosRestantes</c> é nulo quando não tem prazo.
/// </summary>
public record CondicaoAtivaNaFichaResponse(string Nome, string Descricao, string Icone, int? TurnosRestantes, List<string> Linhas);
