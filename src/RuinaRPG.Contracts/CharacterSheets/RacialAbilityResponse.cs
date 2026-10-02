namespace RuinaRPG.Contracts.CharacterSheets;

public record RacialAbilityResponse(string? Nome, string? Descricao, int? ArcaRolada, string? ArcaNome, string? ArcaDescricao, List<ArcaEvolucaoResponse> ArcaEvolucoes, int ArcaDado = 20, bool ArcaForaDaTabela = false);
