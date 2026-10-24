namespace RuinaRPG.Contracts.Fichas;

public record TraitDaFichaResponse(string Id, string TraitId, string Nome, string Descricao, int Custo, string Polaridade, string? Especificacao, bool RequerEspecificacao, bool IsRacial);
