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

        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, request.CaminhoNome, sheet.Vocacao, elementoAntigo: null, subElementoAntigo: null, caminhoNomeAntigo: null, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        if (await HasDuplicateAsync(sheetId, elemento, subElemento, excludingId: null))
            return BadRequest("Já existe uma linha de Afinidade com esse Elemento ou Sub-Elemento.");

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

    [HttpPut("{id}")]
    public async Task<ActionResult<NpcAffinityResponse>> Update(Guid sheetId, Guid id, UpdateNpcAffinityRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var affinity = await db.NpcAffinities.FirstOrDefaultAsync(a => a.Id == id && a.NpcSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, request.CaminhoNome, sheet.Vocacao, affinity.Elemento, affinity.SubElemento, affinity.CaminhoNome, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        if (await HasDuplicateAsync(sheetId, elemento, subElemento, excludingId: id,
                checkElemento: elemento != affinity.Elemento, checkSubElemento: subElemento != affinity.SubElemento))
            return BadRequest("Já existe uma linha de Afinidade com esse Elemento ou Sub-Elemento.");

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

    // Elemento and Sub-Elemento are both optional — an Afinidade row can be added or left as a
    // blank placeholder, matching the PDF sheet's pre-printed empty rows (R0001 2.c). Only when
    // both are actually given does the Matriz Elemental combination get checked.
    private static bool TryParseElementoSubElemento(string? elementoRaw, string? subElementoRaw, string? caminhoNome, Vocacao? vocacao,
        Elemento? elementoAntigo, SubElemento? subElementoAntigo, string? caminhoNomeAntigo,
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

        if (elemento is not null && elemento != elementoAntigo && !VocacaoEscolaMap.PodeEscolherElemento(vocacao, elemento.Value))
        {
            error = "Esse Elemento não é liberado pela Vocação atual.";
            return false;
        }
        if (subElemento is not null && subElemento != subElementoAntigo && !VocacaoEscolaMap.PodeEscolherSubElemento(vocacao, subElemento.Value))
        {
            error = "Esse Sub-Elemento não é liberado pela Vocação atual.";
            return false;
        }

        error = CaminhoSubElementoRules.Validar(elemento, subElemento, caminhoNome, elementoAntigo, subElementoAntigo, caminhoNomeAntigo);
        if (error is not null)
            return false;

        return true;
    }

    private async Task<bool> HasDuplicateAsync(Guid sheetId, Elemento? elemento, SubElemento? subElemento,
        Guid? excludingId, bool checkElemento = true, bool checkSubElemento = true)
    {
        var query = db.NpcAffinities.Where(a => a.NpcSheetId == sheetId);
        if (excludingId is not null)
            query = query.Where(a => a.Id != excludingId);

        return await query.AnyAsync(a =>
            (checkElemento && elemento != null && a.Elemento == elemento) ||
            (checkSubElemento && subElemento != null && a.SubElemento == subElemento));
    }

    private static NpcAffinityResponse ToResponse(NpcAffinity a) =>
        new(a.Id.ToString(), a.Elemento?.ToString(), a.ElementoValor, a.SubElemento?.ToString(), a.SubElementoValor, a.CaminhoNome, a.Experiencia);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
