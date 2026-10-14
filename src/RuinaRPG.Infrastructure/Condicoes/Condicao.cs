namespace RuinaRPG.Infrastructure.Condicoes;

/// <summary>
/// Catálogo global de Condições, mantido pelo Auditor de Regras (ver "Requisitos - Condições").
/// Exclusão lógica: uma Condição já aplicada a um participante é uma cópia independente, então
/// apagar a entrada do catálogo nunca a afeta. Filtrada explicitamente por consulta, como Trait.
/// </summary>
public class Condicao
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public required string Icone { get; set; }
    public int? DuracaoPadraoTurnos { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
