using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/skills")]
public class CreatureSkillsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CreatureSkillResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var skills = await db.CreatureSkills.Where(s => s.CreatureSheetId == sheetId).OrderBy(s => s.Pericia).ToListAsync();

        var attributeTotals = await db.CreatureAttributes
            .Where(a => a.CreatureSheetId == sheetId)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria, artefatos: 0));

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto);
                var total = s.AtributoEscolhido is not null && attributeTotals.TryGetValue(s.AtributoEscolhido.Value, out var atributoTotal)
                    ? SkillFormulas.Total(modificador, atributoTotal)
                    : (int?)null;
                return new CreatureSkillResponse(s.Pericia.ToString(), s.Gasto, modificador, s.AtributoEscolhido?.ToString(), total);
            })
            .ToList();
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, Pericia pericia, UpdateCreatureSkillRequest request)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        // R0005's "lista fixa mais curta" — only the 20 allowed Pericia values have a seeded
        // CreatureSkill row. Checked before the SingleAsync below so a disallowed-but-real Pericia
        // (e.g. Alquimia) 400s instead of 500ing on a row that was never seeded.
        if (!CreatureSkillAllowList.IsAllowed(pericia))
            return BadRequest("Perícia fora da lista permitida para Criaturas.");

        var skill = await db.CreatureSkills.SingleAsync(s => s.CreatureSheetId == sheetId && s.Pericia == pericia);
        skill.Gasto = request.Gasto;
        skill.AtributoEscolhido = Enum.TryParse<AtributoCriatura>(request.AtributoEscolhido, out var parsedAtributo) ? parsedAtributo : null;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
