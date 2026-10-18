using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterArmorSlot : ILinhaDaFicha
{
    public Guid Id { get; set; }
    public Guid SheetId { get; set; }
    public ArmorSlotType Slot { get; set; }
    public Guid? ItemId { get; set; }
    public int? DurabilidadeAtual { get; set; }
}
