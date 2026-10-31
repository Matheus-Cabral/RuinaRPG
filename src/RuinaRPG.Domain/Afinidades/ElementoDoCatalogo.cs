namespace RuinaRPG.Domain.Afinidades;

/// <summary>Um elemento do catálogo de Afinidades; <paramref name="Teto"/> nulo = sem limite.</summary>
public sealed record ElementoDoCatalogo(int Id, string Nome, TipoDeElemento Tipo, int? Teto);
