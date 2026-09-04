# Campaign-Scoped Sheet Pickers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restrict the Item/Magia-Habilidade/Imagem pickers on a player-edited character/NPC/Criatura sheet to that sheet's campaign's public attachments, auto-attach player-created spells and player-uploaded images to the campaign as public, and block players from the GM's campaign-management page.

**Architecture:** A new `CampaignCatalogController` exposes three campaign-membership-checked, `IsPublic`-filtered read endpoints (`available-items`, `available-spell-abilities`, `available-images`) reusing the existing `ItemResponse`/`SpellAbilityEntryResponse`/`ImageSummaryResponse` contracts. The three `*SpellAbilitiesController.Add` endpoints and `ImagesController.Upload` gain auto-attach-as-public logic gated on caller role. Client-side, `FichaDePersonagem`/`FichaDeNpc`/`FichaDeCriatura` pick their data source (unrestricted vs. campaign-scoped endpoint) based on the caller's role, determined once per page load.

**Tech Stack:** ASP.NET Core 8 Web API, EF Core/Npgsql, Blazor WebAssembly 8, MudBlazor, xUnit + Testcontainers (integration), bUnit (client).

**Spec:** `docs/superpowers/specs/2026-09-04-campaign-scoped-sheet-pickers-design.md`

## Global Constraints

