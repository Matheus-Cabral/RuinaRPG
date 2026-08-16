# Campanha — Fundação Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a GM create campaigns, add already-linked players as members, and keep a private campaign diary (R0001, R0002, R0003, R0005). **R0004** ("o GM deve poder criar fichas de personagem para os membros da campanha") is deliberately split out: `CharacterSheet.CampaignId` is a required FK (Modelo de Dados §6.1 — unlike `NpcSheets`/`CreatureSheets`, a `CharacterSheet` cannot exist without a campaign), so the sheet-creation endpoint has to be built *after* the `CharacterSheet` entity exists — it belongs to the Ficha de Personagem — Fundação & Informações Básicas plan, which consumes the `Campaign` entity this plan builds. **R0006-R0011** (attachments, granting NPC/Criatura sheets, secret notes) are also out of scope here — they need `Item`/`SpellAbilityBankEntry` (done), `NpcSheet`/`CreatureSheet` (not yet built), so they're the Campanha — Anexos e Concessões plan, later in the queue.

**Architecture:** `Campaign` and `CampaignMember` are ordinary GM-owned entities, same isolation pattern as every other GM-scoped resource in this codebase (`InviteCodesController`, `ItemsController`, `SpellAbilityBankController`). `DiaryEntry`/`DiaryEntryImage`/`DiaryEntryRecipient` are built as the **full three-table family** Modelo de Dados §7 describes in one place — even though this plan only exercises the "campaign diary" case (`CampaignId` set, `IsSecretNote = false`) — because the Ficha de Personagem diary and the Campanha Notas Secretas (both later work) reuse these exact same tables with a different FK populated, and building the family twice would risk the two copies drifting.

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Campanha.md` R0001-R0003, R0005 (R0004, R0006-R0011 deferred as noted above), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §5 (Campanhas) and §7 (Diário e Notas Secretas).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Only the **GM** who owns a campaign may create/edit/delete anything in it — `[Authorize(Roles = "GM")]` on every endpoint in this plan. Player-facing campaign views are out of scope for this plan (they depend on R0009's public/private attachment model, itself deferred to Campanha — Anexos).
- A player can only be added as a campaign member if already linked to that GM's account (`InvitedByGmId`, from the Convite de Jogador plan) — never let a GM add an arbitrary user as a member.
- The campaign diary (R0005) is GM-only, same as `InviteCodesController`'s isolation pattern — never visible to players in this plan (campaign-member player views arrive with Campanha — Anexos' R0009).
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Infrastructure — `Campaign` entity and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Campaigns/Campaign.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CampaignMigrationTests.cs`

**Interfaces:**
- Produces: `Campaign` (`Guid Id`, `Guid GmId`, `string Nome`, `string Descricao`), `RuinaRpgDbContext.Campaigns : DbSet<Campaign>`. Tasks 2-3 and every later task in this plan depend on this shape. The Ficha de Personagem — Fundação plan's `CharacterSheet.CampaignId` FK depends on `Campaign.Id` being stable.

