namespace RuinaRPG.Contracts.Encounters;

/// <summary>
/// <c>DuracaoTurnos</c> vazio usa a duração padrão do catálogo. <c>ValoresAjustados</c> troca o valor das linhas pelo
/// índice (0 = primeira) na lista de modificadores da entrada. <c>ItensDesabilitados</c> restringe uma categoria da
/// entrada a linhas da ficha (categoria → ids); sem a categoria, ela inteira fica desabilitada.
/// </summary>
public record AplicarCondicaoRequest(string CondicaoId, int? DuracaoTurnos, Dictionary<int, int>? ValoresAjustados, Dictionary<string, List<string>>? ItensDesabilitados);