- **No EF migration.** `CampaignAttachment` already has every column needed. `NpcSheet`/`CreatureSheet`'s "campaign" is always resolved through the grant-link `CampaignAttachment` row (`Where(a => a.XSheetId == id).Select(a => a.CampaignId).FirstOrDefault()`), never a stored column.
- **The restriction is keyed off caller role, not sheet ownership.** A GM editing any sheet (their own, or a player's) always gets the unrestricted catalog/bank/image list. Only a Jogador caller gets the campaign-scoped one.
- **Reuse existing response contracts.** The new endpoints return `List<ItemResponse>`, `List<SpellAbilityEntryResponse>`, `List<ImageSummaryResponse>` — the exact same DTOs the GM-facing endpoints already return. No new contract types.
- **Duplicated response-mapping is the established convention in this codebase** (every controller owns a private `ToResponse[Async]`, e.g. `ItemsController`, `SpellAbilityBankController`, `NpcSheetsController` all do this independently) — the new controller's item-mapping switch is written directly in it, not factored into a shared helper, to match that convention rather than unilaterally restructure it.
- Image URLs are always built as `$"/images/{path}"` (the `images/` prefix is required — nginx serves the images volume there; a URL missing it falls through to the SPA's `index.html`).
- Role strings are exactly `"GM"` and `"Jogador"` (see `NavMenu.razor`'s `<AuthorizeView Roles="...">` usage for precedent).

---

## File Structure

**Backend — new:**
- `src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs` — the 3 new scoped read endpoints.
- `tests/RuinaRPG.Tests.Integration/Controllers/CampaignCatalogControllerTests.cs`

**Backend — modified:**
- `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs` — image-URL prefix bugfix.
- `src/RuinaRPG.Api/Controllers/ImagesController.cs` — optional `campaignId` form field + auto-attach.
- `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs` — auto-attach for a Jogador caller.
- `src/RuinaRPG.Api/Controllers/NpcSpellAbilitiesController.cs` — same, with grant-link `CampaignId` resolution.
- `src/RuinaRPG.Api/Controllers/CreatureSpellAbilitiesController.cs` — same.
- `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs` — `NpcSheetResponse` gains `CampaignId`.
- `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs` — `CreatureSheetResponse` gains `CampaignId`.
- `src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs`, `src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetResponse.cs` — the new field.
- `tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs`, `ImagesControllerTests.cs`, `CharacterSpellAbilitiesControllerTests.cs`, `NpcSpellAbilitiesControllerTests.cs`, `CreatureSpellAbilitiesControllerTests.cs`, `NpcSheetsControllerTests.cs`, `CreatureSheetsControllerTests.cs` — new test cases appended.

**Client — modified:**
- `src/RuinaRPG.Client/Shared/ImageAttachmentField.razor` — optional `CampaignId` parameter.
- `tests/RuinaRPG.Tests.Client/Shared/ImageAttachmentFieldTests.cs` — new test case.
- `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor` — Jogador route guard.
- `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` — role-conditional item/spell/image sourcing.
- `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` — same.
- `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` — same.

**Docs:**
- `Docs/Requisitos/Requisitos - Ficha de Personagem.md` — new R0003.
- `Docs/Requisitos/Requisitos - Campanha.md` — new R0012.
- `Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md` — new R0007.
- `Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md` — new R0011.

---

### Task 1: Fix the `AnexosPublicos` image-URL prefix bug

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs:77,87`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new (bugfix only).

- [ ] **Step 1: Write the failing test**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs`, inside the class, after `PlayerView_includes_only_the_public_item_not_the_private_one`:

```csharp
    [Fact]
    public async Task PlayerView_Item_ImageUrl_uses_the_images_prefix()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PvImgGm", "pvimggm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "PvImgPlayer", "pvimgplayer@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha PV Img");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var (imageId, imageUrl) = await UploadImageWithUrlAsync(gmToken);
        var itemRequest = MinimalItemGeral("Espelho") with { ImageId = imageId };
        var itemResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, itemRequest));
        var itemId = (await itemResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
        var attachmentId = await AttachAsync(gmToken, campaignId, new AttachToCampaignRequest(itemId, null, null, null, null));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/player-view", playerToken));

        var body = await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        var entry = body!.AnexosPublicos.Should().ContainSingle(a => a.Id == attachmentId).Subject;
        entry.ImageUrl.Should().Be(imageUrl);
    }

    private async Task<(string Id, string Url)> UploadImageWithUrlAsync(string gmToken)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>();
        return (body!.Id, body.Url);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~PlayerView_Item_ImageUrl_uses_the_images_prefix"`
Expected: FAIL — `entry.ImageUrl` is `"/<path>"`, missing the `images/` prefix, so the assertion against `imageUrl` (`"/images/<path>"`) fails.

- [ ] **Step 3: Fix the bug**

In `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs`, line 77:

```csharp
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Item", item!.Nome, item.ImageId is not null ? $"/images/{(await db.Images.FindAsync(item.ImageId.Value))!.Path}" : null));
```

Line 87:

```csharp
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "Image", null, $"/images/{image!.Path}"));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~PlayerView_Item_ImageUrl_uses_the_images_prefix"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs
git commit -m "fix: AnexosPublicos Item/Image thumbnails used the wrong URL prefix"
```

---

### Task 2: New `CampaignCatalogController` — 3 campaign-scoped read endpoints

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignCatalogControllerTests.cs`

**Interfaces:**
- Consumes: `RuinaRpgDbContext` (`db.Campaigns`, `db.CampaignMembers`, `db.CampaignAttachments`, `db.Items`, `db.SpellAbilityBankEntries`, `db.Images`), `ItemResponse`, `SpellAbilityEntryResponse`, `ImageSummaryResponse` (existing contracts, unchanged).
- Produces: `GET api/campaigns/{campaignId}/available-items?nome=&tipo=`, `GET api/campaigns/{campaignId}/available-spell-abilities?nome=`, `GET api/campaigns/{campaignId}/available-images` — all three used by Task 10/11/12's client wiring.

- [ ] **Step 1: Write the failing tests**

Create `tests/RuinaRPG.Tests.Integration/Controllers/CampaignCatalogControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignCatalogControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignCatalogControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    private static CreateItemRequest MinimalItem(string nome, string tipo = "ItemGeral") =>
        new(tipo, nome, 0.5m, 5, null, "Equipamentos de Aventura", "Descrição.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null);

    private async Task<string> CreateItemAsync(string gmToken, string nome, string tipo = "ItemGeral")
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken, MinimalItem(nome, tipo)));
        return (await response.Content.ReadFromJsonAsync<ItemResponse>())!.Id;
    }

    private async Task<string> CreateBankEntryAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Magia", 1, "Descrição.", [])));
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task<string> UploadImageAsync(string gmToken)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var response = await _client.SendAsync(message);
        return (await response.Content.ReadFromJsonAsync<ImageUploadResponse>())!.Id;
    }

    private async Task<string> AttachAndPublishAsync(string gmToken, string campaignId, AttachToCampaignRequest request)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken, request));
        var attachmentId = (await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
        return attachmentId;
    }

    private record Setup(string GmToken, string PlayerToken, string CampaignId);

    private async Task<Setup> BuildMemberSetupAsync(string suffix)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"CatGm{suffix}", $"catgm{suffix}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"CatPlayer{suffix}", $"catplayer{suffix}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha Catalogo {suffix}");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        return new Setup(gmToken, playerToken, campaignId);
    }

    [Fact]
    public async Task AvailableItems_returns_only_publicly_attached_items()
    {
        var setup = await BuildMemberSetupAsync("Items1");
        var publicItemId = await CreateItemAsync(setup.GmToken, "Corda Pública");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(publicItemId, null, null, null, null));
        var privateItemId = await CreateItemAsync(setup.GmToken, "Corda Privada");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{setup.CampaignId}/attachments", setup.GmToken,
            new AttachToCampaignRequest(privateItemId, null, null, null, null))); // attached but never made public

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-items", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Id == publicItemId);
        body.Should().NotContain(i => i.Id == privateItemId);
    }

    [Fact]
    public async Task AvailableItems_tipo_filter_matches_the_ItemsController_behavior()
    {
        var setup = await BuildMemberSetupAsync("Items2");
        var weaponId = await CreateItemAsync(setup.GmToken, "Espada Longa", "Arma");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(weaponId, null, null, null, null));
        var generalId = await CreateItemAsync(setup.GmToken, "Corda", "ItemGeral");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(generalId, null, null, null, null));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-items?tipo=Arma", setup.PlayerToken));

        var body = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Should().ContainSingle(i => i.Id == weaponId);
        body.Should().NotContain(i => i.Id == generalId);
    }

    [Fact]
    public async Task AvailableItems_by_a_non_member_returns_403()
    {
        var setup = await BuildMemberSetupAsync("Items3");
        var (_, outsiderToken) = await RegisterJogadorLinkedToAsync(setup.GmToken, "CatOutsiderItems3", "catoutsideritems3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-items", outsiderToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AvailableSpellAbilities_returns_only_publicly_attached_entries()
    {
        var setup = await BuildMemberSetupAsync("Spells1");
        var publicEntryId = await CreateBankEntryAsync(setup.GmToken, "Bola de Fogo");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, publicEntryId, null));
        var privateEntryId = await CreateBankEntryAsync(setup.GmToken, "Segredo do GM");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{setup.CampaignId}/attachments", setup.GmToken,
            new AttachToCampaignRequest(null, null, null, privateEntryId, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-spell-abilities", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        body!.Should().ContainSingle(e => e.Id == publicEntryId);
        body.Should().NotContain(e => e.Id == privateEntryId);
    }

    [Fact]
    public async Task AvailableImages_returns_only_publicly_attached_images()
    {
        var setup = await BuildMemberSetupAsync("Images1");
        var publicImageId = await UploadImageAsync(setup.GmToken);
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, null, publicImageId));
        var privateImageId = await UploadImageAsync(setup.GmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{setup.CampaignId}/attachments", setup.GmToken,
            new AttachToCampaignRequest(null, null, null, null, privateImageId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-images", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        body!.Should().ContainSingle(i => i.Id == publicImageId);
        body.Should().NotContain(i => i.Id == privateImageId);
    }

    [Fact]
    public async Task AvailableImages_for_a_nonexistent_campaign_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CatNotFoundGm", "catnotfoundgm@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{Guid.NewGuid()}/available-images", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignCatalogControllerTests"`
Expected: FAIL to compile (no route matches `available-items`/`available-spell-abilities`/`available-images` yet — the controller doesn't exist).

- [ ] **Step 3: Create the controller**

Create `src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Requisitos - Ficha de Personagem R0003 / Requisitos - Catálogo R0011: a Jogador editing their
/// own sheet (or a NPC/Criatura sheet granted to them) may only pick an Item, Magia/Habilidade or
/// Imagem that's attached to that sheet's campaign AND toggled public (Requisitos - Campanha
/// R0008/R0009) — not their linked GM's entire catalog/bank/image library. This controller is
/// that scoped read surface; it returns the exact same response contracts the GM-facing
/// ItemsController/SpellAbilityBankController/ImagesController already use, just filtered through
/// CampaignAttachments instead of GmId/UploadedByUserId. Membership-checked the same way as
/// CampaignPlayerViewController.Get — a GM can call these too (they just see their own campaign's
/// public subset), which is harmless and kept for symmetry.
/// </summary>
[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId}")]
public class CampaignCatalogController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet("available-items")]
    public async Task<ActionResult<List<ItemResponse>>> AvailableItems(Guid campaignId, [FromQuery] string? nome, [FromQuery] string? tipo)
    {
        if (await MembershipErrorAsync(campaignId) is { } error)
            return error;

        var publicItemIds = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.IsPublic && a.ItemId != null)
            .Select(a => a.ItemId!.Value)
            .ToListAsync();

        var query = db.Items.Where(i => publicItemIds.Contains(i.Id));
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(i => EF.Functions.ILike(i.Nome, $"%{nome}%"));
        if (tipo is not null && Enum.TryParse<ItemTipo>(tipo, out var tipoParsed))
            query = query.Where(i => EF.Property<string>(i, "Tipo") == tipoParsed.ToString());

        var items = await query.ToListAsync();
        var responses = new List<ItemResponse>();
        foreach (var item in items)
            responses.Add(await ToItemResponseAsync(item));
        return responses;
    }

    [HttpGet("available-spell-abilities")]
    public async Task<ActionResult<List<SpellAbilityEntryResponse>>> AvailableSpellAbilities(Guid campaignId, [FromQuery] string? nome)
    {
        if (await MembershipErrorAsync(campaignId) is { } error)
            return error;

        var publicEntryIds = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.IsPublic && a.SpellAbilityBankEntryId != null)
            .Select(a => a.SpellAbilityBankEntryId!.Value)
            .ToListAsync();

        var query = db.SpellAbilityBankEntries.Include(e => e.Efeitos).Where(e => publicEntryIds.Contains(e.Id));
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

        var entries = await query.ToListAsync();
        return entries.Select(ToSpellAbilityResponse).ToList();
    }

    [HttpGet("available-images")]
    public async Task<ActionResult<List<ImageSummaryResponse>>> AvailableImages(Guid campaignId)
    {
        if (await MembershipErrorAsync(campaignId) is { } error)
            return error;

        var publicImageIds = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.IsPublic && a.ImageId != null)
            .Select(a => a.ImageId!.Value)
            .ToListAsync();

        return await db.Images
            .Where(i => publicImageIds.Contains(i.Id))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ImageSummaryResponse(i.Id.ToString(), $"/images/{i.Path}", i.CreatedAt))
            .ToListAsync();
    }

    /// <summary>Null when the caller may proceed; otherwise the ActionResult to return as-is
    /// (404 if the campaign doesn't exist, 403 if the caller isn't the GM or a member).</summary>
    private async Task<ActionResult?> MembershipErrorAsync(Guid campaignId)
    {
        var campaign = await db.Campaigns.FindAsync(campaignId);
        if (campaign is null)
            return NotFound();

        var callerId = CurrentUserId();
        var isMember = campaign.GmId == callerId || await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == callerId);
        return isMember ? null : Forbid();
    }

    // Mirrors ItemsController.ToResponseAsync exactly (see its comment for why this isn't
    // factored into a shared helper — every controller in this codebase owns its own mapping).
    private async Task<ItemResponse> ToItemResponseAsync(Item item)
    {
        string? imageUrl = null;
        if (item.ImageId is not null)
        {
            var image = await db.Images.FindAsync(item.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        return item switch
        {
            ItemGeral g => new ItemResponse(g.Id.ToString(), "ItemGeral", g.Nome, g.Peso, g.Preco, imageUrl,
                g.Subcategoria, g.Descricao, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null),
            Arma a => new ItemResponse(a.Id.ToString(), "Arma", a.Nome, a.Peso, a.Preco, imageUrl,
                a.Subcategoria, null, a.Tier?.ToString(), a.Empunhadura?.ToString(), a.Dados, a.Dano, a.Critico, a.Alcance, a.TipoDeDano?.ToString(), a.RequisitoAtributo,
                a.DurabilidadeMaxima, null, null, null, null, null, null, null, null, null, null),
            Armadura ar => new ItemResponse(ar.Id.ToString(), "Armadura", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, ar.DurabilidadeMaxima,
                ar.Categoria?.ToString(), ar.Defesa, ar.RF, ar.RM, ar.Penalidade, ar.RequisitoVigor, null, null, null, null),
            Escudo e => new ItemResponse(e.Id.ToString(), "Escudo", e.Nome, e.Peso, e.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, e.DurabilidadeMaxima,
                e.Categoria?.ToString(), null, null, null, e.Penalidade, e.RequisitoVigor, e.BonusDefesa, null, null, null),
            Artefato ar => new ItemResponse(ar.Id.ToString(), "Artefato", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, ar.TipoDeAlvo?.ToString(), ar.Alvo, ar.Valor),
            _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
        };
    }

    private static SpellAbilityEntryResponse ToSpellAbilityResponse(RuinaRPG.Infrastructure.SpellsAndAbilities.SpellAbilityBankEntry entry) => new(
        entry.Id.ToString(), entry.Nome, entry.Tipo.ToString(), entry.Grau, entry.GastoEmPI, entry.Custo, entry.Descricao,
        entry.Efeitos.Select(e => new SpellAbilityEffectResponse(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList());

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignCatalogControllerTests"`
Expected: PASS (6 tests)

- [ ] **Step 5: Full solution build check**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignCatalogControllerTests.cs
git commit -m "feat: campaign-scoped Item/Magia/Imagem read endpoints for player sheets"
```

---

### Task 3: `ImagesController.Upload` — auto-attach as a public campaign attachment

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/ImagesController.cs:19-57`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/ImagesControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignAttachment` (`RuinaRPG.Infrastructure.Campaigns`).
- Produces: `POST api/images` now accepts an optional `campaignId` multipart form field — consumed by Task 8's `ImageAttachmentField.CampaignId` and Task 10-12's avatar-upload wiring.

- [ ] **Step 1: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/ImagesControllerTests.cs`, inside the class (after the existing `BuildUpload` helper, extend it and add tests):

Replace the existing `BuildUpload` helper:

```csharp
    private static MultipartFormDataContent BuildUpload(byte[] bytes, string fileName = "test.png", string? campaignId = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);
        if (campaignId is not null)
            content.Add(new StringContent(campaignId), "campaignId");
        return content;
    }
```

Add near the bottom of the class, before the closing `}`:

```csharp
    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedWithIdAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Auth.MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAndAddMemberAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/campaigns")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", gmToken) },
            Content = JsonContent.Create(new RuinaRPG.Contracts.Campaigns.CreateCampaignRequest("Campanha Upload", ""))
        });
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignResponse>())!.Id;

        var memberMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/campaigns/{campaignId}/members")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", gmToken) },
            Content = JsonContent.Create(new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId))
        };
        await _client.SendAsync(memberMessage);
        return campaignId;
    }

    [Fact]
    public async Task Upload_by_a_jogador_with_a_campaignId_auto_attaches_it_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ImageAutoGm1", "imageautogm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedWithIdAsync(gmToken, "ImageAutoPlayer1", "imageautoplayer1@teste.com");
        var campaignId = await CreateCampaignAndAddMemberAsync(gmToken, playerId);
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes, campaignId: campaignId) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var imageId = (await response.Content.ReadFromJsonAsync<ImageUploadResponse>())!.Id;

        var availableMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-images");
        availableMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);
        var availableResponse = await _client.SendAsync(availableMessage);
        var available = await availableResponse.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        available!.Should().ContainSingle(i => i.Id == imageId);
    }

    [Fact]
    public async Task Upload_by_a_gm_with_a_campaignId_does_not_auto_attach()
    {
        // GM uploads stay private-by-default via the existing GM-driven Anexos flow — a
        // campaignId on the upload itself is only a Jogador-side convenience.
        var gmToken = await RegisterGmAndGetTokenAsync("ImageAutoGm2", "imageautogm2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedWithIdAsync(gmToken, "ImageAutoPlayer2", "imageautoplayer2@teste.com");
        var campaignId = await CreateCampaignAndAddMemberAsync(gmToken, playerId);
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes, campaignId: campaignId) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);

        var response = await _client.SendAsync(message);
        var imageId = (await response.Content.ReadFromJsonAsync<ImageUploadResponse>())!.Id;

        var availableMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-images");
        availableMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var availableResponse = await _client.SendAsync(availableMessage);
        var available = await availableResponse.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>();
        available!.Should().NotContain(i => i.Id == imageId);
    }

    [Fact]
    public async Task Upload_with_a_campaignId_for_a_campaign_the_jogador_is_not_a_member_of_still_succeeds_without_attaching()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ImageAutoGm3", "imageautogm3@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedWithIdAsync(gmToken, "ImageAutoPlayer3", "imageautoplayer3@teste.com");
        var campaignId = await CreateCampaignAndAddMemberAsync(gmToken, Guid.NewGuid().ToString()); // player never added
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = BuildUpload(PngBytes, campaignId: campaignId) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.Created); // upload itself never fails on this
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~ImagesControllerTests"`
Expected: `Upload_by_a_jogador_with_a_campaignId_auto_attaches_it_as_public` FAILs (nothing gets attached yet); the other two pass trivially already (nothing to break).

- [ ] **Step 3: Implement the auto-attach**

In `src/RuinaRPG.Api/Controllers/ImagesController.cs`, add the import:

```csharp
using RuinaRPG.Infrastructure.Campaigns;
```

Replace the `Upload` method:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~ImagesControllerTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/ImagesController.cs tests/RuinaRPG.Tests.Integration/Controllers/ImagesControllerTests.cs
git commit -m "feat: a Jogador's image upload auto-attaches to their campaign as public"
```

---

### Task 4: `CharacterSpellAbilitiesController` — auto-attach for a Jogador caller

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs:1-82`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSpellAbilitiesControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignAttachment`.
- Produces: nothing new — this is a side effect of the existing `POST character-sheets/{sheetId}/spell-abilities`.

- [ ] **Step 1: Write the failing test**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSpellAbilitiesControllerTests.cs`, inside the class:

```csharp
    private async Task<(string SheetId, string CampaignId)> SetUpSheetWithCampaignAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        return (sheetId, campaignId);
    }

    [Fact]
    public async Task AddFromScratch_by_the_player_auto_attaches_the_bank_copy_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasAutoGm1", "magiasauto1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasAutoPlayer1", "magiasautoplayer1@teste.com");
        var (sheetId, campaignId) = await SetUpSheetWithCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", playerToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task AddFromScratch_by_the_gm_does_not_auto_attach()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasAutoGm2", "magiasauto2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasAutoPlayer2", "magiasautoplayer2@teste.com");
        var (sheetId, campaignId) = await SetUpSheetWithCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", gmToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().NotContain(e => e.Nome == "Bola de Fogo");
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSpellAbilitiesControllerTests"`
Expected: `AddFromScratch_by_the_player_auto_attaches_the_bank_copy_as_public` FAILs; `AddFromScratch_by_the_gm_does_not_auto_attach` already passes (nothing attaches today either way).

- [ ] **Step 3: Implement the auto-attach**

In `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs`, add the import:

```csharp
using RuinaRPG.Infrastructure.Campaigns;
```

In `Add`, right after `db.SpellAbilityBankEntries.Add(bankCopy);` (before `await db.SaveChangesAsync();`):

```csharp
        // Requisitos - Banco de Magias e Habilidades R0007: when the creator is the owning
        // Jogador (not the GM managing the sheet), the bank copy also becomes a public campaign
        // attachment — no GM approval step, per Requisitos - Campanha R0012.
        if (CurrentUserId() != campaignGmId)
        {
            db.CampaignAttachments.Add(new CampaignAttachment
            {
                Id = Guid.NewGuid(),
                CampaignId = sheet.CampaignId,
                SpellAbilityBankEntryId = bankCopy.Id,
                IsPublic = true
            });
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSpellAbilitiesControllerTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSpellAbilitiesControllerTests.cs
git commit -m "feat: a player's from-scratch spell/ability auto-attaches to the campaign as public"
```

---

### Task 5: `NpcSpellAbilitiesController` — same auto-attach, via grant-link `CampaignId`

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/NpcSpellAbilitiesController.cs:1-82`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSpellAbilitiesControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignAttachment`.
- Produces: nothing new — side effect of `POST npc-sheets/{sheetId}/spell-abilities`.

- [ ] **Step 1: Write the failing test**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/NpcSpellAbilitiesControllerTests.cs`, inside the class:

```csharp
    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Auth.MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new RuinaRPG.Contracts.Campaigns.CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignResponse>())!.Id;
    }

    private async Task<string> GrantBlankNpcAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new RuinaRPG.Contracts.Campaigns.GrantSheetRequest(playerId, "Npc", null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.GrantSheetResponse>())!.SheetId;
    }

    [Fact]
    public async Task AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasAutoGm1", "npcmagiasauto1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcMagiasAutoPlayer1", "npcmagiasautoplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha NPC Magia Auto");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", playerToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task AddFromScratch_by_the_gm_on_their_own_npc_does_not_auto_attach()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcMagiasAutoGm2", "npcmagiasauto2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo"); // the bank copy still happens (R0001) — just no CampaignAttachment
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSpellAbilitiesControllerTests"`
Expected: `AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public` FAILs; the other passes already.

- [ ] **Step 3: Implement the auto-attach**

In `src/RuinaRPG.Api/Controllers/NpcSpellAbilitiesController.cs`, add the import:

```csharp
using RuinaRPG.Infrastructure.Campaigns;
```

In `Add`, right after `db.SpellAbilityBankEntries.Add(bankCopy);` (before `await db.SaveChangesAsync();`):

```csharp
        // Same rule as CharacterSpellAbilitiesController (see its comment) — the granted-sheet
        // "campaign" isn't a stored column, it's resolved through the grant-link CampaignAttachment.
        if (CurrentUserId() != sheet.GmId)
        {
            var campaignId = await db.CampaignAttachments
                .Where(a => a.NpcSheetId == sheetId)
                .Select(a => (Guid?)a.CampaignId)
                .FirstOrDefaultAsync();
            if (campaignId is not null)
            {
                db.CampaignAttachments.Add(new CampaignAttachment
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId.Value,
                    SpellAbilityBankEntryId = bankCopy.Id,
                    IsPublic = true
                });
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSpellAbilitiesControllerTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/NpcSpellAbilitiesController.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSpellAbilitiesControllerTests.cs
git commit -m "feat: a granted NPC's from-scratch spell/ability auto-attaches to the campaign as public"
```

---

### Task 6: `CreatureSpellAbilitiesController` — same auto-attach

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CreatureSpellAbilitiesController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSpellAbilitiesControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignAttachment`.
- Produces: nothing new — side effect of `POST creature-sheets/{sheetId}/spell-abilities`.

- [ ] **Step 1: Write the failing tests**

`src/RuinaRPG.Api/Controllers/CreatureSpellAbilitiesController.cs` mirrors `NpcSpellAbilitiesController.cs` exactly (same `Add` structure, `sheet.GmId`, `db.CreatureSpellAbilities`/`bankCopy`). `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSpellAbilitiesControllerTests.cs` already defines `AuthedRequest`, `RegisterGmAndGetTokenAsync`, `CreateSheetAsync(gmToken)` (plain, un-granted `POST creature-sheets`), `BolaDeFogoFromScratch()`, and `GetBankAsync(gmToken)`. Append to that class:

```csharp
    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Auth.MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new RuinaRPG.Contracts.Campaigns.CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.CampaignResponse>())!.Id;
    }

    private async Task<string> GrantBlankCreatureAsync(string gmToken, string campaignId, string playerId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken,
            new RuinaRPG.Contracts.Campaigns.GrantSheetRequest(playerId, "Creature", null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Campaigns.GrantSheetResponse>())!.SheetId;
    }

    [Fact]
    public async Task AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasAutoGm1", "creaturemagiasauto1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureMagiasAutoPlayer1", "creaturemagiasautoplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Creature Magia Auto");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new RuinaRPG.Contracts.Campaigns.AddCampaignMemberRequest(playerId)));
        var sheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", playerToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-spell-abilities", playerToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task AddFromScratch_by_the_gm_on_their_own_creature_does_not_auto_attach()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureMagiasAutoGm2", "creaturemagiasauto2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var bank = await GetBankAsync(gmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo"); // the bank copy still happens (R0001) — just no CampaignAttachment
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CreatureSpellAbilitiesControllerTests"`
Expected: `AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public` FAILs; `AddFromScratch_by_the_gm_on_their_own_creature_does_not_auto_attach` already passes.

- [ ] **Step 3: Implement the auto-attach**

Same edit as Task 5, applied to `src/RuinaRPG.Api/Controllers/CreatureSpellAbilitiesController.cs`: add `using RuinaRPG.Infrastructure.Campaigns;`, and after its `db.SpellAbilityBankEntries.Add(bankCopy);`:

```csharp
        if (CurrentUserId() != sheet.GmId)
        {
            var campaignId = await db.CampaignAttachments
                .Where(a => a.CreatureSheetId == sheetId)
                .Select(a => (Guid?)a.CampaignId)
                .FirstOrDefaultAsync();
            if (campaignId is not null)
            {
                db.CampaignAttachments.Add(new CampaignAttachment
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId.Value,
                    SpellAbilityBankEntryId = bankCopy.Id,
                    IsPublic = true
                });
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CreatureSpellAbilitiesControllerTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CreatureSpellAbilitiesController.cs tests/RuinaRPG.Tests.Integration/Controllers/CreatureSpellAbilitiesControllerTests.cs
git commit -m "feat: a granted Creature's from-scratch spell/ability auto-attaches to the campaign as public"
```

---

### Task 7: `NpcSheetResponse` / `CreatureSheetResponse` gain `CampaignId`

**Files:**
- Modify: `src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs`, `src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs:340-373`, `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs:303-`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`, `CreatureSheetsControllerTests.cs`

**Interfaces:**
- Produces: `NpcSheetResponse.CampaignId` (`string?`), `CreatureSheetResponse.CampaignId` (`string?`) — consumed by Task 11/12's `_campaignId` field.

- [ ] **Step 1: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`, inside the class:

```csharp
    [Fact]
    public async Task CampaignId_is_populated_for_a_granted_sheet_and_null_for_an_ungranted_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcCampaignIdGm1", "npccampaignid1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcCampaignIdPlayer1", "npccampaignidplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha NPC CampaignId");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var grantedSheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);
        var ungrantedSheetId = await CreateSheetAsync(gmToken); // read helper: POST npc-sheets, no update needed

        var grantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{grantedSheetId}", playerToken));
        var granted = await grantedResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        granted!.CampaignId.Should().Be(campaignId);

        var ungrantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{ungrantedSheetId}", gmToken));
        var ungranted = await ungrantedResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        ungranted!.CampaignId.Should().BeNull();
    }
```

`NpcSheetsControllerTests.cs` already defines every helper the test above uses (`RegisterGmAndGetTokenAsync`, `AuthedRequest`, `RegisterJogadorLinkedToAsync`, `CreateCampaignAsync`, `AddMemberAsync`, `GrantBlankNpcAsync`, `CreateSheetAsync` — the last one a plain, un-granted `POST npc-sheets`).

Append the analogous test to `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs` (it defines the same set of helpers under the same names, substituting `creature-sheets`/`CreatureSheetResponse`; its grant helper is named `GrantBlankCreatureAsync` and grants `"Creature"`):

```csharp
    [Fact]
    public async Task CampaignId_is_populated_for_a_granted_sheet_and_null_for_an_ungranted_one()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureCampaignIdGm1", "creaturecampaignid1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "CreatureCampaignIdPlayer1", "creaturecampaignidplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Creature CampaignId");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var grantedSheetId = await GrantBlankCreatureAsync(gmToken, campaignId, playerId);
        var ungrantedSheetId = await CreateSheetAsync(gmToken);

        var grantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{grantedSheetId}", playerToken));
        var granted = await grantedResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        granted!.CampaignId.Should().Be(campaignId);

        var ungrantedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{ungrantedSheetId}", gmToken));
        var ungranted = await ungrantedResponse.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        ungranted!.CampaignId.Should().BeNull();
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignId_is_populated"`
Expected: FAIL to compile — `NpcSheetResponse`/`CreatureSheetResponse` have no `CampaignId` member yet.

- [ ] **Step 3: Add the field and populate it**

`src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs` — add a trailing parameter:

```csharp
    int VitalidadeMaximo,
    int FocoMaximo,
    int AdrenalinaMaximo,
    int EstresseMaximo,
    string? CampaignId);
```

`src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetResponse.cs` — add a trailing parameter:

```csharp
    int VitalidadeMaximo, int FocoMaximo, int AdrenalinaMaximo,
    string? CampaignId);
```

In `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`, inside `ToResponseAsync`, right before the `return new NpcSheetResponse(...)`:

```csharp
        var campaignId = await db.CampaignAttachments
            .Where(a => a.NpcSheetId == s.Id)
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();
```

And append `campaignId?.ToString()` as the last constructor argument.

In `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs`, the same, keyed on `a.CreatureSheetId == s.Id`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignId_is_populated"`
Expected: PASS

- [ ] **Step 5: Full solution build check**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` — this confirms every other construction site of `NpcSheetResponse`/`CreatureSheetResponse` (there should be none besides `ToResponseAsync`, since these are `record`s built only there) still compiles.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs src/RuinaRPG.Contracts/CreatureSheets/CreatureSheetResponse.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs
git commit -m "feat: expose a granted NPC/Criatura sheet's CampaignId"
```

---

### Task 8: `ImageAttachmentField` — optional `CampaignId` passthrough on upload

**Files:**
- Modify: `src/RuinaRPG.Client/Shared/ImageAttachmentField.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/ImageAttachmentFieldTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `[Parameter] public string? CampaignId` — set by `FichaDePersonagem.razor` (Task 10) on its Diário `ImageAttachmentField` usages.

- [ ] **Step 1: Write the failing test**

Append to `tests/RuinaRPG.Tests.Client/Shared/ImageAttachmentFieldTests.cs`, inside the class:

```csharp
    [Fact]
    public async Task Uploading_with_a_CampaignId_set_includes_it_in_the_multipart_request()
    {
        HttpRequestMessage? captured = null;
        MultipartFormDataContent? capturedContent = null;
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            captured = req;
            capturedContent = req.Content as MultipartFormDataContent;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ImageUploadResponse("new-id", "https://cdn.example/new.png")),
            };
        });
        Services.AddScoped(_ => http);

        var cut = Render<ImageAttachmentField>(p => p
            .Add(x => x.AvailableImages, new List<ImageSummaryResponse>())
            .Add(x => x.SelectedIds, new List<string>())
            .Add(x => x.CampaignId, "campaign-123"));

        await cut.InvokeAsync(() => cut.Instance.UploadForTests(new FakeBrowserFile("foto.png")));

        captured.Should().NotBeNull();
        var campaignIdPart = capturedContent!.FirstOrDefault(p => p.Headers.ContentDisposition?.Name == "\"campaignId\"");
        campaignIdPart.Should().NotBeNull();
        (await campaignIdPart!.ReadAsStringAsync()).Should().Be("campaign-123");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~Uploading_with_a_CampaignId_set"`
