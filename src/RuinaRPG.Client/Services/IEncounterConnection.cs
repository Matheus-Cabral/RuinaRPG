namespace RuinaRPG.Client.Services;

/// <summary>
/// The realtime channel behind the encounter page. An interface only so the page can render under
/// bUnit without a SignalR server — SignalREncounterConnection is the one real implementation.
/// </summary>
public interface IEncounterConnection : IAsyncDisposable
{
    /// <summary>
    /// Connects and joins the encounter's group. <paramref name="onChanged"/> runs on every
    /// "ParticipantsChanged" and also after an automatic reconnect (once the group is re-joined),
    /// since events may have been missed.
    /// </summary>
    Task StartAsync(string encounterId, Func<Task> onChanged);
}
