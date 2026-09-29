using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Auditoria over the DurabilidadesPorRank table (see "Tabela de Durabilidade por Rank.md") — the
/// 8 rows are fixed (one per RankDeItem value, seeded once) and never created/deleted here, only
/// edited. List (GET) stays open to any authenticated caller, same treatment as HistoricosController;
/// Update (PUT) is gated to the Rules Auditor, checked directly against the DB
/// (ApplicationUser.IsRulesAuditor), not a JWT claim.
/// </summary>
[ApiController]
[Route("api/durabilidades-por-rank")]
[Authorize]
public class DurabilidadesPorRankController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DurabilidadePorRankResponse>>> List()
    {
        var linhas = await db.DurabilidadesPorRank.ToListAsync();
        return linhas.OrderBy(l => l.Rank).Select(ToResponse).ToList();
    }

    [HttpPut("{rank}")]
    public async Task<IActionResult> Update(string rank, UpdateDurabilidadePorRankRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        // Enum.TryParse<RankDeItem> alone would also accept a numeric string like "3" (RankDeItem
        // is stored as int) — require the exact enum name instead.
        if (!Enum.GetNames<RankDeItem>().Contains(rank) || !Enum.TryParse<RankDeItem>(rank, out var parsedRank))
            return NotFound();

        var linha = await db.DurabilidadesPorRank.SingleOrDefaultAsync(l => l.Rank == parsedRank);
        if (linha is null)
            return NotFound();

        if (!request.Inquebravel && (request.Durabilidade is null || request.Durabilidade < 1))
            return BadRequest("Informe a durabilidade (mínimo 1) ou marque Inquebrável.");

        linha.Inquebravel = request.Inquebravel;
        linha.Durabilidade = request.Inquebravel ? null : request.Durabilidade;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static DurabilidadePorRankResponse ToResponse(RuinaRPG.Infrastructure.Items.DurabilidadePorRank l) =>
        new(l.Rank.ToString(), l.Durabilidade, l.Inquebravel);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
