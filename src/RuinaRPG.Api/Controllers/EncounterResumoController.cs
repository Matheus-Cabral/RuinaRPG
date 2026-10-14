using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// O resumo de um NPC/Criatura participante do encontro, com as Condições do participante aplicadas (Gerenciador de
/// Encontros R0014). Só leitura. É o único lugar onde uma Condição age sobre uma ficha do GM (sem dono): os endpoints
/// da própria ficha seguem sem Condição.
/// </summary>
[ApiController]
[Authorize(Roles = "GM")]
[Route("api/encounters/{encounterId}/participants/{participantId}/resumo")]
public class EncounterResumoController(RuinaRpgDbContext db, ResumoDeFichaDoParticipante resumos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ResumoDeFichaResponse>> Get(Guid encounterId, Guid participantId)
    {
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return NotFound();
        var gmId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        if (!await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == gmId))
            return NotFound();

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == participantId && p.EncounterId == encounterId);
        if (participant is null)
            return NotFound();
        if (participant.SourceCharacterSheetId is not null)
            return BadRequest("O resumo é só para NPCs e Criaturas.");

        var resumo = await resumos.MontarAsync(participant);
        return resumo is null ? NotFound() : resumo;
    }
}
