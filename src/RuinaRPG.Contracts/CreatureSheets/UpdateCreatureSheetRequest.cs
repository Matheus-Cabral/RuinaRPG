namespace RuinaRPG.Contracts.CreatureSheets;

public record UpdateCreatureSheetRequest(
    string? ImageId, string? Nome, string? Raca, string? Arquetipo, string? SubArquetipo, string? Afinidade,
    string? Rank, int Nivel, int ExperienciaAtual, int PontosDeIgnicao,
    int VitalidadeAtual, int FocoAtual, int AdrenalinaAtual, string Cobertura);
