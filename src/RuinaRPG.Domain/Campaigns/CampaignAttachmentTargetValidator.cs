namespace RuinaRPG.Domain.Campaigns;

public static class CampaignAttachmentTargetValidator
{
    // Parâmetros nomeados e explícitos (não "params") de propósito: ao adicionar uma nova FK de alvo
    // em CampaignAttachment, o compilador obriga a atualizar cada chamador — nenhum alvo fica de fora
    // da contagem sem que ninguém perceba. Exatamente um deve estar preenchido.
    public static bool ExactlyOneSet(
        string? itemId,
        string? npcSheetId,
        string? creatureSheetId,
        string? spellAbilityBankEntryId,
        string? imageId,
        string? runeBankEntryId) =>
        new[] { itemId, npcSheetId, creatureSheetId, spellAbilityBankEntryId, imageId, runeBankEntryId }
            .Count(id => id is not null) == 1;
}
