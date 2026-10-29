namespace RuinaRPG.Contracts.Players;

/// <param name="Estado">Estado da conta: Ativa ou Desativada (excluídas não aparecem; Convite de Jogador R0006).</param>
public record PlayerSearchResultResponse(string Id, string Nickname, string Email, string Estado);
