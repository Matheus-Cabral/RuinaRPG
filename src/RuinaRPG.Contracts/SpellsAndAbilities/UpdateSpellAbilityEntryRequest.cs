namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record UpdateSpellAbilityEntryRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos);
