using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public class EquipmentKitChoiceSlot
{
    public Guid Id { get; set; }
    public Guid KitId { get; set; }
    public required string Label { get; set; }
    public ItemTipo Tipo { get; set; }
    public string? SubcategoriasCsv { get; set; }
    public Tier? Tier { get; set; }
    public int Qtd { get; set; }
    public string? BonusSubcategoria { get; set; }
    public string? BonusNome { get; set; }
    public int? BonusQtd { get; set; }
    public RuinaRPG.Domain.CharacterSheets.ArmorSlotType? ArmorSlot { get; set; }
}
