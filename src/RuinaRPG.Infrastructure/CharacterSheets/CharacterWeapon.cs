namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterWeapon
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid ItemId { get; set; }
    public bool IsEquipped { get; set; }
    public int DurabilidadeAtual { get; set; }
}
