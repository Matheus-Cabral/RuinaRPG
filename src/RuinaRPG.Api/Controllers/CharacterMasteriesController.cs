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
[Route("api/character-sheets/{sheetId}/masteries")]
public class CharacterMasteriesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterMasteryResponse>> Add(Guid sheetId, AddCharacterMasteryRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        if (!Enum.TryParse<Pericia>(request.Pericia, out var pericia) || !Enum.IsDefined(pericia))
            return BadRequest("Perícia ou Atributo desconhecido.");
        if (!Enum.TryParse<Atributo>(request.Atributo, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest("Perícia ou Atributo desconhecido.");

        var mastery = new CharacterMastery { Id = Guid.NewGuid(), CharacterSheetId = sheetId, Nome = request.Nome, Pericia = pericia, Atributo = atributo, GastoMaestria = request.GastoMaestria };
        db.CharacterMasteries.Add(mastery);
        await db.SaveChangesAsync();

        var total = await ComputeTotalAsync(sheetId, pericia, atributo, mastery.GastoMaestria);
        return Created(string.Empty, ToResponse(mastery, total));
    }

    [HttpGet]
    public async Task<ActionResult<List<CharacterMasteryResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var masteries = await db.CharacterMasteries.Where(m => m.CharacterSheetId == sheetId).ToListAsync();

        var responses = new List<CharacterMasteryResponse>();
        foreach (var mastery in masteries)
        {
            var total = await ComputeTotalAsync(sheetId, mastery.Pericia, mastery.Atributo, mastery.GastoMaestria);
            responses.Add(ToResponse(mastery, total));
        }

        return responses;
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

        var mastery = await db.CharacterMasteries.FirstOrDefaultAsync(m => m.Id == id && m.CharacterSheetId == sheetId);
        if (mastery is null)
            return NotFound();

        db.CharacterMasteries.Remove(mastery);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, Atributo atributo, int gastoMaestria)
    {
        var skill = await db.CharacterSkills.SingleAsync(s => s.CharacterSheetId == sheetId && s.Pericia == pericia);
        var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);
        var bruto = SkillFormulas.Modificador(skill.Gasto);
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }

    private static CharacterMasteryResponse ToResponse(CharacterMastery m, int total) =>
        new(m.Id.ToString(), m.Nome, m.Pericia.ToString(), m.Atributo.ToString(), m.GastoMaestria, total);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
