namespace RuinaRPG.Contracts.Campaigns;

public record CharacterSheetSummary(string Id, string? Nome, int Nivel);
public record GrantedSheetSummary(string Id, string Tipo, string? Nome); // Tipo: "Npc" | "Creature"
public record PublicAttachmentSummary(string Id, string Tipo, string? Nome, string? ImageUrl);

public record PlayerCampaignViewResponse(List<CharacterSheetSummary> MinhasFichas, List<GrantedSheetSummary> MeusCompanheiros, List<PublicAttachmentSummary> AnexosPublicos);
