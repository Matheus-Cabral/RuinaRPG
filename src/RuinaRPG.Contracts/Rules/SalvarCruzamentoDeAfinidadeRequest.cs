namespace RuinaRPG.Contracts.Rules;

/// <summary>
/// Criar ou editar um cruzamento: o par é simétrico, então os dois elementos podem vir em qualquer ordem — cada
/// mínimo acompanha o seu elemento (mínimos ≥ 1).
/// </summary>
public record SalvarCruzamentoDeAfinidadeRequest(int ElementoAId, int MinimoA, int ElementoBId, int MinimoB, int SubElementoId);
