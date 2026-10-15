namespace RuinaRPG.Domain.Condicoes;

/// <param name="Maestrias">
/// Só numa linha de Maestria aplicada no encontro: as linhas de Maestria da ficha a que o GM a restringiu ao aplicar.
/// Nulo = como no catálogo (todas as Maestrias, ou as da Perícia da linha).
/// </param>
public sealed record ModificadorDeCondicao(AlvoDeCondicao Alvo, string? AlvoEspecifico, TipoDeValorDeCondicao TipoDeValor, int Valor, MomentoDeCondicao? Momento,
    IReadOnlySet<Guid>? Maestrias = null);
