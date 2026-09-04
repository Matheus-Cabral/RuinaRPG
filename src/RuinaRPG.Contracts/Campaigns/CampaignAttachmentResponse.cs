namespace RuinaRPG.Contracts.Campaigns;

public record CampaignAttachmentResponse(string Id, string Tipo, string Nome, bool? IsPublic, bool? NpcNomePublico, bool? NpcImagemPublica, bool? CreatureNomePublico, bool? CreatureImagemPublica, string? ImageUrl);
