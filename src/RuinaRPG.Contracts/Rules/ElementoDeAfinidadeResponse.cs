namespace RuinaRPG.Contracts.Rules;

/// <summary>
/// Um elemento do catálogo de Afinidades (Auditoria de Regras R0018): o <see cref="Id"/> estável que fichas, Criaturas,
/// Condições e Passivas guardam, o nome atual, o tipo (<c>Base</c>, <c>Caminho</c> ou <c>SubElemento</c>) e o teto
/// de pontos (nulo = sem limite).
/// </summary>
public record ElementoDeAfinidadeResponse(int Id, string Nome, string Tipo, int? Teto);
