# Convite de Jogador Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a GM generate, list, and revoke invite codes, search the players linked to their account and reset a player's password, and let a Jogador finally register (using a code) — unblocking the second half of Login e Cadastro R0003, which the Foundation plan deliberately left out of scope.

**Architecture:** Extends the existing `AuthController`/Identity foundation rather than introducing a new subsystem: a new `InviteCode` entity + EF Core migration, two new controllers (`InviteCodesController`, `PlayersController`) both scoped to the authenticated GM's own data via `[Authorize(Roles = "GM")]`, and a new `RegisterJogador` action alongside the existing `RegisterGm` on `AuthController`. Invite-code status (Ativo/Usado/Revogado/Expirado) is never stored — it's computed on read from `RevokedAt`/`RedeemedByUserId`/`ExpiresAt`, by a pure function in `RuinaRPG.Domain` shared between the list endpoint and redemption validation, so the two can never disagree. On the client, the existing `/cadastro` page gains a Jogador tab, and two new GM-only pages are added — but first, the Client needs actual Bearer-token attachment on outgoing requests, which nothing in the Foundation plan built (its only authenticated caller, `/me`, was never called from the Client).

**Tech Stack:** Same as Foundation — ASP.NET Core 8 Web API (controllers), EF Core 8 + Npgsql/PostgreSQL, ASP.NET Core Identity, Blazor WebAssembly 8, xUnit + FluentAssertions + Testcontainers.PostgreSql, plus `Microsoft.Extensions.Http` (new, for the Client's `IHttpClientFactory`-based Bearer-token handler).

**Spec:** `Docs/Requisitos/Requisitos - Convite de Jogador.md` (all 7 requirements), `Docs/Requisitos/Requisitos - Login e Cadastro.md` R0003 (Jogador tab), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §1 (`InviteCodes` table), `Docs/Requisitos/Requisitos - Técnico.md` R0006 (rate limiting).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Invite codes are 8-character alphanumeric strings, unique in the system, valid for 48 hours from generation (Convite de Jogador R0001).
- Status is always computed, never stored, with this precedence: Revogado > Usado > Expirado > Ativo (Convite de Jogador R0002/R0004/R0005 read together — a revoked code stays Revogado even past its expiry or if somehow redeemed; a redeemed code stays Usado even past its expiry).
- Every GM-only endpoint scopes its query to the authenticated GM's own data (`GmId` on invite codes, `InvitedByGmId` on players) — never return or act on another GM's codes or players.
- Invite-code redemption shares the same per-IP `"login"` rate-limit policy as login/refresh (Técnico R0006 names it explicitly, guarding the 8-character code against brute force).
- No secret ever hardcoded — carried forward from Técnico R0003, unaffected by this plan.
- The known rate-limiter/Cloudflare-Tunnel topology gap from the Foundation plan is explicitly OUT of scope here — do not touch `ForwardedHeadersOptions` or `nginx.conf`'s proxy blocks in this plan.

---

### Task 1: Domain — invite code status calculation

**Files:**
- Create: `src/RuinaRPG.Domain/Invites/InviteCodeStatus.cs`
- Create: `src/RuinaRPG.Domain/Invites/InviteCodeStatusCalculator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Invites/InviteCodeStatusCalculatorTests.cs`

**Interfaces:**
- Produces: `enum InviteCodeStatus { Ativo, Usado, Revogado, Expirado }`; `InviteCodeStatusCalculator.Compute(DateTime? revokedAt, Guid? redeemedByUserId, DateTime expiresAt, DateTime now) : InviteCodeStatus`. Tasks 2 (entity shape it mirrors), 3 (list/generate), 4 (revoke), 5 (redemption) all call this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Invites/InviteCodeStatusCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Invites;

namespace RuinaRPG.Tests.Unit.Invites;

public class InviteCodeStatusCalculatorTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Compute_returns_Ativo_when_not_revoked_not_redeemed_and_not_expired()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: null, redeemedByUserId: null, expiresAt: Now.AddHours(1), now: Now);

        status.Should().Be(InviteCodeStatus.Ativo);
    }

    [Fact]
    public void Compute_returns_Expirado_once_expiresAt_has_passed()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: null, redeemedByUserId: null, expiresAt: Now.AddHours(-1), now: Now);

        status.Should().Be(InviteCodeStatus.Expirado);
    }

    [Fact]
    public void Compute_returns_Usado_when_redeemed_even_if_also_expired()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: null, redeemedByUserId: Guid.NewGuid(), expiresAt: Now.AddHours(-1), now: Now);

        status.Should().Be(InviteCodeStatus.Usado);
    }

    [Fact]
    public void Compute_returns_Revogado_even_if_also_redeemed_or_expired()
    {
        var status = InviteCodeStatusCalculator.Compute(
            revokedAt: Now.AddMinutes(-5), redeemedByUserId: Guid.NewGuid(), expiresAt: Now.AddHours(-1), now: Now);

        status.Should().Be(InviteCodeStatus.Revogado);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter InviteCodeStatusCalculatorTests`
Expected: FAIL to compile — `InviteCodeStatusCalculator`/`InviteCodeStatus` don't exist yet.

- [ ] **Step 3: Write the enum and the calculator**

`src/RuinaRPG.Domain/Invites/InviteCodeStatus.cs`:

```csharp
namespace RuinaRPG.Domain.Invites;

public enum InviteCodeStatus
{
    Ativo,
    Usado,
    Revogado,
    Expirado
}
```

`src/RuinaRPG.Domain/Invites/InviteCodeStatusCalculator.cs`:

```csharp
namespace RuinaRPG.Domain.Invites;

public static class InviteCodeStatusCalculator
{
    public static InviteCodeStatus Compute(DateTime? revokedAt, Guid? redeemedByUserId, DateTime expiresAt, DateTime now)
    {
        if (revokedAt is not null)
            return InviteCodeStatus.Revogado;

        if (redeemedByUserId is not null)
            return InviteCodeStatus.Usado;

        if (now >= expiresAt)
            return InviteCodeStatus.Expirado;

        return InviteCodeStatus.Ativo;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter InviteCodeStatusCalculatorTests`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/Invites tests/RuinaRPG.Tests.Unit/Invites
git commit -m "feat: add invite code status calculation"
```

---

### Task 2: Infrastructure — InviteCode entity and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Invites/InviteCode.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/InviteCodeMigrationTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1 directly (the entity stores raw fields; status is computed by callers).
- Produces: `InviteCode` (`Guid Id`, `string Code`, `Guid GmId`, `DateTime GeneratedAt`, `DateTime ExpiresAt`, `DateTime? RevokedAt`, `Guid? RedeemedByUserId`, `DateTime? RedeemedAt`), `RuinaRpgDbContext.InviteCodes : DbSet<InviteCode>`. Tasks 3-5 depend on this shape exactly.

- [ ] **Step 1: Write the entity**

`src/RuinaRPG.Infrastructure/Invites/InviteCode.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Invites;

public class InviteCode
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public Guid GmId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RedeemedByUserId { get; set; }
    public DateTime? RedeemedAt { get; set; }
}
```

- [ ] **Step 2: Register the DbSet and constraints in `RuinaRpgDbContext`**

In `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`, add the using and the `DbSet`:

```csharp
using RuinaRPG.Infrastructure.Invites;
```

```csharp
public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
```

Inside `OnModelCreating`, after the existing `builder.Entity<RefreshToken>(...)` block, add:

```csharp
builder.Entity<InviteCode>(entity =>
{
    entity.HasIndex(c => c.Code).IsUnique();
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(c => c.GmId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(c => c.RedeemedByUserId)
        .OnDelete(DeleteBehavior.SetNull);
});
```

- [ ] **Step 3: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/InviteCodeMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class InviteCodeMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public InviteCodeMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_InviteCodes_table_with_a_unique_index_on_Code()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddInviteCodes"));

        var gm = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "gm@migrationtest.com",
            Email = "gm@migrationtest.com",
            Nickname = "MigrationGm",
            Role = UserRole.GM
        };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.InviteCodes.Add(new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = "DUPLICAT",
            GmId = gm.Id,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        });
        await db.SaveChangesAsync();

        db.InviteCodes.Add(new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = "DUPLICAT",
            GmId = gm.Id,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        });

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter InviteCodeMigrationTests`
Expected: FAIL — no `AddInviteCodes` migration exists yet, so `GetAppliedMigrationsAsync()` doesn't contain it (and `db.InviteCodes`/`InviteCode` may not even compile until Step 1-2 are saved; save those first, then this step should fail specifically on the missing migration).

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddInviteCodes \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter InviteCodeMigrationTests`
Expected: PASS (requires Docker running).

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure tests/RuinaRPG.Tests.Integration/Persistence/InviteCodeMigrationTests.cs
git commit -m "feat: add InviteCode entity and migration"
```

---

### Task 3: Api — generate and list invite codes (R0001, R0002)

**Files:**
- Create: `src/RuinaRPG.Contracts/Invites/InviteCodeResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/InviteCodesController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/InviteCodesControllerTests.cs`

**Interfaces:**
- Consumes: `InviteCode` (Task 2), `InviteCodeStatusCalculator.Compute` (Task 1).
- Produces: `POST /api/invite-codes` → `201` + `InviteCodeResponse`; `GET /api/invite-codes` → `200` + `List<InviteCodeResponse>`, both `[Authorize(Roles = "GM")]`. `InviteCodeResponse(string Code, string Status, DateTime GeneratedAt, DateTime? ExpiresAt, string? RedeemedByNickname, string? RedeemedByEmail, DateTime? RedeemedAt)` — Tasks 4, 10 depend on this shape.

- [ ] **Step 1: Write the response contract**

`src/RuinaRPG.Contracts/Invites/InviteCodeResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Invites;

