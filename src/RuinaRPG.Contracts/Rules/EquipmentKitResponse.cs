namespace RuinaRPG.Contracts.Rules;

public record EquipmentKitResponse(string Id, string Nome, string Descricao, int Ciclos,
    List<EquipmentKitItemResponse> Items, List<EquipmentKitChoiceSlotResponse> ChoiceSlots);

public record EquipmentKitItemResponse(string Id, string Nome, string Tipo, int Qtd, string? SubcategoriaHint);

public record EquipmentKitChoiceSlotResponse(string Id, string Label, string Tipo, List<string>? Subcategorias,
    string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd, string? ArmorSlot);
