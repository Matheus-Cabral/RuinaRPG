namespace RuinaRPG.Domain.Condicoes;

/// <summary>
/// Uma Condição ativa numa ficha ou participante. Em <see cref="Desabilitacoes"/>, um conjunto vazio
/// desabilita a categoria inteira; senão, só os itens listados.
/// </summary>
public sealed record CondicaoAtiva(
    Guid Id,
    string Nome,
    IReadOnlyList<ModificadorDeCondicao> Modificadores,
    IReadOnlyDictionary<CategoriaDesabilitavel, IReadOnlySet<Guid>> Desabilitacoes);

/// <summary>O que uma Condição faz, em linhas legíveis para a ficha.</summary>
public sealed record ExplicacaoDeCondicao(string Nome, IReadOnlyList<string> Linhas);
