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

    private static EncounterResponse ToResponse(Encounter e) => new(e.Id.ToString(), e.Nome, e.CurrentRound, e.CurrentParticipantIndex);

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
