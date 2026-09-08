namespace RuinaRPG.Contracts.NpcSheets;

public record NpcTraitsListResponse(List<NpcTraitResponse> Positivas, int TotalPositivas, List<NpcTraitResponse> Negativas, int TotalNegativas, int PontosDisponiveis);
