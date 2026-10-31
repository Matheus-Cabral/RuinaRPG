using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace RuinaRPG.Tests.Client.Shared.AuditoriaDeAfinidades;

/// <summary>Base dos testes das seções: a API falsa registrada e a seção renderizada com os provedores de diálogo e popover.</summary>
public abstract class SecoesDeAfinidadesTestes : MudBunitContext
{
    protected ApiFalsaDeAfinidades Api { get; } = new();

    protected SecoesDeAfinidadesTestes() => Services.AddScoped(_ => Api.Cliente());

    protected async Task<IRenderedComponent<ContainerFragment>> RenderizarAsync<T>() where T : IComponent
    {
        var cut = Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudPopoverProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<T>(2);
            builder.CloseComponent();
        });
        await Task.Delay(50);
        return cut;
    }

    /// <summary>Dispara o ValueChanged de um campo, como o usuário ao confirmar um valor.</summary>
    protected static async Task DefinirAsync<TValor>(IRenderedComponent<ContainerFragment> cut, EventCallback<TValor> valueChanged, TValor valor)
    {
        await cut.InvokeAsync(() => valueChanged.InvokeAsync(valor));
        await Task.Delay(50);
    }

    protected static async Task ClicarAsync(IRenderedComponent<ContainerFragment> cut, string seletor)
    {
        cut.Find(seletor).Click();
        await Task.Delay(50);
    }

    protected static async Task ConfirmarAsync(IRenderedComponent<ContainerFragment> cut, string botao)
    {
        cut.FindAll(".mud-dialog-actions button").First(b => b.TextContent.Contains(botao)).Click();
        await Task.Delay(50);
    }

    /// <summary>O valor exibido em cada campo da coluna <paramref name="coluna"/> (1 = primeira) da tabela.</summary>
    protected static List<string?> Coluna(IRenderedComponent<ContainerFragment> cut, int coluna) =>
        cut.FindAll($"tbody tr td:nth-child({coluna}) input").Select(i => i.GetAttribute("value")).ToList();

    /// <summary>Abre o MudSelect com este rótulo e devolve os textos das opções.</summary>
    protected static List<string> Opcoes<TValor>(IRenderedComponent<ContainerFragment> cut, string label)
    {
        cut.FindComponents<MudSelect<TValor>>().Single(c => c.Instance.Label == label).Find(".mud-input-control").MouseDown();
        return cut.FindAll(".mud-list-item").Select(li => li.TextContent.Trim()).ToList();
    }
}
