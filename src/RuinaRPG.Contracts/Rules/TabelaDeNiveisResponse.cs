namespace RuinaRPG.Contracts.Rules;

public record ColunaDeNivelResponse(Guid Id, string Nome, string Tipo, string? ChaveDeSistema, bool DoSistema, int Ordem);

public record LinhaDeNivelResponse(int Nivel, string? OutrosBonus, Dictionary<Guid, int?> Valores);

public record TabelaDeNiveisResponse(List<ColunaDeNivelResponse> Colunas, List<LinhaDeNivelResponse> Linhas, bool MostrarLimitesNoLivro = false);
