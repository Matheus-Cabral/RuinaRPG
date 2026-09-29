namespace RuinaRPG.Infrastructure.CharacterSheets;

public class ArcaEntry
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public int Roll { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public List<ArcaEvolucao> Evolucoes { get; set; } = new();
}
