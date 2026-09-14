using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// The catalog's structured rule for one Efeito — a plain projection of the Efeito table (which
/// lives in Infrastructure/EF), kept here so EfeitoCustoCalculator/EfeitoValidator stay pure and
/// have zero dependency on EF or Contracts. The API layer maps Infrastructure.Efeito rows into
/// this shape before calling into Domain.
/// </summary>
public sealed record EfeitoRegra(
    string Nome,
    int Grau,
    TipoDeCusto TipoDeCusto,
    int? CustoFixo,
    int? CustoPorUnidade,
    string? QuantidadeDerivadaDeEfeito,
    int? MaxUnidades,
    bool MaxEscalaPorGrau,
    int? MaxContandoAPartirDoGrau,
    int? CustoAlternativo,
    int? CustoAlternativoAPartirDoGrau,
    IReadOnlyList<IReadOnlyList<string>> PreRequisitos);

/// <summary>One Efeito entry as submitted by a client when creating/updating a Magia/Habilidade.</summary>
public sealed record EfeitoSubmetido(string EfeitoNome, int? Quantidade, int CustoPI);
