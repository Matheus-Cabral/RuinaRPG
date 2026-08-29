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
    public async Task<ActionResult<List<NpcSkillResponse>>> List(Guid sheetId, [FromQuery] string? atributoEscolhido)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var skills = await db.NpcSkills.Where(s => s.NpcSheetId == sheetId).ToListAsync();

        int? atributoTotal = null;
        if (Enum.TryParse<Atributo>(atributoEscolhido, out var parsedAtributo))
        {
            var attribute = await db.NpcAttributes.SingleOrDefaultAsync(a => a.NpcSheetId == sheetId && a.Atributo == parsedAtributo);
            if (attribute is not null)
                atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        }

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto);
                var total = atributoTotal is not null ? SkillFormulas.Total(modificador, atributoTotal.Value) : (int?)null;
                return new NpcSkillResponse(s.Pericia.ToString(), s.Gasto, modificador, atributoEscolhido, total);
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
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