public record InviteCodeResponse(
    string Code,
    string Status,
    DateTime GeneratedAt,
    DateTime? ExpiresAt,
    string? RedeemedByNickname,
    string? RedeemedByEmail,
    DateTime? RedeemedAt);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/InviteCodesControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;

namespace RuinaRPG.Tests.Integration.Controllers;

public class InviteCodesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public InviteCodesControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    [Fact]
    public async Task Generate_without_a_token_returns_401()
    {
        var response = await _client.PostAsync("/api/invite-codes", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Generate_returns_201_with_an_8_char_code_in_Ativo_status()
    {
        var token = await RegisterGmAndGetTokenAsync("ConviteGm1", "convite1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", token));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InviteCodeResponse>();
        body!.Code.Should().HaveLength(8);
        body.Status.Should().Be("Ativo");
        body.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));
        body.RedeemedByNickname.Should().BeNull();
        body.RedeemedAt.Should().BeNull();
    }

    [Fact]
    public async Task List_returns_only_codes_generated_by_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("ConviteGmA", "convitea@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("ConviteGmB", "conviteb@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", tokenA));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", tokenB));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/invite-codes", tokenA));

        var body = await response.Content.ReadFromJsonAsync<List<InviteCodeResponse>>();
        body!.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/invite-codes");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter InviteCodesControllerTests`
Expected: FAIL — the `/api/invite-codes` routes don't exist yet (404, not the expected 401/201/200).

- [ ] **Step 4: Write `InviteCodesController`**

