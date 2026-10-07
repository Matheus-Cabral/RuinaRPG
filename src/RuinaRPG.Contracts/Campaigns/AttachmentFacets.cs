namespace RuinaRPG.Contracts.Campaigns;

/// <summary>
/// O que a lista de anexos da campanha usa para filtrar dentro de cada grupo (Campanha R0006/R0009),
/// além do Nome. Só o que faz sentido para o tipo do anexo vem preenchido: Item → ItemTipo e
/// Subcategoria; Magia/Habilidade → EntradaTipo e Grau (uma Passiva não tem Grau); Runa → Grau e
/// Disciplina. Imagem, NPC e Criatura não têm facetas.
/// </summary>
public record AttachmentFacets(string? ItemTipo = null, string? Subcategoria = null, string? EntradaTipo = null, int? Grau = null, string? Disciplina = null);
