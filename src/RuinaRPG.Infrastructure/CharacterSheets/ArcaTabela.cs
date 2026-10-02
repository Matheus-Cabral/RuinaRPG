namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// O dado que um GM escolheu para a sua Tabela de Arcas (D6, D8, D10, D12, D20 ou D100). Sem linha, vale D20.
/// </summary>
public class ArcaTabela
{
    public Guid GmId { get; set; }
    public int Dado { get; set; }
}
