using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.CreatureSheets;

public record AddCreatureSpellAbilityRequest(string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos);
