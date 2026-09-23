using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public class EquipmentKitItem
{
    public Guid Id { get; set; }
    public Guid KitId { get; set; }
    public required string Nome { get; set; }
    public ItemTipo Tipo { get; set; }
    public int Qtd { get; set; }
    public string? SubcategoriaHint { get; set; }
}
