# Catálogo de Itens e Equipamentos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a GM build a curated library of items (Item Geral, Arma, Armadura, Escudo, Artefato) with type-specific fields, filterable listing, and image uploads. Along the way this plan builds the **shared image upload pipeline** (`Image` entity, magic-bytes validation, disk storage) that every later plan needing an image field — Ficha de Personagem, NPCs, Criaturas, Campanha diary entries — will reuse rather than reimplement.

**Architecture:** `Item` is EF Core Table-Per-Hierarchy (Modelo de Dados §3 names this explicitly): an abstract `Item` base class plus five concrete subclasses (`ItemGeral`, `Arma`, `Armadura`, `Escudo`, `Artefato`), one physical table, `Tipo` as the discriminator column. Type-specific fields are ordinary properties on each subclass — nonexistent on other subclasses, which is exactly the "campo não se aplica → travessão" convention the spec describes, enforced by the C# type system rather than runtime null-checks. `Image` is a separate, already-partially-plumbed entity (the Foundation plan's `docker-compose.yml` already mounts a named `images` volume into both `api` (`/images`, read-write) and `nginx` (`/usr/share/nginx/html/images`, read-only), and nginx's catch-all `location /` block already serves any file under its web root before falling back to the SPA — so an uploaded image at container path `/images/<guid>.<ext>` is reachable at `GET /<guid>.images/<guid>.ext`... concretely: files are saved under `/images/`, and requests to `/images/<guid>.<ext>` are served directly by nginx, no new nginx config needed). Validation (Técnico R0005) checks real file bytes (magic numbers), not the filename extension, and enforces `img_max_size`. GM-only write endpoints; the image upload endpoint itself is NOT GM-only, since later plans (a player's own character portrait, diary images) need players to upload too — this plan only wires the GM-only *Item* endpoints, but builds the upload endpoint generically from the start.

**Tech Stack:** Same as established. No new packages — magic-byte sniffing is a handful of `byte[]` comparisons, no library needed.

**Spec:** `Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md` (all 10 requirements), `Docs/Requisitos/Requisitos - Técnico.md` R0001 (image storage), R0003 (`img_max_size`, `Storage__ImagesPath`), R0005 (upload validation), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §2 (Images) and §3 (Items).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Only the **GM** may create, edit, or delete Items (Catálogo preamble, "Modelo de edição") — `[Authorize(Roles = "GM")]` on every Item write and list endpoint. Uploading an image is a separate, non-GM-restricted concern (see Architecture).
- `Tipo` is immutable after creation (R0002) — no endpoint may change it; changing type means delete-and-recreate.
- Every item is scoped to the GM who created it (`GmId`) — never return or act on another GM's items (same isolation pattern as `InviteCodesController`/`PlayersController` from the Convite de Jogador plan).
- Image upload validates real file bytes (magic numbers) for WebP/JPEG/JPG/PNG/GIF, never trusting the filename extension or client-supplied `Content-Type` alone, and enforces the `img_max_size` env var (R0005). Every field default/NULL convention in this plan follows the Catálogo doc's preamble: NULL or not-applicable-to-this-Tipo displays as a travessão (—) on the Client.
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Domain — image format and size validation

**Files:**
- Create: `src/RuinaRPG.Domain/Images/ImageValidationResult.cs`
- Create: `src/RuinaRPG.Domain/Images/ImageValidator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Images/ImageValidatorTests.cs`

**Interfaces:**
- Produces: `enum ImageValidationResult { Valid, UnsupportedFormat, TooLarge }`; `ImageValidator.Validate(byte[] fileBytes, int maxSizeBytes) : ImageValidationResult`. Task 3 (upload endpoint) calls this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Images/ImageValidatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Images;

namespace RuinaRPG.Tests.Unit.Images;

