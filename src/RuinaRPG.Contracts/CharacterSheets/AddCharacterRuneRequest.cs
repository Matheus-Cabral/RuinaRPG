namespace RuinaRPG.Contracts.CharacterSheets;

// Exatamente um caminho: Nome + Descricao + Grau (montar do zero) OU SourceBankEntryId (partir de uma
// entrada do Banco de Runas). SourceBankEntryId e ImageId ficam no fim, opcionais, pra não quebrar quem já
// monta do zero. ImageId só vale ao montar do zero; ao partir do banco a imagem vem da entrada.
public record AddCharacterRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null, string? ImageId = null);
