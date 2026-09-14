namespace RuinaRPG.Contracts.Rules;

public record CreateCreatureExclusiveTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao);
