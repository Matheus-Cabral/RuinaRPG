using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules.Niveis;

public interface ITabelaDeNiveis
{
    Task<ProgressaoDeNivel> ObterAsync();
}

/// <summary>Carrega a Tabela de Níveis (ignorando colunas excluídas) uma vez por requisição.</summary>
public class TabelaDeNiveis(RuinaRpgDbContext db) : ITabelaDeNiveis
{
    private ProgressaoDeNivel? _cache;

    public async Task<ProgressaoDeNivel> ObterAsync()
    {
        if (_cache is not null) return _cache;

        var colunas = await db.ColunasDeNivel.AsNoTracking().Where(c => !c.IsDeleted).ToListAsync();
        var ids = colunas.Select(c => c.Id).ToHashSet();
        var niveis = await db.NiveisProgressao.AsNoTracking().ToListAsync();
        var valores = (await db.ValoresDeNivel.AsNoTracking().ToListAsync())
            .Where(v => ids.Contains(v.ColunaId)).ToLookup(v => v.Nivel);

        var linhas = niveis.Select(n => new LinhaDeNivel(n.Nivel, n.OutrosBonus,
            valores[n.Nivel].ToDictionary(v => v.ColunaId, v => v.Valor))).ToList();
        var defs = colunas.Select(c => new ColunaDeNivelDef(c.Id, c.Nome, c.Tipo, c.ChaveDeSistema, c.Ordem)).ToList();

        return _cache = new ProgressaoDeNivel(defs, linhas);
    }
}
