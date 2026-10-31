namespace RuinaRPG.Contracts.Rules;

/// <summary>
/// Criar ou editar um elemento do catálogo (Auditoria de Regras R0018/R0019): <see cref="Tipo"/> é <c>Base</c>,
/// <c>Caminho</c> ou <c>SubElemento</c>; <see cref="Teto"/> nulo = sem limite, senão ≥ 1.
/// </summary>
public record SalvarElementoDeAfinidadeRequest(string Nome, string Tipo, int? Teto);
