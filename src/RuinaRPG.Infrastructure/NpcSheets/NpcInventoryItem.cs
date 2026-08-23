namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcInventoryItem
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
}
