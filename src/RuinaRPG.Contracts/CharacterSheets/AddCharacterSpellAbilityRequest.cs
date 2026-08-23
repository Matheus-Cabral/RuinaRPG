using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterSpellAbilityRequest(string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos);
