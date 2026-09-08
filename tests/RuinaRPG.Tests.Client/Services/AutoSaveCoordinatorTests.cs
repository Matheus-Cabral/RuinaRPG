using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class AutoSaveCoordinatorTests
{
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
