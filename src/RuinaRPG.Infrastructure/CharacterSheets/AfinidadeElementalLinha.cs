namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// Uma linha da Tabela de Afinidades — semeada de "Tabela de Afinidades.md" e mantida (criar, editar,
/// excluir) pelo Auditor de Regras. Afinidade é única.
/// </summary>
public class AfinidadeElementalLinha
{
    public Guid Id { get; set; }
    public int Afinidade { get; set; }
    public int Eficiencia { get; set; }
    public int Dano { get; set; }
}