Expected: FAIL to compile — `ImageAttachmentField` has no `CampaignId` parameter yet.

- [ ] **Step 3: Add the parameter and wire it into the upload**

In `src/RuinaRPG.Client/Shared/ImageAttachmentField.razor`, add the parameter next to `ButtonSize`:

```csharp
    /// <summary>When set, every upload through this field includes it as a "campaignId" form
    /// field, letting the server auto-attach the new image to that campaign as public
    /// (ImagesController.Upload, gated to a Jogador caller who's actually a member — see there).
    /// Leave null for GM-facing usages, which never auto-attach.</summary>
    [Parameter] public string? CampaignId { get; set; }
```

In `HandleUploadAsync`, right after `content.Add(fileContent, "file", file.Name);`:

```csharp
        if (CampaignId is not null)
            content.Add(new StringContent(CampaignId), "campaignId");
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~Uploading_with_a_CampaignId_set"`
Expected: PASS

- [ ] **Step 5: Run the full client suite**

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: PASS, no regressions.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Shared/ImageAttachmentField.razor tests/RuinaRPG.Tests.Client/Shared/ImageAttachmentFieldTests.cs
git commit -m "feat: ImageAttachmentField passes an optional CampaignId through to upload"
```

---

### Task 9: `CampanhaDetalhe.razor` — block direct Jogador navigation

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor` (top of `@code`, `OnInitializedAsync`)

