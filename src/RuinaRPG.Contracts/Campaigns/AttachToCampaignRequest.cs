namespace RuinaRPG.Contracts.Campaigns;

public record AttachToCampaignRequest(string? ItemId, string? NpcSheetId, string? CreatureSheetId, string? SpellAbilityBankEntryId, string? ImageId);
