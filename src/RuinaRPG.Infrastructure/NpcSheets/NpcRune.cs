namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcRune
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }
}
