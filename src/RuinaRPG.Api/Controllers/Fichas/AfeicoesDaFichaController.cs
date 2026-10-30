using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Afeições da ficha (criar, listar e apagar), para os três tipos. A rota fica na subclasse de cada tipo;
/// editar uma Afeição existente só o Personagem tem, na própria subclasse.
/// </summary>
public abstract class AfeicoesDaFichaController<TSheet, TLinha>(RuinaRpgDbContext db, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, ILinhaDaFicha
{
    protected RuinaRpgDbContext Db { get; } = db;

    protected DbSet<TLinha> Linhas => Db.Set<TLinha>();

    /// <summary>Uma Afeição nova do tipo (as entidades têm Nome required, então não servem para new()).</summary>
    protected abstract TLinha Nova(Guid sheetId, AddAffectionRequest request);

    protected abstract AffectionResponse Resposta(TLinha affection);

    [HttpPost("affections")]
    public async Task<ActionResult<AffectionResponse>> AddAffection(Guid sheetId, AddAffectionRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var affection = Nova(sheetId, request);
        Linhas.Add(affection);
        await Db.SaveChangesAsync();

        return Created(string.Empty, Resposta(affection));
    }

    [HttpGet("affections")]
    public async Task<ActionResult<List<AffectionResponse>>> ListAffections(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var affections = await Linhas.Where(a => a.SheetId == sheetId).ToListAsync();
        return affections.Select(Resposta).ToList();
    }

    [HttpDelete("affections/{id}")]
    public async Task<IActionResult> DeleteAffection(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var affection = await Linhas.FirstOrDefaultAsync(a => a.Id == id && a.SheetId == sheetId);
        if (affection is null)
            return NotFound();

        Linhas.Remove(affection);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
