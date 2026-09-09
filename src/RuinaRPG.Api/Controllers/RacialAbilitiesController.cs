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

/// <summary>
/// GM-editable overrides for RacialAbilityLookup's hardcoded defaults (Ruína RPG - Sistema
/// Básico.md §7), plus the "tabela de Arcas" that Sinir/Laonir's (Humano) racial ability
/// references by name ("Role 1d18 na tabela de Arcas") but that no doc actually defines — it's
/// free-form GM content, not a fixed system rule. Curating either is GM-only; reading both is
/// open to a linked Jogador too, since CharacterSheetsController/NpcSheetsController's
/// racial-ability endpoint reads from here.
/// </summary>
[ApiController]
[Authorize]
public class RacialAbilitiesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet("api/racial-abilities")]
    public async Task<ActionResult<List<RacialAbilityEntryResponse>>> ListRacialAbilities()
    {
        var gmId = await ResolveEffectiveGmIdAsync();
        if (gmId is null)
            return Forbid();

        var overrides = await db.RacialAbilityOverrides.Where(o => o.GmId == gmId).ToListAsync();

        var responses = new List<RacialAbilityEntryResponse>();
        foreach (var variante in Enum.GetValues<Variante>())
        {
            var over = overrides.FirstOrDefault(o => o.Variante == variante);
            if (over is not null)
            {
                responses.Add(new RacialAbilityEntryResponse(variante.ToString(), over.Nome, over.Descricao, false));
            }
            else
            {
                var def = RacialAbilityLookup.For(variante);
                responses.Add(new RacialAbilityEntryResponse(variante.ToString(), def.Nome, def.Descricao, true));
            }
        }
        return responses;
    }

    [HttpPut("api/racial-abilities/{variante}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> UpdateRacialAbility(string variante, UpdateRacialAbilityRequest request)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante))
            return BadRequest("Variante desconhecida.");

        var gmId = CurrentUserId();
        var existing = await db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is null)
        {
            db.RacialAbilityOverrides.Add(new RacialAbilityOverride { Id = Guid.NewGuid(), GmId = gmId, Variante = parsedVariante, Nome = request.Nome, Descricao = request.Descricao });
        }
        else
        {
            existing.Nome = request.Nome;
            existing.Descricao = request.Descricao;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/racial-abilities/{variante}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> DeleteRacialAbilityOverride(string variante)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante))
            return BadRequest("Variante desconhecida.");

        var gmId = CurrentUserId();
        var existing = await db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is not null)
        {
            db.RacialAbilityOverrides.Remove(existing);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpGet("api/arcas")]
    public async Task<ActionResult<List<ArcaEntryResponse>>> ListArcas()
    {
        var gmId = await ResolveEffectiveGmIdAsync();
        if (gmId is null)
            return Forbid();

        var entries = await db.ArcaEntries.Where(a => a.GmId == gmId).ToListAsync();

        var responses = new List<ArcaEntryResponse>();
        for (var roll = 1; roll <= 18; roll++)
        {
            var entry = entries.FirstOrDefault(a => a.Roll == roll);
            responses.Add(new ArcaEntryResponse(roll, entry?.Nome, entry?.Descricao));
        }
        return responses;
    }

    [HttpPut("api/arcas/{roll:int}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> UpdateArca(int roll, UpdateArcaEntryRequest request)
    {
        if (roll is < 1 or > 18)
            return BadRequest("Roll deve estar entre 1 e 18.");

        var gmId = CurrentUserId();
        var existing = await db.ArcaEntries.FirstOrDefaultAsync(a => a.GmId == gmId && a.Roll == roll);
        if (existing is null)
        {
            db.ArcaEntries.Add(new ArcaEntry { Id = Guid.NewGuid(), GmId = gmId, Roll = roll, Nome = request.Nome, Descricao = request.Descricao });
        }
        else
        {
            existing.Nome = request.Nome;
            existing.Descricao = request.Descricao;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    /// <summary>
    /// Same "whose library" resolution already used by ItemsController/SpellAbilityBankController:
    /// a GM's own id, or their linked GM's id for a Jogador (a Jogador always belongs to exactly
    /// one GM via InvitedByGmId). Null means the caller has no library to see.
    /// </summary>
    private async Task<Guid?> ResolveEffectiveGmIdAsync()
    {
        var callerId = CurrentUserId();
        if (User.IsInRole("GM"))
            return callerId;

        var caller = await db.Users.FindAsync(callerId);
        return caller?.InvitedByGmId;
    }
}