- [ ] **Step 1: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CampaignMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CampaignMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CampaignMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Campaigns_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCampaigns"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@camptest.com", Email = "gm@camptest.com", Nickname = "CampTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.Campaigns.Add(new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "A Ruína Aguarda", Descricao = "Uma campanha de teste." });
        await db.SaveChangesAsync();

        (await db.Campaigns.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignMigrationTests`
Expected: FAIL — no `AddCampaigns` migration exists yet.

- [ ] **Step 3: Write `Campaign`**

`src/RuinaRPG.Infrastructure/Campaigns/Campaign.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Campaigns;

public class Campaign
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
}
```

- [ ] **Step 4: Register the DbSet in `RuinaRpgDbContext`**

Add `using RuinaRPG.Infrastructure.Campaigns;` and:

```csharp
public DbSet<Campaign> Campaigns => Set<Campaign>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<Campaign>(entity =>
{
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(c => c.GmId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddCampaigns \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignMigrationTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Campaigns src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/CampaignMigrationTests.cs
git commit -m "feat: add Campaign entity and migration"
```

---

### Task 2: Contracts + Api — create and list campaigns (R0001, R0002)

**Files:**
- Create: `src/RuinaRPG.Contracts/Campaigns/CreateCampaignRequest.cs`
- Create: `src/RuinaRPG.Contracts/Campaigns/CampaignResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CampaignsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`

**Interfaces:**
- Consumes: `Campaign` (Task 1).
- Produces: `POST /api/campaigns` → `201` + `CampaignResponse`, `[Authorize(Roles = "GM")]`. `GET /api/campaigns` → `200` + `List<CampaignResponse>`, scoped to the caller's own campaigns. `CreateCampaignRequest(string Nome, string Descricao)`; `CampaignResponse(string Id, string Nome, string Descricao)`. Tasks 3-7 and the Client tasks depend on both shapes.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/Campaigns/CreateCampaignRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record CreateCampaignRequest(string Nome, string Descricao);
```

`src/RuinaRPG.Contracts/Campaigns/CampaignResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record CampaignResponse(string Id, string Nome, string Descricao);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CampaignsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CampaignsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/campaigns", new CreateCampaignRequest("Nome", "Desc"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_returns_201_with_the_campaign()
    {
        var token = await RegisterGmAndGetTokenAsync("CampGm1", "camp1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", token, new CreateCampaignRequest("A Ruína Aguarda", "Descrição")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignResponse>();
        body!.Nome.Should().Be("A Ruína Aguarda");
    }

    [Fact]
    public async Task List_returns_only_campaigns_owned_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("CampGmA", "campgma@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("CampGmB", "campgmb@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenA, new CreateCampaignRequest("Campanha A", "")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", tokenB, new CreateCampaignRequest("Campanha B", "")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/campaigns", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>();
        body!.Should().ContainSingle(c => c.Nome == "Campanha A");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignsControllerTests`
Expected: FAIL — `/api/campaigns` doesn't exist yet.

- [ ] **Step 4: Write `CampaignsController`**

`src/RuinaRPG.Api/Controllers/CampaignsController.cs`:

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignsControllerTests`
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Campaigns src/RuinaRPG.Api/Controllers/CampaignsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs
git commit -m "feat: add campaign creation and listing"
```

---

### Task 3: Infrastructure + Api — campaign membership (R0003)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Campaigns/CampaignMember.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Create: `src/RuinaRPG.Contracts/Campaigns/AddCampaignMemberRequest.cs`
- Create: `src/RuinaRPG.Contracts/Campaigns/CampaignMemberResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CampaignMemberMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignsController` (Task 2), `ApplicationUser.InvitedByGmId` (Convite de Jogador plan, already built).
- Produces: `CampaignMember` (`Guid Id`, `Guid CampaignId`, `Guid UserId`), `RuinaRpgDbContext.CampaignMembers`. `POST /api/campaigns/{campaignId}/members` → `204` on success, `400` if the target user isn't linked to the caller's account, `404` if the campaign doesn't exist or isn't owned by the caller. `GET /api/campaigns/{campaignId}/members` → `200` + `List<CampaignMemberResponse>`. `AddCampaignMemberRequest(string UserId)`; `CampaignMemberResponse(string UserId, string Nickname, string Email)`.

- [ ] **Step 1: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CampaignMemberMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CampaignMemberMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CampaignMemberMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CampaignMembers_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCampaignMembers"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@membertest.com", Email = "gm@membertest.com", Nickname = "MemberTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@membertest.com", Email = "player@membertest.com", Nickname = "MemberTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();

        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        db.CampaignMembers.Add(new CampaignMember { Id = Guid.NewGuid(), CampaignId = campaign.Id, UserId = player.Id });
        await db.SaveChangesAsync();

        (await db.CampaignMembers.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignMemberMigrationTests`
Expected: FAIL — `CampaignMember` doesn't exist yet, then fails on the missing migration once it does.

- [ ] **Step 3: Write `CampaignMember`**

`src/RuinaRPG.Infrastructure/Campaigns/CampaignMember.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Campaigns;

public class CampaignMember
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid UserId { get; set; }
}
```

- [ ] **Step 4: Register the DbSet and relationships**

Add to `RuinaRpgDbContext`:

```csharp
public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<CampaignMember>(entity =>
{
    entity.HasIndex(m => new { m.CampaignId, m.UserId }).IsUnique();
    entity.HasOne<Campaign>()
        .WithMany()
        .HasForeignKey(m => m.CampaignId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(m => m.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddCampaignMembers \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the migration test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignMemberMigrationTests`
Expected: PASS.

- [ ] **Step 7: Write the contracts**

`src/RuinaRPG.Contracts/Campaigns/AddCampaignMemberRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record AddCampaignMemberRequest(string UserId);
```

`src/RuinaRPG.Contracts/Campaigns/CampaignMemberResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record CampaignMemberResponse(string UserId, string Nickname, string Email);
```

- [ ] **Step 8: Write the failing controller tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`, inside the class:

```csharp
private async Task<string> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
{
    var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
    var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

    var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
        new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
    var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

    var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
    me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    var meResponse = await _client.SendAsync(me);
    var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
    return meBody!.Id;
}

private async Task<string> CreateCampaignAsync(string gmToken, string nome)
{
    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
    return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
}

[Fact]
public async Task AddMember_a_player_linked_to_the_caller_returns_204_and_they_appear_in_members()
{
    var gmToken = await RegisterGmAndGetTokenAsync("CampMemberGm1", "campmember1@teste.com");
    var playerId = await RegisterJogadorLinkedToAsync(gmToken, "CampMemberPlayer1", "campmemberplayer1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Membro");

    var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/members", gmToken));
    var body = await listResponse.Content.ReadFromJsonAsync<List<CampaignMemberResponse>>();
    body!.Should().ContainSingle(m => m.Nickname == "CampMemberPlayer1");
}

[Fact]
public async Task AddMember_a_player_not_linked_to_the_caller_returns_400()
{
    var gmTokenA = await RegisterGmAndGetTokenAsync("CampMemberGmA", "campmembergma@teste.com");
    var gmTokenB = await RegisterGmAndGetTokenAsync("CampMemberGmB", "campmembergmb@teste.com");
    var playerOfB = await RegisterJogadorLinkedToAsync(gmTokenB, "CampMemberPlayerB", "campmemberplayerb@teste.com");
    var campaignOfA = await CreateCampaignAsync(gmTokenA, "Campanha de A");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfA}/members", gmTokenA, new AddCampaignMemberRequest(playerOfB)));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task AddMember_to_a_campaign_owned_by_another_gm_returns_404()
{
    var gmTokenOwner = await RegisterGmAndGetTokenAsync("CampMemberOwner", "campmemberowner@teste.com");
    var gmTokenOther = await RegisterGmAndGetTokenAsync("CampMemberOther", "campmemberother@teste.com");
    var playerOfOther = await RegisterJogadorLinkedToAsync(gmTokenOther, "CampMemberPlayerOther", "campmemberplayerother@teste.com");
    var campaignOfOwner = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignOfOwner}/members", gmTokenOther, new AddCampaignMemberRequest(playerOfOther)));

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

Add `using RuinaRPG.Contracts.Auth;` at the top if not already present (it already is).

- [ ] **Step 9: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignsControllerTests`
Expected: the 3 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 10: Add the membership actions**

In `src/RuinaRPG.Api/Controllers/CampaignsController.cs`, add `using Microsoft.AspNetCore.Identity;` and `using RuinaRPG.Infrastructure.Identity;`, change the constructor to also take `UserManager<ApplicationUser>`:

```csharp
public class CampaignsController(RuinaRpgDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
```

Then add:

```csharp
[HttpPost("{campaignId}/members")]
public async Task<IActionResult> AddMember(Guid campaignId, AddCampaignMemberRequest request)
{
    var gmId = CurrentGmId();
    var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId && c.GmId == gmId);
    if (campaign is null)
        return NotFound();

    var playerId = Guid.Parse(request.UserId);
    var player = await userManager.FindByIdAsync(request.UserId);
    if (player is null || player.InvitedByGmId != gmId)
        return BadRequest("O jogador informado não está vinculado à sua conta.");

    if (await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == playerId))
        return NoContent(); // already a member — idempotent, not an error

    db.CampaignMembers.Add(new CampaignMember { Id = Guid.NewGuid(), CampaignId = campaignId, UserId = playerId });
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
```

- [ ] **Step 11: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignsControllerTests`
Expected: PASS (6/6).

- [ ] **Step 12: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Campaigns src/RuinaRPG.Infrastructure/Persistence src/RuinaRPG.Contracts/Campaigns src/RuinaRPG.Api/Controllers/CampaignsController.cs tests/RuinaRPG.Tests.Integration
git commit -m "feat: add campaign membership"
```

---

### Task 4: Infrastructure — diary entity family and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Diary/DiaryEntry.cs`
- Create: `src/RuinaRPG.Infrastructure/Diary/DiaryEntryImage.cs`
- Create: `src/RuinaRPG.Infrastructure/Diary/DiaryEntryRecipient.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/DiaryMigrationTests.cs`

**Interfaces:**
- Consumes: `Image` (Catálogo de Itens e Equipamentos plan, already built).
- Produces: `DiaryEntry` (`Guid Id`, `Guid AuthorUserId`, `Guid? CharacterSheetId`, `Guid? CampaignId`, `bool IsSecretNote`, `string Texto`, `DateTime CreatedAt`); `DiaryEntryImage` (`Guid DiaryEntryId`, `Guid ImageId`); `DiaryEntryRecipient` (`Guid DiaryEntryId`, `Guid UserId`). `RuinaRpgDbContext.DiaryEntries`/`DiaryEntryImages`/`DiaryEntryRecipients`. Task 5 (this plan's campaign-diary endpoints) and later plans (Personagem's own diary, Campanha — Anexos' secret notes) depend on this exact shape. `CharacterSheetId` stays unused (always null) until the Ficha de Personagem plan exists — that's expected, not a defect.

- [ ] **Step 1: Write the entities**

`src/RuinaRPG.Infrastructure/Diary/DiaryEntry.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Diary;

public class DiaryEntry
{
    public Guid Id { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? CharacterSheetId { get; set; }
    public Guid? CampaignId { get; set; }
    public bool IsSecretNote { get; set; }
    public required string Texto { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Diary/DiaryEntryImage.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Diary;

public class DiaryEntryImage
{
    public Guid DiaryEntryId { get; set; }
    public Guid ImageId { get; set; }
}
```

`src/RuinaRPG.Infrastructure/Diary/DiaryEntryRecipient.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Diary;

public class DiaryEntryRecipient
{
    public Guid DiaryEntryId { get; set; }
    public Guid UserId { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/DiaryMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class DiaryMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DiaryMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_diary_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddDiary"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@diarytest.com", Email = "gm@diarytest.com", Nickname = "DiaryTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var entry = new DiaryEntry { Id = Guid.NewGuid(), AuthorUserId = gm.Id, CampaignId = campaign.Id, IsSecretNote = false, Texto = "Primeira entrada.", CreatedAt = DateTime.UtcNow };
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync();

        (await db.DiaryEntries.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter DiaryMigrationTests`
Expected: FAIL — no `AddDiary` migration exists yet.

- [ ] **Step 4: Register the DbSets and relationships**

Add `using RuinaRPG.Infrastructure.Diary;` and:

```csharp
public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
public DbSet<DiaryEntryImage> DiaryEntryImages => Set<DiaryEntryImage>();
public DbSet<DiaryEntryRecipient> DiaryEntryRecipients => Set<DiaryEntryRecipient>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<DiaryEntry>(entity =>
{
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(d => d.AuthorUserId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Campaign>()
        .WithMany()
        .HasForeignKey(d => d.CampaignId)
        .OnDelete(DeleteBehavior.Cascade);
    // CharacterSheetId's FK is added by the Ficha de Personagem plan once CharacterSheets exists —
    // left as a plain nullable Guid column here, no FK constraint yet.
});

builder.Entity<DiaryEntryImage>(entity =>
{
    entity.HasKey(i => new { i.DiaryEntryId, i.ImageId });
    entity.HasOne<DiaryEntry>()
        .WithMany()
        .HasForeignKey(i => i.DiaryEntryId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Image>()
        .WithMany()
        .HasForeignKey(i => i.ImageId)
        .OnDelete(DeleteBehavior.Cascade);
});

builder.Entity<DiaryEntryRecipient>(entity =>
{
    entity.HasKey(r => new { r.DiaryEntryId, r.UserId });
    entity.HasOne<DiaryEntry>()
        .WithMany()
        .HasForeignKey(r => r.DiaryEntryId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(r => r.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

Add `using RuinaRPG.Infrastructure.Images;` if not already present (for the `Image` reference).

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddDiary \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter DiaryMigrationTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Diary src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/DiaryMigrationTests.cs
git commit -m "feat: add diary entity family and migration"
```

---

### Task 5: Api — campaign diary CRUD (R0005)

**Files:**
- Create: `src/RuinaRPG.Contracts/Diary/CreateDiaryEntryRequest.cs`
- Create: `src/RuinaRPG.Contracts/Diary/UpdateDiaryEntryRequest.cs`
- Create: `src/RuinaRPG.Contracts/Diary/DiaryEntryResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`

**Interfaces:**
- Consumes: `CampaignsController` (Tasks 2-3), `DiaryEntry`/`DiaryEntryImage` (Task 4), `Image` (Catálogo plan).
- Produces: `POST /api/campaigns/{campaignId}/diary` → `201` + `DiaryEntryResponse`; `GET /api/campaigns/{campaignId}/diary` → `200` + `List<DiaryEntryResponse>` (newest first); `PUT /api/campaigns/{campaignId}/diary/{entryId}` → `204`/`404`; `DELETE /api/campaigns/{campaignId}/diary/{entryId}` → `204`/`404`. All GM-only, scoped to campaigns the caller owns. `CreateDiaryEntryRequest(string Texto, List<string> ImageIds)`; `UpdateDiaryEntryRequest(string Texto, List<string> ImageIds)`; `DiaryEntryResponse(string Id, string Texto, DateTime CreatedAt, List<string> ImageUrls)`.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/Diary/CreateDiaryEntryRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Diary;

public record CreateDiaryEntryRequest(string Texto, List<string> ImageIds);
```

`src/RuinaRPG.Contracts/Diary/UpdateDiaryEntryRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Diary;

public record UpdateDiaryEntryRequest(string Texto, List<string> ImageIds);
```

`src/RuinaRPG.Contracts/Diary/DiaryEntryResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Diary;

public record DiaryEntryResponse(string Id, string Texto, DateTime CreatedAt, List<string> ImageUrls);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs`, inside the class (add `using RuinaRPG.Contracts.Diary;` at the top):

```csharp
[Fact]
public async Task Diary_create_and_list_round_trips_an_entry()
{
    var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGm1", "campdiary1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Diário");

    var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
        new CreateDiaryEntryRequest("Os jogadores chegaram à vila.", [])));
    createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/diary", gmToken));
    var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
    body!.Should().ContainSingle(e => e.Texto == "Os jogadores chegaram à vila.");
}

[Fact]
public async Task Diary_update_changes_the_text()
{
    var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGm2", "campdiary2@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Diário 2");
    var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
        new CreateDiaryEntryRequest("Texto original.", [])));
    var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

    var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/diary/{entryId}", gmToken,
        new UpdateDiaryEntryRequest("Texto revisado.", [])));
    updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/diary", gmToken));
    var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
    body!.Should().ContainSingle(e => e.Texto == "Texto revisado.");
}

[Fact]
public async Task Diary_delete_removes_the_entry()
{
    var gmToken = await RegisterGmAndGetTokenAsync("CampDiaryGm3", "campdiary3@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Diário 3");
    var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmToken,
        new CreateDiaryEntryRequest("Para excluir.", [])));
    var entryId = (await createResponse.Content.ReadFromJsonAsync<DiaryEntryResponse>())!.Id;

    var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campaignId}/diary/{entryId}", gmToken));
    deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/diary", gmToken));
    var body = await listResponse.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>();
    body!.Should().BeEmpty();
}

