namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// The racial-characteristic choices for a Variante: a "Característica Gratuita" (always at least
/// one option — the player picks exactly one) and a "Característica Obrigatória" (empty for Sinir/
/// Laonir, which have none; a single fixed option for Alóra — no real choice; two options for the
/// rest). Ruína RPG - Sistema Básico.md §7 is the source; RacialTraitLookup holds the hardcoded
/// defaults, RacialTraitOverride lets a GM replace either list per Variante.
/// </summary>
public sealed record RacialTraitSlots(List<RacialTraitOption> Gratuita, List<RacialTraitOption> Obrigatoria);
