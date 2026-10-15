namespace RuinaRPG.Contracts.CreatureSheets;

/// <summary>Os <c>*AtualCarregad*</c> seguem a regra de <see cref="CharacterSheets.UpdateCharacterSheetRequest"/>.</summary>
public record UpdateCreatureSheetRequest(
    string? ImageId, string? Nome, string? Raca, string? Arquetipo, string? SubArquetipo, string? Afinidade,
    string? Rank, int Nivel, int ExperienciaAtual, int PontosDeIgnicao,
    int VitalidadeAtual, int FocoAtual, int AdrenalinaAtual, string Cobertura,
    int? VitalidadeAtualCarregada = null, int? FocoAtualCarregado = null, int? AdrenalinaAtualCarregada = null);
