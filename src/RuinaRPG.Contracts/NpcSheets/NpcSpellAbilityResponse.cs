using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.NpcSheets;

public record NpcSpellAbilityResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos);
