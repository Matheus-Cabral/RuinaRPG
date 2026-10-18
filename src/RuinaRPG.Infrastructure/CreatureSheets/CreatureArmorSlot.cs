using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureArmorSlot : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public ArmorSlotType Slot { get; set; }
    public Guid? ItemId { get; set; }
    public int? DurabilidadeAtual { get; set; }
}
