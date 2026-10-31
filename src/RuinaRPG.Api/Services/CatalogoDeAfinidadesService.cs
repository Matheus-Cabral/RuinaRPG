using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>
/// O catálogo único de elementos de Afinidade do Auditor (Auditoria de Regras R0018–R0021), carregado uma vez por
/// requisição: elementos (pelo id), cruzamentos, total por Círculo e visibilidade por Vocação/Classe.
/// </summary>
public class CatalogoDeAfinidadesService(RuinaRpgDbContext db)
{
    private CatalogoDeAfinidades? _catalogo;

    public async Task<CatalogoDeAfinidades> CarregarAsync()
    {
        if (_catalogo is not null)
            return _catalogo;

        var elementos = await db.ElementosDeAfinidade.AsNoTracking().OrderBy(e => e.Id)
            .Select(e => new ElementoDoCatalogo(e.Id, e.Nome, e.Tipo, e.Teto)).ToListAsync();
        var cruzamentos = await db.CruzamentosDeAfinidade.AsNoTracking()
            .Select(c => new Cruzamento(c.ElementoAId, c.MinimoA, c.ElementoBId, c.MinimoB, c.SubElementoId)).ToListAsync();
        var totais = await db.TotaisDeAfinidadePorCirculo.AsNoTracking().ToDictionaryAsync(t => t.Circulo, t => t.Total);
        var visibilidade = await db.VisibilidadesDeElemento.AsNoTracking().ToListAsync();

        return _catalogo = new CatalogoDeAfinidades(elementos, cruzamentos, totais,
            visibilidade.Select(v => v.Origem == OrigemDaVisibilidade.Vocacao
                ? ((string?)v.Chave, (string?)null, v.ElementoId)
                : (null, v.Chave, v.ElementoId)).ToList());
    }
}
