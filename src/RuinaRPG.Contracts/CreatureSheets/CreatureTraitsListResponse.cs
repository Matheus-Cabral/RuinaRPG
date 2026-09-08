namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureTraitsListResponse(List<CreatureTraitResponse> Positivas, int TotalPositivas, List<CreatureTraitResponse> Negativas, int TotalNegativas, int PontosDisponiveis);
