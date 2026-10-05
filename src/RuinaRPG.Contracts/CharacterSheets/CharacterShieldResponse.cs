namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterShieldResponse(string Id, string ItemId, string Nome, string? Categoria, int? BonusDefesa, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima, string? ImageUrl, string? Descricao, bool Inquebravel = false, List<string>? Requisitos = null, List<string>? RequisitosPendentes = null, List<string>? Penalidade = null);
