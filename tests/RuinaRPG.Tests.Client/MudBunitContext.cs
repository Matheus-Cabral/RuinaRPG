using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using RuinaRPG.Client.Services;
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
        // Same registration as the app's Program.cs: the fichas, CatalogoItemForm, AuditoriaHistoricos,
        // HistoricoSelect and RequisitosDePassivaEditor inject it. It resolves the HttpClient each test
        // registers, so a test whose page needs Perícia names answers GET pericias in its fake handler.
        Services.AddScoped<PericiaCatalogo>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
}