public class ImageValidatorTests
{
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] JpegMagicBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] GifMagicBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] WebpMagicBytes = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] NotAnImage = "this is plain text, not an image"u8.ToArray();

    [Theory]
    [MemberData(nameof(SupportedFormats))]
    public void Validate_accepts_every_supported_format_within_the_size_limit(byte[] magicBytes)
    {
        var result = ImageValidator.Validate(magicBytes, maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.Valid);
    }

    public static IEnumerable<object[]> SupportedFormats()
    {
        yield return [PngMagicBytes];
        yield return [JpegMagicBytes];
        yield return [GifMagicBytes];
        yield return [WebpMagicBytes];
    }

    [Fact]
    public void Validate_rejects_bytes_that_dont_match_any_supported_magic_number()
    {
        var result = ImageValidator.Validate(NotAnImage, maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.UnsupportedFormat);
    }

    [Fact]
    public void Validate_ignores_a_spoofed_extension_and_checks_real_bytes()
    {
        // The caller never passes a filename/extension to this method at all — this test documents
        // that fact: even "malicious.png" bytes that are actually plain text still fail validation,
        // because Validate only ever looks at the bytes, never a claimed name or Content-Type.
        var result = ImageValidator.Validate(NotAnImage, maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.UnsupportedFormat);
    }

    [Fact]
    public void Validate_rejects_a_valid_format_that_exceeds_the_size_limit()
    {
        var result = ImageValidator.Validate(PngMagicBytes, maxSizeBytes: 5);

        result.Should().Be(ImageValidationResult.TooLarge);
    }

    [Fact]
    public void Validate_checks_size_before_declaring_format_unsupported_is_irrelevant_when_both_fail()
    {
        // Format is checked first; a too-small buffer that doesn't even contain a full magic number
        // is UnsupportedFormat, not a false Valid.
        var result = ImageValidator.Validate([0x00, 0x01], maxSizeBytes: 1_000_000);

        result.Should().Be(ImageValidationResult.UnsupportedFormat);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter ImageValidatorTests`
Expected: FAIL to compile — `ImageValidationResult`/`ImageValidator` don't exist yet.

- [ ] **Step 3: Write `ImageValidationResult`**

`src/RuinaRPG.Domain/Images/ImageValidationResult.cs`:

```csharp
namespace RuinaRPG.Domain.Images;

public enum ImageValidationResult
{
    Valid,
    UnsupportedFormat,
    TooLarge
}
```

- [ ] **Step 4: Write `ImageValidator`**

`src/RuinaRPG.Domain/Images/ImageValidator.cs`:

```csharp
namespace RuinaRPG.Domain.Images;

public static class ImageValidator
{
    public static ImageValidationResult Validate(byte[] fileBytes, int maxSizeBytes)
    {
        if (!IsSupportedFormat(fileBytes))
            return ImageValidationResult.UnsupportedFormat;

        if (fileBytes.Length > maxSizeBytes)
            return ImageValidationResult.TooLarge;

        return ImageValidationResult.Valid;
    }

    private static bool IsSupportedFormat(byte[] bytes) =>
        StartsWith(bytes, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) // PNG
        || StartsWith(bytes, [0xFF, 0xD8, 0xFF]) // JPEG/JPG
        || StartsWith(bytes, [0x47, 0x49, 0x46, 0x38]) // GIF (GIF87a/GIF89a)
        || IsWebp(bytes);

    private static bool IsWebp(byte[] bytes) =>
        bytes.Length >= 12
        && StartsWith(bytes, [0x52, 0x49, 0x46, 0x46]) // "RIFF"
        && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50; // "WEBP"

    private static bool StartsWith(byte[] bytes, byte[] prefix) =>
        bytes.Length >= prefix.Length && prefix.AsSpan().SequenceEqual(bytes.AsSpan(0, prefix.Length));
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter ImageValidatorTests`
Expected: PASS (8/8, counting the 4 `[Theory]` cases).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/Images tests/RuinaRPG.Tests.Unit/Images/ImageValidatorTests.cs
git commit -m "feat: add magic-byte image format and size validation"
```

---

### Task 2: Infrastructure — `Image` entity and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Images/Image.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/ImageMigrationTests.cs`

**Interfaces:**
- Produces: `Image` (`Guid Id`, `string Path`, `string ContentType`, `Guid UploadedByUserId`, `DateTime CreatedAt`), `RuinaRpgDbContext.Images : DbSet<Image>`. Task 3 (upload endpoint) and Task 6 (`Item.ImageId`) depend on this shape.

- [ ] **Step 1: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/ImageMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class ImageMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public ImageMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Images_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddImages"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@imgtest.com", Email = "gm@imgtest.com", Nickname = "ImgTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.Images.Add(new Image { Id = Guid.NewGuid(), Path = "abc123.png", ContentType = "image/png", UploadedByUserId = gm.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        (await db.Images.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ImageMigrationTests`
Expected: FAIL — no `AddImages` migration exists yet.

- [ ] **Step 3: Write `Image`**

`src/RuinaRPG.Infrastructure/Images/Image.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Images;

public class Image
{
    public Guid Id { get; set; }
    public required string Path { get; set; }
    public required string ContentType { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 4: Register the DbSet in `RuinaRpgDbContext`**

Add `using RuinaRPG.Infrastructure.Images;` and:

```csharp
public DbSet<Image> Images => Set<Image>();
```

Inside `OnModelCreating`, after the existing entity configuration blocks:

```csharp
builder.Entity<Image>(entity =>
{
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(i => i.UploadedByUserId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddImages \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ImageMigrationTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Images src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/ImageMigrationTests.cs
git commit -m "feat: add Image entity and migration"
```

---

### Task 3: Infrastructure + Api — image upload and listing endpoints (R0010)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Images/ImageStorageOptions.cs`
- Create: `src/RuinaRPG.Infrastructure/Images/IImageFileStore.cs`
- Create: `src/RuinaRPG.Infrastructure/Images/DiskImageFileStore.cs`
- Create: `src/RuinaRPG.Contracts/Images/ImageUploadResponse.cs`
- Create: `src/RuinaRPG.Contracts/Images/ImageSummaryResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/ImagesController.cs`
- Modify: `src/RuinaRPG.Api/Program.cs`
- Modify: `.env.example`
- Modify: `docker-compose.yml`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/ImagesControllerTests.cs`

**Interfaces:**
- Consumes: `ImageValidator.Validate` (Task 1), `Image`/`RuinaRpgDbContext.Images` (Task 2).
- Produces: `POST /api/images` (multipart form, field name `file`) → `201` + `ImageUploadResponse(string Id, string Url)`, `400` on invalid format/size, `[Authorize]` (any authenticated role); `GET /api/images/mine` → `200` + `List<ImageSummaryResponse>`, the caller's own previously-uploaded images (newest first), `[Authorize]` — this is what makes "reference instead of re-upload" possible (R0010): the GM side of R0010 ("pode referenciar a Imagem de qualquer item, NPC ou Criatura seus") is satisfied by referencing anything *that GM already uploaded*, since every image an item/NPC/Criatura of theirs displays was necessarily uploaded by them first. `ImageSummaryResponse(string Id, string Url, DateTime CreatedAt)`. `IImageFileStore.SaveAsync(byte[] bytes, string extension) : Task<string>` (returns the relative path written) — a seam later plans' tests can fake if they ever need to avoid real disk I/O, though this plan's own tests use the real `DiskImageFileStore` against a temp directory. The **Jogador** side of R0010 (referencing an image made visible through campaign attachments, from a different GM's item) is out of scope here — it depends on `Campanha`'s visibility rules and is listed under "Explicitly out of scope" at the end of this plan.

- [ ] **Step 1: Add the config surface**

`src/RuinaRPG.Infrastructure/Images/ImageStorageOptions.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Images;

public class ImageStorageOptions
{
    public required string ImagesPath { get; set; }
    public int MaxSizeMb { get; set; }
}
```

In `.env.example`, add near the other configuration:

```
IMG_MAX_SIZE_MB=10
```

In `docker-compose.yml`, inside the `api` service's `environment:` block, add:

```yaml
      Img__MaxSizeMb: ${IMG_MAX_SIZE_MB}
```

(`Storage__ImagesPath: /images` is already present from the Foundation plan — no change needed there.)

- [ ] **Step 2: Write `IImageFileStore` and `DiskImageFileStore`**

`src/RuinaRPG.Infrastructure/Images/IImageFileStore.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Images;

public interface IImageFileStore
{
    Task<string> SaveAsync(byte[] bytes, string extension);
}
```

`src/RuinaRPG.Infrastructure/Images/DiskImageFileStore.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace RuinaRPG.Infrastructure.Images;

public class DiskImageFileStore(IOptions<ImageStorageOptions> options) : IImageFileStore
{
    public async Task<string> SaveAsync(byte[] bytes, string extension)
    {
        Directory.CreateDirectory(options.Value.ImagesPath);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(options.Value.ImagesPath, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes);
        return fileName;
    }
}
```

- [ ] **Step 3: Write the response contract**

`src/RuinaRPG.Contracts/Images/ImageUploadResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Images;

public record ImageUploadResponse(string Id, string Url);
```

`src/RuinaRPG.Contracts/Images/ImageSummaryResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Images;

public record ImageSummaryResponse(string Id, string Url, DateTime CreatedAt);
```

- [ ] **Step 4: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/ImagesControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Images;

namespace RuinaRPG.Tests.Integration.Controllers;

public class ImagesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    public ImagesControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private static MultipartFormDataContent BuildUpload(byte[] bytes, string fileName = "test.png")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/images", BuildUpload(PngBytes));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_a_valid_png_returns_201_with_a_usable_url()
    {
        var token = await RegisterGmAndGetTokenAsync("ImageGm1", "image1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        body!.Url.Should().EndWith(".png");
    }

    [Fact]
    public async Task Upload_bytes_that_are_not_a_real_image_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ImageGm2", "image2@teste.com");
        var fakeBytes = "not an image"u8.ToArray();
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(fakeBytes, "fake.png") };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mine_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/images/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_returns_only_images_uploaded_by_the_caller_R0010()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("ImageGm3", "image3@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("ImageGm4", "image4@teste.com");
        var uploadA = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        uploadA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await _client.SendAsync(uploadA);
        var uploadB = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes) };
        uploadB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await _client.SendAsync(uploadB);

        var mineMessage = new HttpRequestMessage(HttpMethod.Get, "/api/images/mine");
        mineMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(mineMessage);

        var body = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        body.Should().ContainSingle();
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ImagesControllerTests`
Expected: FAIL — `/api/images` doesn't exist yet.

- [ ] **Step 6: Write `ImagesController`**

`src/RuinaRPG.Api/Controllers/ImagesController.cs`:

```csharp
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
```

- [ ] **Step 7: Register configuration and services**

In `src/RuinaRPG.Api/Program.cs`, add:

```csharp
builder.Services.Configure<ImageStorageOptions>(options =>
{
    options.ImagesPath = builder.Configuration["Storage:ImagesPath"] ?? "/images";
    options.MaxSizeMb = int.Parse(builder.Configuration["Img:MaxSizeMb"] ?? "10");
});
builder.Services.AddScoped<IImageFileStore, DiskImageFileStore>();
```

Add `using RuinaRPG.Infrastructure.Images;` to the top of the file.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ImagesControllerTests`
Expected: PASS (5/5) — `ApiFactory`'s test configuration needs `Storage:ImagesPath` to resolve to a writable path; if `ApiFactory` doesn't already set one, it falls back to `/images` per Step 7's default, which won't be writable in the test container. If Step 8 fails specifically on a directory-permission error (not a validation error), check `tests/RuinaRPG.Tests.Integration/ApiFactory.cs` for how it overrides configuration and add a `Storage:ImagesPath` override pointing at a temp directory (e.g. `Path.Combine(Path.GetTempPath(), "ruinarpg-test-images")`), following whatever pattern that file already uses for other test-only overrides.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Images src/RuinaRPG.Contracts/Images src/RuinaRPG.Api/Controllers/ImagesController.cs src/RuinaRPG.Api/Program.cs .env.example docker-compose.yml tests/RuinaRPG.Tests.Integration/Controllers/ImagesControllerTests.cs
git commit -m "feat: add image upload endpoint with magic-byte validation"
```

---

### Task 4: Domain — Item enums

**Files:**
- Create: `src/RuinaRPG.Domain/Items/ItemTipo.cs`
- Create: `src/RuinaRPG.Domain/Items/Tier.cs`
- Create: `src/RuinaRPG.Domain/Items/Empunhadura.cs`
- Create: `src/RuinaRPG.Domain/Items/TipoDeDano.cs`
- Create: `src/RuinaRPG.Domain/Items/CategoriaProtecao.cs`
- Create: `src/RuinaRPG.Domain/Items/TipoDeAlvo.cs`

**Interfaces:**
- Produces: 6 enums consumed by Task 6 (`Item` entity family) and every later task in this plan. No tests — enums have no behavior; their correctness is proven by the entities and controllers that use them compiling and passing their own tests.

- [ ] **Step 1: Write the enums**

`src/RuinaRPG.Domain/Items/ItemTipo.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

public enum ItemTipo
{
    ItemGeral,
    Arma,
    Armadura,
    Escudo,
    Artefato
}
```

`src/RuinaRPG.Domain/Items/Tier.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

public enum Tier
{
    F,
    E,
    D,
    C,
    B,
    A,
    S
}
```

`src/RuinaRPG.Domain/Items/Empunhadura.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

public enum Empunhadura
{
    UmaMao,
    DuasMaos
}
```

`src/RuinaRPG.Domain/Items/TipoDeDano.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

public enum TipoDeDano
{
    Cortante,
    Perfurante,
    Contundente,
    Magico
}
```

`src/RuinaRPG.Domain/Items/CategoriaProtecao.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

public enum CategoriaProtecao
{
    Leve,
    Medio,
    Pesada
}
```

`src/RuinaRPG.Domain/Items/TipoDeAlvo.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

public enum TipoDeAlvo
{
    Atributo,
    Pericia,
    SubAtributo,
    Dano
}
```

- [ ] **Step 2: Verify the Domain project still builds**

Run: `dotnet build src/RuinaRPG.Domain`
Expected: `Build succeeded`, 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Domain/Items
git commit -m "feat: add item type enums"
```

---

### Task 5: Infrastructure — `Item` entity family (TPH) and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Items/Item.cs`
- Create: `src/RuinaRPG.Infrastructure/Items/ItemGeral.cs`
- Create: `src/RuinaRPG.Infrastructure/Items/Arma.cs`
- Create: `src/RuinaRPG.Infrastructure/Items/Armadura.cs`
- Create: `src/RuinaRPG.Infrastructure/Items/Escudo.cs`
- Create: `src/RuinaRPG.Infrastructure/Items/Artefato.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/ItemMigrationTests.cs`

**Interfaces:**
- Consumes: `ItemTipo`, `Tier`, `Empunhadura`, `TipoDeDano`, `CategoriaProtecao`, `TipoDeAlvo` (Task 4), `Image` (Task 2).
- Produces: abstract `Item` (`Guid Id`, `Guid GmId`, `string Nome`, `decimal Peso`, `int Preco`, `Guid? ImageId`) and 5 subclasses matching Modelo de Dados §3's per-type columns exactly (see Step 1). `RuinaRpgDbContext.Items : DbSet<Item>`, `RuinaRpgDbContext.Set<Arma>()` etc. for typed queries. Tasks 6-9 depend on this shape exactly.

- [ ] **Step 1: Write the entity family**

`src/RuinaRPG.Infrastructure/Items/Item.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Items;

public abstract class Item
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public decimal Peso { get; set; }
    public int Preco { get; set; }
    public Guid? ImageId { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Items/ItemGeral.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Items;

public class ItemGeral : Item
{
    public string? Subcategoria { get; set; }
    public string? Descricao { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Items/Arma.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Arma : Item
{
    public string? Subcategoria { get; set; }
    public Tier? Tier { get; set; }
    public Empunhadura? Empunhadura { get; set; }
    public string? Dados { get; set; }
    public int? Dano { get; set; }
    public string? Critico { get; set; }
    public int? Alcance { get; set; }
    public TipoDeDano? TipoDeDano { get; set; }
    public string? RequisitoAtributo { get; set; }
    public int? DurabilidadeMaxima { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Items/Armadura.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Armadura : Item
{
    public CategoriaProtecao? Categoria { get; set; }
    public int? Defesa { get; set; }
    public int? RF { get; set; }
    public int? RM { get; set; }
    public string? Penalidade { get; set; }
    public int? RequisitoVigor { get; set; }
    public int? DurabilidadeMaxima { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Items/Escudo.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Escudo : Item
{
    public CategoriaProtecao? Categoria { get; set; }
    public int? BonusDefesa { get; set; }
    public string? Penalidade { get; set; }
    public int? RequisitoVigor { get; set; }
    public int? DurabilidadeMaxima { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Items/Artefato.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Artefato : Item
{
    public TipoDeAlvo? TipoDeAlvo { get; set; }
    public string? Alvo { get; set; }
    public int? Valor { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/ItemMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class ItemMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public ItemMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_one_Items_table_holding_all_five_types()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddItems"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@itemtest.com", Email = "gm@itemtest.com", Nickname = "ItemTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.Set<Arma>().Add(new Arma { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Espada Curta", Peso = 1.5m, Preco = 50, Tier = Tier.F, Dano = 3 });
        db.Set<ItemGeral>().Add(new ItemGeral { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Corda", Peso = 0.5m, Preco = 5 });
        await db.SaveChangesAsync();

        (await db.Items.CountAsync()).Should().Be(2);
        (await db.Set<Arma>().CountAsync()).Should().Be(1);
        (await db.Set<ItemGeral>().CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemMigrationTests`
Expected: FAIL — `Item`/`Arma`/`ItemGeral` don't compile yet until Step 1 is saved; once saved, fails on the missing `AddItems` migration.

- [ ] **Step 4: Configure TPH in `RuinaRpgDbContext`**

Add `using RuinaRPG.Infrastructure.Items;` and:

```csharp
public DbSet<Item> Items => Set<Item>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<Item>(entity =>
{
    entity.HasDiscriminator<string>("Tipo")
        .HasValue<ItemGeral>("ItemGeral")
        .HasValue<Arma>("Arma")
        .HasValue<Armadura>("Armadura")
        .HasValue<Escudo>("Escudo")
        .HasValue<Artefato>("Artefato");
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(i => i.GmId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Image>()
        .WithMany()
        .HasForeignKey(i => i.ImageId)
        .OnDelete(DeleteBehavior.SetNull);
});
```

The discriminator is a plain `string`, not the `ItemTipo` enum directly — EF Core's `HasDiscriminator<TDiscriminator>` needs a type it can compare with `==` cheaply at the SQL level, and a string column matching each subclass's simple name is the most transparent choice for a column a DBA might read directly; the Api layer (Task 7-8) is what exposes `ItemTipo` to callers, mapping to/from this string.

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddItems \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemMigrationTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Items src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/ItemMigrationTests.cs
git commit -m "feat: add Item TPH entity family and migration"
```

---

### Task 6: Contracts + Api — create an item (R0002-R0006, R0009)

**Files:**
- Create: `src/RuinaRPG.Contracts/Items/CreateItemRequest.cs`
- Create: `src/RuinaRPG.Contracts/Items/ItemResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/ItemsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs`

**Interfaces:**
- Consumes: `Item` family (Task 5).
- Produces: `POST /api/items` → `201` + `ItemResponse`, `[Authorize(Roles = "GM")]`. `CreateItemRequest` is one flat record covering every field of every type (nullable except `Tipo`, `Nome`, `Peso`, `Preco`) — the GM's create form (Task 11) only sends the fields relevant to the chosen `Tipo`; anything else is ignored server-side. `ItemResponse` mirrors it, plus `Id` and a resolved `ImageUrl`. Tasks 7-9 and Task 10-11 (Client) depend on both shapes exactly.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/Items/CreateItemRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Items;

public record CreateItemRequest(
    string Tipo,
    string Nome,
    decimal Peso,
    int Preco,
    string? ImageId,
    string? Subcategoria,
    string? Descricao,
    string? Tier,
    string? Empunhadura,
    string? Dados,
    int? Dano,
    string? Critico,
    int? Alcance,
    string? TipoDeDano,
    string? RequisitoAtributo,
    int? DurabilidadeMaxima,
    string? Categoria,
    int? Defesa,
    int? RF,
    int? RM,
    string? Penalidade,
    int? RequisitoVigor,
    int? BonusDefesa,
    string? TipoDeAlvo,
    string? Alvo,
    int? Valor);
```

`src/RuinaRPG.Contracts/Items/ItemResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Items;

public record ItemResponse(
    string Id,
    string Tipo,
    string Nome,
    decimal Peso,
    int Preco,
    string? ImageUrl,
    string? Subcategoria,
    string? Descricao,
    string? Tier,
    string? Empunhadura,
    string? Dados,
    int? Dano,
    string? Critico,
    int? Alcance,
    string? TipoDeDano,
    string? RequisitoAtributo,
    int? DurabilidadeMaxima,
    string? Categoria,
    int? Defesa,
    int? RF,
    int? RM,
    string? Penalidade,
    int? RequisitoVigor,
    int? BonusDefesa,
    string? TipoDeAlvo,
    string? Alvo,
    int? Valor);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Tests.Integration.Controllers;

public class ItemsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ItemsControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private static CreateItemRequest MinimalItemGeral(string nome) =>
        new("ItemGeral", nome, 0.5m, 5, null, "Equipamentos de Aventura", "Uma corda resistente.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null);

    private static CreateItemRequest MinimalArma(string nome) =>
        new("Arma", nome, 1.5m, 50, null, "Espadas", null,
            "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, 10,
            null, null, null, null, null, null,
            null, null, null, null);

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/items", MinimalItemGeral("Corda"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_as_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ItemGm1", "item1@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "ItemJogador1", "itemjogador1@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalItemGeral("Corda")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jogadorToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_an_ItemGeral_returns_201_with_only_ItemGeral_fields_set()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm2", "item2@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalItemGeral("Corda")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("ItemGeral");
        body.Nome.Should().Be("Corda");
        body.Subcategoria.Should().Be("Equipamentos de Aventura");
        body.Dano.Should().BeNull();
    }

    [Fact]
    public async Task Create_an_Arma_returns_201_with_arma_specific_fields_set()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm3", "item3@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalArma("Espada Curta")) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.Tipo.Should().Be("Arma");
        body.Tier.Should().Be("F");
        body.Dano.Should().Be(3);
        body.Subcategoria.Should().Be("Espadas");
    }

    [Fact]
    public async Task Create_with_an_unknown_Tipo_returns_400()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGm4", "item4@teste.com");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(MinimalItemGeral("Corda") with { Tipo = "NaoExiste" }) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemsControllerTests`
Expected: FAIL — `/api/items` doesn't exist yet.

- [ ] **Step 4: Write `ItemsController` (create action only for now)**

`src/RuinaRPG.Api/Controllers/ItemsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/items")]
[Authorize(Roles = "GM")]
public class ItemsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ItemResponse>> Create(CreateItemRequest request)
    {
        if (!Enum.TryParse<ItemTipo>(request.Tipo, out var tipo))
            return BadRequest("Tipo de item desconhecido.");

        var gmId = CurrentGmId();
        Guid? imageId = request.ImageId is not null ? Guid.Parse(request.ImageId) : null;

        Item item = tipo switch
        {
            ItemTipo.ItemGeral => new ItemGeral { Subcategoria = request.Subcategoria, Descricao = request.Descricao },
            ItemTipo.Arma => new Arma
            {
                Subcategoria = request.Subcategoria,
                Tier = ParseEnum<Tier>(request.Tier),
                Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura),
                Dados = request.Dados,
                Dano = request.Dano,
                Critico = request.Critico,
                Alcance = request.Alcance,
                TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano),
                RequisitoAtributo = request.RequisitoAtributo,
                DurabilidadeMaxima = request.DurabilidadeMaxima
            },
            ItemTipo.Armadura => new Armadura
            {
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                Defesa = request.Defesa,
                RF = request.RF,
                RM = request.RM,
                Penalidade = request.Penalidade,
                RequisitoVigor = request.RequisitoVigor,
                DurabilidadeMaxima = request.DurabilidadeMaxima
            },
            ItemTipo.Escudo => new Escudo
            {
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                BonusDefesa = request.BonusDefesa,
                Penalidade = request.Penalidade,
                RequisitoVigor = request.RequisitoVigor,
                DurabilidadeMaxima = request.DurabilidadeMaxima
            },
            ItemTipo.Artefato => new Artefato
            {
                TipoDeAlvo = ParseEnum<TipoDeAlvo>(request.TipoDeAlvo),
                Alvo = request.Alvo,
                Valor = request.Valor
            },
            _ => throw new InvalidOperationException("Unreachable — Tipo already validated above.")
        };

        item.Id = Guid.NewGuid();
        item.GmId = gmId;
        item.Nome = request.Nome;
        item.Peso = request.Peso;
        item.Preco = request.Preco;
        item.ImageId = imageId;

        db.Items.Add(item);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(item));
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is not null && Enum.TryParse<TEnum>(value, out var parsed) ? parsed : null;

    private async Task<ItemResponse> ToResponseAsync(Item item)
    {
        string? imageUrl = null;
        if (item.ImageId is not null)
        {
            var image = await db.Images.FindAsync(item.ImageId.Value);
            imageUrl = image is not null ? $"/{image.Path}" : null;
        }

        return item switch
        {
            ItemGeral g => new ItemResponse(g.Id.ToString(), "ItemGeral", g.Nome, g.Peso, g.Preco, imageUrl,
                g.Subcategoria, g.Descricao, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null),
            Arma a => new ItemResponse(a.Id.ToString(), "Arma", a.Nome, a.Peso, a.Preco, imageUrl,
                a.Subcategoria, null, a.Tier?.ToString(), a.Empunhadura?.ToString(), a.Dados, a.Dano, a.Critico, a.Alcance, a.TipoDeDano?.ToString(), a.RequisitoAtributo,
                a.DurabilidadeMaxima, null, null, null, null, null, null, null, null, null),
            Armadura ar => new ItemResponse(ar.Id.ToString(), "Armadura", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null,
                ar.Categoria?.ToString(), ar.Defesa, ar.RF, ar.RM, ar.Penalidade, ar.RequisitoVigor, null, null, null, null),
            Escudo e => new ItemResponse(e.Id.ToString(), "Escudo", e.Nome, e.Peso, e.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null,
                e.Categoria?.ToString(), null, null, null, e.Penalidade, e.RequisitoVigor, e.BonusDefesa, null, null, null),
            Artefato ar => new ItemResponse(ar.Id.ToString(), "Artefato", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, ar.TipoDeAlvo?.ToString(), ar.Alvo, ar.Valor),
            _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
        };
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemsControllerTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Items src/RuinaRPG.Api/Controllers/ItemsController.cs tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs
git commit -m "feat: add item creation across all five types"
```

---

### Task 7: Api — list items with filters (R0001)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/ItemsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs`

**Interfaces:**
- Consumes: `ItemsController` (Task 6).
- Produces: `GET /api/items?tipo=&subcategoria=&tier=&categoria=&tipoDeDano=` → `200` + `List<ItemResponse>`, scoped to the authenticated GM's own items, `[Authorize(Roles = "GM")]`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs`, inside the class:

```csharp
private async Task<HttpResponseMessage> PostItemAsync(string token, CreateItemRequest request)
{
    var message = new HttpRequestMessage(HttpMethod.Post, "/api/items") { Content = JsonContent.Create(request) };
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await _client.SendAsync(message);
}

[Fact]
public async Task List_returns_only_items_created_by_the_authenticated_gm()
{
    var tokenA = await RegisterGmAndGetTokenAsync("ItemGmA", "itemgma@teste.com");
    var tokenB = await RegisterGmAndGetTokenAsync("ItemGmB", "itemgmb@teste.com");
    await PostItemAsync(tokenA, MinimalItemGeral("Corda A"));
    await PostItemAsync(tokenB, MinimalItemGeral("Corda B"));

    var message = new HttpRequestMessage(HttpMethod.Get, "/api/items");
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
    var response = await _client.SendAsync(message);

    var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
    body!.Should().ContainSingle(i => i.Nome == "Corda A");
}

[Fact]
public async Task List_can_filter_by_Tipo()
{
    var token = await RegisterGmAndGetTokenAsync("ItemGmFilter1", "itemfilter1@teste.com");
    await PostItemAsync(token, MinimalItemGeral("Corda"));
    await PostItemAsync(token, MinimalArma("Espada"));

    var message = new HttpRequestMessage(HttpMethod.Get, "/api/items?tipo=Arma");
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var response = await _client.SendAsync(message);

    var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
    body!.Should().OnlyContain(i => i.Tipo == "Arma");
}

[Fact]
public async Task List_can_filter_by_Subcategoria_and_Tier_together()
{
    var token = await RegisterGmAndGetTokenAsync("ItemGmFilter2", "itemfilter2@teste.com");
    await PostItemAsync(token, MinimalArma("Espada Curta")); // Subcategoria "Espadas", Tier F

    var message = new HttpRequestMessage(HttpMethod.Get, "/api/items?subcategoria=Espadas&tier=F");
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var response = await _client.SendAsync(message);

    var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
    body!.Should().ContainSingle(i => i.Nome == "Espada Curta");
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemsControllerTests`
Expected: the 3 new tests FAIL (404 route not found); the earlier 5 tests still pass.

- [ ] **Step 3: Add the `List` action**

In `src/RuinaRPG.Api/Controllers/ItemsController.cs`, add:

```csharp
[HttpGet]
public async Task<ActionResult<List<ItemResponse>>> List(
    [FromQuery] string? tipo,
    [FromQuery] string? subcategoria,
    [FromQuery] string? tier,
    [FromQuery] string? categoria,
    [FromQuery] string? tipoDeDano)
{
    var gmId = CurrentGmId();
    var query = db.Items.Where(i => i.GmId == gmId);

    if (tipo is not null && Enum.TryParse<ItemTipo>(tipo, out var tipoParsed))
        query = query.Where(i => EF.Property<string>(i, "Tipo") == tipoParsed.ToString());

    var items = await query.ToListAsync();

    var responses = new List<ItemResponse>();
    foreach (var item in items)
        responses.Add(await ToResponseAsync(item));

    return responses
        .Where(r => subcategoria is null || r.Subcategoria == subcategoria)
        .Where(r => tier is null || r.Tier == tier)
        .Where(r => categoria is null || r.Categoria == categoria)
        .Where(r => tipoDeDano is null || r.TipoDeDano == tipoDeDano)
        .ToList();
}
```

Add `using Microsoft.EntityFrameworkCore;` to the top of the file if not already present.

The `Subcategoria`/`Tier`/`Categoria`/`TipoDeDano` filters run in memory over the already-materialized `ItemResponse` list rather than as SQL `WHERE` clauses — deliberate for this plan's scale (one GM's catalog is at most a few hundred rows) and simplest given those fields live on different subclasses; if the catalog grows large enough for this to matter, push these filters into the per-subclass EF queries instead.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemsControllerTests`
Expected: PASS (8/8).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/ItemsController.cs tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs
git commit -m "feat: add item listing with filters"
```

---

### Task 8: Api — edit and delete an item (R0002, R0007)

**Files:**
- Create: `src/RuinaRPG.Contracts/Items/UpdateItemRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/ItemsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs`

**Interfaces:**
- Consumes: `ItemsController` (Tasks 6-7).
- Produces: `PUT /api/items/{id}` → `204` on success, `404` if the item doesn't exist or isn't owned by the caller. `DELETE /api/items/{id}` → `204`/`404`, same ownership rule. `UpdateItemRequest` has the same shape as `CreateItemRequest` minus `Tipo` (R0002: immutable after creation).

- [ ] **Step 1: Write the request contract**

`src/RuinaRPG.Contracts/Items/UpdateItemRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Items;

public record UpdateItemRequest(
    string Nome,
    decimal Peso,
    int Preco,
    string? ImageId,
    string? Subcategoria,
    string? Descricao,
    string? Tier,
    string? Empunhadura,
    string? Dados,
    int? Dano,
    string? Critico,
    int? Alcance,
    string? TipoDeDano,
    string? RequisitoAtributo,
    int? DurabilidadeMaxima,
    string? Categoria,
    int? Defesa,
    int? RF,
    int? RM,
    string? Penalidade,
    int? RequisitoVigor,
    int? BonusDefesa,
    string? TipoDeAlvo,
    string? Alvo,
    int? Valor);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs`:

```csharp
[Fact]
public async Task Update_an_owned_item_returns_204_and_the_change_is_visible_on_list()
{
    var token = await RegisterGmAndGetTokenAsync("ItemGmUpdate1", "itemupdate1@teste.com");
    var createResponse = await PostItemAsync(token, MinimalItemGeral("Corda"));
    var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

    var update = new UpdateItemRequest("Corda Reforçada", 0.6m, 8, null, "Equipamentos de Aventura", "Mais resistente.",
        null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null,
        null, null, null, null);
    var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{itemId}") { Content = JsonContent.Create(update) };
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await _client.SendAsync(message);

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/items");
    listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var listResponse = await _client.SendAsync(listMessage);
    var body = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
    body!.Should().ContainSingle(i => i.Nome == "Corda Reforçada" && i.Preco == 8);
}

[Fact]
public async Task Update_a_item_owned_by_another_gm_returns_404()
{
    var tokenOwner = await RegisterGmAndGetTokenAsync("ItemGmUpdateOwner", "itemupdateowner@teste.com");
    var tokenOther = await RegisterGmAndGetTokenAsync("ItemGmUpdateOther", "itemupdateother@teste.com");
    var createResponse = await PostItemAsync(tokenOwner, MinimalItemGeral("Corda"));
    var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

    var update = new UpdateItemRequest("Hack", 0m, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{itemId}") { Content = JsonContent.Create(update) };
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenOther);

    var response = await _client.SendAsync(message);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task Delete_an_owned_item_returns_204_and_it_no_longer_appears_on_list()
{
    var token = await RegisterGmAndGetTokenAsync("ItemGmDelete1", "itemdelete1@teste.com");
    var createResponse = await PostItemAsync(token, MinimalItemGeral("Corda"));
    var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

    var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/items/{itemId}");
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var response = await _client.SendAsync(message);

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/items");
    listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var listResponse = await _client.SendAsync(listMessage);
    var body = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
    body!.Should().NotContain(i => i.Id == itemId);
}

[Fact]
public async Task Delete_a_nonexistent_item_returns_404()
{
    var token = await RegisterGmAndGetTokenAsync("ItemGmDelete2", "itemdelete2@teste.com");
    var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/items/{Guid.NewGuid()}");
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await _client.SendAsync(message);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemsControllerTests`
Expected: the 4 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 4: Add `Update` and `Delete` actions**

In `src/RuinaRPG.Api/Controllers/ItemsController.cs`, add:

```csharp
[HttpPut("{id}")]
public async Task<IActionResult> Update(Guid id, UpdateItemRequest request)
{
    var gmId = CurrentGmId();
    var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.GmId == gmId);
    if (item is null)
        return NotFound();

    item.Nome = request.Nome;
    item.Peso = request.Peso;
    item.Preco = request.Preco;
    item.ImageId = request.ImageId is not null ? Guid.Parse(request.ImageId) : null;

    switch (item)
    {
        case ItemGeral g:
            g.Subcategoria = request.Subcategoria;
            g.Descricao = request.Descricao;
            break;
        case Arma a:
            a.Subcategoria = request.Subcategoria;
            a.Tier = ParseEnum<Tier>(request.Tier);
            a.Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura);
            a.Dados = request.Dados;
            a.Dano = request.Dano;
            a.Critico = request.Critico;
            a.Alcance = request.Alcance;
            a.TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano);
            a.RequisitoAtributo = request.RequisitoAtributo;
            a.DurabilidadeMaxima = request.DurabilidadeMaxima;
            break;
        case Armadura ar:
            ar.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
            ar.Defesa = request.Defesa;
            ar.RF = request.RF;
            ar.RM = request.RM;
            ar.Penalidade = request.Penalidade;
            ar.RequisitoVigor = request.RequisitoVigor;
            ar.DurabilidadeMaxima = request.DurabilidadeMaxima;
            break;
        case Escudo e:
            e.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
            e.BonusDefesa = request.BonusDefesa;
            e.Penalidade = request.Penalidade;
            e.RequisitoVigor = request.RequisitoVigor;
            e.DurabilidadeMaxima = request.DurabilidadeMaxima;
            break;
        case Artefato art:
            art.TipoDeAlvo = ParseEnum<TipoDeAlvo>(request.TipoDeAlvo);
            art.Alvo = request.Alvo;
            art.Valor = request.Valor;
            break;
    }

    await db.SaveChangesAsync();
    return NoContent();
}

[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id)
{
    var gmId = CurrentGmId();
    var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.GmId == gmId);
    if (item is null)
        return NotFound();

    db.Items.Remove(item);
    await db.SaveChangesAsync();
    return NoContent();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter ItemsControllerTests`
Expected: PASS (12/12).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Items/UpdateItemRequest.cs src/RuinaRPG.Api/Controllers/ItemsController.cs tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs
git commit -m "feat: add item update and delete"
```

---

### Task 9: Client — Catálogo list and filter page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/Catalogo.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `ItemResponse` (Task 6), the authenticated `HttpClient` (already wired by the Convite de Jogador plan).
- Produces: the `/catalogo` route. Task 10 (create/edit form) links from this page but is a separate route.

- [ ] **Step 1: Write the page**

`src/RuinaRPG.Client/Pages/Catalogo.razor`:

```razor
@page "/catalogo"
@inject HttpClient Http
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Items

<h1>Catálogo de Itens e Equipamentos</h1>

<button @onclick="@(() => Navigation.NavigateTo("/catalogo/novo"))">Novo Item</button>

<div>
    <select @bind="_tipoFiltro">
        <option value="">Todos os Tipos</option>
        <option value="ItemGeral">Item Geral</option>
        <option value="Arma">Arma</option>
        <option value="Armadura">Armadura</option>
        <option value="Escudo">Escudo</option>
        <option value="Artefato">Artefato</option>
    </select>
    <button @onclick="LoadAsync">Filtrar</button>
</div>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<table>
    <thead>
        <tr>
            <th>Nome</th>
            <th>Tipo</th>
            <th>Subcategoria</th>
            <th>Peso</th>
            <th>Preço</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
        @foreach (var item in _items)
        {
            <tr>
                <td>@item.Nome</td>
                <td>@item.Tipo</td>
                <td>@(item.Subcategoria ?? "—")</td>
                <td>@item.Peso</td>
                <td>@item.Preco</td>
                <td>
                    <button @onclick="@(() => Navigation.NavigateTo($"/catalogo/{item.Id}/editar"))">Editar</button>
                    <button @onclick="@(() => DeleteAsync(item.Id))">Excluir</button>
                </td>
            </tr>
        }
    </tbody>
</table>

@code {
    private string _tipoFiltro = "";
    private List<ItemResponse> _items = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var queryString = string.IsNullOrEmpty(_tipoFiltro) ? "" : $"?tipo={_tipoFiltro}";
        var response = await Http.GetAsync($"items{queryString}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar o catálogo.";
            return;
        }

        _items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        _errorMessage = null;
    }

    private async Task DeleteAsync(string id)
    {
        var response = await Http.DeleteAsync($"items/{id}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível excluir o item.";
            return;
        }

        await LoadAsync();
    }
}
```

- [ ] **Step 2: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/catalogo` following the file's established pattern — label it "Catálogo de Itens".

- [ ] **Step 3: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/Catalogo.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add the catalogo list and filter page"
```

---

### Task 10: Client — item create/edit form

**Files:**
- Create: `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`

**Interfaces:**
- Consumes: `CreateItemRequest`/`UpdateItemRequest`/`ItemResponse` (Tasks 6, 8), `ImageUploadResponse`/`ImageSummaryResponse` (Task 3), the authenticated `HttpClient`.
- Produces: the `/catalogo/novo` and `/catalogo/{id}/editar` routes. No later task depends on this file.

The image field offers both options R0010 requires for the GM: upload a new file, or pick one already uploaded (via `GET /api/images/mine`, Task 3) — satisfying "referenciar em vez de reenviar" without a second round trip to re-upload bytes the GM already has stored.

- [ ] **Step 1: Write the page**

`src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`:

```razor
@page "/catalogo/novo"
@page "/catalogo/{ItemId}/editar"
@inject HttpClient Http
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Items
@using RuinaRPG.Contracts.Images

<h1>@(ItemId is null ? "Novo Item" : "Editar Item")</h1>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<EditForm Model="_form" OnValidSubmit="SubmitAsync">
    @if (ItemId is null)
    {
        <label>
            Tipo
            <select @bind="_form.Tipo">
                <option value="ItemGeral">Item Geral</option>
                <option value="Arma">Arma</option>
                <option value="Armadura">Armadura</option>
                <option value="Escudo">Escudo</option>
                <option value="Artefato">Artefato</option>
            </select>
        </label>
    }

    <label>Nome <InputText @bind-Value="_form.Nome" /></label>
    <label>Peso <InputNumber @bind-Value="_form.Peso" /></label>
    <label>Preço (Ciclos) <InputNumber @bind-Value="_form.Preco" /></label>

    <label>Enviar nova imagem <InputFile OnChange="UploadImageAsync" /></label>
    <label>
        Ou reutilizar uma imagem já enviada (R0010)
        <select @bind="_form.ImageId">
            <option value="">— nenhuma —</option>
            @foreach (var image in _myImages)
            {
                <option value="@image.Id">@image.Url (@image.CreatedAt.ToString("g"))</option>
            }
        </select>
    </label>

    @if (_form.Tipo == "ItemGeral")
    {
        <label>Subcategoria <InputText @bind-Value="_form.Subcategoria" /></label>
        <label>Descrição <InputTextArea @bind-Value="_form.Descricao" /></label>
    }
    else if (_form.Tipo == "Arma")
    {
        <label>Subcategoria <InputText @bind-Value="_form.Subcategoria" /></label>
        <label>Tier <InputText @bind-Value="_form.Tier" /></label>
        <label>Empunhadura <InputText @bind-Value="_form.Empunhadura" /></label>
        <label>Dados <InputText @bind-Value="_form.Dados" /></label>
        <label>Dano <InputNumber @bind-Value="_form.Dano" /></label>
        <label>Crítico <InputText @bind-Value="_form.Critico" /></label>
        <label>Alcance <InputNumber @bind-Value="_form.Alcance" /></label>
        <label>Tipo de Dano <InputText @bind-Value="_form.TipoDeDano" /></label>
        <label>Requisito de Atributo <InputText @bind-Value="_form.RequisitoAtributo" /></label>
        <label>Durabilidade <InputNumber @bind-Value="_form.DurabilidadeMaxima" /></label>
    }
    else if (_form.Tipo is "Armadura" or "Escudo")
    {
        <label>Categoria <InputText @bind-Value="_form.Categoria" /></label>
        @if (_form.Tipo == "Armadura")
        {
            <label>Defesa <InputNumber @bind-Value="_form.Defesa" /></label>
            <label>RF <InputNumber @bind-Value="_form.RF" /></label>
            <label>RM <InputNumber @bind-Value="_form.RM" /></label>
        }
        else
        {
            <label>Bônus de Defesa <InputNumber @bind-Value="_form.BonusDefesa" /></label>
        }
        <label>Penalidade <InputText @bind-Value="_form.Penalidade" /></label>
        <label>Requisito de Vigor <InputNumber @bind-Value="_form.RequisitoVigor" /></label>
        <label>Durabilidade <InputNumber @bind-Value="_form.DurabilidadeMaxima" /></label>
    }
    else if (_form.Tipo == "Artefato")
    {
        <label>Tipo de Alvo <InputText @bind-Value="_form.TipoDeAlvo" /></label>
        <label>Alvo <InputText @bind-Value="_form.Alvo" /></label>
        <label>Valor <InputNumber @bind-Value="_form.Valor" /></label>
    }

    <button type="submit">Salvar</button>
</EditForm>

@code {
    [Parameter] public string? ItemId { get; set; }

    private readonly ItemFormModel _form = new();
    private List<ImageSummaryResponse> _myImages = new();
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        var imagesResponse = await Http.GetAsync("images/mine");
        if (imagesResponse.IsSuccessStatusCode)
            _myImages = await imagesResponse.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>() ?? new();

        if (ItemId is null)
            return;

        var response = await Http.GetAsync($"items?tipo="); // full list; find the one being edited
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        var existing = items.SingleOrDefault(i => i.Id == ItemId);
        if (existing is null)
        {
            _errorMessage = "Item não encontrado.";
            return;
        }

        _form.Tipo = existing.Tipo;
        _form.Nome = existing.Nome;
        _form.Peso = existing.Peso;
        _form.Preco = existing.Preco;
        _form.Subcategoria = existing.Subcategoria;
        _form.Descricao = existing.Descricao;
        _form.Tier = existing.Tier;
        _form.Empunhadura = existing.Empunhadura;
        _form.Dados = existing.Dados;
        _form.Dano = existing.Dano;
        _form.Critico = existing.Critico;
        _form.Alcance = existing.Alcance;
        _form.TipoDeDano = existing.TipoDeDano;
        _form.RequisitoAtributo = existing.RequisitoAtributo;
        _form.DurabilidadeMaxima = existing.DurabilidadeMaxima;
        _form.Categoria = existing.Categoria;
        _form.Defesa = existing.Defesa;
        _form.RF = existing.RF;
        _form.RM = existing.RM;
        _form.Penalidade = existing.Penalidade;
        _form.RequisitoVigor = existing.RequisitoVigor;
        _form.BonusDefesa = existing.BonusDefesa;
        _form.TipoDeAlvo = existing.TipoDeAlvo;
        _form.Alvo = existing.Alvo;
        _form.Valor = existing.Valor;
    }

    private async Task UploadImageAsync(InputFileChangeEventArgs e)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(e.File.OpenReadStream(maxAllowedSize: 20_000_000));
        content.Add(fileContent, "file", e.File.Name);

        var response = await Http.PostAsync("images", content);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível enviar a imagem.";
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        _form.ImageId = body!.Id;
        _myImages.Insert(0, new ImageSummaryResponse(body.Id, body.Url, DateTime.UtcNow)); // so the <select> has an option matching the ID just set
    }

    private async Task SubmitAsync()
    {
        HttpResponseMessage response;
        if (ItemId is null)
        {
            var request = new CreateItemRequest(_form.Tipo, _form.Nome, _form.Peso, _form.Preco, _form.ImageId,
                _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
                _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
                _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
                _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor);
            response = await Http.PostAsJsonAsync("items", request);
        }
        else
        {
            var request = new UpdateItemRequest(_form.Nome, _form.Peso, _form.Preco, _form.ImageId,
                _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
                _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
                _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
                _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor);
            response = await Http.PutAsJsonAsync($"items/{ItemId}", request);
        }

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar o item.";
            return;
        }

        Navigation.NavigateTo("/catalogo");
    }

    private class ItemFormModel
    {
        public string Tipo { get; set; } = "ItemGeral";
        public string Nome { get; set; } = "";
        public decimal Peso { get; set; }
        public int Preco { get; set; }
        public string? ImageId { get; set; }
        public string? Subcategoria { get; set; }
        public string? Descricao { get; set; }
        public string? Tier { get; set; }
        public string? Empunhadura { get; set; }
        public string? Dados { get; set; }
        public int? Dano { get; set; }
        public string? Critico { get; set; }
        public int? Alcance { get; set; }
        public string? TipoDeDano { get; set; }
        public string? RequisitoAtributo { get; set; }
        public int? DurabilidadeMaxima { get; set; }
        public string? Categoria { get; set; }
        public int? Defesa { get; set; }
        public int? RF { get; set; }
        public int? RM { get; set; }
        public string? Penalidade { get; set; }
        public int? RequisitoVigor { get; set; }
        public int? BonusDefesa { get; set; }
        public string? TipoDeAlvo { get; set; }
        public string? Alvo { get; set; }
        public int? Valor { get; set; }
    }
}
```

Note the `LoadAsync`-via-full-list approach in `OnInitializedAsync` (`items?tipo=` fetching everything, then finding the one matching `ItemId` client-side) is deliberately simple rather than adding a `GET /api/items/{id}` endpoint this plan doesn't otherwise need — acceptable for a GM's catalog of a few hundred items; revisit with a dedicated endpoint if that stops being true.

- [ ] **Step 2: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CatalogoItemForm.razor
git commit -m "feat: add the catalogo item create/edit form"
```

