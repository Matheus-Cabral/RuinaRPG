using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// GM-editable override for RacialTraitLookup's hardcoded defaults (Ruína RPG - Sistema
/// Básico.md §7) — one row per GM+Variante, replacing either or both option lists wholesale.
/// Each list is stored as JSON (a plain string column, no EF value converter) rather than as
/// child rows — small, fixed-shape lists (2-3 options), never queried by content, only ever
/// read/written whole. Mirrors RacialAbilityOverride.
/// </summary>
public class RacialTraitOverride
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public Variante Variante { get; set; }
    public required string GratuitaOptionsJson { get; set; }
    public required string ObrigatoriaOptionsJson { get; set; }
}
