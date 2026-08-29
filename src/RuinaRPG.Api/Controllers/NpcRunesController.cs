using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/runes")]
public class NpcRunesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NpcRuneResponse>> Add(Guid sheetId, AddNpcRuneRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var rune = new NpcRune { Id = Guid.NewGuid(), NpcSheetId = sheetId, Nome = request.Nome, Descricao = request.Descricao, Grau = request.Grau };
        db.NpcRunes.Add(rune);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(rune));
    }

    [HttpGet]
    public async Task<ActionResult<List<NpcRuneResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var runes = await db.NpcRunes.Where(r => r.NpcSheetId == sheetId).ToListAsync();
        return runes.Select(ToResponse).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var rune = await db.NpcRunes.FirstOrDefaultAsync(r => r.Id == id && r.NpcSheetId == sheetId);
        if (rune is null)
            return NotFound();

        db.NpcRunes.Remove(rune);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static NpcRuneResponse ToResponse(NpcRune r) =>
        new(r.Id.ToString(), r.Nome, r.Descricao, r.Grau);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
