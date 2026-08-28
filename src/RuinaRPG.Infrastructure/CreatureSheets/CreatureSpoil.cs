namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureSpoil
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
    public int DT { get; set; }
}