[Fact]
public async Task Diary_actions_on_a_campaign_owned_by_another_gm_return_404()
{
    var gmTokenOwner = await RegisterGmAndGetTokenAsync("CampDiaryOwner", "campdiaryowner@teste.com");
    var gmTokenOther = await RegisterGmAndGetTokenAsync("CampDiaryOther", "campdiaryother@teste.com");
    var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha Privada");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/diary", gmTokenOther,
        new CreateDiaryEntryRequest("Invasão.", [])));

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignsControllerTests`
Expected: the 4 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 4: Add the diary actions**

In `src/RuinaRPG.Api/Controllers/CampaignsController.cs`, add `using RuinaRPG.Contracts.Diary;` and `using RuinaRPG.Infrastructure.Diary;`, then:

```csharp
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
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CampaignsControllerTests`
Expected: PASS (10/10).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Diary src/RuinaRPG.Api/Controllers/CampaignsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignsControllerTests.cs
git commit -m "feat: add campaign diary CRUD"
```

---

### Task 6: Client — campaign list, create, and detail pages

**Files:**
- Create: `src/RuinaRPG.Client/Pages/Campanhas.razor`
- Create: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `CampaignResponse`/`CreateCampaignRequest` (Task 2), `CampaignMemberResponse`/`AddCampaignMemberRequest` (Task 3), `DiaryEntryResponse`/`CreateDiaryEntryRequest` (Task 5), `PlayerSearchResultResponse` (Convite de Jogador plan, for picking members), the authenticated `HttpClient`.
- Produces: the `/campanhas` and `/campanhas/{id}` routes. No later task in this plan depends on these files, but the Ficha de Personagem — Fundação plan's "create a sheet for a member" UI links from `/campanhas/{id}`.