`src/RuinaRPG.Api/Controllers/InviteCodesController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Domain.Invites;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/invite-codes")]
[Authorize(Roles = "GM")]
public class InviteCodesController(RuinaRpgDbContext db) : ControllerBase
{
    private const string CodeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int CodeLength = 8;
    private const int MaxGenerationAttempts = 5;

    [HttpPost]
    public async Task<ActionResult<InviteCodeResponse>> Generate()
    {
        var gmId = CurrentGmId();

        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var code = new InviteCode
            {
                Id = Guid.NewGuid(),
                Code = GenerateRandomCode(),
                GmId = gmId,
                GeneratedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(48)
            };

            db.InviteCodes.Add(code);

            try
            {
                await db.SaveChangesAsync();
                return Created(string.Empty, ToResponse(code));
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um código de convite único após várias tentativas.");
    }

    [HttpGet]
    public async Task<ActionResult<List<InviteCodeResponse>>> List()
    {
        var gmId = CurrentGmId();

        var codes = await db.InviteCodes
            .Where(c => c.GmId == gmId)
            .OrderByDescending(c => c.GeneratedAt)
            .ToListAsync();

        var redeemerIds = codes.Where(c => c.RedeemedByUserId is not null).Select(c => c.RedeemedByUserId!.Value).ToList();
        var redeemers = await db.Users.Where(u => redeemerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        return codes.Select(c => ToResponse(c, redeemers)).ToList();
    }

    private static string GenerateRandomCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);
        return new string(bytes.Select(b => CodeChars[b % CodeChars.Length]).ToArray());
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    private static InviteCodeResponse ToResponse(InviteCode code, Dictionary<Guid, ApplicationUser>? redeemers = null)
    {
        var status = InviteCodeStatusCalculator.Compute(code.RevokedAt, code.RedeemedByUserId, code.ExpiresAt, DateTime.UtcNow);
        ApplicationUser? redeemer = null;
        if (code.RedeemedByUserId is not null)
            redeemers?.TryGetValue(code.RedeemedByUserId.Value, out redeemer);

        return new InviteCodeResponse(
            code.Code,
            status.ToString(),
            code.GeneratedAt,
            status == InviteCodeStatus.Ativo ? code.ExpiresAt : null,
            redeemer?.Nickname,
            redeemer?.Email,
            code.RedeemedAt);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter InviteCodesControllerTests`
Expected: PASS (4/4).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Invites src/RuinaRPG.Api/Controllers/InviteCodesController.cs tests/RuinaRPG.Tests.Integration/Controllers/InviteCodesControllerTests.cs
git commit -m "feat: add invite code generation and listing"
```

---

### Task 4: Api — revoke an invite code (R0005)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/InviteCodesController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/InviteCodesControllerTests.cs`

**Interfaces:**
- Consumes: `InviteCodesController` (Task 3).
- Produces: `POST /api/invite-codes/{code}/revoke` → `204` on success, `400` if the code isn't currently Ativo, `404` if the code doesn't exist or isn't owned by the caller.

- [ ] **Step 1: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/InviteCodesControllerTests.cs`, inside the class:

```csharp
private async Task<string> GenerateCodeAsync(string token)
{
    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", token));
    var body = await response.Content.ReadFromJsonAsync<InviteCodeResponse>();
    return body!.Code;
}

[Fact]
public async Task Revoke_an_active_code_returns_204_and_the_list_shows_Revogado()
{
    var token = await RegisterGmAndGetTokenAsync("RevokeGm1", "revoke1@teste.com");
    var code = await GenerateCodeAsync(token);

    var revoke = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", token));
    revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var list = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/invite-codes", token));
    var body = await list.Content.ReadFromJsonAsync<List<InviteCodeResponse>>();
    body!.Single(c => c.Code == code).Status.Should().Be("Revogado");
}

