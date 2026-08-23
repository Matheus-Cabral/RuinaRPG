using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterSpellAbilityResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos);
