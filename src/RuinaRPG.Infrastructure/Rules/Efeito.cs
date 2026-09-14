using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// The "[[GRAUS & CÍRCULOS]]" effect catalog, hand-authored (not parsed — see EfeitoSeeder) and
/// Auditor-editable. Same IsCustomized/IsDeleted convention as Trait: a manual edit always wins
/// over the seed on the next resync, and delete is soft (a Magia/Habilidade may already reference
/// this Nome).
/// </summary>
public class Efeito
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public int Grau { get; set; }
    public required string Descricao { get; set; }
    public TipoDeCusto TipoDeCusto { get; set; }
    public int? CustoFixo { get; set; }
    public int? CustoPorUnidade { get; set; }
    public string? UnidadeLabel { get; set; }
    public string? QuantidadeDerivadaDeEfeito { get; set; }
    public int? MaxUnidades { get; set; }
    public bool MaxEscalaPorGrau { get; set; }
    public int? MaxContandoAPartirDoGrau { get; set; }
    public int? CustoAlternativo { get; set; }
    public int? CustoAlternativoAPartirDoGrau { get; set; }

    /// <summary>JSON-serialized List&lt;List&lt;string&gt;&gt; — AND of OR-groups. Same
    /// string-column-holding-JSON convention as RacialTraitOverride.GratuitaOptionsJson.</summary>
    public string? PreRequisitosJson { get; set; }

    public bool IsCustomized { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