[Fact]
public async Task Revoke_a_code_owned_by_another_gm_returns_404()
{
    var tokenOwner = await RegisterGmAndGetTokenAsync("RevokeOwner", "revokeowner@teste.com");
    var tokenOther = await RegisterGmAndGetTokenAsync("RevokeOther", "revokeother@teste.com");
    var code = await GenerateCodeAsync(tokenOwner);

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", tokenOther));

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task Revoke_an_already_revoked_code_returns_400()
{
    var token = await RegisterGmAndGetTokenAsync("RevokeTwice", "revoketwice@teste.com");
    var code = await GenerateCodeAsync(token);
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", token));

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/invite-codes/{code}/revoke", token));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Revoke_a_nonexistent_code_returns_404()
{
    var token = await RegisterGmAndGetTokenAsync("RevokeMissing", "revokemissing@teste.com");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes/NOTAREAL/revoke", token));

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter InviteCodesControllerTests`
Expected: the 4 new tests FAIL (404 route not found on `/revoke`); the Task 3 tests still pass.

- [ ] **Step 3: Add the `Revoke` action**

In `src/RuinaRPG.Api/Controllers/InviteCodesController.cs`, add inside the class:

```csharp
[HttpPost("{code}/revoke")]
public async Task<IActionResult> Revoke(string code)
{
    var gmId = CurrentGmId();
    var inviteCode = await db.InviteCodes.SingleOrDefaultAsync(c => c.Code == code && c.GmId == gmId);

    if (inviteCode is null)
        return NotFound();

    var status = InviteCodeStatusCalculator.Compute(inviteCode.RevokedAt, inviteCode.RedeemedByUserId, inviteCode.ExpiresAt, DateTime.UtcNow);
    if (status != InviteCodeStatus.Ativo)
        return BadRequest("Somente um código Ativo pode ser revogado.");

    inviteCode.RevokedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return NoContent();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter InviteCodesControllerTests`
Expected: PASS (8/8: 4 from Task 3 + 4 new).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/InviteCodesController.cs tests/RuinaRPG.Tests.Integration/Controllers/InviteCodesControllerTests.cs
git commit -m "feat: add invite code revocation"
```

---

### Task 5: Api — Jogador registration / redemption (R0003, R0004; Login e Cadastro R0003)

**Files:**
- Create: `src/RuinaRPG.Contracts/Auth/RegisterJogadorRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/AuthController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRegisterJogadorTests.cs`

**Interfaces:**
- Consumes: `AuthController.IssueTokensAsync`/`NicknameIsTakenAsync` (existing private helpers), `InviteCode` (Task 2), `InviteCodeStatusCalculator` (Task 1).
- Produces: `POST /api/auth/register/jogador` → `201` + `AuthResponse` on success. `RegisterJogadorRequest(string Nickname, string Email, string Senha, string ConfirmacaoSenha, string CodigoDeAcesso)` — Task 9 (Client) depends on this shape.

- [ ] **Step 1: Write the request contract**

`src/RuinaRPG.Contracts/Auth/RegisterJogadorRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Auth;

public record RegisterJogadorRequest(string Nickname, string Email, string Senha, string ConfirmacaoSenha, string CodigoDeAcesso);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRegisterJogadorTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerRegisterJogadorTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerRegisterJogadorTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<(string Token, Guid GmId)> RegisterGmAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        return (tokens.AccessToken, Guid.Parse(meBody!.Id));
    }

    private async Task<string> GenerateCodeAsync(string gmToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var response = await _client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<InviteCodeResponse>();
        return body!.Code;
    }

    [Fact]
    public async Task Register_jogador_with_a_valid_active_code_returns_201_and_links_to_the_gm()
    {
        var (gmToken, gmId) = await RegisterGmAsync("JogadorGm1", "jogadorgm1@teste.com");
        var code = await GenerateCodeAsync(gmToken);

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador1", "jogador1@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var jogador = await db.Users.SingleAsync(u => u.Nickname == "Jogador1");
        jogador.InvitedByGmId.Should().Be(gmId);
    }

    [Fact]
    public async Task Register_jogador_marks_the_code_as_Usado_so_it_cannot_be_reused()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm2", "jogadorgm2@teste.com");
        var code = await GenerateCodeAsync(gmToken);
        await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador2", "jogador2@teste.com", "Senha!123", "Senha!123", code));

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador2b", "jogador2b@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_a_nonexistent_code_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("JogadorNoCode", "jogadornocode@teste.com", "Senha!123", "Senha!123", "NAOEXIST"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_a_revoked_code_returns_400()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm3", "jogadorgm3@teste.com");
        var code = await GenerateCodeAsync(gmToken);
        var revoke = new HttpRequestMessage(HttpMethod.Post, $"/api/invite-codes/{code}/revoke");
        revoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        await _client.SendAsync(revoke);

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador3", "jogador3@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_an_expired_code_returns_400()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm4", "jogadorgm4@teste.com");
        var code = await GenerateCodeAsync(gmToken);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var inviteCode = await db.InviteCodes.SingleAsync(c => c.Code == code);
            inviteCode.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador4", "jogador4@teste.com", "Senha!123", "Senha!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_jogador_with_mismatched_password_confirmation_returns_400()
    {
        var (gmToken, _) = await RegisterGmAsync("JogadorGm5", "jogadorgm5@teste.com");
        var code = await GenerateCodeAsync(gmToken);

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest("Jogador5", "jogador5@teste.com", "Senha!123", "Outra!123", code));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerRegisterJogadorTests`
Expected: FAIL — `/api/auth/register/jogador` doesn't exist yet (404).

- [ ] **Step 4: Add `RegisterJogador` to `AuthController`**

In `src/RuinaRPG.Api/Controllers/AuthController.cs`, add `using RuinaRPG.Domain.Invites;` to the top, and add this action (near `RegisterGm`):

```csharp
[HttpPost("register/jogador")]
[EnableRateLimiting("login")]
public async Task<ActionResult<AuthResponse>> RegisterJogador(RegisterJogadorRequest request)
{
    if (request.Senha != request.ConfirmacaoSenha)
        return BadRequest("A confirmação de senha não confere com a senha.");

    if (await NicknameIsTakenAsync(request.Nickname))
        return BadRequest(NicknameTakenMessage);

    var inviteCode = await db.InviteCodes.SingleOrDefaultAsync(c => c.Code == request.CodigoDeAcesso);
    if (inviteCode is null)
        return BadRequest("Código de acesso inválido.");

    var status = InviteCodeStatusCalculator.Compute(inviteCode.RevokedAt, inviteCode.RedeemedByUserId, inviteCode.ExpiresAt, DateTime.UtcNow);
    var statusError = status switch
    {
        InviteCodeStatus.Usado => "Este código de acesso já foi usado.",
        InviteCodeStatus.Revogado => "Este código de acesso foi revogado.",
        InviteCodeStatus.Expirado => "Este código de acesso expirou.",
        _ => null
    };
    if (statusError is not null)
        return BadRequest(statusError);

    var user = new ApplicationUser
    {
        Id = Guid.NewGuid(),
        UserName = request.Email,
        Email = request.Email,
        Nickname = request.Nickname,
        Role = UserRole.Jogador,
        InvitedByGmId = inviteCode.GmId
    };

    try
    {
        var result = await userManager.CreateAsync(user, request.Senha);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        inviteCode.RedeemedByUserId = user.Id;
        inviteCode.RedeemedAt = DateTime.UtcNow;

        return Created(string.Empty, await IssueTokensAsync(user));
    }
    catch (DbUpdateException)
    {
        db.ChangeTracker.Clear();
        if (await NicknameIsTakenAsync(request.Nickname))
            return BadRequest(NicknameTakenMessage);

        throw;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerRegisterJogadorTests`
Expected: PASS (6/6).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Auth/RegisterJogadorRequest.cs src/RuinaRPG.Api/Controllers/AuthController.cs tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRegisterJogadorTests.cs
git commit -m "feat: add jogador registration via invite code redemption"
```

---

### Task 6: Api — search players linked to the GM (R0006)

**Files:**
- Create: `src/RuinaRPG.Contracts/Players/PlayerSearchResultResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/PlayersController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/PlayersControllerTests.cs`

**Interfaces:**
- Consumes: `ApplicationUser.InvitedByGmId` (existing, from Foundation).
- Produces: `GET /api/players?q=...` → `200` + `List<PlayerSearchResultResponse>`, `[Authorize(Roles = "GM")]`. `PlayerSearchResultResponse(string Id, string Nickname, string Email)` — Task 7 extends this controller, Task 11 (Client) depends on this shape.

- [ ] **Step 1: Write the response contract**

`src/RuinaRPG.Contracts/Players/PlayerSearchResultResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Players;

public record PlayerSearchResultResponse(string Id, string Nickname, string Email);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/PlayersControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Players;

namespace RuinaRPG.Tests.Integration.Controllers;

public class PlayersControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public PlayersControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return message;
    }

    private async Task<string> RegisterGmAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private async Task RegisterJogadorAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
    }

    [Fact]
    public async Task Search_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/players");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_with_no_query_returns_all_players_linked_to_the_gm()
    {
        var gmToken = await RegisterGmAsync("PlayersGm1", "playersgm1@teste.com");
        await RegisterJogadorAsync(gmToken, "PlayerOne", "playerone@teste.com");
        await RegisterJogadorAsync(gmToken, "PlayerTwo", "playertwo@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Select(p => p.Nickname).Should().BeEquivalentTo("PlayerOne", "PlayerTwo");
    }

    [Fact]
    public async Task Search_excludes_players_linked_to_a_different_gm()
    {
        var gmTokenA = await RegisterGmAsync("PlayersGmA", "playersgma@teste.com");
        var gmTokenB = await RegisterGmAsync("PlayersGmB", "playersgmb@teste.com");
        await RegisterJogadorAsync(gmTokenA, "PlayerA", "playera@teste.com");
        await RegisterJogadorAsync(gmTokenB, "PlayerB", "playerb@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players", gmTokenA));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Select(p => p.Nickname).Should().BeEquivalentTo("PlayerA");
    }

    [Fact]
    public async Task Search_by_partial_nickname_is_case_insensitive()
    {
        var gmToken = await RegisterGmAsync("PlayersGm2", "playersgm2@teste.com");
        await RegisterJogadorAsync(gmToken, "FindableNickname", "findable@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players?q=findable", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Should().ContainSingle(p => p.Nickname == "FindableNickname");
    }

    [Fact]
    public async Task Search_by_partial_email_matches()
    {
        var gmToken = await RegisterGmAsync("PlayersGm3", "playersgm3@teste.com");
        await RegisterJogadorAsync(gmToken, "EmailMatch", "unique-mailbox@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players?q=unique-mailbox", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>();
        body!.Should().ContainSingle(p => p.Nickname == "EmailMatch");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter PlayersControllerTests`
Expected: FAIL — `/api/players` doesn't exist yet (404).

- [ ] **Step 4: Write `PlayersController`**

`src/RuinaRPG.Api/Controllers/PlayersController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/players")]
[Authorize(Roles = "GM")]
public class PlayersController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PlayerSearchResultResponse>>> Search([FromQuery] string? q)
    {
        var gmId = CurrentGmId();

        var query = db.Users.Where(u => u.InvitedByGmId == gmId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var normalized = q.ToUpperInvariant();
            query = query.Where(u =>
                u.NormalizedNickname.Contains(normalized) ||
                u.NormalizedEmail!.Contains(normalized));
        }

        var players = await query
            .OrderBy(u => u.Nickname)
            .Select(u => new PlayerSearchResultResponse(u.Id.ToString(), u.Nickname, u.Email!))
            .ToListAsync();

        return players;
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter PlayersControllerTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Players/PlayerSearchResultResponse.cs src/RuinaRPG.Api/Controllers/PlayersController.cs tests/RuinaRPG.Tests.Integration/Controllers/PlayersControllerTests.cs
git commit -m "feat: add player search for the authenticated gm"
```

---

### Task 7: Api — GM resets a player's password (R0007)

**Files:**
- Create: `src/RuinaRPG.Contracts/Players/ResetPlayerPasswordRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/PlayersController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/PlayersControllerTests.cs`

**Interfaces:**
- Consumes: `PlayersController` (Task 6).
- Produces: `POST /api/players/{playerId}/reset-password` → `204` on success, `400` on mismatched confirmation or a password Identity rejects, `404` if the player doesn't exist or isn't linked to the caller.

- [ ] **Step 1: Write the request contract**

`src/RuinaRPG.Contracts/Players/ResetPlayerPasswordRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Players;

public record ResetPlayerPasswordRequest(string NovaSenha, string ConfirmacaoNovaSenha);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/PlayersControllerTests.cs`, inside the class (add `using RuinaRPG.Contracts.Players;` is already present):

```csharp
[Fact]
public async Task Reset_password_for_a_linked_player_returns_204_and_the_new_password_works()
{
    var gmToken = await RegisterGmAsync("ResetGm1", "resetgm1@teste.com");
    await RegisterJogadorAsync(gmToken, "ResetPlayer1", "resetplayer1@teste.com");
    var players = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players", gmToken));
    var playerId = (await players.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>())!.Single().Id;

    var reset = new HttpRequestMessage(HttpMethod.Post, $"/api/players/{playerId}/reset-password")
    {
        Content = JsonContent.Create(new ResetPlayerPasswordRequest("NovaSenha!456", "NovaSenha!456"))
    };
    reset.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
    var resetResponse = await _client.SendAsync(reset);

    resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("ResetPlayer1", "NovaSenha!456"));
    login.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task Reset_password_for_a_player_linked_to_another_gm_returns_404()
{
    var gmTokenOwner = await RegisterGmAsync("ResetOwner", "resetowner@teste.com");
    var gmTokenOther = await RegisterGmAsync("ResetOther", "resetother@teste.com");
    await RegisterJogadorAsync(gmTokenOwner, "ResetPlayer2", "resetplayer2@teste.com");
    var players = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players", gmTokenOwner));
    var playerId = (await players.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>())!.Single().Id;

    var reset = new HttpRequestMessage(HttpMethod.Post, $"/api/players/{playerId}/reset-password")
    {
        Content = JsonContent.Create(new ResetPlayerPasswordRequest("NovaSenha!456", "NovaSenha!456"))
    };
    reset.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmTokenOther);
    var response = await _client.SendAsync(reset);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task Reset_password_with_mismatched_confirmation_returns_400()
{
    var gmToken = await RegisterGmAsync("ResetGm3", "resetgm3@teste.com");
    await RegisterJogadorAsync(gmToken, "ResetPlayer3", "resetplayer3@teste.com");
    var players = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/players", gmToken));
    var playerId = (await players.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>())!.Single().Id;

    var reset = new HttpRequestMessage(HttpMethod.Post, $"/api/players/{playerId}/reset-password")
    {
        Content = JsonContent.Create(new ResetPlayerPasswordRequest("NovaSenha!456", "Outra!789"))
    };
    reset.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
    var response = await _client.SendAsync(reset);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Reset_password_for_a_nonexistent_player_returns_404()
{
    var gmToken = await RegisterGmAsync("ResetGm4", "resetgm4@teste.com");

    var reset = new HttpRequestMessage(HttpMethod.Post, $"/api/players/{Guid.NewGuid()}/reset-password")
    {
        Content = JsonContent.Create(new ResetPlayerPasswordRequest("NovaSenha!456", "NovaSenha!456"))
    };
    reset.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
    var response = await _client.SendAsync(reset);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

Add `using System.Net.Http.Json;` and `using RuinaRPG.Contracts.Auth;` at the top if not already present (both already are, from Task 6).

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter PlayersControllerTests`
Expected: the 4 new tests FAIL (404 route not found); the 5 Task 6 tests still pass.

- [ ] **Step 4: Add the `ResetPassword` action**

In `src/RuinaRPG.Api/Controllers/PlayersController.cs`, change the constructor to also take `UserManager<ApplicationUser>`, add the needed usings, and add the action:

```csharp
using Microsoft.AspNetCore.Identity;
using RuinaRPG.Infrastructure.Identity;
```

```csharp
public class PlayersController(RuinaRpgDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
```

```csharp
[HttpPost("{playerId}/reset-password")]
public async Task<IActionResult> ResetPassword(Guid playerId, ResetPlayerPasswordRequest request)
{
    if (request.NovaSenha != request.ConfirmacaoNovaSenha)
        return BadRequest("A confirmação de senha não confere com a nova senha.");

    var gmId = CurrentGmId();
    var player = await userManager.FindByIdAsync(playerId.ToString());

    if (player is null || player.InvitedByGmId != gmId)
        return NotFound();

    var resetToken = await userManager.GeneratePasswordResetTokenAsync(player);
    var result = await userManager.ResetPasswordAsync(player, resetToken, request.NovaSenha);

    if (!result.Succeeded)
        return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

    return NoContent();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter PlayersControllerTests`
Expected: PASS (9/9).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Players/ResetPlayerPasswordRequest.cs src/RuinaRPG.Api/Controllers/PlayersController.cs tests/RuinaRPG.Tests.Integration/Controllers/PlayersControllerTests.cs
git commit -m "feat: let a gm reset a linked player's password"
```

---

### Task 8: Client — attach the Bearer token to authenticated requests

**Files:**
- Create: `src/RuinaRPG.Client/Services/BearerTokenHandler.cs`
- Modify: `src/RuinaRPG.Client/Program.cs`
- Modify: `src/RuinaRPG.Client/RuinaRPG.Client.csproj` (package)

**Interfaces:**
- Consumes: `AuthStateService.GetAccessTokenAsync()` (existing, from Foundation).
- Produces: the shared `HttpClient` registered in DI now attaches `Authorization: Bearer <token>` to every outgoing request automatically. Tasks 10 and 11's pages depend on this — they call `Http.GetAsync`/`PostAsync` with no manual header handling, same as every existing page.

This task has no automated tests — it's Client wiring verified by `dotnet build`, same as Foundation's Task 8. The next tasks' pages are the functional proof once combined with Task 12's smoke test.

- [ ] **Step 1: Add the `Microsoft.Extensions.Http` package**

```bash
dotnet add src/RuinaRPG.Client package Microsoft.Extensions.Http --version 8.0.1
```

- [ ] **Step 2: Write `BearerTokenHandler`**

`src/RuinaRPG.Client/Services/BearerTokenHandler.cs`:

```csharp
using System.Net.Http.Headers;

namespace RuinaRPG.Client.Services;

public class BearerTokenHandler(AuthStateService authState) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await authState.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 3: Replace the manual `HttpClient` registration with a factory-built one**

In `src/RuinaRPG.Client/Program.cs`, replace:

```csharp
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "/";
var httpClientBaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBaseAddress);
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = httpClientBaseAddress });
```

with:

```csharp
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "/";
var httpClientBaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBaseAddress);

builder.Services.AddTransient<BearerTokenHandler>();
builder.Services
    .AddHttpClient("Api", client => client.BaseAddress = httpClientBaseAddress)
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));
```

`BearerTokenHandler` must be `AddTransient`, not `AddScoped`, because `IHttpClientFactory` pools and recycles the handler chain internally — registering it as anything other than transient causes `HttpMessageHandlerBuilder` to throw at startup.

- [ ] **Step 4: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client
git commit -m "feat: attach the bearer token to authenticated client requests"
```

---

### Task 9: Client — Cadastro gains a Jogador tab (Login e Cadastro R0003)

**Files:**
- Delete: `src/RuinaRPG.Client/Pages/CadastroGm.razor`
- Create: `src/RuinaRPG.Client/Pages/Cadastro.razor`

**Interfaces:**
- Consumes: `RegisterGmRequest`, `RegisterJogadorRequest` (Task 5), `AuthResponse`, `AuthStateService` (existing).
- Produces: the `/cadastro` route, now with both tabs. No later task depends on this file.

- [ ] **Step 1: Delete the old GM-only page and write the tabbed replacement**

Delete `src/RuinaRPG.Client/Pages/CadastroGm.razor`.

`src/RuinaRPG.Client/Pages/Cadastro.razor`:

```razor
@page "/cadastro"
@inject HttpClient Http
@inject AuthStateService AuthState
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Auth

<h1>Cadastro</h1>

<div>
    <button @onclick='() => _activeTab = "gm"' disabled="@(_activeTab == "gm")">Gamemaster</button>
    <button @onclick='() => _activeTab = "jogador"' disabled="@(_activeTab == "jogador")">Jogador</button>
</div>

@if (_activeTab == "gm")
{
    <EditForm Model="_gmRequest" OnValidSubmit="SubmitGmAsync">
        <label>Nickname <InputText @bind-Value="_gmRequest.Nickname" /></label>
        <label>Email <InputText @bind-Value="_gmRequest.Email" /></label>
        <label>Senha <InputText type="password" @bind-Value="_gmRequest.Senha" /></label>
        <label>Confirmação da Senha <InputText type="password" @bind-Value="_gmRequest.ConfirmacaoSenha" /></label>
        <button type="submit">Cadastrar</button>
    </EditForm>
}
else
{
    <EditForm Model="_jogadorRequest" OnValidSubmit="SubmitJogadorAsync">
        <label>Nickname <InputText @bind-Value="_jogadorRequest.Nickname" /></label>
        <label>Email <InputText @bind-Value="_jogadorRequest.Email" /></label>
        <label>Senha <InputText type="password" @bind-Value="_jogadorRequest.Senha" /></label>
        <label>Confirmação da Senha <InputText type="password" @bind-Value="_jogadorRequest.ConfirmacaoSenha" /></label>
        <label>Código de acesso <InputText @bind-Value="_jogadorRequest.CodigoDeAcesso" /></label>
        <button type="submit">Cadastrar</button>
    </EditForm>
}

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

@code {
    private string _activeTab = "gm";
    private readonly GmFormModel _gmRequest = new();
    private readonly JogadorFormModel _jogadorRequest = new();
    private string? _errorMessage;

    private async Task SubmitGmAsync()
    {
        var response = await Http.PostAsJsonAsync("auth/register/gm",
            new RegisterGmRequest(_gmRequest.Nickname, _gmRequest.Email, _gmRequest.Senha, _gmRequest.ConfirmacaoSenha));
        await HandleResponseAsync(response);
    }

    private async Task SubmitJogadorAsync()
    {
        var response = await Http.PostAsJsonAsync("auth/register/jogador",
            new RegisterJogadorRequest(_jogadorRequest.Nickname, _jogadorRequest.Email, _jogadorRequest.Senha,
                _jogadorRequest.ConfirmacaoSenha, _jogadorRequest.CodigoDeAcesso));
        await HandleResponseAsync(response);
    }

    private async Task HandleResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = await response.Content.ReadAsStringAsync();
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        await AuthState.SetTokensAsync(body!);
        Navigation.NavigateTo("/painel");
    }

    private class GmFormModel
    {
        public string Nickname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Senha { get; set; } = "";
        public string ConfirmacaoSenha { get; set; } = "";
    }

    private class JogadorFormModel
    {
        public string Nickname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Senha { get; set; } = "";
        public string ConfirmacaoSenha { get; set; } = "";
        public string CodigoDeAcesso { get; set; } = "";
    }
}
```

- [ ] **Step 2: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/Cadastro.razor
git rm src/RuinaRPG.Client/Pages/CadastroGm.razor
git commit -m "feat: add the jogador signup tab to cadastro"
```

---

### Task 10: Client — GM invite-code management page (R0001, R0002, R0005)

**Files:**
- Create: `src/RuinaRPG.Client/Pages/GmConvites.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `InviteCodeResponse` (Task 3), the authenticated `HttpClient` (Task 8).
- Produces: the `/painel/convites` route. No later task depends on this file.

- [ ] **Step 1: Write the page**

`src/RuinaRPG.Client/Pages/GmConvites.razor`:

```razor
@page "/painel/convites"
@inject HttpClient Http
@using RuinaRPG.Contracts.Invites

<h1>Convidar Jogador</h1>

<button @onclick="GenerateAsync">Gerar código de convite</button>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<table>
    <thead>
        <tr>
            <th>Código</th>
            <th>Status</th>
            <th>Gerado em</th>
            <th>Expira em</th>
            <th>Resgatado por</th>
            <th>Resgatado em</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
        @foreach (var code in _codes)
        {
            <tr>
                <td>@code.Code</td>
                <td>@code.Status</td>
                <td>@code.GeneratedAt</td>
                <td>@(code.ExpiresAt?.ToString() ?? "—")</td>
                <td>@(code.RedeemedByNickname is null ? "—" : $"{code.RedeemedByNickname} ({code.RedeemedByEmail})")</td>
                <td>@(code.RedeemedAt?.ToString() ?? "—")</td>
                <td>
                    @if (code.Status == "Ativo")
                    {
                        <button @onclick="() => RevokeAsync(code.Code)">Revogar</button>
                    }
                </td>
            </tr>
        }
    </tbody>
</table>

@code {
    private List<InviteCodeResponse> _codes = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var response = await Http.GetAsync("invite-codes");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar os códigos de convite.";
            return;
        }

        _codes = await response.Content.ReadFromJsonAsync<List<InviteCodeResponse>>() ?? new();
    }

    private async Task GenerateAsync()
    {
        var response = await Http.PostAsync("invite-codes", null);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível gerar um código de convite.";
            return;
        }

        await LoadAsync();
    }

    private async Task RevokeAsync(string code)
    {
        var response = await Http.PostAsync($"invite-codes/{code}/revoke", null);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível revogar o código.";
            return;
        }

        await LoadAsync();
    }
}
```

- [ ] **Step 2: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/painel/convites` following the existing entries' exact markup pattern in that file (same CSS classes, same `<span class="oi" ...></span>` icon structure as the Login/Cadastro links) — label it "Convidar Jogador".

- [ ] **Step 3: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/GmConvites.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add the gm invite code management page"
```

---

### Task 11: Client — GM player search and password reset page (R0006, R0007)

**Files:**
- Create: `src/RuinaRPG.Client/Pages/GmJogadores.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `PlayerSearchResultResponse`, `ResetPlayerPasswordRequest` (Task 6/7), the authenticated `HttpClient` (Task 8).
- Produces: the `/painel/jogadores` route. No later task depends on this file.

- [ ] **Step 1: Write the page**

`src/RuinaRPG.Client/Pages/GmJogadores.razor`:

```razor
@page "/painel/jogadores"
@inject HttpClient Http
@using RuinaRPG.Contracts.Players

<h1>Jogadores</h1>

<input @bind="_query" @bind:event="oninput" placeholder="Buscar por nickname ou e-mail" />
<button @onclick="SearchAsync">Buscar</button>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<ul>
    @foreach (var player in _players)
    {
        <li>
            @player.Nickname (@player.Email)
            <button @onclick="() => StartReset(player.Id)">Redefinir senha</button>
        </li>
    }
</ul>

@if (_resettingPlayerId is not null)
{
    <EditForm Model="_resetRequest" OnValidSubmit="ConfirmResetAsync">
        <label>Nova senha <InputText type="password" @bind-Value="_resetRequest.NovaSenha" /></label>
        <label>Confirmação da nova senha <InputText type="password" @bind-Value="_resetRequest.ConfirmacaoNovaSenha" /></label>
        <button type="submit">Confirmar</button>
    </EditForm>
}

@code {
    private string _query = "";
    private List<PlayerSearchResultResponse> _players = new();
    private string? _errorMessage;
    private string? _resettingPlayerId;
    private readonly ResetFormModel _resetRequest = new();

    private async Task SearchAsync()
    {
        var response = await Http.GetAsync($"players?q={Uri.EscapeDataString(_query)}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível buscar jogadores.";
            return;
        }

        _players = await response.Content.ReadFromJsonAsync<List<PlayerSearchResultResponse>>() ?? new();
    }

    private void StartReset(string playerId) => _resettingPlayerId = playerId;

    private async Task ConfirmResetAsync()
    {
        var response = await Http.PostAsJsonAsync($"players/{_resettingPlayerId}/reset-password",
            new ResetPlayerPasswordRequest(_resetRequest.NovaSenha, _resetRequest.ConfirmacaoNovaSenha));

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível redefinir a senha.";
            return;
        }

        _resettingPlayerId = null;
    }

    private class ResetFormModel
    {
        public string NovaSenha { get; set; } = "";
        public string ConfirmacaoNovaSenha { get; set; } = "";
    }
}
```

- [ ] **Step 2: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/painel/jogadores`, following the same pattern as Task 10's link — label it "Jogadores".

- [ ] **Step 3: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/GmJogadores.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add the gm player search and password reset page"
```

---

### Task 12: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files — this task only exercises what Tasks 1-11 built, mirroring the Foundation plan's Task 9.

**Interfaces:**
- Consumes: the full stack, including everything this plan added.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers come up; migrations (including `AddInviteCodes`) apply automatically in Development.

- [ ] **Step 2: Register a GM and get a token, through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/gm \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeGm","email":"smokegm@teste.com","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' \
  | tee /tmp/gm-register.json
```

Expected: HTTP 201 with `accessToken`/`refreshToken`. Extract the access token for the next steps (e.g. `TOKEN=$(jq -r .accessToken /tmp/gm-register.json)`).

- [ ] **Step 3: Generate an invite code as that GM, through nginx**

```bash
curl -sf -X POST http://localhost/api/invite-codes \
  -H "Authorization: Bearer $TOKEN" \
  | tee /tmp/invite-code.json
```

Expected: HTTP 201 with `"status":"Ativo"` and an 8-character `code`. Extract it (e.g. `CODE=$(jq -r .code /tmp/invite-code.json)` — note the JSON property name is camelCased by default ASP.NET Core serialization, so it's `code`, not `Code`).

- [ ] **Step 4: Register a Jogador with that code, through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/jogador \
  -H "Content-Type: application/json" \
  -d "{\"nickname\":\"SmokeJogador\",\"email\":\"smokejogador@teste.com\",\"senha\":\"Senha!123\",\"confirmacaoSenha\":\"Senha!123\",\"codigoDeAcesso\":\"$CODE\"}"
```

Expected: HTTP 201 with `accessToken`/`refreshToken`.

- [ ] **Step 5: Confirm the code now shows as Usado**

```bash
curl -sf http://localhost/api/invite-codes -H "Authorization: Bearer $TOKEN"
```

Expected: the code from Step 3 now has `"status":"Usado"` and `redeemedByNickname":"SmokeJogador"`.

- [ ] **Step 6: Tear down**

```bash
make down
```

- [ ] **Step 7: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the invite code flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Fixing the rate-limiter/Cloudflare-Tunnel gap from the Foundation plan (deliberately queued for later).
- Any GM/Jogador panel content beyond the three new pages (`/painel/convites`, `/painel/jogadores`, and the tabbed `/cadastro`) — the rest of `Painel.razor` stays a placeholder.
- Route-level auth guarding in the Blazor client (e.g. redirecting an unauthenticated visitor away from `/painel/convites`) — the API already enforces `[Authorize(Roles = "GM")]`, so an unauthorized visit just fails to load data; client-side route guards are a future concern, not part of any requirement this plan implements.
