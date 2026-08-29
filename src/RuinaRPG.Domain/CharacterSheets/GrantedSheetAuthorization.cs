namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// NPC/Criatura sheets start out as GM-only roster content (OwnerId null) and can later be
/// granted to a player (Requisitos - Campanha R0010), who then edits it "com o mesmo modelo de
/// edição" as their own Ficha de Personagem — i.e. CharacterSheetAuthorization.CanEdit's owner-or-
/// GM rule, just with a nullable OwnerId since an un-granted sheet has no owner at all yet.
/// </summary>
public static class GrantedSheetAuthorization
{
    public static bool CanEdit(Guid callerId, Guid? ownerId, Guid gmId) =>
        callerId == gmId || (ownerId is not null && callerId == ownerId.Value);
}
