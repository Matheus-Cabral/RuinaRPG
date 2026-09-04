using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Domain.Images;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/images")]
[Authorize]
public class ImagesController(RuinaRpgDbContext db, IImageFileStore fileStore, IOptions<ImageStorageOptions> options) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ImageUploadResponse>> Upload(IFormFile file, [FromForm] string? campaignId = null)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        var maxSizeBytes = options.Value.MaxSizeMb * 1024 * 1024;
        var validation = ImageValidator.Validate(bytes, maxSizeBytes);
        if (validation != ImageValidationResult.Valid)
        {
            return BadRequest(validation == ImageValidationResult.TooLarge
                ? $"A imagem excede o tamanho máximo de {options.Value.MaxSizeMb}MB."
                : "Formato de imagem não suportado. Use WebP, JPEG, JPG, PNG ou GIF.");
        }

        // The on-disk extension and stored ContentType are derived from the magic-number format
        // that just validated these bytes — never from the client-supplied file.FileName or
        // file.ContentType. The images volume is served directly by nginx with default mime-type
        // sniffing, so trusting the client's claimed extension would let valid image bytes be
        // saved under an attacker-chosen extension (e.g. ".html") and served same-origin as that
        // content type — stored XSS.
        var format = ImageValidator.DetectFormat(bytes)!.Value;
        var fileName = await fileStore.SaveAsync(bytes, format.ToFileExtension());

        var image = new Infrastructure.Images.Image
        {
            Id = Guid.NewGuid(),
            Path = fileName,
            ContentType = format.ToMimeType(),
            UploadedByUserId = CurrentUserId(),
            CreatedAt = DateTime.UtcNow
        };
        db.Images.Add(image);

        // Requisitos - Campanha R0012: an image a Jogador uploads is auto-attached to that
        // campaign as public, no GM approval step. GM uploads never auto-attach — the GM's
        // existing Anexos flow attaches explicitly and defaults to private (R0008). A malformed
        // or non-member campaignId is silently ignored — it's best-effort context, not a
        // requirement of the upload itself.
        if (campaignId is not null && Guid.TryParse(campaignId, out var parsedCampaignId) && User.IsInRole("Jogador"))
        {
            var callerId = CurrentUserId();
            var isMember = await db.CampaignMembers.AnyAsync(m => m.CampaignId == parsedCampaignId && m.UserId == callerId);
            if (isMember)
            {
                db.CampaignAttachments.Add(new CampaignAttachment
                {
                    Id = Guid.NewGuid(),
                    CampaignId = parsedCampaignId,
                    ImageId = image.Id,
                    IsPublic = true
                });
            }
        }

        await db.SaveChangesAsync();

        return Created(string.Empty, new ImageUploadResponse(image.Id.ToString(), $"/images/{fileName}"));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<ImageSummaryResponse>>> Mine()
    {
        var userId = CurrentUserId();
        return await db.Images
            .Where(i => i.UploadedByUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ImageSummaryResponse(i.Id.ToString(), $"/images/{i.Path}", i.CreatedAt))
            .ToListAsync();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
