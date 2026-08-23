using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.NpcSheets;

public record AddNpcSpellAbilityRequest(string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos);
