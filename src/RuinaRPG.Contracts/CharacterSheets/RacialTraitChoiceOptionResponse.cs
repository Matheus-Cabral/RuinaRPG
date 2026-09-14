namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// A racial-characteristic option as offered to the sheet's owner/GM when resolving the pending
/// choice — unlike RacialTraitOptionResponse (the GM-editor's raw slot shape, also used to
/// (de)serialize RacialTraitOverride's stored JSON), this carries RequerEspecificacao, a value
/// derived from the Trait catalog at read time, never persisted — so the client knows whether to
/// show a free-text Especificação field before letting the player confirm this pick.
/// </summary>
public record RacialTraitChoiceOptionResponse(string TraitNome, string? Especificacao, bool RequerEspecificacao);
