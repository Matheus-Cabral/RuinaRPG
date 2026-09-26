namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>Aba História (Personagem e NPC): the rich-text backstory as HTML; the API sanitizes it.</summary>
public record UpdateHistoriaRequest(string? Historia);
