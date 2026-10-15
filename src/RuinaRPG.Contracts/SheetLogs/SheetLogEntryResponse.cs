namespace RuinaRPG.Contracts.SheetLogs;

/// <param name="Automatico">Feita pelo servidor por conta própria (corte do Atual por uma Condição, expiração), dentro da ação de <paramref name="ActorNickname"/>.</param>
public record SheetLogEntryResponse(string Id, DateTime OccurredAt, string ActorNickname, bool ActorIsGm,
    string Categoria, string Acao, string Secao, string? Campo, string? ItemNome, string? ValorAntes, string? ValorDepois, bool Automatico = false);
