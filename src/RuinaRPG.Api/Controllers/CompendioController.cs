using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/compendio")]
[Authorize]
public class CompendioController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<List<CompendioSearchResultResponse>>> Search(
        [FromQuery] string? q,
        [FromQuery] List<CompendioCategoria>? categorias)
    {
        var traits = await db.Traits
            .Select(t => new TraitSeed(t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString()))
            .ToListAsync();

        // Model binding gives an empty (not null) List<T> when the query string omits
        // "categorias" entirely, so an unfiltered request must normalize that back to null -
        // otherwise CompendioSearchService's "categorias is null" no-filter check never
        // triggers and every category is (wrongly) excluded.
        var categoriaFiltro = categorias is { Count: > 0 } ? categorias : null;

        var results = CompendioSearchService.Search(q, categoriaFiltro, traits, rules);

        return results
            .Select(r => new CompendioSearchResultResponse(r.Categoria.ToString(), r.Origem, r.Titulo, r.Conteudo))
            .ToList();
    }
}
