namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>Cemitério de fichas: o GM marca ou desmarca a caixa "Morto" da Ficha de Personagem.</summary>
public record DefinirMorteRequest(bool Morto);
