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

        if (!TryParseEssencias(request.Elemento, request.SegundaEssencia, out var elemento, out var segunda, out var error))
            return BadRequest(error);

        var subElemento = Derivar(elemento, segunda);
        if (await HasDuplicateSubElementoAsync(sheetId, subElemento, excludingId: null))
            return BadRequest("Já existe uma linha de Afinidade com esse Sub-Elemento.");

        var affinity = new CharacterAffinity
        {
            Id = Guid.NewGuid(), CharacterSheetId = sheetId, Elemento = elemento, ElementoValor = request.ElementoValor,
            SegundaEssencia = segunda, SegundaEssenciaValor = request.SegundaEssenciaValor,
            SubElemento = subElemento, SubElementoValor = request.SubElementoValor, Experiencia = request.Experiencia
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

        var affinity = await db.CharacterAffinities.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        if (!TryParseEssencias(request.Elemento, request.SegundaEssencia, out var elemento, out var segunda, out var error))
            return BadRequest(error);

        var essenciaMudou = elemento != affinity.Elemento || segunda != affinity.SegundaEssencia;
        var subElemento = essenciaMudou ? Derivar(elemento, segunda) : affinity.SubElemento;
        if (subElemento != affinity.SubElemento && await HasDuplicateSubElementoAsync(sheetId, subElemento, id))
            return BadRequest("Já existe uma linha de Afinidade com esse Sub-Elemento.");

        affinity.Elemento = elemento;
        affinity.ElementoValor = request.ElementoValor;
        affinity.SegundaEssencia = segunda;
        affinity.SegundaEssenciaValor = request.SegundaEssenciaValor;
        affinity.SubElemento = subElemento;
        affinity.SubElementoValor = request.SubElementoValor;
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

    // Essência 1 e 2 são opcionais (linha em branco continua valendo). O Sub-Elemento nunca vem do
    // cliente: é a interseção na Matriz Elemental (Requisitos - Ficha de Personagem 2.c).
    private static bool TryParseEssencias(string? elementoRaw, string? segundaRaw,
        out Elemento? elemento, out EssenciaBasica? segunda, out string? error)
    {
        elemento = null;
        segunda = null;
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

        if (!string.IsNullOrEmpty(segundaRaw))
        {
            if (!Enum.TryParse<EssenciaBasica>(segundaRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Essência Básica desconhecida.";
                return false;
            }
            segunda = parsed;
        }

        if (segunda is not null && elemento is null)
        {
            error = "Escolha a Essência Básica 1 antes da 2.";
            return false;
        }

        if (elemento is { } e1 && segunda is { } e2 && MatrizElemental.Intersecao(e1, e2) is null)
        {
            error = "Essas duas Essências não se cruzam na Matriz Elemental.";
            return false;
        }

        return true;
    }

    private static SubElemento? Derivar(Elemento? elemento, EssenciaBasica? segunda) =>
        elemento is { } e1 && segunda is { } e2 ? MatrizElemental.Intersecao(e1, e2) : null;

    private async Task<bool> HasDuplicateSubElementoAsync(Guid sheetId, SubElemento? subElemento, Guid? excludingId) =>
        subElemento is not null && await db.CharacterAffinities.AnyAsync(a =>
            a.CharacterSheetId == sheetId && a.SubElemento == subElemento && (excludingId == null || a.Id != excludingId));

    private static CharacterAffinityResponse ToResponse(CharacterAffinity a) =>
        new(a.Id.ToString(), a.Elemento?.ToString(), a.ElementoValor, a.SubElemento?.ToString(), a.SubElementoValor, a.CaminhoNome, a.Experiencia,
            a.SegundaEssencia?.ToString(), a.SegundaEssenciaValor);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
