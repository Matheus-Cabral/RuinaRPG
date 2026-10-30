namespace RuinaRPG.Contracts.Fichas;

/// <summary>Os pontos e a Experiência de um elemento na ficha (2.c); os dois ≥ 0, a Experiência opcional.</summary>
public record AtualizarAfinidadeRequest(int Pontos, int? Experiencia);
