using Bunit;
using MudBlazor.Services;
using Xunit;

namespace RuinaRPG.Tests.Client;

/// <summary>
/// Base class for any bUnit test that renders a MudBlazor component. Registers MudBlazor's DI
/// services and routes teardown through IAsyncLifetime, since MudBlazor registers at least one
/// DI service (PointerEventsNoneService) that only implements IAsyncDisposable — xUnit's default
/// synchronous IDisposable.Dispose() teardown can't dispose that cleanly otherwise.
/// </summary>
public abstract class MudBunitContext : BunitContext, IAsyncLifetime
{
    protected MudBunitContext()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
}