- [ ] **Step 1: Write the list/create page**

`src/RuinaRPG.Client/Pages/Campanhas.razor`:

```razor
@page "/campanhas"
@inject HttpClient Http
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Campaigns

<h1>Campanhas</h1>

<EditForm Model="_form" OnValidSubmit="CreateAsync">
    <label>Nome <InputText @bind-Value="_form.Nome" /></label>
    <label>Descrição <InputTextArea @bind-Value="_form.Descricao" /></label>
    <button type="submit">Criar Campanha</button>
</EditForm>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<ul>
    @foreach (var campaign in _campaigns)
    {
        <li>
            <a href="@($"campanhas/{campaign.Id}")">@campaign.Nome</a>
        </li>
    }
</ul>

@code {
    private readonly CampaignFormModel _form = new();
    private List<CampaignResponse> _campaigns = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var response = await Http.GetAsync("campaigns");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar as campanhas.";
            return;
        }

        _campaigns = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>() ?? new();
        _errorMessage = null;
    }

    private async Task CreateAsync()
    {
        var response = await Http.PostAsJsonAsync("campaigns", new CreateCampaignRequest(_form.Nome, _form.Descricao));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível criar a campanha.";
            return;
        }

        _form.Nome = "";
        _form.Descricao = "";
        await LoadAsync();
    }

    private class CampaignFormModel
    {
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
    }
}
```

