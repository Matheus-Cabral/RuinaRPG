namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcAffection
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public required string Nome { get; set; }
    public int Favorabilidade { get; set; }
}
