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
[Route("api/npc-sheets/{sheetId}/attributes")]
public class NpcAttributesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NpcAttributeResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var attributes = await db.NpcAttributes.Where(a => a.NpcSheetId == sheetId).ToListAsync();
        return attributes
            .Select(a => new NpcAttributeResponse(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria, artefatos: 0)))
            .ToList();
    }

    [HttpPut("{atributo}")]
    public async Task<IActionResult> Update(Guid sheetId, Atributo atributo, UpdateNpcAttributeRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        attribute.Gasto = request.Gasto;
        attribute.Bonus = request.Bonus;
        attribute.TemMaestria = request.TemMaestria;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
