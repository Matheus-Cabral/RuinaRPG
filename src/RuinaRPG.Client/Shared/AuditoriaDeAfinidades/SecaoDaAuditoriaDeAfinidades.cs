using System.Net;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Services;
using RuinaRPG.Domain.Afinidades;

namespace RuinaRPG.Client.Shared.AuditoriaDeAfinidades;

/// <summary>
/// O que toda seção da Auditoria de Afinidades faz igual (Auditoria de Regras R0018–R0021): carregar, e depois de
/// cada escrita: 403 trava a seção com o aviso de sem permissão, outra falha mostra a mensagem do servidor e relê
/// (o valor recusado não fica na tela), sucesso relê. Uma escrita que muda o catálogo de elementos recarrega também
/// o <see cref="CatalogoDeElementos"/> da sessão, para os nomes não ficarem velhos no resto do app.
/// </summary>
public abstract class SecaoDaAuditoriaDeAfinidades : ComponentBase
{
    [Inject] protected HttpClient Http { get; set; } = default!;
    [Inject] protected CatalogoDeElementos Elementos { get; set; } = default!;

    protected string? MensagemDeErro { get; set; }
    protected bool SemPermissao { get; private set; }

    protected IReadOnlyList<ElementoDoCatalogo> Catalogo => Elementos.Catalogo.Elementos;

    protected override async Task OnInitializedAsync()
    {
        await Elementos.CarregarAsync();
        await CarregarAsync();
    }

    /// <summary>Lê o que a seção mostra além do catálogo de elementos (que já vem carregado).</summary>
    protected virtual Task CarregarAsync() => Task.CompletedTask;

    protected async Task<bool> ConcluirAsync(HttpResponseMessage response, bool mudouOCatalogo = false)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            SemPermissao = true;
            return false;
        }

        var ok = response.IsSuccessStatusCode;
        if (!ok)
        {
            var detalhe = await response.Content.ReadAsStringAsync();
            MensagemDeErro = string.IsNullOrWhiteSpace(detalhe) ? "Não foi possível salvar a alteração." : detalhe;
        }
        if (mudouOCatalogo)
            await Elementos.RecarregarAsync();
        await CarregarAsync();
        return ok;
    }

    protected static string Rotulo(TipoDeElemento tipo) => tipo switch
    {
        TipoDeElemento.Base => "Elemento base",
        TipoDeElemento.Caminho => "Caminho",
        _ => "Sub-Elemento",
    };
}
