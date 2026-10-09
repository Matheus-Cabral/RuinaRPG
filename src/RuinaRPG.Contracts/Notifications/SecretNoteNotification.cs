namespace RuinaRPG.Contracts.Notifications;

/// <summary>Campanha R0015: payload of SecretNoteReceived. Never carries the note's text.</summary>
public record SecretNoteNotification(string CampaignId, string CampaignName);
