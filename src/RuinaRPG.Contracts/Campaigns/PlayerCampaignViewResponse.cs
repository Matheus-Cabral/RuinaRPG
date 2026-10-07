namespace RuinaRPG.Contracts.Campaigns;

public record CharacterSheetSummary(string Id, string? Nome, int Nivel, string? ImageUrl);
public record GrantedSheetSummary(string Id, string Tipo, string? Nome, string? ImageUrl); // Tipo: "Npc" | "Creature"
public record PublicAttachmentSummary(string Id, string Tipo, string? Nome, string? ImageUrl, AttachmentFacets? Facets = null);

public record PlayerCampaignViewResponse(List<CharacterSheetSummary> MinhasFichas, List<GrantedSheetSummary> MeusCompanheiros, List<PublicAttachmentSummary> AnexosPublicos);
