using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

[Collection("Tempo real")]
public class AutoSaveCoordinatorTests
{
    [Fact]
    public async Task Ocupado_is_true_from_the_commit_until_the_save_finishes()
    {
        var coordinator = new AutoSaveCoordinator();
        var release = new TaskCompletionSource<bool>();

        Assert.False(coordinator.Ocupado);
        coordinator.NotifyChanged(() => release.Task);
        Assert.True(coordinator.Ocupado);

        await Task.Delay(700);
        Assert.True(coordinator.Ocupado); // the save is in flight

        release.SetResult(true);
        await Task.Delay(100);
        Assert.False(coordinator.Ocupado);
    }

    [Fact]
    public async Task Ocupado_goes_back_to_false_when_a_newer_commit_cancels_the_pending_one()
    {
        var coordinator = new AutoSaveCoordinator();

        coordinator.NotifyChanged(() => Task.FromResult(true));
        coordinator.NotifyChanged(() => Task.FromResult(true));
        await Task.Delay(800);

        Assert.False(coordinator.Ocupado);
    }

    [Fact]
    public async Task Rapid_repeated_calls_collapse_into_a_single_save_after_the_debounce_window()
    {
        var coordinator = new AutoSaveCoordinator();
        var callCount = 0;
        Task<bool> Save() { callCount++; return Task.FromResult(true); }

        coordinator.NotifyChanged(Save);
        await Task.Delay(100);
        coordinator.NotifyChanged(Save);
        await Task.Delay(100);
        coordinator.NotifyChanged(Save);

        await Task.Delay(700);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task A_delegate_returning_false_leaves_state_Idle_and_does_not_set_LastSavedAt()
    {
        var coordinator = new AutoSaveCoordinator();

        coordinator.NotifyChanged(() => Task.FromResult(false));
        await Task.Delay(700);

        Assert.Equal(AutoSaveState.Idle, coordinator.State);
        Assert.Null(coordinator.LastSavedAt);
    }

    [Fact]
    public async Task A_delegate_that_throws_sets_state_Error()
    {
        var coordinator = new AutoSaveCoordinator();

        coordinator.NotifyChanged(() => throw new InvalidOperationException("boom"));
        await Task.Delay(700);

        Assert.Equal(AutoSaveState.Error, coordinator.State);
    }

    [Fact]
    public async Task A_delegate_returning_true_sets_state_Saved_and_LastSavedAt()
    {
        var coordinator = new AutoSaveCoordinator();
        var before = DateTime.Now;

        coordinator.NotifyChanged(() => Task.FromResult(true));
        await Task.Delay(700);

        Assert.Equal(AutoSaveState.Saved, coordinator.State);
        Assert.NotNull(coordinator.LastSavedAt);
        Assert.True(coordinator.LastSavedAt >= before);
    }

    [Fact]
    public async Task StateChanged_fires_at_least_once_as_the_save_completes()
    {
        var coordinator = new AutoSaveCoordinator();
        var fired = 0;
        coordinator.StateChanged += () => fired++;

        coordinator.NotifyChanged(() => Task.FromResult(true));
        await Task.Delay(700);

        Assert.True(fired > 0);
    }

    [Fact]
    public async Task A_slow_save_still_in_flight_never_lets_an_older_result_overwrite_a_newer_completed_save()
    {
        var coordinator = new AutoSaveCoordinator();
        var order = new List<string>();
        var aStarted = new TaskCompletionSource();
        var aGate = new TaskCompletionSource();

        async Task<bool> SaveA()
        {
            aStarted.SetResult();
            await aGate.Task; // stays "in flight" until the test releases it
            lock (order) order.Add("A");
            return true;
        }

        Task<bool> SaveB()
        {
            lock (order) order.Add("B");
            return Task.FromResult(true);
        }

        // Trigger save A and wait for its debounce to elapse and its delegate to actually start
        // running (i.e. it is now "in flight" issuing its network call).
        coordinator.NotifyChanged(SaveA);
        await aStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // While A is still in flight, a second field blurs, triggering save B. Give B's own
        // debounce window plenty of time to elapse.
        coordinator.NotifyChanged(SaveB);
        await Task.Delay(700);

        // B must NOT have run yet: A is still in flight and saves must serialize, not race.
        lock (order) Assert.Empty(order);

        // Now let A finish.
        aGate.SetResult();

        // B's save should now run, strictly after A's.
        await WaitUntilAsync(() =>
        {
            lock (order) return order.Count == 2;
        }, TimeSpan.FromSeconds(5));

        lock (order) Assert.Equal(new[] { "A", "B" }, order);
        Assert.Equal(AutoSaveState.Saved, coordinator.State);
    }

    [Fact]
    public async Task Saves_on_different_channels_do_not_cancel_each_other()
    {
        var coordinator = new AutoSaveCoordinator();
        var ran = new List<string>();

        coordinator.NotifyChanged(() => { lock (ran) ran.Add("geral"); return Task.FromResult(true); });
        coordinator.NotifyChanged(() => { lock (ran) ran.Add("pi"); return Task.FromResult(true); }, "pi");
        await Task.Delay(700);

        lock (ran) Assert.Equal(new[] { "geral", "pi" }, ran.OrderBy(x => x));
    }

    [Fact]
    public async Task A_second_call_on_the_same_channel_still_replaces_the_first()
    {
        var coordinator = new AutoSaveCoordinator();
        var ran = new List<string>();

        coordinator.NotifyChanged(() => { lock (ran) ran.Add("pi-1"); return Task.FromResult(true); }, "pi");
        coordinator.NotifyChanged(() => { lock (ran) ran.Add("pi-2"); return Task.FromResult(true); }, "pi");
        await Task.Delay(700);

        lock (ran) Assert.Equal(new[] { "pi-2" }, ran);
    }

    [Fact]
    public async Task Saves_of_different_channels_never_overlap()
    {
        var coordinator = new AutoSaveCoordinator();
        var running = 0; var maxRunning = 0;
        async Task<bool> Save() { maxRunning = Math.Max(maxRunning, ++running); await Task.Delay(100); running--; return true; }

        coordinator.NotifyChanged(Save);
        coordinator.NotifyChanged(Save, "pi");
        await Task.Delay(900);

        Assert.Equal(1, maxRunning);
    }

    // Spec 2.1.0 A.4: um recarregamento nunca sobrescreve um campo com edição pendente ou em envio, em qualquer canal.
    [Fact]
    public async Task Um_recarregamento_pedido_com_outra_edicao_pendente_espera_ela_terminar()
    {
        var coordinator = new AutoSaveCoordinator();
        var eventos = new List<string>();
        void Anotar(string e) { lock (eventos) eventos.Add(e); }

        coordinator.NotifyChanged(async () =>
        {
            Anotar("salva geral");
            await coordinator.RecarregarAsync(() => { Anotar("lê"); return Task.FromResult(1); }, _ => { Anotar("aplica"); return Task.CompletedTask; });
            return true;
        });
        await Task.Delay(50);
        coordinator.NotifyChanged(() => { Anotar("salva pi"); return Task.FromResult(true); }, "pi");

        await WaitUntilAsync(() => { lock (eventos) return eventos.Contains("aplica"); }, TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        lock (eventos) Assert.Equal(new[] { "salva geral", "salva pi", "lê", "aplica" }, eventos);
        Assert.False(coordinator.Ocupado);
    }

    [Fact]
    public async Task Um_recarregamento_sem_edicao_pendente_aplica_antes_de_o_salvamento_terminar()
    {
        var coordinator = new AutoSaveCoordinator();
        var aplicado = false;
        var aplicadoAntesDoFim = false;

        coordinator.NotifyChanged(async () =>
        {
            await coordinator.RecarregarAsync(() => Task.FromResult(1), _ => { aplicado = true; return Task.CompletedTask; });
            aplicadoAntesDoFim = aplicado;
            return true;
        });
        await WaitUntilAsync(() => coordinator.State == AutoSaveState.Saved, TimeSpan.FromSeconds(3));

        Assert.True(aplicadoAntesDoFim);
    }

    [Fact]
    public async Task Uma_edicao_que_chega_durante_a_leitura_descarta_o_que_foi_lido_e_o_recarregamento_se_repete_depois()
    {
        var coordinator = new AutoSaveCoordinator();
        var leituras = 0;
        var primeiraLeitura = new TaskCompletionSource<int>();
        var aplicados = new List<int>();

        var recarga = coordinator.RecarregarAsync(
            () => ++leituras == 1 ? primeiraLeitura.Task : Task.FromResult(2),
            valor => { lock (aplicados) aplicados.Add(valor); return Task.CompletedTask; });
        coordinator.NotifyChanged(() => Task.FromResult(true));
        primeiraLeitura.SetResult(1);
        await recarga;

        lock (aplicados) Assert.Empty(aplicados); // o lido antes da edição não sobrescreve o campo editado
        await WaitUntilAsync(() => { lock (aplicados) return aplicados.Count > 0; }, TimeSpan.FromSeconds(3));
        lock (aplicados) Assert.Equal(new[] { 2 }, aplicados);
    }

    [Fact]
    public async Task Ocupado_continua_verdadeiro_ate_as_recargas_adiadas_terminarem()
    {
        var coordinator = new AutoSaveCoordinator();
        var leitura = new TaskCompletionSource<int>();
        var lendo = false;
        var aplicado = false;

        coordinator.NotifyChanged(async () =>
        {
            await coordinator.RecarregarAsync(() => { lendo = true; return leitura.Task; }, _ => { aplicado = true; return Task.CompletedTask; });
            return true;
        });
        await Task.Delay(50);
        coordinator.NotifyChanged(() => Task.FromResult(true), "pi");

        await WaitUntilAsync(() => lendo, TimeSpan.FromSeconds(3));
        Assert.True(coordinator.Ocupado); // os salvamentos terminaram, mas a releitura adiada ainda corre

        leitura.SetResult(1);
        await WaitUntilAsync(() => !coordinator.Ocupado, TimeSpan.FromSeconds(3));
        Assert.True(aplicado);
    }

    [Fact]
    public async Task Recargas_adiadas_iguais_numa_rajada_viram_uma_so_leitura()
    {
        var coordinator = new AutoSaveCoordinator();
        var leituras = 0;
        Task<int> Ler() { leituras++; return Task.FromResult(1); }
        Task Aplicar(int _) => Task.CompletedTask;

        coordinator.NotifyChanged(async () =>
        {
            await coordinator.RecarregarAsync(Ler, Aplicar);
            await coordinator.RecarregarAsync(Ler, Aplicar);
            await coordinator.RecarregarAsync(Ler, Aplicar);
            return true;
        });
        await Task.Delay(50);
        coordinator.NotifyChanged(() => Task.FromResult(true), "pi");

        await WaitUntilAsync(() => leituras > 0 && !coordinator.Ocupado, TimeSpan.FromSeconds(3));
        await Task.Delay(100);

        Assert.Equal(1, leituras);
    }

    [Fact]
    public async Task Recargas_adiadas_diferentes_continuam_todas_a_rodar()
    {
        var coordinator = new AutoSaveCoordinator();
        var leituras = 0;
        Task<int> LerA() { leituras++; return Task.FromResult(1); }
        Task<int> LerB() { leituras++; return Task.FromResult(2); }
        Task Aplicar(int _) => Task.CompletedTask;

        coordinator.NotifyChanged(async () =>
        {
            await coordinator.RecarregarAsync(LerA, Aplicar);
            await coordinator.RecarregarAsync(LerB, Aplicar);
            return true;
        });
        await Task.Delay(50);
        coordinator.NotifyChanged(() => Task.FromResult(true), "pi");

        await WaitUntilAsync(() => leituras >= 2 && !coordinator.Ocupado, TimeSpan.FromSeconds(3));

        Assert.Equal(2, leituras);
    }

    [Fact]
    public async Task Uma_recarga_adiada_que_falha_poe_o_estado_em_Error_e_libera_o_Ocupado()
    {
        var coordinator = new AutoSaveCoordinator();

        coordinator.NotifyChanged(async () =>
        {
            await coordinator.RecarregarAsync<int>(() => throw new InvalidOperationException("boom"), _ => Task.CompletedTask);
            return true;
        });
        await Task.Delay(50);
        coordinator.NotifyChanged(() => Task.FromResult(true), "pi");

        await WaitUntilAsync(() => coordinator.State == AutoSaveState.Error, TimeSpan.FromSeconds(3));
        await WaitUntilAsync(() => !coordinator.Ocupado, TimeSpan.FromSeconds(3));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met within the timeout.");
            await Task.Delay(20);
        }
    }
}
