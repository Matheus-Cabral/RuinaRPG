using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/skills")]
public class CharacterSkillsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CharacterSkillResponse>>> List(Guid sheetId, [FromQuery] string? atributoEscolhido)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var skills = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId).ToListAsync();

        int? atributoTotal = null;
        if (Enum.TryParse<Atributo>(atributoEscolhido, out var parsedAtributo))
        {
            var attribute = await db.CharacterAttributes.SingleOrDefaultAsync(a => a.CharacterSheetId == sheetId && a.Atributo == parsedAtributo);
            if (attribute is not null)
                atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        }

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto);
                var total = atributoTotal is not null ? SkillFormulas.Total(modificador, atributoTotal.Value) : (int?)null;
                return new CharacterSkillResponse(s.Pericia.ToString(), s.Gasto, modificador, atributoEscolhido, total);
            })
            .ToList();
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, Pericia pericia, UpdateCharacterSkillRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var skill = await db.CharacterSkills.SingleAsync(s => s.CharacterSheetId == sheetId && s.Pericia == pericia);
        skill.Gasto = request.Gasto;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
