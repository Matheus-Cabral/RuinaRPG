namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record SpellAbilityEntryResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos);
