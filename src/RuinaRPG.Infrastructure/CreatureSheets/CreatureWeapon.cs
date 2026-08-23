using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.CreatureSheets;

public class CreatureWeapon
{
    public Guid Id { get; set; }
    public Guid CreatureSheetId { get; set; }
    public Guid? ItemId { get; set; } // null = natural attack, uses the Manual* fields below instead
    public bool IsEquipped { get; set; }
    public int? DurabilidadeAtual { get; set; } // null for natural attacks — they have no durability
    public string? ManualNome { get; set; }
    public TipoDeDano? ManualTipoDeDano { get; set; }
    public string? ManualDados { get; set; }
    public int? ManualDano { get; set; }
}
