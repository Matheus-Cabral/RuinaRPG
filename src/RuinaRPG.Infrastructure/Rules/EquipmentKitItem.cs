using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public class EquipmentKitItem
{
    public Guid Id { get; set; }
    public Guid KitId { get; set; }
    /// <summary>Legado (antes da 1.4.3): o item fixo vem de FixedItemId. Sai do schema numa versão futura.</summary>
    public string? Nome { get; set; }
    public Guid? FixedItemId { get; set; }
    public RuinaRPG.Domain.CharacterSheets.ArmorSlotType? ArmorSlot { get; set; }
    public ItemTipo Tipo { get; set; }
    public int Qtd { get; set; }
    public string? SubcategoriaHint { get; set; }
}
