namespace RuinaRPG.Infrastructure.Afinidades;

/// <summary>
/// Dois elementos que geram um Sub-Elemento, com o mínimo de pontos exigido em cada um (Auditoria de Regras R0020).
/// O par é simétrico e único: gravado com <see cref="ElementoAId"/> &lt; <see cref="ElementoBId"/>.
/// </summary>
public class CruzamentoDeAfinidade
{
    public Guid Id { get; set; }
    public int ElementoAId { get; set; }
    public int MinimoA { get; set; } = 1;
    public int ElementoBId { get; set; }
    public int MinimoB { get; set; } = 1;
    public int SubElementoId { get; set; }
}
