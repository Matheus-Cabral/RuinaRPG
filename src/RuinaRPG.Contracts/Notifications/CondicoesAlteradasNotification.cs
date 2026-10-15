namespace RuinaRPG.Contracts.Notifications;

/// <summary>
/// As Condições ativas de uma ficha mudaram (aplicada, removida, expirada, encontro iniciado, finalizado ou reaberto):
/// quem tem a ficha aberta recarrega os números. <paramref name="Kind"/> é "character", "npc" ou "creature".
/// </summary>
public record CondicoesAlteradasNotification(string Kind, string SheetId);
