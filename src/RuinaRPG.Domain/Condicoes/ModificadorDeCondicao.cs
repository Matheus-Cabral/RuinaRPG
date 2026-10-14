namespace RuinaRPG.Domain.Condicoes;

public sealed record ModificadorDeCondicao(AlvoDeCondicao Alvo, string? AlvoEspecifico, TipoDeValorDeCondicao TipoDeValor, int Valor, MomentoDeCondicao? Momento);
