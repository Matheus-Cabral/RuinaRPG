namespace RuinaRPG.Contracts.Fichas;

public record TraitsListResponse(List<TraitDaFichaResponse> Positivas, int TotalPositivas, List<TraitDaFichaResponse> Negativas, int TotalNegativas, int PontosDisponiveis, int PontosDisponiveisNegativas);
