namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterTraitsListResponse(List<CharacterTraitResponse> Positivas, int TotalPositivas, List<CharacterTraitResponse> Negativas, int TotalNegativas);
