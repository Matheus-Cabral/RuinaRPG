namespace RuinaRPG.Contracts.Rules;

public record EquipmentKitOptionResponse(string Id, string Nome, string Descricao, int Ciclos,
    List<EquipmentKitChoiceSlotOptionResponse> ChoiceSlots);

public record EquipmentKitChoiceSlotOptionResponse(string SlotId, string Label, List<EquipmentKitEligibleItemResponse> Options);

public record EquipmentKitEligibleItemResponse(string ItemId, string Nome);
