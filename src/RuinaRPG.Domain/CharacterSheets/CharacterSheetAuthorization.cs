namespace RuinaRPG.Domain.CharacterSheets;

public static class CharacterSheetAuthorization
{
    public static bool CanEdit(Guid callerId, Guid ownerId, Guid campaignGmId) =>
        callerId == ownerId || callerId == campaignGmId;
}
