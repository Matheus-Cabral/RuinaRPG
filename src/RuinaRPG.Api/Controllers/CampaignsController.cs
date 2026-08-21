using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize(Roles = "GM")]
public class CampaignsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CampaignResponse>> Create(CreateCampaignRequest request)
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = CurrentGmId(), Nome = request.Nome, Descricao = request.Descricao };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(campaign));
    }

    [HttpGet]
    public async Task<ActionResult<List<CampaignResponse>>> List()
    {
        var gmId = CurrentGmId();
        return await db.Campaigns
            .Where(c => c.GmId == gmId)
            .Select(c => new CampaignResponse(c.Id.ToString(), c.Nome, c.Descricao))
            .ToListAsync();
    }

    private static CampaignResponse ToResponse(Campaign c) => new(c.Id.ToString(), c.Nome, c.Descricao);

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
