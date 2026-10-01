using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public interface IPericiaCatalogo
{
    /// <summary>Todas as linhas, inclusive as removidas.</summary>
    Task<IReadOnlyList<PericiaDefinicao>> TodasAsync();

    /// <summary>Nulo se a chave não existe ou a Perícia foi removida.</summary>
    Task<PericiaDefinicao?> AtivaPorChaveAsync(string chave);

    /// <summary>Todas as linhas, inclusive as removidas, por Id.</summary>
    Task<IReadOnlyDictionary<int, PericiaDefinicao>> PorIdAsync();
}

/// <summary>Perícias carregadas uma vez por requisição (scoped) — a tabela tem ~40 linhas.</summary>
public class PericiaCatalogo(RuinaRpgDbContext db) : IPericiaCatalogo
{
    private List<PericiaDefinicao>? _todas;
    private Dictionary<int, PericiaDefinicao>? _porId;

    public async Task<IReadOnlyList<PericiaDefinicao>> TodasAsync() =>
        _todas ??= await db.Pericias.AsNoTracking().OrderBy(p => p.Id).ToListAsync();

    public async Task<PericiaDefinicao?> AtivaPorChaveAsync(string chave) =>
        (await TodasAsync()).FirstOrDefault(p => !p.IsDeleted && p.Chave == chave);

    public async Task<IReadOnlyDictionary<int, PericiaDefinicao>> PorIdAsync() =>
        _porId ??= (await TodasAsync()).ToDictionary(p => p.Id);
}
