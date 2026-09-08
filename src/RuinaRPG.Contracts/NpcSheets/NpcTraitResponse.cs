namespace RuinaRPG.Contracts.NpcSheets;

public record NpcTraitResponse(string Id, string TraitId, string Nome, string Descricao, int Custo, string Polaridade, string? Especificacao, bool RequerEspecificacao);
