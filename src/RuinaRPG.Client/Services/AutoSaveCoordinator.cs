namespace RuinaRPG.Client.Services;

public enum AutoSaveState { Idle, Saving, Saved, Error }

/// <summary>
/// Debounces a burst of field-commit events into one save call, 400ms after the last one. One
/// instance per page/form — not a DI service. See docs/superpowers/specs/2026-09-04-autosave-on-blur-design.md.
/// </summary>
public class AutoSaveCoordinator
{
    private const int DebounceMilliseconds = 400;
    private CancellationTokenSource? _debounceCts;

    public AutoSaveState State { get; private set; } = AutoSaveState.Idle;
    public DateTime? LastSavedAt { get; private set; }
    public event Action? StateChanged;

    /// <summary>
    /// Call from every in-scope field's commit (blur, or selection change for non-text controls).
    /// Resets the debounce window; the delegate only actually runs once no further call arrives
    /// within DebounceMilliseconds.
    /// </summary>
    public void NotifyChanged(Func<Task<bool>> validateAndSaveAsync)
    {
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = DebounceAndSaveAsync(validateAndSaveAsync, cts.Token);
    }

    private async Task DebounceAndSaveAsync(Func<Task<bool>> validateAndSaveAsync, CancellationToken token)
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
}
