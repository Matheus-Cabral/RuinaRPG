using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/diary")]
public class CharacterDiaryController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DiaryEntryResponse>> CreateDiaryEntry(Guid sheetId, CreateDiaryEntryRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var authorId = CurrentUserId();

        if (!TryParseImageIds(request.ImageIds, out var imageIds))
            return BadRequest("Um dos identificadores de imagem informados é inválido.");

        if (!await OwnsAllImagesAsync(imageIds, authorId))
            return BadRequest("Imagem não encontrada.");

        var entry = new DiaryEntry { Id = Guid.NewGuid(), AuthorUserId = authorId, CharacterSheetId = sheetId, IsSecretNote = false, Texto = request.Texto, CreatedAt = DateTime.UtcNow };
        db.DiaryEntries.Add(entry);
        foreach (var imageId in imageIds)
            db.DiaryEntryImages.Add(new DiaryEntryImage { DiaryEntryId = entry.Id, ImageId = imageId });
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(entry));
    }

    [HttpGet]
    public async Task<ActionResult<List<DiaryEntryResponse>>> ListDiaryEntries(Guid sheetId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var entries = await db.DiaryEntries
            .Where(d => d.CharacterSheetId == sheetId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var responses = new List<DiaryEntryResponse>();
        foreach (var entry in entries)
            responses.Add(await ToResponseAsync(entry));
        return responses;
    }

    [HttpPut("{entryId}")]
    public async Task<IActionResult> UpdateDiaryEntry(Guid sheetId, Guid entryId, UpdateDiaryEntryRequest request)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var entry = await db.DiaryEntries.FirstOrDefaultAsync(d => d.Id == entryId && d.CharacterSheetId == sheetId);
        if (entry is null)
            return NotFound();

        if (!TryParseImageIds(request.ImageIds, out var imageIds))
            return BadRequest("Um dos identificadores de imagem informados é inválido.");

        if (!await OwnsAllImagesAsync(imageIds, CurrentUserId()))
            return BadRequest("Imagem não encontrada.");

        entry.Texto = request.Texto;

        var existingImages = await db.DiaryEntryImages.Where(i => i.DiaryEntryId == entryId).ToListAsync();
        db.DiaryEntryImages.RemoveRange(existingImages);
        foreach (var imageId in imageIds)
            db.DiaryEntryImages.Add(new DiaryEntryImage { DiaryEntryId = entryId, ImageId = imageId });

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{entryId}")]
    public async Task<IActionResult> DeleteDiaryEntry(Guid sheetId, Guid entryId)
    {
        var authError = await CheckAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var entry = await db.DiaryEntries.FirstOrDefaultAsync(d => d.Id == entryId && d.CharacterSheetId == sheetId);
        if (entry is null)
            return NotFound();

        db.DiaryEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Gates every action — reads included. Diary visibility for a character sheet is "both the
    /// owning player and the campaign's GM can read" (unlike the campaign diary, which is GM-only),
    /// and write access follows the same population — so a single CanEdit check covers both.
    /// </summary>
    private async Task<ActionResult?> CheckAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        return null;
    }

    /// <summary>
    /// R0010-equivalent guard for diary images: every referenced image must exist and be owned
    /// by the calling user, or attaching another user's (or a nonexistent) image Guid must fail
    /// with a controlled 400 rather than an FK-violation 500. Matches CampaignsController's
    /// OwnsAllImagesAsync, but checked against the actual caller (owner or GM) rather than always
    /// the campaign's GM, since either can author an entry here.
    /// </summary>
    private async Task<bool> OwnsAllImagesAsync(List<Guid> imageIds, Guid userId)
    {
        if (imageIds.Count == 0)
            return true;

        var ownedCount = await db.Images.CountAsync(i => imageIds.Contains(i.Id) && i.UploadedByUserId == userId);
        return ownedCount == imageIds.Count;
    }

    private async Task<DiaryEntryResponse> ToResponseAsync(DiaryEntry entry)
    {
        var imageIds = await db.DiaryEntryImages.Where(i => i.DiaryEntryId == entry.Id).Select(i => i.ImageId).ToListAsync();
        var images = await db.Images.Where(i => imageIds.Contains(i.Id)).ToListAsync();
        return new DiaryEntryResponse(entry.Id.ToString(), entry.Texto, entry.CreatedAt, images.Select(i => $"/images/{i.Path}").ToList());
    }

    private static bool TryParseImageIds(List<string> rawImageIds, out List<Guid> imageIds)
    {
        imageIds = new List<Guid>(rawImageIds.Count);
        foreach (var raw in rawImageIds)
        {
            if (!Guid.TryParse(raw, out var parsed))
            {
                imageIds = [];
                return false;
            }

            imageIds.Add(parsed);
        }

        return true;
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
