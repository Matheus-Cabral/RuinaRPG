namespace RuinaRPG.Contracts.Items;

/// <summary>Um equipamento em uso cujos Requisitos a ficha não cumpre: o que falta e a penalidade aplicada (linhas numéricas) e, à parte, o texto livre que não é aplicado.</summary>
public record PenalidadeAtivaResponse(string ItemNome, List<string> RequisitosPendentes, List<string> Penalidade, string? OutrasPenalidades = null);
