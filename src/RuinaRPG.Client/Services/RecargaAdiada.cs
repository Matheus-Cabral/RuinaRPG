namespace RuinaRPG.Client.Services;

/// <summary>
/// Recarga de uma ficha disparada de fora (Condições mudaram no servidor). Roda na hora quando o usuário não
/// está digitando num campo e nenhum autosave está pendente ou em andamento; senão espera, para nunca trocar o
/// valor do campo editado nem um valor ainda não salvo (o autosave lê o formulário quando roda, então uma recarga
/// antes dele gravaria o valor velho do servidor). A espera confere de tempos em tempos (e quando o foco sai de
/// um campo). Só a espera por um autosave tem limite: passado ele (sem campo de texto em foco) a recarga roda de
/// qualquer jeito; quem digita nunca é interrompido. Uma instância por página; <see cref="Dispose"/> cancela a espera.
/// </summary>
/// <param name="recarregar">Relê os dados da ficha. Não deve disparar autosave.</param>
/// <param name="campoEmEdicao">Há um campo de texto com o foco agora. Se falhar (JS ausente), conta como "não".</param>
/// <param name="salvando">Há um autosave pendente ou em andamento.</param>
/// <param name="espera">Intervalo entre as conferências durante a espera.</param>
/// <param name="limite">Tempo máximo de espera por um autosave (não vale para um campo em edição); padrão 10 s.</param>
/// <param name="aoFalhar">Recebe o erro de uma recarga adiada (não há quem espere por ela).</param>
public sealed class RecargaAdiada(Func<Task> recarregar, Func<Task<bool>> campoEmEdicao, Func<bool> salvando,
    TimeSpan? espera = null, TimeSpan? limite = null, Action<Exception>? aoFalhar = null) : IDisposable
{
    private readonly TimeSpan _espera = espera ?? TimeSpan.FromSeconds(1);
    private readonly TimeSpan _limite = limite ?? TimeSpan.FromSeconds(10);
    private readonly CancellationTokenSource _cancelamento = new();
    private TaskCompletionSource _acordar = new();
    private bool _esperando;

    /// <summary>Recarrega agora, ou adia e volta já; várias solicitações durante a espera viram uma recarga só.</summary>
    public async Task SolicitarAsync()
    {
        if (_esperando)
            return;
        if (!await OcupadoAsync())
        {
            await recarregar();
            return;
        }

        _esperando = true;
        _ = EsperarERecarregarAsync();
    }

    /// <summary>Chamar quando o foco sai de um campo da página: confere sem esperar o próximo intervalo.</summary>
    public void FocoSaiu() => _acordar.TrySetResult();

    private async Task<bool> OcupadoAsync()
    {
        return await EditandoAsync() || salvando();
    }

    private async Task<bool> EditandoAsync()
    {
        try
        {
            return await campoEmEdicao();
        }
        catch (Exception)
        {
            return false; // sem como saber (JS ausente ou desconectado): não segura a recarga
        }
    }

    private async Task EsperarERecarregarAsync()
    {
        try
        {
            DateTime? esperandoSalvar = null;
            while (!_cancelamento.IsCancellationRequested)
            {
                var acordar = _acordar.Task;
                await Task.WhenAny(Task.Delay(_espera, _cancelamento.Token), acordar);
                _acordar = new TaskCompletionSource();
                if (_cancelamento.IsCancellationRequested)
                    return;
                // Campo de texto com foco: alguém está digitando, espera sem limite (perder o texto é pior que um total velho).
                if (await EditandoAsync())
                {
                    esperandoSalvar = null;
                    continue;
                }
                // Só o autosave tem prazo: um salvamento preso não pode segurar a recarga para sempre.
                if (salvando())
                {
                    esperandoSalvar ??= DateTime.UtcNow;
                    if (DateTime.UtcNow - esperandoSalvar < _limite)
                        continue;
                }
                break;
            }

            _esperando = false;
            if (!_cancelamento.IsCancellationRequested)
                await recarregar();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            aoFalhar?.Invoke(ex);
        }
        finally
        {
            _esperando = false;
        }
    }

    public void Dispose() => _cancelamento.Cancel();
}
