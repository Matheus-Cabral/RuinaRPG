namespace RuinaRPG.Contracts.Fichas;

/// <summary>
/// As Afinidades da ficha (Ficha de Personagem 2.c e R0007; Personagem e NPC): o gasto (soma dos pontos), o máximo,
/// se a seção aparece (marcial sem Afinidade Adicional nem coração de mana: não) e os elementos que a ficha vê ou
/// em que já tem pontos.
/// </summary>
public record AfinidadesDaFichaResponse(int Gasto, int Maximo, bool SecaoVisivel, List<ElementoDaFichaResponse> Elementos);
