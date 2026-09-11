namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// Ficha de Personagem 3.f: one read-only value per Tipo de Dano, each the sum of equipped
/// Artefatos (5.b) whose Tipo de alvo is Dano and whose Alvo is that Tipo de Dano. Shared by
/// Personagem/NPC/Criatura, same as SubAttributesResponse.
/// </summary>
public record ModificadorDeDanoResponse(int Cortante, int Perfurante, int Contundente, int Arcano);
