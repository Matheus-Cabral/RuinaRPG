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
[Route("api/character-sheets/{sheetId}/affinities")]
public class CharacterAffinitiesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterAffinityResponse>> Add(Guid sheetId, AddCharacterAffinityRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        if (!Enum.TryParse<Elemento>(request.Elemento, out var elemento) || !Enum.TryParse<SubElemento>(request.SubElemento, out var subElemento))
            return BadRequest("Elemento ou Sub-Elemento desconhecido.");

        if (!ElementoSubElementoValidator.IsValidCombination(elemento, subElemento))
            return BadRequest("Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.");

        var affinity = new CharacterAffinity
        {
            Id = Guid.NewGuid(), CharacterSheetId = sheetId, Elemento = elemento, ElementoValor = request.ElementoValor,
            SubElemento = subElemento, SubElementoValor = request.SubElementoValor, CaminhoNome = request.CaminhoNome, Experiencia = request.Experiencia
        };
        db.CharacterAffinities.Add(affinity);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(affinity));
    }

    [HttpGet]
    public async Task<ActionResult<List<CharacterAffinityResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var affinities = await db.CharacterAffinities.Where(a => a.CharacterSheetId == sheetId).ToListAsync();
        return affinities.Select(ToResponse).ToList();
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

        var affinity = await db.CharacterAffinities.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        db.CharacterAffinities.Remove(affinity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CharacterAffinityResponse ToResponse(CharacterAffinity a) =>
        new(a.Id.ToString(), a.Elemento.ToString(), a.ElementoValor, a.SubElemento.ToString(), a.SubElementoValor, a.CaminhoNome, a.Experiencia);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
