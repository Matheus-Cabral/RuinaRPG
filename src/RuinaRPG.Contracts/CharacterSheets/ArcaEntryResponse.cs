namespace RuinaRPG.Contracts.CharacterSheets;

public record ArcaEntryResponse(int Roll, string? Nome, string? Descricao, List<ArcaEvolucaoResponse> Evolucoes);
