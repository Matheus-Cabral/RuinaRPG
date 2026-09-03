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

        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

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

    [HttpPut("{id}")]
    public async Task<ActionResult<CharacterAffinityResponse>> Update(Guid sheetId, Guid id, UpdateCharacterAffinityRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        var affinity = await db.CharacterAffinities.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        affinity.Elemento = elemento;
        affinity.ElementoValor = request.ElementoValor;
        affinity.SubElemento = subElemento;
        affinity.SubElementoValor = request.SubElementoValor;
        affinity.CaminhoNome = request.CaminhoNome;
        affinity.Experiencia = request.Experiencia;
        await db.SaveChangesAsync();

        return Ok(ToResponse(affinity));
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

    // Elemento and Sub-Elemento are both optional — an Afinidade row can be added or left as a
    // blank placeholder, matching the PDF sheet's pre-printed empty rows (R0001 2.c). Only when
    // both are actually given does the Matriz Elemental combination get checked.
    private static bool TryParseElementoSubElemento(string? elementoRaw, string? subElementoRaw,
        out Elemento? elemento, out SubElemento? subElemento, out string? error)
    {
        elemento = null;
        subElemento = null;
        error = null;

        if (!string.IsNullOrEmpty(elementoRaw))
        {
            if (!Enum.TryParse<Elemento>(elementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Elemento desconhecido.";
                return false;
            }
            elemento = parsed;
        }

        if (!string.IsNullOrEmpty(subElementoRaw))
        {
            if (!Enum.TryParse<SubElemento>(subElementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Sub-Elemento desconhecido.";
                return false;
            }
            subElemento = parsed;
        }

        if (elemento is not null && subElemento is not null && !ElementoSubElementoValidator.IsValidCombination(elemento.Value, subElemento.Value))
        {
            error = "Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.";
            return false;
        }

        return true;
    }

    private static CharacterAffinityResponse ToResponse(CharacterAffinity a) =>
        new(a.Id.ToString(), a.Elemento?.ToString(), a.ElementoValor, a.SubElemento?.ToString(), a.SubElementoValor, a.CaminhoNome, a.Experiencia);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
