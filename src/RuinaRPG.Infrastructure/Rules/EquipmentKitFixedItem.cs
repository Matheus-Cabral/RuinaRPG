using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Item completo da base global de itens fixos dos kits de Equipagem (editada pelo Auditor). Os kits
/// apontam para ele; ao aplicar um kit, vira uma cópia independente no catálogo do GM.
/// </summary>
public class EquipmentKitFixedItem
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public ItemTipo Tipo { get; set; }
    /// <summary>CreateItemRequest serializado (sem ImageId, Requisitos e PenalidadeDeRequisitos). jsonb.</summary>
    public required string Dados { get; set; }
    public RequisitosDePassiva? Requisitos { get; set; }
    public PenalidadeDeEquipamento? PenalidadeDeRequisitos { get; set; }
    /// <summary>True quando a conversão da 1.4.3 só tinha o nome; some na primeira edição pelo Auditor.</summary>
    public bool DetalhesIncompletos { get; set; }
}
