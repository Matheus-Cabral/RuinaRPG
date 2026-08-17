using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Domain.Images;
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
    public async Task<ActionResult<ImageUploadResponse>> Upload(IFormFile file)
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

        var extension = Path.GetExtension(file.FileName) is { Length: > 0 } ext ? ext : ".bin";
        var fileName = await fileStore.SaveAsync(bytes, extension);

        var image = new Infrastructure.Images.Image
        {
            Id = Guid.NewGuid(),
            Path = fileName,
            ContentType = file.ContentType,
            UploadedByUserId = CurrentUserId(),
            CreatedAt = DateTime.UtcNow
        };
        db.Images.Add(image);
        await db.SaveChangesAsync();

        return Created(string.Empty, new ImageUploadResponse(image.Id.ToString(), $"/{fileName}"));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<List<ImageSummaryResponse>>> Mine()
    {
        var userId = CurrentUserId();
        return await db.Images
            .Where(i => i.UploadedByUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ImageSummaryResponse(i.Id.ToString(), $"/{i.Path}", i.CreatedAt))
            .ToListAsync();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
