namespace RuinaRPG.Contracts.Fichas;

public record SkillResponse(string Pericia, int Gasto, int Modificador, string? AtributoEscolhido, int? Total, bool ContaHistorico, string Nome, string? Descricao);
