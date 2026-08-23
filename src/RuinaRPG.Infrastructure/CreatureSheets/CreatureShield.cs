namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureShield
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Guid ItemId { get; set; }
    public bool IsEquipped { get; set; }
    public int DurabilidadeAtual { get; set; }
}
