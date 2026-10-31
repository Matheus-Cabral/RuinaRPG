namespace RuinaRPG.Contracts.Rules;

/// <summary>
/// Um cruzamento (Auditoria de Regras R0020): dois elementos geram <see cref="SubElementoId"/>, exigindo
/// <see cref="MinimoA"/> pontos em <see cref="ElementoAId"/> e <see cref="MinimoB"/> em <see cref="ElementoBId"/>.
/// O par sai sempre com o menor id em <see cref="ElementoAId"/>.
/// </summary>
public record CruzamentoDeAfinidadeResponse(Guid Id, int ElementoAId, int MinimoA, int ElementoBId, int MinimoB, int SubElementoId);
