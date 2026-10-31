namespace RuinaRPG.Contracts.Rules;

/// <summary>
/// Uma ligação de visibilidade (Auditoria de Regras R0021): <see cref="Origem"/> é <c>Vocacao</c> (a
/// <see cref="Chave"/> é o nome do membro do enum, ex. <c>Feiticeiro</c>) ou <c>Classe</c> (o nome da Classe como a
/// ficha grava).
/// </summary>
public record VisibilidadeDeElementoResponse(string Origem, string Chave, int ElementoId);
