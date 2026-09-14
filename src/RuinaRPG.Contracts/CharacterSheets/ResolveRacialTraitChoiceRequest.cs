namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// ObrigatoriaTraitNome is ignored when the Variante's Obrigatória slot has 0 or 1 options (nothing
/// to choose — 0 means no such slot, 1 means it's auto-granted) and required when it has 2+.
/// GratuitaEspecificacao/ObrigatoriaEspecificacao are only used when the chosen option's trait
/// RequerEspecificacao and RacialTraitLookup doesn't already pin one (e.g. Alóra's Desvantagem
/// Elemental "Fogo") — otherwise ignored.
/// </summary>
public record ResolveRacialTraitChoiceRequest(string GratuitaTraitNome, string? ObrigatoriaTraitNome, string? GratuitaEspecificacao = null, string? ObrigatoriaEspecificacao = null);