**Interfaces:**
- Consumes: `AuthenticationStateProvider` (already registered app-wide; see `TokenAuthenticationStateProvider`).
- Produces: nothing new.

- [ ] **Step 1: Add the injection and the guard**

At the top of `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`, alongside the existing `@inject` lines:

```razor
@inject AuthenticationStateProvider AuthProvider
```

In the `@code` block, change `OnInitializedAsync`:

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        if (!authState.User.IsInRole("GM"))
        {
            Navigation.NavigateTo($"campanhas/{CampaignId}/jogador", replace: true);
            return;
        }

        // LoadCampaignNameAsync matches the loaded campaign's ImageUrl against _myImages, so
        // images must be loaded first rather than joining the WhenAll below.
        await LoadImagesAsync();
        await Task.WhenAll(LoadMembersAsync(), LoadSheetsAsync(), LoadDiaryAsync(), LoadEncountersAsync(), LoadAttachmentsAsync(), LoadSecretNotesAsync(), LoadCampaignNameAsync(), SearchPlayersAsync(), LoadGrantsAsync());
    }
```

- [ ] **Step 2: Verify manually — this page has no existing automated test to extend**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

This page has no `RuinaRPG.Tests.Client` coverage today (its bUnit rendering would require mocking every one of its many injected HTTP calls) — a build check is the right-sized verification here, matching how this file has been treated throughout this session's earlier changes.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor
git commit -m "feat: redirect a Jogador who navigates directly to the GM campaign page"
```

