namespace RuinaRPG.Client.Services;

/// <summary>
/// Recarga de uma ficha disparada de fora (Condições mudaram no servidor). Se o usuário está digitando num
/// campo, a recarga espera: roda uma vez, quando o foco sai e nenhum autosave está pendente, para nunca
/// trocar o valor do campo que ele edita nem um valor ainda não salvo. Uma instância por página.
/// </summary>
/// <param name="recarregar">Relê os dados da ficha. Não deve disparar autosave.</param>
/// <param name="campoEmEdicao">Há um campo de texto com o foco agora.</param>
/// <param name="salvando">Há um autosave pendente ou em andamento.</param>
/// <param name="espera">Intervalo entre as checagens depois que o foco sai (o foco passa por outro campo antes de assentar).</param>
public sealed class RecargaAdiada(Func<Task> recarregar, Func<Task<bool>> campoEmEdicao, Func<bool> salvando, TimeSpan? espera = null)
{
    private readonly TimeSpan _espera = espera ?? TimeSpan.FromMilliseconds(300);
    private bool _pendente;

    public async Task SolicitarAsync()
    {
        if (await campoEmEdicao())
        {
            _pendente = true;
            return;
        }
        await recarregar();
    }

    /// <summary>Chamar quando o foco sai de um campo da página: roda a recarga que ficou pendente.</summary>
    public async Task FocoSaiuAsync()
    {
        if (!_pendente)
            return;

        await Task.Delay(_espera);
        while (_pendente && (salvando() || await campoEmEdicao()))
            await Task.Delay(_espera);
        if (!_pendente)
            return;

        _pendente = false;
        await recarregar();
    }
}
