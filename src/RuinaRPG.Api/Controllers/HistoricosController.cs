using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Historico.md's static, GM-independent catalog (seeded once, shared by every GM/Jogador) — no
/// owning GmId, same treatment as TraitsController. List (GET) stays open to any authenticated
/// caller. Create/Update/Delete are gated to the Rules Auditor (see Requisitos - Auditoria de
/// Regras), checked directly against the DB (ApplicationUser.IsRulesAuditor), not a JWT claim.
/// </summary>
[ApiController]
[Route("api/historicos")]
[Authorize]
public class HistoricosController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HistoricoResponse>>> List()
    {
        var historicos = await db.Historicos.Where(h => !h.IsDeleted).OrderBy(h => h.Nome).ToListAsync();
        return historicos.Select(ToResponse).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<HistoricoResponse>> Create(CreateHistoricoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<Pericia>(request.PericiaMaisSeis, out var periciaMaisSeis))
            return BadRequest("PericiaMaisSeis inválida.");
        if (!Enum.TryParse<Pericia>(request.PericiaMaisTres, out var periciaMaisTres))
            return BadRequest("PericiaMaisTres inválida.");
        if (periciaMaisSeis == periciaMaisTres)
            return BadRequest("PericiaMaisSeis e PericiaMaisTres não podem ser a mesma Perícia.");

        var historico = new Historico
        {
            Id = Guid.NewGuid(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            PericiaMaisSeis = periciaMaisSeis,
            PericiaMaisTres = periciaMaisTres,
            IsCustomized = true,
            UpdatedByUserId = CurrentUserId(),
            UpdatedAt = DateTime.UtcNow,
        };
        db.Historicos.Add(historico);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(historico));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateHistoricoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var historico = await db.Historicos.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (historico is null)
            return NotFound();

        if (!Enum.TryParse<Pericia>(request.PericiaMaisSeis, out var periciaMaisSeis))
            return BadRequest("PericiaMaisSeis inválida.");
        if (!Enum.TryParse<Pericia>(request.PericiaMaisTres, out var periciaMaisTres))
            return BadRequest("PericiaMaisTres inválida.");
        if (periciaMaisSeis == periciaMaisTres)
            return BadRequest("PericiaMaisSeis e PericiaMaisTres não podem ser a mesma Perícia.");

        historico.Nome = request.Nome;
        historico.Descricao = request.Descricao;
        historico.PericiaMaisSeis = periciaMaisSeis;
        historico.PericiaMaisTres = periciaMaisTres;
        historico.IsCustomized = true;
        historico.UpdatedByUserId = CurrentUserId();
        historico.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var historico = await db.Historicos.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (historico is null)
            return NotFound();

        var inUse = await db.CharacterSheets.AnyAsync(s => s.HistoricoId == id)
            || await db.NpcSheets.AnyAsync(s => s.HistoricoId == id);
        if (inUse)
            return Conflict("Este Histórico está em uso em pelo menos uma ficha e não pode ser excluído.");

        historico.IsDeleted = true;
        historico.UpdatedByUserId = CurrentUserId();
        historico.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static HistoricoResponse ToResponse(Historico h) =>
        new(h.Id.ToString(), h.Nome, h.Descricao, h.PericiaMaisSeis.ToString(), h.PericiaMaisTres.ToString(), h.IsCustomized);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
