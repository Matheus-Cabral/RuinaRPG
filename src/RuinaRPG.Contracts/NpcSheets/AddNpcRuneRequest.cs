namespace RuinaRPG.Contracts.NpcSheets;

// Mesmo formato de AddCharacterRuneRequest.
// Disciplina obrigatória ao montar do zero; do banco vem da entrada.
public record AddNpcRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null, string? ImageId = null, string? Tipo = null, string? Disciplina = null);