- [ ] **Step 2: Write the detail page**

`src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`:

```razor
@page "/campanhas/{CampaignId}"
@inject HttpClient Http
@using RuinaRPG.Contracts.Campaigns
@using RuinaRPG.Contracts.Diary
@using RuinaRPG.Contracts.Players

<h1>Campanha</h1>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<h2>Membros</h2>
<input @bind="_memberSearch" placeholder="Buscar jogador para adicionar" />
<button @onclick="SearchPlayersAsync">Buscar</button>
<ul>
    @foreach (var player in _searchResults)
    {
        <li>@player.Nickname (@player.Email) <button @onclick="@(() => AddMemberAsync(player.Id))">Adicionar</button></li>
    }
</ul>
<ul>
    @foreach (var member in _members)
    {
        <li>@member.Nickname (@member.Email)</li>
    }
</ul>

<h2>Diário</h2>
<EditForm Model="_diaryForm" OnValidSubmit="AddDiaryEntryAsync">
    <label>Texto <InputTextArea @bind-Value="_diaryForm.Texto" /></label>
    <button type="submit">Adicionar Entrada</button>
</EditForm>
<ul>
    @foreach (var entry in _diaryEntries)
    {
        <li>@entry.CreatedAt.ToString("g") — @entry.Texto</li>
    }
</ul>

@code {
    [Parameter] public string CampaignId { get; set; } = "";

    private List<CampaignMemberResponse> _members = new();
    private List<DiaryEntryResponse> _diaryEntries = new();
    private List<PlayerSearchResultResponse> _searchResults = new();
    private string _memberSearch = "";
    private readonly DiaryFormModel _diaryForm = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => Task.WhenAll(LoadMembersAsync(), LoadDiaryAsync());

    private async Task LoadMembersAsync()
    {
        var response = await Http.GetAsync($"campaigns/{CampaignId}/members");
        if (response.IsSuccessStatusCode)
            _members = await response.Content.ReadFromJsonAsync<List<CampaignMemberResponse>>() ?? new();
    }

    private async Task LoadDiaryAsync()
    {
        var response = await Http.GetAsync($"campaigns/{CampaignId}/diary");
        if (response.IsSuccessStatusCode)
            _diaryEntries = await response.Content.ReadFromJsonAsync<List<DiaryEntryResponse>>() ?? new();
    }

    private async Task SearchPlayersAsync()
    {
        var response = await Http.GetAsync($"players?q={Uri.EscapeDataString(_memberSearch)}");
        if (response.IsSuccessStatusCode)
            _searchResults = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>() ?? new();
    }

    private async Task AddMemberAsync(string playerId)
    {
        var response = await Http.PostAsJsonAsync($"campaigns/{CampaignId}/members", new AddCampaignMemberRequest(playerId));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar o membro.";
            return;
        }

        await LoadMembersAsync();
    }

    private async Task AddDiaryEntryAsync()
    {
        var response = await Http.PostAsJsonAsync($"campaigns/{CampaignId}/diary", new CreateDiaryEntryRequest(_diaryForm.Texto, new List<string>()));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar a entrada.";
            return;
        }

        _diaryForm.Texto = "";
        await LoadDiaryAsync();
    }

    private class DiaryFormModel
    {
        public string Texto { get; set; } = "";
    }
}
```

