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
[Route("api/npc-sheets/{sheetId}/affinities")]
public class NpcAffinitiesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NpcAffinityResponse>> Add(Guid sheetId, AddNpcAffinityRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        if (!Enum.TryParse<Elemento>(request.Elemento, out var elemento) || !Enum.TryParse<SubElemento>(request.SubElemento, out var subElemento))
            return BadRequest("Elemento ou Sub-Elemento desconhecido.");

        if (!ElementoSubElementoValidator.IsValidCombination(elemento, subElemento))
            return BadRequest("Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.");

        var affinity = new NpcAffinity
        {
            Id = Guid.NewGuid(), NpcSheetId = sheetId, Elemento = elemento, ElementoValor = request.ElementoValor,
            SubElemento = subElemento, SubElementoValor = request.SubElementoValor, CaminhoNome = request.CaminhoNome, Experiencia = request.Experiencia
        };
        db.NpcAffinities.Add(affinity);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(affinity));
    }

    [HttpGet]
    public async Task<ActionResult<List<NpcAffinityResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var affinities = await db.NpcAffinities.Where(a => a.NpcSheetId == sheetId).ToListAsync();
        return affinities.Select(ToResponse).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var affinity = await db.NpcAffinities.FirstOrDefaultAsync(a => a.Id == id && a.NpcSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        db.NpcAffinities.Remove(affinity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static NpcAffinityResponse ToResponse(NpcAffinity a) =>
        new(a.Id.ToString(), a.Elemento.ToString(), a.ElementoValor, a.SubElemento.ToString(), a.SubElementoValor, a.CaminhoNome, a.Experiencia);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
