namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureSheetResponse(
    string Id, string? OwnerId, string? ImageUrl, string? Nome, string? Raca, string? Arquetipo, string? SubArquetipo,
    string? Afinidade, string? Rank, int Nivel, int ExperienciaAtual, int Kill, int Assistencia,
    int PontosDeIgnicao, int VitalidadeAtual, int FocoAtual, int AdrenalinaAtual, string Cobertura,
    int VitalidadeMaximo, int FocoMaximo, int AdrenalinaMaximo);