- [ ] **Step 3: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/campanhas` following the file's established pattern — label it "Campanhas".

- [ ] **Step 4: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Pages/Campanhas.razor src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add campaign list, create, and detail pages"
```

---

### Task 7: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files — this task only exercises what Tasks 1-6 built.

**Interfaces:**
- Consumes: the full stack, including everything this plan added.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers come up; migrations (including `AddCampaigns`, `AddCampaignMembers`, `AddDiary`) apply automatically in Development.

- [ ] **Step 2: Register a GM, get a token, create a campaign, add a diary entry, through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/gm \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeCampanhaGm","email":"smokecampanhagm@teste.com","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' \
  | tee /tmp/gm-register.json
TOKEN=$(jq -r .accessToken /tmp/gm-register.json)

curl -sf -X POST http://localhost/api/campaigns \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"nome":"A Ruína Aguarda","descricao":"Campanha de fumaça"}' \
  | tee /tmp/campaign-create.json
CAMPAIGN_ID=$(jq -r .id /tmp/campaign-create.json)

curl -sf -X POST "http://localhost/api/campaigns/$CAMPAIGN_ID/diary" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"texto":"Os jogadores chegaram à vila.","imageIds":[]}'
```

Expected: campaign create returns HTTP 201; diary entry create returns HTTP 201.

- [ ] **Step 3: Confirm the diary entry appears on list through nginx**

```bash
curl -sf "http://localhost/api/campaigns/$CAMPAIGN_ID/diary" -H "Authorization: Bearer $TOKEN"
```

Expected: HTTP 200, JSON array containing the diary entry just created.

- [ ] **Step 4: Tear down**

```bash
make down
```

- [ ] **Step 5: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the campanha foundation flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- R0004 (create a character sheet for a campaign member) — depends on `CharacterSheet` existing; implemented by the Ficha de Personagem — Fundação & Informações Básicas plan, which consumes this plan's `Campaign` entity.
- R0006-R0011 (attaching items/NPC/Criatura sheets/bank entries, public/private toggles, granting sheets to players, secret notes) — depend on `NpcSheet`/`CreatureSheet` existing; implemented by the Campanha — Anexos e Concessões plan, later in the queue.
- Any player-facing campaign view — R0009's "jogador membro só vê..." rule is meaningless before R0006-R0008's attachment/visibility model exists; this plan's diary and membership endpoints are GM-only throughout.
- Removing a campaign member — no requirement in this doc asks for it; only adding is specified (R0003).
