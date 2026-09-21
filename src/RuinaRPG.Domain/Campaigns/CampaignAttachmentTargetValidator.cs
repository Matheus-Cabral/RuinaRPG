namespace RuinaRPG.Domain.Campaigns;

public static class CampaignAttachmentTargetValidator
{
    // "params" so every target FK a CampaignAttachment can carry (item, NPC, criatura, banco de
    // magias, imagem, banco de runas) is counted the same way — exactly one must be set.
    public static bool ExactlyOneSet(params string?[] targetIds) =>
        targetIds.Count(id => id is not null) == 1;
}