---

### Task 10: `FichaDePersonagem.razor` — role-conditional item/spell/image sourcing

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`

**Interfaces:**
- Consumes: `campaigns/{campaignId}/available-items`, `available-spell-abilities`, `available-images` (Task 2); `ImageAttachmentField.CampaignId` (Task 8).
- Produces: nothing new.

- [ ] **Step 1: Add the role check and reorder image/sheet loading**

Add near the top of the file, alongside the existing `@inject HttpClient Http`:

```razor
@inject AuthenticationStateProvider AuthProvider
```

Add a field near `_campaignId` (around line 647):

```csharp
    private bool _isGmCaller;
```

Replace `OnInitializedAsync` (lines 727-737):

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        _isGmCaller = authState.User.IsInRole("GM");

        // LoadSheetAsync sets _campaignId, which LoadImagesAsync now needs (a Jogador's image
        // list is scoped to that campaign) — the reverse of the old ordering, which only needed
        // _myImages loaded first to resolve the sheet's ImageUrl back to an id. That resolution
        // now happens explicitly, right after LoadImagesAsync, instead of inside LoadSheetAsync.
        await LoadSheetAsync();
        await LoadImagesAsync();
        _form.ImageId = _myImages.FirstOrDefault(i => i.Url == _form.ImageUrl)?.Id;

        await Task.WhenAll(LoadLevelUpNoticeAsync(), LoadTabs2And3Async(),
            LoadRacialAbilityAsync(), LoadSpellAbilitiesAsync(), LoadRunesAsync(), LoadMasteriesAsync(),
            LoadInventoryAsync(), LoadArtifactsAsync(), LoadAffectionsAsync(), LoadTraitsAsync(), LoadDiaryAsync(),
            LoadCampaignNameAsync());
    }
```

