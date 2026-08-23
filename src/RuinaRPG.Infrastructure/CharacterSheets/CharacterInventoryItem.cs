namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterInventoryItem
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid ItemId { get; set; }
    public int Qtd { get; set; }
}
