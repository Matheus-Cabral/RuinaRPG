using RuinaRPG.Client.Services;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>Stand-in for the encounter hub connection (bUnit can't host SignalR): lets a test play the server's "ParticipantsChanged".</summary>
public sealed class FakeEncounterConnection : IEncounterConnection
{
    private Func<Task>? _onChanged;

    public string? JoinedEncounterId { get; private set; }

    public Task StartAsync(string encounterId, Func<Task> onChanged)
    {
        JoinedEncounterId = encounterId;
        _onChanged = onChanged;
        return Task.CompletedTask;
    }

    public Task RaiseChangedAsync() => _onChanged!();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
