namespace RuinaRPG.Infrastructure.Encounters;

/// <summary>
/// Fonte da ordem do log do encontro. O EF Core não garante que as linhas de um mesmo save sejam inseridas
/// na ordem em que foram adicionadas, então cada linha recebe aqui, no momento em que é criada, um valor
/// estritamente crescente. Limite: a garantia vale dentro de um único processo da API; entre reinícios
/// os valores continuam crescendo porque partem de <see cref="DateTime.UtcNow"/>.
/// </summary>
public static class EncounterLogOrdem
{
    private static readonly object Trava = new();
    private static long _ultima;

    public static long Proxima()
    {
        lock (Trava)
        {
            _ultima = Math.Max(_ultima + 1, DateTime.UtcNow.Ticks);
            return _ultima;
        }
    }
}
