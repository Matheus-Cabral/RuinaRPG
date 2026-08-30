using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Características.md's static, GM-independent catalog (seeded once, shared by every GM/Jogador)
/// — unlike Item/SpellAbilityBankEntry, Traits have no owning GmId at all. Existed only as raw
/// data behind CompendioController's search until now; this is the first endpoint that lets a
/// caller resolve a Trait's real Id (needed to add one to a sheet via TraitId, not just read its
/// name/description in the Compêndio).
/// </summary>
[ApiController]
[Route("api/traits")]
[Authorize]
public class TraitsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TraitResponse>>> List([FromQuery] string? nome)
    {
        var query = db.Traits.AsQueryable();
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{nome}%"));

        return await query
            .Select(t => new TraitResponse(t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString()))
            .ToListAsync();
    }
}
