namespace RuinaRPG.Contracts.Campaigns;

public record UpdateCampaignRequest(string Nome, string Descricao, string? ImageId, decimal BonusDeCarga = 0m);
