using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize(Roles = "GM")]
public class CampaignsController(RuinaRpgDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
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

    [HttpPost("{campaignId}/members")]
    public async Task<IActionResult> AddMember(Guid campaignId, AddCampaignMemberRequest request)
    {
        var gmId = CurrentGmId();
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (campaign is null)
            return NotFound();

        if (!Guid.TryParse(request.UserId, out _))
            return BadRequest("O jogador informado não está vinculado à sua conta.");

        var player = await userManager.FindByIdAsync(request.UserId);
        if (player is null || player.InvitedByGmId != gmId)
            return BadRequest("O jogador informado não está vinculado à sua conta.");

        if (await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == player.Id))
            return NoContent(); // already a member — idempotent, not an error

        db.CampaignMembers.Add(new CampaignMember { Id = Guid.NewGuid(), CampaignId = campaignId, UserId = player.Id });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{campaignId}/members")]
    public async Task<ActionResult<List<CampaignMemberResponse>>> ListMembers(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var memberIds = await db.CampaignMembers.Where(m => m.CampaignId == campaignId).Select(m => m.UserId).ToListAsync();
        return await db.Users
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => new CampaignMemberResponse(u.Id.ToString(), u.Nickname, u.Email!))
            .ToListAsync();
    }

    [HttpPost("{campaignId}/diary")]
    public async Task<ActionResult<DiaryEntryResponse>> CreateDiaryEntry(Guid campaignId, CreateDiaryEntryRequest request)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var entry = new DiaryEntry { Id = Guid.NewGuid(), AuthorUserId = gmId, CampaignId = campaignId, IsSecretNote = false, Texto = request.Texto, CreatedAt = DateTime.UtcNow };
        db.DiaryEntries.Add(entry);
        foreach (var imageId in request.ImageIds)
            db.DiaryEntryImages.Add(new DiaryEntryImage { DiaryEntryId = entry.Id, ImageId = Guid.Parse(imageId) });
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(entry));
    }

    [HttpGet("{campaignId}/diary")]
    public async Task<ActionResult<List<DiaryEntryResponse>>> ListDiaryEntries(Guid campaignId)
    {
        var gmId = CurrentGmId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var entries = await db.DiaryEntries
            .Where(d => d.CampaignId == campaignId && !d.IsSecretNote)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var responses = new List<DiaryEntryResponse>();
        foreach (var entry in entries)
            responses.Add(await ToResponseAsync(entry));
        return responses;
    }

    [HttpPut("{campaignId}/diary/{entryId}")]
    public async Task<IActionResult> UpdateDiaryEntry(Guid campaignId, Guid entryId, UpdateDiaryEntryRequest request)
    {
        var gmId = CurrentGmId();
        var entry = await FindOwnedDiaryEntryAsync(campaignId, entryId, gmId);
        if (entry is null)
            return NotFound();

        entry.Texto = request.Texto;

        var existingImages = await db.DiaryEntryImages.Where(i => i.DiaryEntryId == entryId).ToListAsync();
        db.DiaryEntryImages.RemoveRange(existingImages);
        foreach (var imageId in request.ImageIds)
            db.DiaryEntryImages.Add(new DiaryEntryImage { DiaryEntryId = entryId, ImageId = Guid.Parse(imageId) });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{campaignId}/diary/{entryId}")]
    public async Task<IActionResult> DeleteDiaryEntry(Guid campaignId, Guid entryId)
    {
        var gmId = CurrentGmId();
        var entry = await FindOwnedDiaryEntryAsync(campaignId, entryId, gmId);
        if (entry is null)
            return NotFound();

        db.DiaryEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<DiaryEntry?> FindOwnedDiaryEntryAsync(Guid campaignId, Guid entryId, Guid gmId)
    {
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return null;

        return await db.DiaryEntries.FirstOrDefaultAsync(d => d.Id == entryId && d.CampaignId == campaignId);
    }

    private async Task<DiaryEntryResponse> ToResponseAsync(DiaryEntry entry)
    {
        var imageIds = await db.DiaryEntryImages.Where(i => i.DiaryEntryId == entry.Id).Select(i => i.ImageId).ToListAsync();
        var images = await db.Images.Where(i => imageIds.Contains(i.Id)).ToListAsync();
        return new DiaryEntryResponse(entry.Id.ToString(), entry.Texto, entry.CreatedAt, images.Select(i => $"/{i.Path}").ToList());
    }

    private static CampaignResponse ToResponse(Campaign c) => new(c.Id.ToString(), c.Nome, c.Descricao);

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