In `LoadSheetAsync` (around line 797), remove the now-redundant line:

```csharp
        _form.ImageId = _myImages.FirstOrDefault(i => i.Url == sheet.ImageUrl)?.Id;
```

(keep `_form.ImageUrl = sheet.ImageUrl;` and everything else in that method unchanged).

- [ ] **Step 2: Scope image loading and every `ImageAttachmentField` usage**

Replace `LoadImagesAsync` (around line 739):

```csharp
    private async Task LoadImagesAsync()
    {
        var url = _isGmCaller ? "images/mine" : $"campaigns/{_campaignId}/available-images";
        var response = await Http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            _myImages = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>() ?? new();
    }
```

Update both `ImageAttachmentField` usages (lines ~594 and ~612, the Diário compose and edit forms) to pass `CampaignId`:

```razor
                    <ImageAttachmentField AvailableImages="_myImages" SelectedIds="_diaryImageIds"
                                          SelectedIdsChanged="@(v => _diaryImageIds = v)" OnError="@(e => _errorMessage = e)"
                                          CampaignId="@(_isGmCaller ? null : _campaignId)" />
```

```razor
                                        <ImageAttachmentField AvailableImages="_myImages" SelectedIds="_editDiaryImageIds"
                                                              SelectedIdsChanged="@(v => _editDiaryImageIds = v)" OnError="@(e => _errorMessage = e)"
                                                              ButtonSize="Size.Small" CampaignId="@(_isGmCaller ? null : _campaignId)" />
```

Update `UploadImageAsync` (the avatar upload, around line 746) to include the campaignId form field when applicable:

```csharp
    private async Task UploadImageAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 20_000_000));
        content.Add(fileContent, "file", file.Name);
        if (!_isGmCaller && _campaignId is not null)
            content.Add(new StringContent(_campaignId), "campaignId");

        var response = await Http.PostAsync("images", content);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível enviar a imagem.";
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        _form.ImageId = body!.Id;
        _form.ImageUrl = body.Url;
        _myImages.Insert(0, new ImageSummaryResponse(body.Id, body.Url, DateTime.UtcNow));
    }
```

- [ ] **Step 3: Scope item search**

Replace `SearchItemsByTipoAsync` (around line 711):

```csharp
    private async Task<List<PickerOption>> SearchItemsByTipoAsync(string query, string tipo)
    {
        var url = _isGmCaller
            ? $"items?nome={Uri.EscapeDataString(query)}&tipo={tipo}"
            : $"campaigns/{_campaignId}/available-items?nome={Uri.EscapeDataString(query)}&tipo={tipo}";
        var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        return items.Select(i => new PickerOption(i.Id, i.Nome)).ToList();
    }
```

- [ ] **Step 4: Scope the spell/ability bank list**

Replace the `_bankEntries` line inside `LoadSpellAbilitiesAsync` (around line 1108):

```csharp
    private async Task LoadSpellAbilitiesAsync()
    {
        _spellAbilities = await Http.GetFromJsonAsync<List<CharacterSpellAbilityResponse>>($"character-sheets/{SheetId}/spell-abilities") ?? new();
        var bankUrl = _isGmCaller ? "spell-ability-bank" : $"campaigns/{_campaignId}/available-spell-abilities";
        _bankEntries = await Http.GetFromJsonAsync<List<SpellAbilityEntryResponse>>(bankUrl) ?? new();
    }
```

- [ ] **Step 5: Build and run the client test suite**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: PASS, no regressions (this page has no dedicated bUnit suite; this confirms nothing else broke).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: scope FichaDePersonagem's item/spell/image pickers to the campaign for a Jogador caller"
```

---

### Task 11: `FichaDeNpc.razor` — role-conditional item/spell/image sourcing

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`

**Interfaces:**
- Consumes: same campaign-scoped endpoints as Task 10; `NpcSheetResponse.CampaignId` (Task 7).
- Produces: nothing new.

- [ ] **Step 1: Add the role check, `_campaignId`, and reorder image/sheet loading**

Add near the top, alongside `@inject HttpClient Http`:

```razor
@inject AuthenticationStateProvider AuthProvider
```

Add fields near `_myImages` (around line 544):

```csharp
    private bool _isGmCaller;
    private string? _campaignId;
```

