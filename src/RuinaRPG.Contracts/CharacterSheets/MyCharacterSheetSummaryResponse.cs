namespace RuinaRPG.Contracts.CharacterSheets;

public record MyCharacterSheetSummaryResponse(string Id, string? ImageUrl, string? Nome, int Nivel, string CampaignId, string CampanhaNome);
