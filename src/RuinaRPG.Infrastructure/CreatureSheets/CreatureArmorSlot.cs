using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureArmorSlot
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public ArmorSlotType Slot { get; set; }
    public Guid? ItemId { get; set; }
    public int? DurabilidadeAtual { get; set; }
}
