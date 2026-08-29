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
[Route("api/npc-sheets/{sheetId}/masteries")]
public class NpcMasteriesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NpcMasteryResponse>> Add(Guid sheetId, AddNpcMasteryRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        if (!Enum.TryParse<Pericia>(request.Pericia, out var pericia) || !Enum.IsDefined(pericia))
            return BadRequest("Perícia ou Atributo desconhecido.");
        if (!Enum.TryParse<Atributo>(request.Atributo, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest("Perícia ou Atributo desconhecido.");

        var mastery = new NpcMastery { Id = Guid.NewGuid(), NpcSheetId = sheetId, Nome = request.Nome, Pericia = pericia, Atributo = atributo, GastoMaestria = request.GastoMaestria };
        db.NpcMasteries.Add(mastery);
        await db.SaveChangesAsync();

        var total = await ComputeTotalAsync(sheetId, pericia, atributo, mastery.GastoMaestria);
        return Created(string.Empty, ToResponse(mastery, total));
    }

    [HttpGet]
    public async Task<ActionResult<List<NpcMasteryResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var masteries = await db.NpcMasteries.Where(m => m.NpcSheetId == sheetId).ToListAsync();

        var responses = new List<NpcMasteryResponse>();
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
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var mastery = await db.NpcMasteries.FirstOrDefaultAsync(m => m.Id == id && m.NpcSheetId == sheetId);
        if (mastery is null)
            return NotFound();

        db.NpcMasteries.Remove(mastery);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, Atributo atributo, int gastoMaestria)
    {
        var skill = await db.NpcSkills.SingleAsync(s => s.NpcSheetId == sheetId && s.Pericia == pericia);
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        var bruto = SkillFormulas.Modificador(skill.Gasto);
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }

    private static NpcMasteryResponse ToResponse(NpcMastery m, int total) =>
        new(m.Id.ToString(), m.Nome, m.Pericia.ToString(), m.Atributo.ToString(), m.GastoMaestria, total);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
