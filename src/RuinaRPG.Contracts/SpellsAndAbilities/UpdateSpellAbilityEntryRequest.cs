using System.ComponentModel.DataAnnotations;

namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record UpdateSpellAbilityEntryRequest(
    [Required(AllowEmptyStrings = false)] string Nome,
    string Tipo,
    [Range(0, int.MaxValue)] int Grau,
    string Descricao,
    List<SpellAbilityEffectRequest> Efeitos);
