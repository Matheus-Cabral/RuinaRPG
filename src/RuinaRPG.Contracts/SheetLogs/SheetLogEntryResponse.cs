namespace RuinaRPG.Contracts.SheetLogs;

public record SheetLogEntryResponse(string Id, DateTime OccurredAt, string ActorNickname, bool ActorIsGm,
    string Categoria, string Acao, string Secao, string? Campo, string? ItemNome, string? ValorAntes, string? ValorDepois);
