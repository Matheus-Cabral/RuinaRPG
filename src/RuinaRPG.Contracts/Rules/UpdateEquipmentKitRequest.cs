namespace RuinaRPG.Contracts.Rules;

public record UpdateEquipmentKitRequest(string Nome, string Descricao, int Ciclos,
    List<EquipmentKitItemInput> Items, List<EquipmentKitChoiceSlotInput> ChoiceSlots);
