namespace RuinaRPG.Contracts.Encounters;

/// <summary>
/// <c>DuracaoTurnos</c> vazio usa a duração padrão do catálogo. <c>ValoresAjustados</c> troca o valor das linhas pelo
/// índice (0 = primeira) na lista de modificadores da entrada. <c>ItensDesabilitados</c> restringe uma categoria da
/// entrada a linhas da ficha (categoria → ids); sem a categoria, ela inteira fica desabilitada. <c>SemDuracao</c> aplica sem
/// duração mesmo quando o catálogo tem uma padrão (não combina com <c>DuracaoTurnos</c>).
/// </summary>
public record AplicarCondicaoRequest(string CondicaoId, int? DuracaoTurnos, Dictionary<int, int>? ValoresAjustados, Dictionary<string, List<string>>? ItensDesabilitados,
    bool SemDuracao = false);
