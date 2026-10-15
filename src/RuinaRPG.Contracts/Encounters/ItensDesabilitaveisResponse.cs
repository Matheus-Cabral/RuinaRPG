namespace RuinaRPG.Contracts.Encounters;

public record ItemDesabilitavelDto(string Id, string Nome);

/// <summary>Uma linha de Maestria da ficha do participante; <c>Pericia</c> é a chave da Perícia dela.</summary>
public record MaestriaDaFichaDto(string Id, string Nome, string Pericia);

/// <summary>
/// As linhas da ficha do participante que uma Condição pode desabilitar, por categoria (as oito, sempre presentes), e as
/// Maestrias da ficha, a que o GM pode restringir uma linha de Maestria ao aplicar.
/// </summary>
public record ItensDesabilitaveisResponse(Dictionary<string, List<ItemDesabilitavelDto>> Categorias, List<MaestriaDaFichaDto>? Maestrias = null);