Replace `OnInitializedAsync` (around line 593):

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        _isGmCaller = authState.User.IsInRole("GM");

        // LoadSheetAsync sets _campaignId, which LoadImagesAsync now needs — see the identical
        // note in FichaDePersonagem.razor's OnInitializedAsync.
        await LoadSheetAsync();
        await LoadImagesAsync();
        _form.ImageId = _myImages.FirstOrDefault(i => i.Url == _form.ImageUrl)?.Id;

        await Task.WhenAll(
            LoadTabs2And3Async(),
            LoadRacialAbilityAsync(), LoadSpellAbilitiesAsync(), LoadRunesAsync(), LoadMasteriesAsync(),
            LoadInventoryAsync(), LoadArtifactsAsync(), LoadAffectionsAsync(), LoadTraitsAsync());
    }
```

In `LoadSheetAsync` (around line 642-652), set `_campaignId` and remove the now-redundant `_form.ImageId` line:

```csharp
    private async Task LoadSheetAsync()
    {
        var response = await Http.GetAsync($"npc-sheets/{SheetId}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar a ficha.";
            return;
        }

        var sheet = await response.Content.ReadFromJsonAsync<NpcSheetResponse>();
        _campaignId = sheet!.CampaignId;
        _form.ImageUrl = sheet.ImageUrl;
```

(leave the rest of the method's field assignments unchanged — only the `ImageId` line right after `ImageUrl` is removed, and `_campaignId = sheet!.CampaignId;` is new).

- [ ] **Step 2: Scope image loading and the avatar upload**

Replace `LoadImagesAsync` (around line 604):

```csharp
    private async Task LoadImagesAsync()
    {
        var url = _isGmCaller ? "images/mine" : $"campaigns/{_campaignId}/available-images";
        var response = await Http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            _myImages = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>() ?? new();
    }
```

In `UploadImageAsync` (around line 611), add the campaignId form field:

```csharp
    private async Task UploadImageAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 20_000_000));
        content.Add(fileContent, "file", file.Name);
        if (!_isGmCaller && _campaignId is not null)
            content.Add(new StringContent(_campaignId), "campaignId");

        var response = await Http.PostAsync("images", content);
```

(the rest of the method is unchanged).

- [ ] **Step 3: Scope item search**

Replace `SearchItemsByTipoAsync` (around line 577):

```csharp
    private async Task<List<PickerOption>> SearchItemsByTipoAsync(string query, string tipo)
    {
        var url = _isGmCaller
            ? $"items?nome={Uri.EscapeDataString(query)}&tipo={tipo}"
            : $"campaigns/{_campaignId}/available-items?nome={Uri.EscapeDataString(query)}&tipo={tipo}";
        var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        return items.Select(i => new PickerOption(i.Id, i.Nome)).ToList();
    }
```

- [ ] **Step 4: Scope the spell/ability bank list**

Find `LoadSpellAbilitiesAsync` (around line 895) and change the `_bankEntries` line:

```csharp
        var bankUrl = _isGmCaller ? "spell-ability-bank" : $"campaigns/{_campaignId}/available-spell-abilities";
        _bankEntries = await Http.GetFromJsonAsync<List<SpellAbilityEntryResponse>>(bankUrl) ?? new();
```

- [ ] **Step 5: Build and run the client test suite**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: PASS, no regressions.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: scope FichaDeNpc's item/spell/image pickers to the campaign for a Jogador caller"
```

---

### Task 12: `FichaDeCriatura.razor` — role-conditional item/spell/image sourcing

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`

**Interfaces:**
- Consumes: same campaign-scoped endpoints as Task 10/11; `CreatureSheetResponse.CampaignId` (Task 7).
- Produces: nothing new.

- [ ] **Step 1: Add the injection and the new fields**

Add near the top, alongside `@inject HttpClient Http`:

```razor
@inject AuthenticationStateProvider AuthProvider
```

Add fields near `_myImages`:

```csharp
    private bool _isGmCaller;
    private string? _campaignId;
```

- [ ] **Step 2: Reorder `OnInitializedAsync` — `LoadSheetAsync` (sets `_campaignId`) must resolve before `LoadImagesAsync`**

Replace:

```csharp
    protected override async Task OnInitializedAsync()
    {
        // LoadSheetAsync matches the sheet's ImageUrl against _myImages, so images must be
        // loaded first rather than joining the WhenAll below.
        await LoadImagesAsync();
        await Task.WhenAll(
            LoadSheetAsync(), LoadTabs2And3Async(),
            LoadSpellAbilitiesAsync(), LoadMasteriesAsync(),
            LoadSpoilsAsync(), LoadArtifactsAsync(), LoadAffectionsAsync(), LoadTraitsAsync());
    }
```

with:

```csharp
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthProvider.GetAuthenticationStateAsync();
        _isGmCaller = authState.User.IsInRole("GM");

        // LoadSheetAsync sets _campaignId, which LoadImagesAsync now needs (a Jogador's image
        // list is scoped to that campaign) — the reverse of the old ordering, which only needed
        // _myImages loaded first to resolve the sheet's ImageUrl back to an id. That resolution
        // now happens explicitly, right after LoadImagesAsync, instead of inside LoadSheetAsync.
        await LoadSheetAsync();
        await LoadImagesAsync();
        _form.ImageId = _myImages.FirstOrDefault(i => i.Url == _form.ImageUrl)?.Id;

        await Task.WhenAll(
            LoadTabs2And3Async(),
            LoadSpellAbilitiesAsync(), LoadMasteriesAsync(),
            LoadSpoilsAsync(), LoadArtifactsAsync(), LoadAffectionsAsync(), LoadTraitsAsync());
    }
```

- [ ] **Step 3: Scope image loading**

Replace:

```csharp
    private async Task LoadImagesAsync()
    {
        var response = await Http.GetAsync("images/mine");
        if (response.IsSuccessStatusCode)
            _myImages = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>() ?? new();
    }
```

with:

```csharp
    private async Task LoadImagesAsync()
    {
        var url = _isGmCaller ? "images/mine" : $"campaigns/{_campaignId}/available-images";
        var response = await Http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            _myImages = await response.Content.ReadFromJsonAsync<List<ImageSummaryResponse>>() ?? new();
    }
```

- [ ] **Step 4: Scope the avatar upload and stop re-deriving `ImageId` inside `LoadSheetAsync`**

Replace:

```csharp
    private async Task UploadImageAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 20_000_000));
        content.Add(fileContent, "file", file.Name);

        var response = await Http.PostAsync("images", content);
```

with:

```csharp
    private async Task UploadImageAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 20_000_000));
        content.Add(fileContent, "file", file.Name);
        if (!_isGmCaller && _campaignId is not null)
            content.Add(new StringContent(_campaignId), "campaignId");

        var response = await Http.PostAsync("images", content);
```

In `LoadSheetAsync`, replace:

```csharp
        var sheet = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        _form.ImageId = _myImages.FirstOrDefault(i => i.Url == sheet!.ImageUrl)?.Id;
        _form.ImageUrl = sheet!.ImageUrl;
        _form.Nome = sheet!.Nome;
```

with:

```csharp
        var sheet = await response.Content.ReadFromJsonAsync<CreatureSheetResponse>();
        _campaignId = sheet!.CampaignId;
        _form.ImageUrl = sheet.ImageUrl;
        _form.Nome = sheet.Nome;
