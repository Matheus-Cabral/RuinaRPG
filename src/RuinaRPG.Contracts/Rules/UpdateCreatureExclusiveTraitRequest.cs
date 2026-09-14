namespace RuinaRPG.Contracts.Rules;

public record UpdateCreatureExclusiveTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao);
