using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/runes")]
public class CharacterRunesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterRuneResponse>> Add(Guid sheetId, AddCharacterRuneRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var rune = new CharacterRune { Id = Guid.NewGuid(), CharacterSheetId = sheetId, Nome = request.Nome, Descricao = request.Descricao, Grau = request.Grau };
        db.CharacterRunes.Add(rune);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(rune));
    }

    [HttpGet]
    public async Task<ActionResult<List<CharacterRuneResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var runes = await db.CharacterRunes.Where(r => r.CharacterSheetId == sheetId).ToListAsync();
        return runes.Select(ToResponse).ToList();
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CharacterRuneResponse>> Update(Guid sheetId, Guid id, UpdateCharacterRuneRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var rune = await db.CharacterRunes.FirstOrDefaultAsync(r => r.Id == id && r.CharacterSheetId == sheetId);
        if (rune is null)
            return NotFound();

        rune.Nome = request.Nome;
        rune.Descricao = request.Descricao;
        rune.Grau = request.Grau;
        await db.SaveChangesAsync();

        return Ok(ToResponse(rune));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var rune = await db.CharacterRunes.FirstOrDefaultAsync(r => r.Id == id && r.CharacterSheetId == sheetId);
        if (rune is null)
            return NotFound();

        db.CharacterRunes.Remove(rune);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CharacterRuneResponse ToResponse(CharacterRune r) =>
        new(r.Id.ToString(), r.Nome, r.Descricao, r.Grau);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
