namespace RuinaRPG.Domain.Rules;

public sealed record GraduacaoEfeito(string Nome, int Grau, string Descricao, string Gasto, bool TemPreRequisito, string? PreRequisitoDescricao);
