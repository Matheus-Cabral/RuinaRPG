namespace RuinaRPG.Contracts.Runes;

// ImageId opcional (uma imagem por Runa); null ou "" significa sem imagem.
// Tipo opcional: "Arcana", "Negra" ou null/"" (sem tipo); qualquer outro valor é 400.
// Disciplina obrigatória: nome do enum; ausente ou desconhecida é 400.
public record CreateRuneBankEntryRequest(string Nome, string Descricao, int Grau, string? ImageId = null, string? Tipo = null, string? Disciplina = null);
