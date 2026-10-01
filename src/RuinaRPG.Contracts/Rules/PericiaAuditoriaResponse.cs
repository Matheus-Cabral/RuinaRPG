namespace RuinaRPG.Contracts.Rules;

public record PericiaAuditoriaResponse(int Id, string Chave, string Nome, string? Descricao, string? AtributoSugerido, bool DisponivelParaCriaturas, bool Protegida, bool IsDeleted);
