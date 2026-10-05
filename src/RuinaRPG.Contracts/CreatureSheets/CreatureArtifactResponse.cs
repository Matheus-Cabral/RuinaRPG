namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureArtifactResponse(string Id, string ArtifactItemId, string Nome, string TipoDeAlvo, string Alvo, int Valor, string? ImageUrl, string? Descricao, List<string>? Requisitos = null, List<string>? RequisitosPendentes = null, List<string>? Penalidade = null, string? OutrasPenalidades = null);
