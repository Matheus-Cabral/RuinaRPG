using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.Fichas;

public record AddSpellAbilityRequest(string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos);
