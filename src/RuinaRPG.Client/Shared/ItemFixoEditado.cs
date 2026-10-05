namespace RuinaRPG.Client.Shared;

/// <summary>Resultado do diálogo de edição de um item fixo de um kit: qual item fixo a linha passa a usar, em qual slot de Armadura, e se os detalhes do item compartilhado foram alterados.</summary>
public record ItemFixoEditado(string FixedItemId, string? ArmorSlot, bool DetalhesAlterados);