```

(every other field assignment later in `LoadSheetAsync` is unchanged).

- [ ] **Step 5: Scope both item search methods**

Replace:

```csharp
    private async Task<List<PickerOption>> SearchItemsByTipoAsync(string query, string tipo)
    {
        var response = await Http.GetAsync($"items?nome={Uri.EscapeDataString(query)}&tipo={tipo}");
        if (!response.IsSuccessStatusCode) return new();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        return items.Select(i => new PickerOption(i.Id, i.Nome)).ToList();
    }

    // Espólios has no Tipo restriction server-side (AddSpoil accepts any catalog Item) — unlike
    // Inventário/Weapon/Armor/Artefato, which are each scoped to a single Tipo.
    private async Task<List<PickerOption>> SearchAllItemsAsync(string query)
    {
        var response = await Http.GetAsync($"items?nome={Uri.EscapeDataString(query)}");
        if (!response.IsSuccessStatusCode) return new();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        return items.Select(i => new PickerOption(i.Id, i.Nome)).ToList();
    }
```

with:

```csharp
    private async Task<List<PickerOption>> SearchItemsByTipoAsync(string query, string tipo)
    {
        var url = _isGmCaller
            ? $"items?nome={Uri.EscapeDataString(query)}&tipo={tipo}"
            : $"campaigns/{_campaignId}/available-items?nome={Uri.EscapeDataString(query)}&tipo={tipo}";
        var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        return items.Select(i => new PickerOption(i.Id, i.Nome)).ToList();
    }

    // Espólios has no Tipo restriction server-side (AddSpoil accepts any catalog Item) — unlike
    // Inventário/Weapon/Armor/Artefato, which are each scoped to a single Tipo.
    private async Task<List<PickerOption>> SearchAllItemsAsync(string query)
    {
        var url = _isGmCaller
            ? $"items?nome={Uri.EscapeDataString(query)}"
            : $"campaigns/{_campaignId}/available-items?nome={Uri.EscapeDataString(query)}";
        var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>() ?? new();
        return items.Select(i => new PickerOption(i.Id, i.Nome)).ToList();
    }
```

- [ ] **Step 6: Scope the spell/ability bank list**

Find `LoadSpellAbilitiesAsync` (it fetches `_spellAbilities` from `creature-sheets/{SheetId}/spell-abilities` and then `_bankEntries` from `spell-ability-bank`). Replace its `_bankEntries` line:

```csharp
        _bankEntries = await Http.GetFromJsonAsync<List<SpellAbilityEntryResponse>>("spell-ability-bank") ?? new();
```

with:

```csharp
        var bankUrl = _isGmCaller ? "spell-ability-bank" : $"campaigns/{_campaignId}/available-spell-abilities";
        _bankEntries = await Http.GetFromJsonAsync<List<SpellAbilityEntryResponse>>(bankUrl) ?? new();
```

- [ ] **Step 7: Build and run the client test suite**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: PASS, no regressions.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeCriatura.razor
git commit -m "feat: scope FichaDeCriatura's item/spell/image pickers to the campaign for a Jogador caller"
```

---

### Task 13: Requirements doc updates

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`, `Docs/Requisitos/Requisitos - Campanha.md`, `Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md`, `Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md`

**Interfaces:** none (documentation only).

- [ ] **Step 1: Add R0003 to `Requisitos - Ficha de Personagem.md`**

Insert after the end of the `## 6. Diário` section (after line 355 currently, right before the `# **R0002**` heading — read the file to place it as its own top-level `# **R00xx**` heading immediately following R0002, matching that file's existing style):

```markdown
# **R0003** - Os campos de Item, Magia/Habilidade e Imagem só oferecem o que a campanha liberou.

> Quando um **jogador** edita a própria Ficha de Personagem, ou uma Ficha de NPC/Criatura que lhe foi concedida (Requisitos - Campanha R0010), os campos de 1.a) Imagem, 3.a)-3.c) Armas/Armaduras/Escudos, 4.b) Magias & Habilidades (ao reaproveitar uma entrada do Banco), 5.a)-5.b) Inventário/Artefatos e 6. Diário só listam Itens, entradas do Banco de Magias e Habilidades e Imagens que estão anexados à campanha daquela ficha **e** marcados como públicos (Requisitos - Campanha R0008/R0009) — não o catálogo/banco/biblioteca de imagens inteiro do GM vinculado.
>
> Um **GM** editando qualquer ficha (a própria, ou a de um jogador) continua com acesso irrestrito ao catálogo/banco/imagens, como hoje.
```

- [ ] **Step 2: Add R0012 to `Requisitos - Campanha.md`**

Insert after R0011, continuing the file's numbering:

```markdown
# **R0012** - Conteúdo criado por um jogador é anexado à campanha automaticamente como público.

> Uma Magia/Habilidade que um jogador cria em sua ficha (Requisitos - Ficha de Personagem R0003, Requisitos - Banco de Magias e Habilidades R0001), e uma Imagem que um jogador envia a partir de sua ficha, são anexadas à campanha correspondente automaticamente como **públicas** — sem etapa de aprovação do GM. Isso não se aplica a Itens: um jogador nunca cria um Item, então este anexo automático não existe para eles.
>
> A tela de gerenciamento da campanha (Membros, Anexos, Diário, Notas Secretas, Conceder Ficha, Detalhes) nunca é alcançável por um jogador — a tela dele é a descrita em R0009.
```

- [ ] **Step 3: Add R0007 to `Requisitos - Banco de Magias e Habilidades.md`**

Insert after R0006, continuing the file's numbering:

```markdown
# **R0007** - A cópia criada por um jogador também vira um anexo público da campanha.

> Quando quem cria a entrada (do zero ou reaproveitando outra) é um **jogador**, não o GM que gerencia a ficha, a cópia independente que R0001 já cria no banco também é anexada automaticamente à campanha daquela ficha como pública, conforme Requisitos - Campanha R0012.
```

- [ ] **Step 4: Add R0011 to `Requisitos - Catálogo de Itens e Equipamentos.md`**

Insert after R0010, continuing the file's numbering:

```markdown
# **R0011** - O escopo de R0010 vale para todo campo de Item na ficha, não só o de reaproveitar Imagem.

> R0010 já limita um jogador a imagens de itens anexados-e-públicos à sua campanha. A mesma regra vale de forma geral para qualquer campo de Item nas fichas (Armas, Armaduras, Escudos, Inventário, Artefatos — Requisitos - Ficha de Personagem R0003): um jogador só pode escolher um Item que esteja anexado à campanha da ficha **e** marcado como público (Requisitos - Campanha R0008/R0009), nunca todo o catálogo do GM.
```

- [ ] **Step 5: Commit**

```bash
git add "Docs/Requisitos/Requisitos - Ficha de Personagem.md" "Docs/Requisitos/Requisitos - Campanha.md" "Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md" "Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md"
git commit -m "docs: record the campaign-scoped sheet picker requirement"
```

---

## Final Verification

After all 13 tasks:

- [ ] Run `dotnet build` — expect 0 warnings, 0 errors.
- [ ] Run `dotnet test tests/RuinaRPG.Tests.Client` — expect all green.
- [ ] Run `dotnet test tests/RuinaRPG.Tests.Unit` — expect all green.
- [ ] Run `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignCatalogControllerTests|FullyQualifiedName~ImagesControllerTests|FullyQualifiedName~CharacterSpellAbilitiesControllerTests|FullyQualifiedName~NpcSpellAbilitiesControllerTests|FullyQualifiedName~CreatureSpellAbilitiesControllerTests|FullyQualifiedName~NpcSheetsControllerTests|FullyQualifiedName~CreatureSheetsControllerTests|FullyQualifiedName~CampaignPlayerViewControllerTests"` — expect all green. **Do not run the full integration suite at once** — this sandbox's Docker daemon runs out of disk/containers when every test class's Postgres Testcontainer starts in parallel (see this session's own earlier incident); narrow with `--filter` per the CLAUDE.md guidance.
