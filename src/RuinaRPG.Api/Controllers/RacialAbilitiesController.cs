using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
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
/// free-form GM content, not a fixed system rule. Both curating and browsing this page are
/// GM-only — a Jogador never reaches these endpoints directly; they only see the already-resolved
/// Nome/Descrição/Arca on their own sheet, which CharacterSheetsController/NpcSheetsController
/// compute by querying RacialAbilityOverrides/ArcaEntries themselves, not through this controller.
/// </summary>
[ApiController]
[Authorize(Roles = "GM")]
public class RacialAbilitiesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet("api/racial-abilities")]
    public async Task<ActionResult<List<RacialAbilityEntryResponse>>> ListRacialAbilities()
    {
        var gmId = CurrentUserId();
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
    public async Task<IActionResult> UpdateRacialAbility(string variante, UpdateRacialAbilityRequest request)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
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
    public async Task<IActionResult> DeleteRacialAbilityOverride(string variante)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
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
        var gmId = CurrentUserId();
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

    /// <summary>
    /// GM-editable overrides for RacialTraitLookup's hardcoded Característica Gratuita/Obrigatória
    /// defaults (Ruína RPG - Sistema Básico.md §7). Same shape as ListRacialAbilities above.
    /// </summary>
    [HttpGet("api/racial-traits")]
    public async Task<ActionResult<List<RacialTraitSlotsEntryResponse>>> ListRacialTraits()
    {
        var gmId = CurrentUserId();
        var overrides = await db.RacialTraitOverrides.Where(o => o.GmId == gmId).ToListAsync();

        var responses = new List<RacialTraitSlotsEntryResponse>();
        foreach (var variante in Enum.GetValues<Variante>())
        {
            var over = overrides.FirstOrDefault(o => o.Variante == variante);
            if (over is not null)
            {
                responses.Add(new RacialTraitSlotsEntryResponse(variante.ToString(),
                    DeserializeOptions(over.GratuitaOptionsJson), DeserializeOptions(over.ObrigatoriaOptionsJson), false));
            }
            else
            {
                var def = RacialTraitLookup.For(variante);
                responses.Add(new RacialTraitSlotsEntryResponse(variante.ToString(), ToResponseOptions(def.Gratuita), ToResponseOptions(def.Obrigatoria), true));
            }
        }
        return responses;
    }

    [HttpPut("api/racial-traits/{variante}")]
    public async Task<IActionResult> UpdateRacialTraitSlots(string variante, UpdateRacialTraitSlotsRequest request)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
            return BadRequest("Variante desconhecida.");

        if (request.Gratuita.Count == 0)
            return BadRequest("A Característica Gratuita precisa de ao menos uma opção.");

        var gmId = CurrentUserId();
        var gratuitaJson = JsonSerializer.Serialize(request.Gratuita.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)));
        var obrigatoriaJson = JsonSerializer.Serialize(request.Obrigatoria.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)));

        var existing = await db.RacialTraitOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is null)
        {
            db.RacialTraitOverrides.Add(new RacialTraitOverride
            {
                Id = Guid.NewGuid(), GmId = gmId, Variante = parsedVariante,
                GratuitaOptionsJson = gratuitaJson, ObrigatoriaOptionsJson = obrigatoriaJson,
            });
        }
        else
        {
            existing.GratuitaOptionsJson = gratuitaJson;
            existing.ObrigatoriaOptionsJson = obrigatoriaJson;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/racial-traits/{variante}")]
    public async Task<IActionResult> DeleteRacialTraitOverride(string variante)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
            return BadRequest("Variante desconhecida.");

        var gmId = CurrentUserId();
        var existing = await db.RacialTraitOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is not null)
        {
            db.RacialTraitOverrides.Remove(existing);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    private static List<RacialTraitOptionResponse> DeserializeOptions(string json) =>
        JsonSerializer.Deserialize<List<RacialTraitOptionResponse>>(json) ?? [];

    private static List<RacialTraitOptionResponse> ToResponseOptions(List<RacialTraitOption> options) =>
        options.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)).ToList();

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
