namespace RuinaRPG.Domain.Campaigns;

public static class CampaignAttachmentTargetValidator
{
    public static bool ExactlyOneSet(string? itemId, string? npcSheetId, string? creatureSheetId, string? bankEntryId, string? imageId) =>
        new[] { itemId, npcSheetId, creatureSheetId, bankEntryId, imageId }.Count(id => id is not null) == 1;
}
