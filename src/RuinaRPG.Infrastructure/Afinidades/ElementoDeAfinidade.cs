using RuinaRPG.Domain.Afinidades;

namespace RuinaRPG.Infrastructure.Afinidades;

/// <summary>
/// Um elemento do catálogo de Afinidades (Auditoria de Regras R0018/R0019). O <see cref="Id"/> é estável: os 18
/// primeiros são os inteiros do antigo enum <c>AfinidadeElemental</c> e Mundano é 18; os criados pelo Auditor
/// continuam a sequência.
/// </summary>
public class ElementoDeAfinidade
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public TipoDeElemento Tipo { get; set; }

    /// <summary>Máximo de pontos por ficha; null = sem limite.</summary>
    public int? Teto { get; set; }
}
