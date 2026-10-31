namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// Ficha de Personagem 3.f: one read-only value per Tipo de Dano, each the sum of equipped
/// Artefatos (5.b) whose Tipo de alvo is Dano and whose Alvo is that Tipo de Dano. Shared by
/// Personagem/NPC/Criatura, same as SubAttributesResponse.
///
/// <c>DanoElemental</c> é o modificador de dano por elemento (chave = id do elemento no catálogo de Afinidades, um
/// por elemento que não é Caminho), que só as Condições ativas dão: vale 0 em todos sem Condição. Não confundir com SubAttributesResponse.DanoElemental, o dano da
/// Tabela de Afinidades.
/// </summary>
public record ModificadorDeDanoResponse(int Cortante, int Perfurante, int Contundente, int Arcano, Dictionary<int, int> DanoElemental);
