namespace RuinaRPG.Client.Services;

public enum AutoSaveState { Idle, Saving, Saved, Error }

/// <summary>
/// Debounces a burst of field-commit events into one save call, 400ms after the last one. One
/// instance per page/form — not a DI service. See docs/superpowers/archive/specs/2026-09-04-autosave-on-blur-design.md.
/// </summary>
public class AutoSaveCoordinator
{
    private const int DebounceMilliseconds = 400;
    private readonly Dictionary<string, CancellationTokenSource> _debounceCts = new();
    private Task _inFlight = Task.CompletedTask;
    private int _emAndamento;

    /// <summary>True from a field's commit until its save has finished (or was superseded): a reload must not run in between.</summary>
    public bool Ocupado => _emAndamento > 0;

    public AutoSaveState State { get; private set; } = AutoSaveState.Idle;
    public DateTime? LastSavedAt { get; private set; }
    public event Action? StateChanged;

    /// <summary>
    /// Call from every in-scope field's commit (blur, or selection change for non-text controls).
    /// Resets the debounce window of <paramref name="canal"/>; the delegate only actually runs once no
    /// further call arrives on that channel within DebounceMilliseconds. Fields that save through
    /// their own endpoint (Pontos de Ignição, Nível, Experiência) use their own channel, so a commit
    /// of another field right after them does not cancel their save. Saves of every channel still run
    /// one at a time.
    /// </summary>
    public void NotifyChanged(Func<Task<bool>> validateAndSaveAsync, string canal = "geral")
    {
        if (_debounceCts.TryGetValue(canal, out var pendente))
            pendente.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts[canal] = cts;
        _emAndamento++;
        _ = DebounceAndSaveAsync(validateAndSaveAsync, cts.Token);
    }

    private async Task DebounceAndSaveAsync(Func<Task<bool>> validateAndSaveAsync, CancellationToken token)
    {
        try
        {
            await DebounceAndSaveCoreAsync(validateAndSaveAsync, token);
        }
        finally
        {
            _emAndamento--;
        }
    }

    private async Task DebounceAndSaveCoreAsync(Func<Task<bool>> validateAndSaveAsync, CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceMilliseconds, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        // Serialize saves: wait for any prior save still in flight to finish before this one
        // starts, so an older, slower save can never complete after (and overwrite) a newer one.
        var previous = _inFlight;
        var tcs = new TaskCompletionSource();
        _inFlight = tcs.Task;
        try
        {
            await previous;

            if (token.IsCancellationRequested)
                return;

            State = AutoSaveState.Saving;
            StateChanged?.Invoke();

            try
            {
                var attempted = await validateAndSaveAsync();
                if (attempted)
                {
                    State = AutoSaveState.Saved;
                    LastSavedAt = DateTime.Now;
                }
                else
                {
                    State = AutoSaveState.Idle;
                }
            }
            catch
            {
                State = AutoSaveState.Error;
            }

            StateChanged?.Invoke();
        }
        finally
        {
            tcs.SetResult();
        }
    }
}
