namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>Evolução de uma Arca, liberada a partir de Nivel (Requisitos - Habilidades Raciais R0006).</summary>
public class ArcaEvolucao
{
    public Guid Id { get; set; }
    public Guid ArcaEntryId { get; set; }
    public int Nivel { get; set; }
    public required string Descricao { get; set; }
    /// <summary>Desempate estável entre evoluções do mesmo nível.</summary>
    public DateTimeOffset CriadaEm { get; set; }
}
