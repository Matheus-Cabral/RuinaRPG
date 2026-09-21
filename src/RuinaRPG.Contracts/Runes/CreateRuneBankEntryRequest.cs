namespace RuinaRPG.Contracts.Runes;

// ImageId opcional (uma imagem por Runa); null ou "" significa sem imagem.
public record CreateRuneBankEntryRequest(string Nome, string Descricao, int Grau, string? ImageId = null);
