namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record CreateSpellAbilityEntryRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos);
