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
}
