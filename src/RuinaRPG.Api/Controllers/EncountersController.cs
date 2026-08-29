using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/campaigns/{campaignId}/encounters")]
public class EncountersController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EncounterResponse>> Create(Guid campaignId, CreateEncounterRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var encounter = new Encounter { Id = Guid.NewGuid(), CampaignId = campaignId, Nome = request.Nome };
        db.Encounters.Add(encounter);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(encounter));
    }

    [HttpGet]
    public async Task<ActionResult<List<EncounterResponse>>> List(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        return await db.Encounters.Where(e => e.CampaignId == campaignId).Select(e => ToResponse(e)).ToListAsync();
    }

    [HttpPut("~/api/encounters/{encounterId}")]
    public async Task<IActionResult> Update(Guid encounterId, UpdateEncounterRequest request)
    {
        var gmId = CurrentGmId();
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return NotFound();
        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == gmId);
        if (!isOwner)
            return NotFound();

        encounter.Nome = request.Nome;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("~/api/encounters/{encounterId}/advance-turn")]
    public async Task<IActionResult> AdvanceTurn(Guid encounterId)
    {
        var gmId = CurrentGmId();
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return NotFound();
        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == gmId);
        if (!isOwner)
            return NotFound();

        var participantCount = await db.EncounterParticipants.CountAsync(p => p.EncounterId == encounterId);
        if (participantCount == 0)
            return BadRequest("Adicione ao menos um participante antes de avançar o turno.");

        var (nextRound, nextIndex) = RuinaRPG.Domain.Encounters.TurnAdvanceCalculator.Advance(encounter.CurrentRound, encounter.CurrentParticipantIndex, participantCount);
        encounter.CurrentRound = nextRound;
        encounter.CurrentParticipantIndex = nextIndex;

        var nextParticipant = await db.EncounterParticipants
            .Where(p => p.EncounterId == encounterId)
            .OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id)
            .Skip(nextIndex)
            .FirstAsync();
        nextParticipant.AcoesRestantes = 3;

        await db.SaveChangesAsync();
        return NoContent();
    }

    private static EncounterResponse ToResponse(Encounter e) => new(e.Id.ToString(), e.Nome, e.CurrentRound, e.CurrentParticipantIndex);

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
