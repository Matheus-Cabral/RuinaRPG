namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureAffection
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public required string Nome { get; set; }
    public int Favorabilidade { get; set; }
}
