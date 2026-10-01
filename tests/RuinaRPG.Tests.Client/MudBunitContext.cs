using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;
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

    /// <summary>
    /// Renders <typeparamref name="T"/> next to a MudPopoverProvider (a MudSelect's items only exist inside one,
    /// and only once opened) so a test can read the option labels via <see cref="OpenSelect"/>.
    /// </summary>
    protected IRenderedComponent<ContainerFragment> RenderWithPopover<T>(params (string Name, object? Value)[] parameters)
        where T : IComponent =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<T>(1);
            for (var i = 0; i < parameters.Length; i++)
                builder.AddAttribute(2 + i, parameters[i].Name, parameters[i].Value);
            builder.CloseComponent();
        });

    /// <summary>Opens the MudSelect with this label (same MouseDown its input listens for) and returns its option labels.</summary>
    protected static List<string> OpenSelect(IRenderedComponent<ContainerFragment> root, string label)
    {
        root.FindComponents<MudSelect<string>>().Single(c => c.Instance.Label == label).Find(".mud-input-control").MouseDown();
        return root.FindAll(".mud-list-item").Select(li => li.TextContent.Trim()).ToList();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
}