---

### Task 11: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files — this task only exercises what Tasks 1-10 built.

**Interfaces:**
- Consumes: the full stack, including everything this plan added.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers come up; migrations (including `AddImages` and `AddItems`) apply automatically in Development.

- [ ] **Step 2: Register a GM and get a token, through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/gm \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeCatalogoGm","email":"smokecatalogogm@teste.com","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' \
  | tee /tmp/gm-register.json
TOKEN=$(jq -r .accessToken /tmp/gm-register.json)
```

- [ ] **Step 3: Create an item through nginx**

```bash
curl -sf -X POST http://localhost/api/items \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"tipo":"Arma","nome":"Espada Curta","peso":1.5,"preco":50,"imageId":null,"subcategoria":"Espadas","tier":"F","dano":3}' \
  | tee /tmp/item-create.json
```

Expected: HTTP 201 with `"tipo":"Arma"`, `"nome":"Espada Curta"`.

- [ ] **Step 4: List items through nginx and confirm the created item is present**

```bash
curl -sf "http://localhost/api/items" -H "Authorization: Bearer $TOKEN"
```

Expected: HTTP 200, JSON array containing the "Espada Curta" item.

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the catalogo item flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- The **Jogador** side of R0010 — referencing an image made visible through a campaign attachment (an item marked public, or an NPC/Criatura with its image individually released) that belongs to a *different* GM's catalog. That visibility model doesn't exist until the Campanha plan builds `CampaignAttachments`; this plan only builds the GM-referencing-their-own-uploads half (`GET /api/images/mine`).
- Image display/download through the Client beyond constructing the `/​<filename>` URL — no `<img>` preview polish, no client-side crop/resize.
- Any consumption of `Item`/`Image` by Ficha de Personagem (arsenal, armor slots, inventory) — those plans reference `Item.Id`/`Image.Id` by FK, but building that consumption is their job.
- Deleting the underlying image file from disk when an `Image` row (or the last `Item`/sheet referencing it) is removed — `Image` rows and files accumulate; a cleanup job is future work, not required by any requirement this plan implements.
- Bulk import of the real item data mentioned in the Catálogo doc's preamble (`Docs/Sistema RPG/ruina-itens.docx`) — the GM enters items one at a time through the form this plan builds; a bulk-import tool is not requested by any numbered requirement.
