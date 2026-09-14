namespace RuinaRPG.Contracts.Rules;

public record EfeitoResponse(
    string Id, string Nome, int Grau, string Descricao, string TipoDeCusto,
    int? CustoFixo, int? CustoPorUnidade, string? UnidadeLabel, string? QuantidadeDerivadaDeEfeito,
    int? MaxUnidades, bool MaxEscalaPorGrau, int? MaxContandoAPartirDoGrau,
    int? CustoAlternativo, int? CustoAlternativoAPartirDoGrau,
    List<List<string>> PreRequisitos);
