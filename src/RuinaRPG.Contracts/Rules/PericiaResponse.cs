namespace RuinaRPG.Contracts.Rules;

public record PericiaResponse(int Id, string Chave, string Nome, string? Descricao, string? AtributoSugerido, bool DisponivelParaCriaturas, bool Protegida);
