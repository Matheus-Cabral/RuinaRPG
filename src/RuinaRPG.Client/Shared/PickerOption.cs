namespace RuinaRPG.Client.Shared;

/// <summary>One selectable result for <see cref="EntityPicker"/> — the underlying entity's real Id plus a human-readable label to show for it.</summary>
public record PickerOption(string Id, string Label);
