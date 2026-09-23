namespace RuinaRPG.Contracts.Rules;

public record ChooseEquipmentKitRequest(string KitId, List<ChoiceSlotSelectionRequest> ChoiceSelections);

public record ChoiceSlotSelectionRequest(string SlotId, string ItemId);
