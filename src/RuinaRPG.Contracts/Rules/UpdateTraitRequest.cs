namespace RuinaRPG.Contracts.Rules;

public record UpdateTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao);
