namespace RuinaRPG.Contracts.SpellsAndAbilities;

/// <summary>Edição de uma Magia/Habilidade montada do zero numa ficha (Ficha de Personagem 4.b).</summary>
public record UpdateSheetSpellAbilityRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos);
