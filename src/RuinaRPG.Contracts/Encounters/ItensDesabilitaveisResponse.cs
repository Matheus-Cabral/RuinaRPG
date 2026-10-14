namespace RuinaRPG.Contracts.Encounters;

public record ItemDesabilitavelDto(string Id, string Nome);

/// <summary>As linhas da ficha do participante que uma Condição pode desabilitar, por categoria (as oito, sempre presentes).</summary>
public record ItensDesabilitaveisResponse(Dictionary<string, List<ItemDesabilitavelDto>> Categorias);
