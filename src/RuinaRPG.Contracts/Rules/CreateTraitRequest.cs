namespace RuinaRPG.Contracts.Rules;

public record CreateTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao);
