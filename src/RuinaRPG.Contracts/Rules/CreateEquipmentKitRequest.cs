namespace RuinaRPG.Contracts.Rules;

public record CreateEquipmentKitRequest(string Nome, string Descricao, int Ciclos,
    List<EquipmentKitItemInput> Items, List<EquipmentKitChoiceSlotInput> ChoiceSlots);

public record EquipmentKitItemInput(string FixedItemId, int Qtd, string? ArmorSlot = null);

public record EquipmentKitChoiceSlotInput(string Label, string Tipo, List<string>? Subcategorias,
    string? Rank, int Qtd, string? BonusSubcategoria, string? BonusFixedItemId, int? BonusQtd, string? ArmorSlot);
