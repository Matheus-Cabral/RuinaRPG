namespace RuinaRPG.Api.Services;

/// <summary>Um Atributo da ficha com o total já calculado (Artefatos, penalidades e Condições): o que a lista da ficha e o resumo do encontro mostram.</summary>
public record AtributoCalculado(string Atributo, int Gasto, int Bonus, bool TemMaestria, int Total);

/// <summary>Uma Perícia da ficha com o total calculado; <c>Total</c> é nulo quando a Perícia não tem Atributo.</summary>
public record PericiaCalculada(string Chave, string Nome, string? Descricao, int Gasto, int Modificador, string? Atributo, int? Total, bool BonusDeHistorico);
