using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/skills")]
public class NpcSkillsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NpcSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var skills = await db.NpcSkills.Where(s => s.NpcSheetId == sheetId).OrderBy(s => s.Pericia).ToListAsync();

        var attributeTotals = await db.NpcAttributes
            .Where(a => a.NpcSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria, artefatos: 0));

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto);
                var total = s.AtributoEscolhido is not null && attributeTotals.TryGetValue(s.AtributoEscolhido.Value, out var atributoTotal)
                    ? SkillFormulas.Total(modificador, atributoTotal)
                    : (int?)null;
                return new NpcSkillResponse(s.Pericia.ToString(), s.Gasto, modificador, s.AtributoEscolhido?.ToString(), total);
            })
            .ToList();
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, Pericia pericia, UpdateNpcSkillRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var skill = await db.NpcSkills.SingleAsync(s => s.NpcSheetId == sheetId && s.Pericia == pericia);
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<Atributo>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
