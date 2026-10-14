namespace RuinaRPG.Contracts.Encounters;

/// <summary>Uma Condição do catálogo já aplicada a um participante. <c>Ativa</c> só com o encontro em andamento e o prazo não vencido.</summary>
public record CondicaoAplicadaResponse(string Id, string Nome, string Descricao, string Icone, int? TurnosRestantes, bool Ativa);
