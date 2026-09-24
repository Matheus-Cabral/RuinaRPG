using MudBlazor;
using MudBlazor.Services;

namespace RuinaRPG.Tests.Client.Layout;

/// <summary>
/// A deterministic stand-in for MudBlazor's real <see cref="IBrowserViewportService"/>, which
/// normally talks to a JS ResizeObserver that bUnit's JSInterop (even in Loose mode) can't
/// meaningfully drive — Loose mode just returns default(BrowserWindowSize)/Breakpoint.Xs for every
/// unconfigured call, so the real service can never be told "this is a desktop viewport" from a
/// test.
///
/// Registering an instance of this fake (after MudBunitContext's own <c>AddMudServices()</c>, so it
/// wins DI resolution) lets a test declare <see cref="CurrentBreakpoint"/> up front. Every observer
/// that subscribes afterwards — MainLayout's own <c>MudBreakpointProvider</c> AND the MudDrawer it
/// renders both resolve the very same registered service, so one fake drives both.
/// </summary>
public sealed class FakeBrowserViewportService : IBrowserViewportService
{
    private readonly Dictionary<Guid, Func<BrowserViewportEventArgs, Task>> _subscribers = new();

    public Breakpoint CurrentBreakpoint { get; set; } = Breakpoint.Xl;

    public ResizeOptions ResizeOptions { get; } = new();

    public async Task SubscribeAsync(IBrowserViewportObserver observer, bool fireImmediately = true)
    {
        _subscribers[observer.Id] = observer.NotifyBrowserViewportChangeAsync;
        if (fireImmediately)
            await observer.NotifyBrowserViewportChangeAsync(MakeArgs(observer.Id, isImmediate: true));
    }

    public async Task SubscribeAsync(Guid observerId, Action<BrowserViewportEventArgs> lambda, ResizeOptions? options = null, bool fireImmediately = true)
    {
        Task Wrapped(BrowserViewportEventArgs args)
        {
            lambda(args);
            return Task.CompletedTask;
        }

        _subscribers[observerId] = Wrapped;
        if (fireImmediately)
            await Wrapped(MakeArgs(observerId, isImmediate: true));
    }

    public async Task SubscribeAsync(Guid observerId, Func<BrowserViewportEventArgs, Task> lambda, ResizeOptions? options = null, bool fireImmediately = true)
    {
        _subscribers[observerId] = lambda;
        if (fireImmediately)
            await lambda(MakeArgs(observerId, isImmediate: true));
    }

    public Task UnsubscribeAsync(IBrowserViewportObserver observer) => UnsubscribeAsync(observer.Id);

    public Task UnsubscribeAsync(Guid observerId)
    {
        _subscribers.Remove(observerId);
        return Task.CompletedTask;
    }

    public Task<bool> IsMediaQueryMatchAsync(string mediaQuery) => Task.FromResult(false);

    public Task<bool> IsBreakpointWithinWindowSizeAsync(Breakpoint breakpoint) => Task.FromResult(breakpoint <= CurrentBreakpoint);

    public Task<bool> IsBreakpointWithinReferenceSizeAsync(Breakpoint breakpoint, Breakpoint reference) => Task.FromResult(breakpoint <= reference);

    public Task<Breakpoint> GetCurrentBreakpointAsync() => Task.FromResult(CurrentBreakpoint);

    public Task<BrowserWindowSize> GetCurrentBrowserWindowSizeAsync() => Task.FromResult(new BrowserWindowSize());

    /// <summary>
    /// Simulates the browser window crossing into a new breakpoint: updates <see cref="CurrentBreakpoint"/>
    /// and notifies every currently-subscribed observer (mirrors a non-immediate, real resize event).
    /// </summary>
    public async Task RaiseBreakpointChangedAsync(Breakpoint breakpoint)
    {
        CurrentBreakpoint = breakpoint;
        foreach (var (id, notify) in _subscribers.ToArray())
            await notify(MakeArgs(id, isImmediate: false));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private BrowserViewportEventArgs MakeArgs(Guid id, bool isImmediate) =>
        new(id, new BrowserWindowSize(), CurrentBreakpoint, isImmediate);
}
