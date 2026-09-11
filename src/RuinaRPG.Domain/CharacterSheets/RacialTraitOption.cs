namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// One option a player can pick for a racial characteristic slot (Gratuita/Obrigatória) — a name
/// from "Características" plus, when that trait's RequerEspecificacao is set, the pre-filled
/// Especificação the racial grant always uses (e.g. Alóra's Obrigatória is "Desvantagem Elemental"
/// specified to "Fogo", not a free player choice).
/// </summary>
public sealed record RacialTraitOption(string TraitNome, string? Especificacao = null);
