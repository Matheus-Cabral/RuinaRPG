using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterTrait
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid TraitId { get; set; }
    public Polaridade Polaridade { get; set; }
    public string? Especificacao { get; set; }

    // Granted automatically by RacialTraitLookup/RacialTraitOverride when the player resolves the
    // Variante's racial-characteristic choice — costs 0 regardless of the Trait's own Custo (5.d),
    // and RacialVariante records which Variante granted it, so a later Variante change can tell
    // this grant is stale (belongs to the old race) and needs re-resolving. Both null/false for an
    // ordinary player-picked characteristic.
    public bool IsRacial { get; set; }
    public Variante? RacialVariante { get; set; }
}
