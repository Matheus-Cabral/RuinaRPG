namespace RuinaRPG.Contracts.Rules;

public record AtualizarValorDeNivelRequest(int? Valor);

public record AtualizarOutrosBonusRequest(string? OutrosBonus);

public record CriarColunaDeNivelRequest(string Nome, string Tipo);

public record RenomearColunaDeNivelRequest(string Nome);

public record ReordenarColunasDeNivelRequest(List<Guid> Ids);

public record AtualizarConfigDaTabelaDeNiveisRequest(bool MostrarLimitesNoLivro);
