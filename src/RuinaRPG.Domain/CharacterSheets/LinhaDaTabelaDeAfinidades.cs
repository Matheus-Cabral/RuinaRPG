namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Uma linha da Tabela de Afinidades: a partir de <paramref name="Afinidade"/> pontos na essência ou
/// elemento da Afinidade escolhida, valem esta Eficiência Elemental e este Dano Elemental.
/// </summary>
public readonly record struct LinhaDaTabelaDeAfinidades(int Afinidade, int Eficiencia, int Dano);
