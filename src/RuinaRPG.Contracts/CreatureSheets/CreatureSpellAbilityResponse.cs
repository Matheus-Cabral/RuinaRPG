using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.CreatureSheets;

public record CreatureSpellAbilityResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos);
