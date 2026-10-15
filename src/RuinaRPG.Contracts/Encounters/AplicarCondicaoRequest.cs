namespace RuinaRPG.Contracts.Encounters;

/// <summary>
/// <c>DuracaoTurnos</c> vazio usa a duração padrão do catálogo. <c>ValoresAjustados</c> troca o valor das linhas pelo
/// índice (0 = primeira) na lista de modificadores da entrada. <c>ItensDesabilitados</c> restringe uma categoria da
/// entrada a linhas da ficha (categoria → ids); sem a categoria, ela inteira fica desabilitada. <c>SemDuracao</c> aplica sem
/// duração mesmo quando o catálogo tem uma padrão (não combina com <c>DuracaoTurnos</c>). <c>MaestriasEscolhidas</c> restringe
/// uma linha de Maestria (pelo índice) a linhas de Maestria da ficha (ids); sem a linha, ou com a lista vazia, ela vale como no
/// catálogo.
/// </summary>
public record AplicarCondicaoRequest(string CondicaoId, int? DuracaoTurnos, Dictionary<int, int>? ValoresAjustados, Dictionary<string, List<string>>? ItensDesabilitados,
    bool SemDuracao = false, Dictionary<int, List<string>>? MaestriasEscolhidas = null);
