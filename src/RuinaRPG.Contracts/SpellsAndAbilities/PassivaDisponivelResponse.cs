namespace RuinaRPG.Contracts.SpellsAndAbilities;

/// <summary>Uma Passiva do Banco que a ficha pode receber, com o que falta para cumprir os requisitos.</summary>
public record PassivaDisponivelResponse(SpellAbilityEntryResponse Entrada, List<string> Pendencias);
