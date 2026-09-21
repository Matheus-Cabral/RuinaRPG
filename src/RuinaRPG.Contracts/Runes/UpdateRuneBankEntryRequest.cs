namespace RuinaRPG.Contracts.Runes;

// ImageId opcional: null ou "" remove a imagem da entrada.
public record UpdateRuneBankEntryRequest(string Nome, string Descricao, int Grau, string? ImageId = null);
