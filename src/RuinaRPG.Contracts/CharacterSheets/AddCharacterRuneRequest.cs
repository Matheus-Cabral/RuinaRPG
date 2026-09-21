namespace RuinaRPG.Contracts.CharacterSheets;

// Exatamente um caminho: Nome + Descricao + Grau (montar do zero) OU SourceBankEntryId (partir de uma
// entrada do Banco de Runas). SourceBankEntryId fica no fim, opcional, pra não quebrar quem já monta do zero.
public record AddCharacterRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null);
