namespace RuinaRPG.Contracts.Notifications;

/// <summary>Event names of the notifications hub, shared by the API (sender) and the client (listener).</summary>
public static class NotificationEvents
{
    public const string SecretNoteReceived = "SecretNoteReceived";
    public const string SecretNotesChanged = "SecretNotesChanged";
}
