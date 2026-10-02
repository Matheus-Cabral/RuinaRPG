namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>Aba História (Personagem e NPC): a galeria inteira, na ordem desejada — substitui a lista atual.</summary>
public record UpdateHistoriaImagensRequest(List<string> ImageIds);
