using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/masteries")]
public class CreatureMasteriesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreatureMasteryResponse>> Add(Guid sheetId, AddCreatureMasteryRequest request)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        if (!Enum.TryParse<Pericia>(request.Pericia, out var pericia) || !Enum.IsDefined(pericia))
            return BadRequest("Perícia ou Atributo desconhecido.");
        if (!Enum.TryParse<AtributoCriatura>(request.Atributo, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest("Perícia ou Atributo desconhecido.");
        // R0005's "lista fixa mais curta" — only the 20 allowed Pericia values have a seeded
        // CreatureSkill row; without this check a valid-but-disallowed Pericia (e.g. Alquimia)
        // would pass Enum.IsDefined above and then 500 in ComputeTotalAsync's SingleAsync.
        if (!CreatureSkillAllowList.IsAllowed(pericia))
            return BadRequest("Perícia fora da lista permitida para Criaturas.");

        var mastery = new CreatureMastery { Id = Guid.NewGuid(), CreatureSheetId = sheetId, Nome = request.Nome, Pericia = pericia, Atributo = atributo, GastoMaestria = request.GastoMaestria };
        db.CreatureMasteries.Add(mastery);
        await db.SaveChangesAsync();

        var total = await ComputeTotalAsync(sheetId, pericia, atributo, mastery.GastoMaestria);
        return Created(string.Empty, ToResponse(mastery, total));
    }

    [HttpGet]
    public async Task<ActionResult<List<CreatureMasteryResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var masteries = await db.CreatureMasteries.Where(m => m.CreatureSheetId == sheetId).ToListAsync();

        var responses = new List<CreatureMasteryResponse>();
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
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var mastery = await db.CreatureMasteries.FirstOrDefaultAsync(m => m.Id == id && m.CreatureSheetId == sheetId);
        if (mastery is null)
            return NotFound();

        db.CreatureMasteries.Remove(mastery);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<int> ComputeTotalAsync(Guid sheetId, Pericia pericia, AtributoCriatura atributo, int gastoMaestria)
    {
        var skill = await db.CreatureSkills.SingleAsync(s => s.CreatureSheetId == sheetId && s.Pericia == pericia);
        var attribute = await db.CreatureAttributes.SingleAsync(a => a.CreatureSheetId == sheetId && a.Atributo == atributo);
        var bruto = SkillFormulas.Modificador(skill.Gasto);
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }

    private static CreatureMasteryResponse ToResponse(CreatureMastery m, int total) =>
        new(m.Id.ToString(), m.Nome, m.Pericia.ToString(), m.Atributo.ToString(), m.GastoMaestria, total);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
