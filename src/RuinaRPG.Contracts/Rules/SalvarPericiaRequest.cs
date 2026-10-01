namespace RuinaRPG.Contracts.Rules;

public record SalvarPericiaRequest(string Nome, string? Descricao, string? AtributoSugerido, bool DisponivelParaCriaturas);
