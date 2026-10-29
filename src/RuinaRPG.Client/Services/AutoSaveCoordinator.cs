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
    // Quantos commits já chegaram: um recarregamento cuja leitura começou antes de um commit não aplica o que leu.
    private long _commits;
    // Recarregamentos adiados porque havia edição pendente; rodam quando o auto-save fica ocioso.
    private readonly List<Func<Task>> _recargasAdiadas = new();
    // Verdadeiro só dentro do delegate de salvamento em execução: o próprio salvamento não conta como "outra" pendência.
    private static readonly AsyncLocal<AutoSaveCoordinator?> _salvamentoEmExecucao = new();

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
        _commits++;
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

        if (_emAndamento == 0 && _recargasAdiadas.Count > 0)
            await ExecutarRecargasAdiadasAsync();
    }

    /// <summary>
    /// Relê o servidor e aplica o lido no formulário sem nunca sobrescrever um campo com edição pendente ou em envio
    /// (spec 2.1.0 A.4). Sem outra edição pendente, lê e aplica já (dentro de um salvamento, antes de ele terminar).
    /// Com outra edição pendente (em qualquer canal), ou se um commit chegar durante a leitura, nada é aplicado agora:
    /// o recarregamento roda de novo quando o auto-save ficar ocioso, e então o formulário mostra o servidor.
    /// <paramref name="aplicar"/> deve escrever no formulário antes do primeiro await.
    /// </summary>
    public async Task RecarregarAsync<T>(Func<Task<T>> ler, Func<T, Task> aplicar)
    {
        while (true)
        {
            if (HaOutraEdicaoPendente)
            {
                _recargasAdiadas.Add(() => RecarregarAsync(ler, aplicar));
                return;
            }

            var commitsAntesDaLeitura = _commits;
            var lido = await ler();
            if (commitsAntesDaLeitura == _commits && !HaOutraEdicaoPendente)
            {
                await aplicar(lido);
                return;
            }
        }
    }

    private bool HaOutraEdicaoPendente => _emAndamento - (_salvamentoEmExecucao.Value == this ? 1 : 0) > 0;

    private async Task ExecutarRecargasAdiadasAsync()
    {
        var recargas = _recargasAdiadas.ToList();
        _recargasAdiadas.Clear();
        foreach (var recarga in recargas)
        {
            try
            {
                await recarga();
            }
            catch
            {
                State = AutoSaveState.Error;
            }
        }
        StateChanged?.Invoke();
    }

    private async Task<bool> ExecutarSalvamentoAsync(Func<Task<bool>> validateAndSaveAsync)
    {
        // Um AsyncLocal mudado aqui só vale para este método e o que ele espera: quem chamou não vê a mudança.
        _salvamentoEmExecucao.Value = this;
        return await validateAndSaveAsync();
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
                var attempted = await ExecutarSalvamentoAsync(validateAndSaveAsync);
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
