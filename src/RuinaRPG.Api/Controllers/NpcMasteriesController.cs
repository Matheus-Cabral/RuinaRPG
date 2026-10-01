using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/masteries")]
public class NpcMasteriesController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NpcMasteryResponse>> Add(Guid sheetId, AddNpcMasteryRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var pericia = await pericias.AtivaPorChaveAsync(request.Pericia);
        if (pericia is null)
            return BadRequest("Perícia ou Atributo desconhecido.");
        if (!Enum.TryParse<Atributo>(request.Atributo, out var atributo) || !Enum.IsDefined(atributo))
            return BadRequest("Perícia ou Atributo desconhecido.");

        var mastery = new NpcMastery { Id = Guid.NewGuid(), NpcSheetId = sheetId, Nome = request.Nome, PericiaId = pericia.Id, Atributo = atributo, GastoMaestria = request.GastoMaestria };
        db.NpcMasteries.Add(mastery);
        await db.SaveChangesAsync();

        var porId = await pericias.PorIdAsync();
        var total = await ComputeTotalAsync(sheetId, pericia.Id, atributo, mastery.GastoMaestria);
        return Created(string.Empty, ToResponse(mastery, total, porId));
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

        var porId = await pericias.PorIdAsync();
        var responses = new List<NpcMasteryResponse>();
        foreach (var mastery in masteries)
        {
            var total = await ComputeTotalAsync(sheetId, mastery.PericiaId, mastery.Atributo, mastery.GastoMaestria);
            responses.Add(ToResponse(mastery, total, porId));
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

    private async Task<int> ComputeTotalAsync(Guid sheetId, int periciaId, Atributo atributo, int gastoMaestria)
    {
        var skill = await db.NpcSkills.SingleAsync(s => s.NpcSheetId == sheetId && s.PericiaId == periciaId);
        var attribute = await db.NpcAttributes.SingleAsync(a => a.NpcSheetId == sheetId && a.Atributo == atributo);
        var historicoId = await db.NpcSheets.Where(s => s.Id == sheetId).Select(s => s.HistoricoId).SingleAsync();
        var historico = historicoId is null ? null : await db.Historicos.FindAsync(historicoId.Value);
        var bruto = SkillFormulas.Modificador(skill.Gasto, HistoricoBonusCalculator.For(periciaId, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId));
        var atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        return gastoMaestria + bruto + atributoTotal;
    }

    private static NpcMasteryResponse ToResponse(NpcMastery m, int total, IReadOnlyDictionary<int, PericiaDefinicao> porId) =>
        new(m.Id.ToString(), m.Nome, porId[m.PericiaId].Chave, m.Atributo.ToString(), m.GastoMaestria, total);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
