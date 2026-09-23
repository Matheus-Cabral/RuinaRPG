namespace RuinaRPG.Contracts.Rules;

public record CreateEquipmentKitRequest(string Nome, string Descricao, int Ciclos,
    List<EquipmentKitItemInput> Items, List<EquipmentKitChoiceSlotInput> ChoiceSlots);

public record EquipmentKitItemInput(string Nome, string Tipo, int Qtd, string? SubcategoriaHint);

public record EquipmentKitChoiceSlotInput(string Label, string Tipo, List<string>? Subcategorias,
    string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd);
