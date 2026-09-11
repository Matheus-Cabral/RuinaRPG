namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// Sheet-facing: whether the sheet's current Variante still has an unresolved racial-characteristic
/// choice. When Pending is true, Gratuita/Obrigatoria list the options to present in the chooser
/// (Obrigatoria empty means there's no such slot for this Variante at all).
/// </summary>
public record PendingRacialTraitChoiceResponse(bool Pending, List<RacialTraitOptionResponse> Gratuita, List<RacialTraitOptionResponse> Obrigatoria);
