namespace RuinaRPG.Infrastructure.NpcSheets;

public class NpcWeapon
{
    public Guid Id { get; set; }
    public Guid NpcSheetId { get; set; }
    public Guid ItemId { get; set; }
    public bool IsEquipped { get; set; }
    public int DurabilidadeAtual { get; set; }
}
