# Rules Audit System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let one server-console-designated GM ("Rules Auditor") edit the Livro de Regras' displayed text and fully manage the Características catalog, with effects visible to every GM/Jogador on the server immediately.

**Architecture:** A new `IsRulesAuditor` bool on the user, set via a one-shot CLI arg (`docker compose exec` through a new `make` target) and checked directly against the DB on every request (no JWT claim, no relogin needed). Características becomes a fully-editable DB table (`Traits` gains audit/soft-delete columns; `TraitSeeder` learns to leave manually-touched rows alone). The other 3 rulebook documents get a markdown-text override table, read by `RulebookRenderer` in preference to the embedded `.md` resource. The Livro de Regras "Características" tab is rewritten to render from the live `Traits` table instead of a second, disconnected copy of the same prose.

**Tech Stack:** ASP.NET Core 8 (Api), EF Core/Npgsql (Infrastructure), Blazor WebAssembly (Client), xUnit + Testcontainers Postgres (Tests.Integration), xUnit (Tests.Unit).

**Spec:** `docs/superpowers/specs/2026-09-11-rules-audit-system-design.md`

## Global Constraints

- **Livro de Regras editing is display-only.** Never touch `IRulesDataProvider`, its parsers, or any gameplay calculator (Nível/EAP/Vitalidade/Graduação/etc.). Only `RulebookRenderer`'s markdown source for `sistema-basico`/`graus-e-circulos`/`tabela-de-niveis` changes.
- **Características editing is full CRUD** (rename/re-cost/re-polarize/delete an originally-seeded row included) — never blocked by `TraitSeeder`.
- **The Rules Auditor grant is checked directly against the DB on every request** — never as a JWT claim, so it takes effect on the very next request, no relogin.
- **`dotnet build` must stay at 0 warnings, 0 errors** after every task.
- **TDD**: write the failing test first, then the minimal code to pass it, for every task below.
- Migration command (always from the repo root):
  `dotnet ef migrations add <Name> --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`

---

## Task 1: `IsRulesAuditor` flag + `Me` reflects it

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs`
- Modify: `src/RuinaRPG.Api/Controllers/AuthController.cs` (the `Me` action, near the end of the file)
- Modify: `src/RuinaRPG.Contracts/Auth/MeResponse.cs`
- Migration: new file under `src/RuinaRPG.Infrastructure/Persistence/Migrations/`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerTests.cs` (existing file — add to it)
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/RulesAuditorMigrationTests.cs` (new)

**Interfaces:**
- Produces: `ApplicationUser.IsRulesAuditor` (bool, default false); `MeResponse(string Id, string Nickname, string Role, bool IsRulesAuditor)`.

- [ ] **Step 1: Write the failing test for `MeResponse`'s new field**

Find the existing `Me` test in `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerTests.cs` (search for `"Me_"` or `/api/auth/me`) and add this fact near it:

```csharp
[Fact]
public async Task Me_reports_IsRulesAuditor_false_by_default()
{
    var gmToken = await RegisterGmAndGetTokenAsync("RulesAuditorGm1", "rulesauditorgm1@teste.com");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));

    var body = await response.Content.ReadFromJsonAsync<MeResponse>();
    body!.IsRulesAuditor.Should().BeFalse();
}
```

If `RegisterGmAndGetTokenAsync`/`AuthedRequest` helpers don't already exist in that file under those exact names, copy the pattern from `tests/RuinaRPG.Tests.Integration/Controllers/RacialAbilitiesControllerTests.cs` (same helpers, same shape) instead of inventing new ones.

- [ ] **Step 2: Run it to confirm it fails to compile**

Run: `dotnet build tests/RuinaRPG.Tests.Integration`
Expected: `CS1729` or `CS7036`-style error — `MeResponse` has no `IsRulesAuditor` parameter yet.

- [ ] **Step 3: Add the column and the response field**

In `src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs`, add after `public Guid? InvitedByGmId { get; set; }`:

```csharp
    // Granted via `make grant-rules-auditor EMAIL=...` (Program.cs's --grant-rules-auditor arg) —
    // checked directly against this column on every request (AuthController.Me, and the new
    // Traits/RulebookDocuments admin endpoints), never baked into the JWT, so a grant/revoke takes
    // effect on the very next request instead of waiting for the holder to log in again.
    public bool IsRulesAuditor { get; set; }
```

In `src/RuinaRPG.Contracts/Auth/MeResponse.cs`, change to:

```csharp
namespace RuinaRPG.Contracts.Auth;

public record MeResponse(string Id, string Nickname, string Role, bool IsRulesAuditor);
```

In `src/RuinaRPG.Api/Controllers/AuthController.cs`, find:

```csharp
    public ActionResult<MeResponse> Me() => Ok(new MeResponse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty,
        User.FindFirstValue("nickname") ?? string.Empty,
        User.FindFirstValue("role") ?? string.Empty));
```

Replace with (note the new `async`/`await` — `Me` was synchronous before):

```csharp
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var isRulesAuditor = userId is not null
            && await db.Users.Where(u => u.Id == Guid.Parse(userId)).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();

        return Ok(new MeResponse(
            userId ?? string.Empty,
            User.FindFirstValue("nickname") ?? string.Empty,
            User.FindFirstValue("role") ?? string.Empty,
            isRulesAuditor));
    }
```

Check the top of `AuthController.cs` for `using Microsoft.EntityFrameworkCore;` — add it if missing (needed for `.Where`/`.Select`/`SingleOrDefaultAsync`).

Also check every other place in the codebase that constructs a `MeResponse` positionally (`grep -rn "new MeResponse(" src`) — as of this plan's writing there are none outside `AuthController.Me`, but confirm before moving on; add `false` as the 4th argument to any you find.

- [ ] **Step 4: Generate and inspect the migration**

Run: `dotnet ef migrations add AddIsRulesAuditor --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`

Open the generated `*_AddIsRulesAuditor.cs` and confirm it's a single `AddColumn<bool>` on `AspNetUsers` named `IsRulesAuditor`, `nullable: false`, `defaultValue: false`. If it also touches unrelated tables, something else is out of sync — stop and investigate before continuing (do not hand-edit the migration to remove unrelated changes without understanding why they appeared).

- [ ] **Step 5: Write the migration integration test**

Create `tests/RuinaRPG.Tests.Integration/Persistence/RulesAuditorMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RulesAuditorMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RulesAuditorMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_IsRulesAuditor_column_defaulted_to_false()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddIsRulesAuditor"));

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "auditortest@teste.com", Email = "auditortest@teste.com", Nickname = "AuditorMigrationTest", Role = RuinaRPG.Domain.Enums.UserRole.GM };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        (await db.Users.SingleAsync(u => u.Id == user.Id)).IsRulesAuditor.Should().BeFalse();

        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        (await db.Users.SingleAsync(u => u.Id == user.Id)).IsRulesAuditor.Should().BeTrue();
    }
}
```

- [ ] **Step 6: Run both new tests**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulesAuditorMigrationTests|FullyQualifiedName~Me_reports_IsRulesAuditor"`
Expected: both PASS.

- [ ] **Step 7: Build the whole solution**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs src/RuinaRPG.Contracts/Auth/MeResponse.cs src/RuinaRPG.Api/Controllers/AuthController.cs src/RuinaRPG.Infrastructure/Persistence/Migrations/*AddIsRulesAuditor* tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerTests.cs tests/RuinaRPG.Tests.Integration/Persistence/RulesAuditorMigrationTests.cs
git commit -m "feat: add IsRulesAuditor flag, surfaced on /api/auth/me"
```

---

## Task 2: CLI grant/revoke + `make` targets

**Files:**
- Modify: `src/RuinaRPG.Api/Program.cs` (near the existing `if (args.Contains("--migrate"))` block)
- Modify: `Makefile`
- Modify: `Docs/Requisitos/Requisitos - Técnico.md` (document the two new `make` targets, next to the existing `make migrate` row)
- Test: `tests/RuinaRPG.Tests.Integration/RulesAuditorCliTests.cs` (new)

**Interfaces:**
- Consumes: `ApplicationUser.IsRulesAuditor` (Task 1).
- Produces: nothing new consumed by later tasks — this is a leaf feature (the CLI path), but the *behavior* it flips (`IsRulesAuditor = true`) is exactly what Tasks 3+ gate on.

**Note on testability:** `Program.cs`'s top-level statements run the whole app; there is no existing precedent in this codebase for unit-testing a CLI arg branch directly (the `--migrate` branch itself has no dedicated test — it's only exercised implicitly by every integration test's `ApiFactory` already having migrated). This task instead extracts the actual logic into a small, directly-testable static method, and calls that method from both the CLI branch and the test.

- [ ] **Step 1: Write the failing test for the extracted grant/revoke logic**

Create `tests/RuinaRPG.Tests.Integration/RulesAuditorCliTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

public class RulesAuditorCliTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RulesAuditorCliTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<RuinaRpgDbContext> NewDbAsync()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task SetRulesAuditorAsync_grants_a_GM_found_by_email()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "cligrant@teste.com", Email = "cligrant@teste.com", Nickname = "CliGrantGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "cligrant@teste.com", grant: true);

        result.Should().BeNull(); // null = success, no error message
        (await db.Users.SingleAsync(u => u.Id == gm.Id)).IsRulesAuditor.Should().BeTrue();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_matches_email_case_insensitively()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clicase@teste.com", Email = "clicase@teste.com", Nickname = "CliCaseGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "CLICASE@TESTE.COM", grant: true);

        result.Should().BeNull();
        (await db.Users.SingleAsync(u => u.Id == gm.Id)).IsRulesAuditor.Should().BeTrue();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_revokes_an_already_granted_GM()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clirevoke@teste.com", Email = "clirevoke@teste.com", Nickname = "CliRevokeGm", Role = UserRole.GM, IsRulesAuditor = true };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "clirevoke@teste.com", grant: false);

        result.Should().BeNull();
        (await db.Users.SingleAsync(u => u.Id == gm.Id)).IsRulesAuditor.Should().BeFalse();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_returns_an_error_for_an_unknown_email()
    {
        await using var db = await NewDbAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "naoexiste@teste.com", grant: true);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_refuses_to_grant_a_Jogador()
    {
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "cligmowner@teste.com", Email = "cligmowner@teste.com", Nickname = "CliGmOwner", Role = UserRole.GM };
        db.Users.Add(gm);
        var jogador = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clijogador@teste.com", Email = "clijogador@teste.com", Nickname = "CliJogador", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.Add(jogador);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "clijogador@teste.com", grant: true);

        result.Should().NotBeNull();
        (await db.Users.SingleAsync(u => u.Id == jogador.Id)).IsRulesAuditor.Should().BeFalse();
    }

    [Fact]
    public async Task SetRulesAuditorAsync_allows_revoking_a_Jogador_even_though_granting_one_is_refused()
    {
        // Revoke is always safe to allow unconditionally — it only ever turns the flag off,
        // never grants access to someone who shouldn't have it.
        await using var db = await NewDbAsync();
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clijgmowner2@teste.com", Email = "clijgmowner2@teste.com", Nickname = "CliGmOwner2", Role = UserRole.GM };
        db.Users.Add(gm);
        var jogador = new ApplicationUser { Id = Guid.NewGuid(), UserName = "clijogador2@teste.com", Email = "clijogador2@teste.com", Nickname = "CliJogador2", Role = UserRole.Jogador, InvitedByGmId = gm.Id, IsRulesAuditor = true };
        db.Users.Add(jogador);
        await db.SaveChangesAsync();

        var result = await RulesAuditorCli.SetRulesAuditorAsync(db, "clijogador2@teste.com", grant: false);

        result.Should().BeNull();
        (await db.Users.SingleAsync(u => u.Id == jogador.Id)).IsRulesAuditor.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run it to confirm it fails to compile**

Run: `dotnet build tests/RuinaRPG.Tests.Integration`
Expected: `CS0234` — the type or namespace `RulesAuditorCli` does not exist in `RuinaRPG.Api`.

- [ ] **Step 3: Create the extracted CLI logic class**

Create `src/RuinaRPG.Api/RulesAuditorCli.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api;

/// <summary>
/// Backs Program.cs's --grant-rules-auditor/--revoke-rules-auditor one-shot CLI args (see the
/// Makefile's grant-rules-auditor/revoke-rules-auditor targets, which exec into the running
/// container to invoke them). Extracted to a plain static method — rather than inlined in
/// Program.cs's top-level statements, which can't otherwise be exercised by a test — so this is
/// directly testable against a real database.
/// </summary>
public static class RulesAuditorCli
{
    /// <summary>Returns null on success, or a human-readable error message otherwise.</summary>
    public static async Task<string?> SetRulesAuditorAsync(RuinaRpgDbContext db, string email, bool grant)
    {
        var normalizedEmail = email.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null)
            return $"Nenhum usuário encontrado com o e-mail \"{email}\".";

        // Only grant is restricted to GM — revoke is always safe (it only ever turns access off,
        // and a Jogador should never have had it in the first place, but clearing a stray true is
        // harmless).
        if (grant && user.Role != UserRole.GM)
            return $"\"{email}\" não é uma conta de GM — só um GM pode ser Auditor de Regras.";

        user.IsRulesAuditor = grant;
        await db.SaveChangesAsync();
        return null;
    }
}
```

- [ ] **Step 4: Run the test again to confirm it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulesAuditorCliTests"`
Expected: all 6 facts PASS.

- [ ] **Step 5: Wire the CLI args into `Program.cs`**

Open `src/RuinaRPG.Api/Program.cs` and find the existing block:

```csharp
if (args.Contains("--migrate"))
{
    ...
    return;
}
```

Add a new block immediately after it (still before `app.Run()`):

```csharp
if (args.Contains("--grant-rules-auditor") || args.Contains("--revoke-rules-auditor"))
{
    var grant = args.Contains("--grant-rules-auditor");
    var flagIndex = Array.IndexOf(args, grant ? "--grant-rules-auditor" : "--revoke-rules-auditor");
    var email = flagIndex >= 0 && flagIndex + 1 < args.Length ? args[flagIndex + 1] : null;

    if (string.IsNullOrWhiteSpace(email))
    {
        app.Logger.LogError("Uso: dotnet RuinaRPG.Api.dll --{Flag} <email>", grant ? "grant-rules-auditor" : "revoke-rules-auditor");
        return;
    }

    using var rulesAuditorScope = app.Services.CreateScope();
    var rulesAuditorDb = rulesAuditorScope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
    var error = await RulesAuditorCli.SetRulesAuditorAsync(rulesAuditorDb, email, grant);
    if (error is not null)
        app.Logger.LogError("{Error}", error);
    else
        app.Logger.LogInformation("{Action} Auditor de Regras: {Email}", grant ? "Concedido" : "Revogado", email);
    return;
}
```

- [ ] **Step 6: Add the `make` targets**

Open `Makefile` and add, near the existing `migrate:` target:

```makefile
grant-rules-auditor:
	docker compose exec api dotnet RuinaRPG.Api.dll --grant-rules-auditor $(EMAIL)

revoke-rules-auditor:
	docker compose exec api dotnet RuinaRPG.Api.dll --revoke-rules-auditor $(EMAIL)
```

Update the `.PHONY` line at the top of the file to include both new target names.

- [ ] **Step 7: Document the new `make` targets**

Open `Docs/Requisitos/Requisitos - Técnico.md`, find the table/section documenting `make migrate` (search for "migrate"), and add a short paragraph (or table row, matching whatever the surrounding format is) for `make grant-rules-auditor EMAIL=...` / `make revoke-rules-auditor EMAIL=...`, stating: designates a GM (by e-mail, case-insensitive) as Rules Auditor / revokes it; effective immediately (checked per-request against the DB, no relogin needed); grant is refused for a non-GM account.

- [ ] **Step 8: Build and run the full test file once more**

Run: `dotnet build && dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulesAuditorCliTests"`
Expected: 0 warnings/errors, all facts PASS.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Api/RulesAuditorCli.cs src/RuinaRPG.Api/Program.cs Makefile "Docs/Requisitos/Requisitos - Técnico.md" tests/RuinaRPG.Tests.Integration/RulesAuditorCliTests.cs
git commit -m "feat: make grant-rules-auditor/revoke-rules-auditor CLI targets"
```

---

## Task 3: `Trait` gains audit/soft-delete columns

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/Trait.cs`
- Migration: new file
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/TraitAuditFieldsMigrationTests.cs` (new)

**Interfaces:**
- Produces: `Trait.IsCustomized` (bool), `Trait.IsDeleted` (bool), `Trait.UpdatedByUserId` (Guid?), `Trait.UpdatedAt` (DateTime?).

- [ ] **Step 1: Write the failing migration test**

Create `tests/RuinaRPG.Tests.Integration/Persistence/TraitAuditFieldsMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class TraitAuditFieldsMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TraitAuditFieldsMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_new_Trait_audit_columns_defaulted_false_and_null()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddTraitAuditFields"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "traitauditgm@teste.com", Email = "traitauditgm@teste.com", Nickname = "TraitAuditGm", Role = UserRole.GM };
        db.Users.Add(gm);
        var trait = new Trait { Id = Guid.NewGuid(), Nome = "Teste Migração", Descricao = "Teste.", Custo = 1, Polaridade = Polaridade.Positiva };
        db.Traits.Add(trait);
        await db.SaveChangesAsync();

        var reloaded = await db.Traits.SingleAsync(t => t.Id == trait.Id);
        reloaded.IsCustomized.Should().BeFalse();
        reloaded.IsDeleted.Should().BeFalse();
        reloaded.UpdatedByUserId.Should().BeNull();
        reloaded.UpdatedAt.Should().BeNull();

        reloaded.IsCustomized = true;
        reloaded.IsDeleted = true;
        reloaded.UpdatedByUserId = gm.Id;
        reloaded.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var reReloaded = await db.Traits.SingleAsync(t => t.Id == trait.Id);
        reReloaded.IsCustomized.Should().BeTrue();
        reReloaded.IsDeleted.Should().BeTrue();
        reReloaded.UpdatedByUserId.Should().Be(gm.Id);
        reReloaded.UpdatedAt.Should().NotBeNull();
    }
}
```

- [ ] **Step 2: Run it to confirm it fails to compile**

Run: `dotnet build tests/RuinaRPG.Tests.Integration`
Expected: compile errors — `Trait` has no `IsCustomized`/`IsDeleted`/`UpdatedByUserId`/`UpdatedAt` members yet.

- [ ] **Step 3: Add the columns**

In `src/RuinaRPG.Infrastructure/Rules/Trait.cs`, replace the whole file with:

```csharp
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Rules;

public class Trait
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Custo { get; set; }
    public Polaridade Polaridade { get; set; }
    public bool RequerEspecificacao { get; set; }

    // Set by TraitsController's Create/Update — once true, TraitSeeder never touches this row
    // again (see TraitSeeder.SeedAsync's updated doc comment), so a manual edit always wins over
    // whatever Características.md says.
    public bool IsCustomized { get; set; }

    // Soft delete: hidden from every read path (an explicit "!IsDeleted" filter per query, not an
    // EF global filter — see TraitsController), but the row itself stays so TraitSeeder still
    // recognizes it as "already present" and never resurrects it.
    public bool IsDeleted { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

- [ ] **Step 4: Generate and inspect the migration**

Run: `dotnet ef migrations add AddTraitAuditFields --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`

Confirm the generated file adds exactly 4 columns to `Traits`: `IsCustomized` (bool, not null, default false), `IsDeleted` (bool, not null, default false), `UpdatedByUserId` (uuid, nullable), `UpdatedAt` (timestamp, nullable). No FK constraint is expected for `UpdatedByUserId` at this stage (it isn't configured as a navigation/FK in the entity — a plain nullable Guid column is enough for this feature; do not add a `HasOne<ApplicationUser>()` FK config unless the migration diff surprises you by requiring one).

- [ ] **Step 5: Run the new test**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~TraitAuditFieldsMigrationTests"`
Expected: PASS.

- [ ] **Step 6: Build the whole solution**

Run: `dotnet build`
Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/Trait.cs src/RuinaRPG.Infrastructure/Persistence/Migrations/*AddTraitAuditFields* tests/RuinaRPG.Tests.Integration/Persistence/TraitAuditFieldsMigrationTests.cs
git commit -m "feat: add IsCustomized/IsDeleted/audit columns to Trait"
```

---

## Task 4: `TraitSeeder` respects manual edits and soft-deletes

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/TraitSeederTests.cs` (existing file — add to it)

**Interfaces:**
- Consumes: `Trait.IsCustomized`, `Trait.IsDeleted` (Task 3).
- Produces: nothing new — `TraitSeeder.SeedAsync`'s signature is unchanged; only its matching behavior changes.

- [ ] **Step 1: Write the two failing tests**

Open `tests/RuinaRPG.Tests.Integration/Rules/TraitSeederTests.cs` and add these two facts at the end of the class (before the closing `}`):

```csharp
    [Fact]
    public async Task SeedAsync_never_touches_a_row_flagged_IsCustomized_even_if_RequerEspecificacao_would_otherwise_change()
    {
        await using var db = await NewDbAsync();

        // A GM edit of "Alergia": Descricao and RequerEspecificacao both diverge from what a
        // fresh parse of Markdown would produce, and IsCustomized is set — the seeder must leave
        // every field alone, including RequerEspecificacao (which it otherwise always syncs).
        var customId = Guid.NewGuid();
        db.Traits.Add(new Trait
        {
            Id = customId,
            Nome = "Alergia",
            Descricao = "Texto totalmente reescrito pelo Auditor de Regras.",
            Custo = -1,
            Polaridade = RuinaRPG.Domain.Enums.Polaridade.Negativa,
            RequerEspecificacao = false,
            IsCustomized = true,
        });
        await db.SaveChangesAsync();

        var result = await TraitSeeder.SeedAsync(db, Markdown);

        result.Inserted.Should().Be(1); // only "Alfabetizado" is new
        result.Updated.Should().Be(0); // "Alergia" is skipped entirely, not corrected
        var alergia = await db.Traits.SingleAsync(t => t.Id == customId);
        alergia.Descricao.Should().Be("Texto totalmente reescrito pelo Auditor de Regras.");
        alergia.RequerEspecificacao.Should().BeFalse();
    }

    [Fact]
    public async Task SeedAsync_never_reinserts_a_row_that_was_soft_deleted()
    {
        await using var db = await NewDbAsync();
        await TraitSeeder.SeedAsync(db, Markdown);
        var alfabetizado = await db.Traits.SingleAsync(t => t.Nome == "Alfabetizado");
        alfabetizado.IsDeleted = true;
        await db.SaveChangesAsync();

        var result = await TraitSeeder.SeedAsync(db, Markdown);

        result.Inserted.Should().Be(0); // "Alfabetizado" still matches by key, even soft-deleted
        (await db.Traits.CountAsync(t => t.Nome == "Alfabetizado")).Should().Be(1);
    }
```

- [ ] **Step 2: Run them to confirm they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SeedAsync_never_touches_a_row_flagged_IsCustomized|FullyQualifiedName~SeedAsync_never_reinserts_a_row_that_was_soft_deleted"`
Expected: `SeedAsync_never_reinserts...` already passes by accident (the current `existingByKey` dictionary is built from *all* rows regardless of any flag, since those flags don't exist in the read yet at all before this task's Step 3 — actually re-check: this fails to even COMPILE first, since `IsCustomized`/`IsDeleted` were only added in Task 3's `Trait.cs`, which already landed, so it compiles fine — the failure here is a real *assertion* failure for the first test (`result.Updated` will be `1`, not `0`, because nothing skips the row yet).

- [ ] **Step 3: Update `TraitSeeder.SeedAsync`**

Open `src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs` and replace the whole file with:

```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public static class TraitSeeder
{
    /// <summary>
    /// Características.md is the source of truth, re-read on every startup/migrate: a trait
    /// missing from the table (matched by Nome+Custo+Polaridade) is inserted, and an existing row
    /// whose RequerEspecificacao no longer matches the freshly parsed value is corrected in place
    /// — this is what lets a row seeded before that flag existed pick up its correct value without
    /// a manual data fix on already-deployed databases. Descricao is intentionally NOT synced on
    /// update: only the match key and RequerEspecificacao are ever compared/written for an existing
    /// row.
    ///
    /// Two exceptions, both added for the Rules Audit System (a GM-editable Características
    /// catalog, see TraitsController):
    /// - A row with IsCustomized = true (manually created or edited through that admin page) is
    ///   skipped entirely — not even RequerEspecificacao is synced. A manual edit wins forever.
    /// - The match-key lookup includes soft-deleted rows (IsDeleted = true), so a deliberately
    ///   deleted trait is recognized as "already present" and never reinserted.
    /// </summary>
    public static async Task<(int Inserted, int Updated)> SeedAsync(RuinaRpgDbContext db, string caracteristicasMarkdown)
    {
        var parsed = TraitSeedParser.Parse(caracteristicasMarkdown);
        var existing = await db.Traits.ToListAsync();
        var existingByKey = existing.ToDictionary(t => (t.Nome, t.Custo, Polaridade: t.Polaridade.ToString()));

        var toInsert = new List<Trait>();
        var updated = 0;

        foreach (var seed in parsed)
        {
            if (existingByKey.TryGetValue((seed.Nome, seed.Custo, seed.Polaridade), out var existingTrait))
            {
                if (existingTrait.IsCustomized)
                    continue;

                if (existingTrait.RequerEspecificacao != seed.RequerEspecificacao)
                {
                    existingTrait.RequerEspecificacao = seed.RequerEspecificacao;
                    updated++;
                }
                continue;
            }

            toInsert.Add(new Trait
            {
                Id = Guid.NewGuid(),
                Nome = seed.Nome,
                Descricao = seed.Descricao,
                Custo = seed.Custo,
                Polaridade = Enum.Parse<Polaridade>(seed.Polaridade),
                RequerEspecificacao = seed.RequerEspecificacao
            });
        }

        if (toInsert.Count == 0 && updated == 0)
            return (0, 0);

        if (toInsert.Count > 0)
            db.Traits.AddRange(toInsert);

        await db.SaveChangesAsync();
        return (toInsert.Count, updated);
    }
}
```

(The only functional changes from before: `existingByKey` is unchanged in how it's built — it already included every row regardless of any flag, since `IsDeleted`/`IsCustomized` didn't exist as filter criteria before; the new `if (existingTrait.IsCustomized) continue;` line is the one addition that matters. The doc comment is rewritten to explain both guarantees.)

- [ ] **Step 4: Run the two new tests, then the whole file**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~TraitSeederTests"`
Expected: all facts in the file PASS (the two new ones plus the four pre-existing ones).

- [ ] **Step 5: Build the whole solution**

Run: `dotnet build`
Expected: 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/TraitSeeder.cs tests/RuinaRPG.Tests.Integration/Rules/TraitSeederTests.cs
git commit -m "feat: TraitSeeder skips IsCustomized rows, never resurrects soft-deleted ones"
```

---

## Task 5: `TraitsController` full CRUD, Rules-Auditor-gated

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/CreateTraitRequest.cs`
- Create: `src/RuinaRPG.Contracts/Rules/UpdateTraitRequest.cs`
- Modify: `src/RuinaRPG.Contracts/Rules/TraitResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/TraitsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/TraitsControllerTests.cs` (existing file — add to it; if it doesn't already have GM/Jogador registration helpers matching the rest of this codebase's pattern, copy them from `tests/RuinaRPG.Tests.Integration/Controllers/RacialAbilitiesControllerTests.cs`)

**Interfaces:**
- Consumes: `Trait.IsCustomized`/`IsDeleted`/`UpdatedByUserId`/`UpdatedAt` (Task 3), `ApplicationUser.IsRulesAuditor` (Task 1).
- Produces: `CreateTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao)`, `UpdateTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao)`, `TraitResponse(string Id, string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao, bool IsCustomized)` (the last field is new — check every existing construction site is updated).

- [ ] **Step 1: Write the failing tests**

Open `tests/RuinaRPG.Tests.Integration/Controllers/TraitsControllerTests.cs`. If the file doesn't exist yet, create it with the standard boilerplate (copy `RegisterGmAndGetTokenAsync`/`RegisterJogadorTokenAsync`/`AuthedRequest` verbatim from `RacialAbilitiesControllerTests.cs`, same namespace `RuinaRPG.Tests.Integration.Controllers`). Add:

```csharp
    private async Task<string> GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        return email;
    }

    [Fact]
    public async Task CreateTrait_by_a_Rules_Auditor_persists_it_as_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm1", "traitcrudgm1@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Sortudo", "Ganha sorte extra.", 2, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<TraitResponse>();
        body!.Nome.Should().Be("Sortudo");
        body.IsCustomized.Should().BeTrue();

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<TraitResponse>>();
        list!.Should().Contain(t => t.Nome == "Sortudo");
    }

    [Fact]
    public async Task CreateTrait_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm2", "traitcrudgm2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Sortudo", "Ganha sorte extra.", 2, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTrait_with_a_Custo_sign_mismatched_to_Polaridade_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm3", "traitcrudgm3@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Custo Errado", "Teste.", -2, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTrait_duplicating_an_existing_Nome_Custo_Polaridade_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm4", "traitcrudgm4@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm4@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Duplicada", "Original.", 3, "Positiva", false)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Duplicada", "Outra descrição.", 3, "Positiva", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTrait_by_a_Rules_Auditor_persists_the_change_and_marks_IsCustomized()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm5", "traitcrudgm5@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm5@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Para Editar", "Antes.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/traits/{created!.Id}", gmToken,
            new UpdateTraitRequest("Para Editar", "Depois.", 5, "Positiva", false)));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<TraitResponse>>();
        var edited = list!.Single(t => t.Id == created.Id);
        edited.Descricao.Should().Be("Depois.");
        edited.Custo.Should().Be(5);
    }

    [Fact]
    public async Task DeleteTrait_soft_deletes_and_it_no_longer_appears_in_List()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm6", "traitcrudgm6@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm6@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Para Excluir", "Teste.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/traits/{created!.Id}", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/traits", gmToken));
        var list = await listResponse.Content.ReadFromJsonAsync<List<TraitResponse>>();
        list!.Should().NotContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task DeleteTrait_that_is_already_in_use_on_a_sheet_returns_409()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("TraitCrudGm7", "traitcrudgm7@teste.com");
        await GrantRulesAuditorAsync("traitcrudgm7@teste.com");
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/traits", gmToken,
            new CreateTraitRequest("Em Uso", "Teste.", 1, "Positiva", false)));
        var created = await createResponse.Content.ReadFromJsonAsync<TraitResponse>();

        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "TraitCrudPlayer7", "traitcrudplayer7@teste.com");
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha Trait", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/traits", playerToken, new AddCharacterTraitRequest(created!.Id, null)));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/traits/{created.Id}", gmToken));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
```

Add the necessary `using` directives at the top of the file if missing: `RuinaRPG.Contracts.Rules`, `RuinaRPG.Contracts.CharacterSheets`, `RuinaRPG.Contracts.Campaigns`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.EntityFrameworkCore`.

If `RegisterJogadorLinkedToAsync` doesn't already exist in this file, copy it verbatim from `tests/RuinaRPG.Tests.Integration/Controllers/CharacterRacialTraitsControllerTests.cs`.

- [ ] **Step 2: Run the new tests to confirm they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~TraitsControllerTests"`
Expected: compile errors first (`CreateTraitRequest`/`UpdateTraitRequest` don't exist, `TraitResponse` has no `IsCustomized`, `TraitsController` has no POST/PUT/DELETE).

- [ ] **Step 3: Add the contracts**

Create `src/RuinaRPG.Contracts/Rules/CreateTraitRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record CreateTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao);
```

Create `src/RuinaRPG.Contracts/Rules/UpdateTraitRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record UpdateTraitRequest(string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao);
```

Modify `src/RuinaRPG.Contracts/Rules/TraitResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record TraitResponse(string Id, string Nome, string Descricao, int Custo, string Polaridade, bool RequerEspecificacao, bool IsCustomized);
```

- [ ] **Step 4: Rewrite `TraitsController`**

Replace `src/RuinaRPG.Api/Controllers/TraitsController.cs` with:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Características.md's static, GM-independent catalog (seeded once, shared by every GM/Jogador)
/// — unlike Item/SpellAbilityBankEntry, Traits have no owning GmId at all. List (GET) stays open
/// to any authenticated caller — the Compêndio and every sheet's "add characteristic" picker use
/// it. Create/Update/Delete are gated to the Rules Auditor (see Requisitos - Auditoria de
/// Regras) — checked directly against the DB (ApplicationUser.IsRulesAuditor), not a JWT claim,
/// so a grant/revoke via `make grant-rules-auditor` takes effect on the auditor's very next
/// request.
/// </summary>
[ApiController]
[Route("api/traits")]
[Authorize]
public class TraitsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TraitResponse>>> List([FromQuery] string? nome)
    {
        var query = db.Traits.Where(t => !t.IsDeleted);
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(t => EF.Functions.ILike(t.Nome, $"%{nome}%"));

        return await query
            .Select(t => new TraitResponse(t.Id.ToString(), t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString(), t.RequerEspecificacao, t.IsCustomized))
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<TraitResponse>> Create(CreateTraitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<Polaridade>(request.Polaridade, out var polaridade))
            return BadRequest("Polaridade inválida.");
        if (!IsCustoSignConsistent(request.Custo, polaridade))
            return BadRequest("O sinal do Custo não é consistente com a Polaridade (Positiva >= 0, Negativa <= 0).");
        if (await db.Traits.AnyAsync(t => !t.IsDeleted && t.Nome == request.Nome && t.Custo == request.Custo && t.Polaridade == polaridade))
            return BadRequest("Já existe uma característica com esse Nome, Custo e Polaridade.");

        var trait = new Trait
        {
            Id = Guid.NewGuid(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            Custo = request.Custo,
            Polaridade = polaridade,
            RequerEspecificacao = request.RequerEspecificacao,
            IsCustomized = true,
            UpdatedByUserId = CurrentUserId(),
            UpdatedAt = DateTime.UtcNow,
        };
        db.Traits.Add(trait);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(trait));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateTraitRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (trait is null)
            return NotFound();

        if (!Enum.TryParse<Polaridade>(request.Polaridade, out var polaridade))
            return BadRequest("Polaridade inválida.");
        if (!IsCustoSignConsistent(request.Custo, polaridade))
            return BadRequest("O sinal do Custo não é consistente com a Polaridade (Positiva >= 0, Negativa <= 0).");

        trait.Nome = request.Nome;
        trait.Descricao = request.Descricao;
        trait.Custo = request.Custo;
        trait.Polaridade = polaridade;
        trait.RequerEspecificacao = request.RequerEspecificacao;
        trait.IsCustomized = true;
        trait.UpdatedByUserId = CurrentUserId();
        trait.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (trait is null)
            return NotFound();

        var inUse = await db.CharacterTraits.AnyAsync(t => t.TraitId == id) || await db.NpcTraits.AnyAsync(t => t.TraitId == id);
        if (inUse)
            return Conflict("Esta característica está em uso em pelo menos uma ficha e não pode ser excluída.");

        trait.IsDeleted = true;
        trait.UpdatedByUserId = CurrentUserId();
        trait.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static bool IsCustoSignConsistent(int custo, Polaridade polaridade) =>
        polaridade == Polaridade.Positiva ? custo >= 0 : custo <= 0;

    private static TraitResponse ToResponse(Trait trait) =>
        new(trait.Id.ToString(), trait.Nome, trait.Descricao, trait.Custo, trait.Polaridade.ToString(), trait.RequerEspecificacao, trait.IsCustomized);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Fix every other construction site of `TraitResponse`**

Run: `grep -rn "new TraitResponse(" src` — as of this plan's writing there's exactly one, inside `TraitsController` itself (now replaced above). If the grep finds any other, add `t.IsCustomized` (or the equivalent boolean expression for that context) as the 7th positional argument.

Run: `grep -rln "db.Traits\b" src/RuinaRPG.Api/Controllers` and open each hit that isn't `TraitsController.cs`. As of this plan's writing that's: `CompendioController.cs`, `CharacterPossessionsController.cs`, `NpcPossessionsController.cs`. Add a `!IsDeleted` guard to each:

In `src/RuinaRPG.Api/Controllers/CompendioController.cs`, find:

```csharp
        var traits = await db.Traits
            .Select(t => new TraitSeed(t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString(), t.RequerEspecificacao))
            .ToListAsync();
```

Change to:

```csharp
        var traits = await db.Traits
            .Where(t => !t.IsDeleted)
            .Select(t => new TraitSeed(t.Nome, t.Descricao, t.Custo, t.Polaridade.ToString(), t.RequerEspecificacao))
            .ToListAsync();
```

In `src/RuinaRPG.Api/Controllers/CharacterPossessionsController.cs`, find (inside `AddTrait`):

```csharp
        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == traitId);
        if (trait is null)
            return BadRequest("Trait não encontrado.");
```

Change to:

```csharp
        var trait = await db.Traits.FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted);
        if (trait is null)
            return BadRequest("Trait não encontrado.");
```

Also find, inside `ResolveRacialTraitChoice`:

```csharp
            var trait = await db.Traits.FirstOrDefaultAsync(t => t.Nome == grant.TraitNome);
            if (trait is null)
                return BadRequest($"Característica \"{grant.TraitNome}\" não encontrada no catálogo.");
```

Change to:

```csharp
            var trait = await db.Traits.FirstOrDefaultAsync(t => t.Nome == grant.TraitNome && !t.IsDeleted);
            if (trait is null)
                return BadRequest($"Característica \"{grant.TraitNome}\" não encontrada no catálogo.");
```

Make the exact same two changes (same before/after text) in `src/RuinaRPG.Api/Controllers/NpcPossessionsController.cs` — it has the identical two call sites (`AddTrait`'s `db.Traits.FirstOrDefaultAsync(t => t.Id == traitId)` and `ResolveRacialTraitChoice`'s `db.Traits.FirstOrDefaultAsync(t => t.Nome == grant.TraitNome)`).

- [ ] **Step 6: Run the tests**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~TraitsControllerTests"`
Expected: all 7 new facts PASS.

Then run the full pre-existing suites that touch Traits to check nothing regressed:

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CompendioControllerTests|FullyQualifiedName~CharacterAffectionsAndTraitsControllerTests|FullyQualifiedName~CharacterRacialTraitsControllerTests|FullyQualifiedName~NpcRacialTraitsControllerTests"`
Expected: all PASS (adjust the filter list if any of these class names differ from what actually exists — find them with `grep -rl "class.*ControllerTests" tests/RuinaRPG.Tests.Integration/Controllers | xargs grep -l "db.Traits\|AddCharacterTraitRequest\|AddNpcTraitRequest\|ResolveRacialTraitChoiceRequest"` first if unsure).

- [ ] **Step 7: Build the whole solution**

Run: `dotnet build`
Expected: 0 warnings, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Contracts/Rules/CreateTraitRequest.cs src/RuinaRPG.Contracts/Rules/UpdateTraitRequest.cs src/RuinaRPG.Contracts/Rules/TraitResponse.cs src/RuinaRPG.Api/Controllers/TraitsController.cs src/RuinaRPG.Api/Controllers/CompendioController.cs src/RuinaRPG.Api/Controllers/CharacterPossessionsController.cs src/RuinaRPG.Api/Controllers/NpcPossessionsController.cs tests/RuinaRPG.Tests.Integration/Controllers/TraitsControllerTests.cs
git commit -m "feat: full Trait CRUD gated to the Rules Auditor"
```

---

## Task 6: `RulebookDocumentOverride` entity + migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/RulebookDocumentOverride.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Migration: new file
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/RulebookDocumentOverrideMigrationTests.cs` (new)

**Interfaces:**
- Produces: `RulebookDocumentOverride { Guid Id; string Slug; string MarkdownText; Guid UpdatedByUserId; DateTime UpdatedAt; }`, `RuinaRpgDbContext.RulebookDocumentOverrides` (`DbSet<RulebookDocumentOverride>`).

- [ ] **Step 1: Write the failing migration test**

Create `tests/RuinaRPG.Tests.Integration/Persistence/RulebookDocumentOverrideMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RulebookDocumentOverrideMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RulebookDocumentOverrideMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_RulebookDocumentOverrides_table_with_a_unique_Slug()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddRulebookDocumentOverrides"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "rulebookoverridegm@teste.com", Email = "rulebookoverridegm@teste.com", Nickname = "RulebookOverrideGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.RulebookDocumentOverrides.Add(new RulebookDocumentOverride { Id = Guid.NewGuid(), Slug = "sistema-basico", MarkdownText = "# Teste\n\nConteúdo.", UpdatedByUserId = gm.Id, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var reloaded = await db.RulebookDocumentOverrides.SingleAsync(o => o.Slug == "sistema-basico");
        reloaded.MarkdownText.Should().Contain("Conteúdo.");

        // Slug is unique — a second row for the same slug must fail.
        db.RulebookDocumentOverrides.Add(new RulebookDocumentOverride { Id = Guid.NewGuid(), Slug = "sistema-basico", MarkdownText = "Outro.", UpdatedByUserId = gm.Id, UpdatedAt = DateTime.UtcNow });
        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
```

- [ ] **Step 2: Run it to confirm it fails to compile**

Run: `dotnet build tests/RuinaRPG.Tests.Integration`
Expected: `RulebookDocumentOverride`/`RulebookDocumentOverrides` don't exist yet.

- [ ] **Step 3: Create the entity**

Create `src/RuinaRPG.Infrastructure/Rules/RulebookDocumentOverride.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// GM-editable override of one Livro de Regras document's raw Markdown, keyed by the same Slug
/// RulebookRenderer already uses ("sistema-basico", "graus-e-circulos", "tabela-de-niveis" — never
/// "caracteristicas", which is DB-driven from Traits instead, see RulebookRenderer). One row per
/// Slug; absence of a row means "use the embedded .md resource", exactly as before this feature
/// existed. Display-only: nothing here feeds IRulesDataProvider or any gameplay calculator.
/// </summary>
public class RulebookDocumentOverride
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public required string MarkdownText { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 4: Register the DbSet and the unique index**

In `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`, find:

```csharp
    public DbSet<RacialTraitOverride> RacialTraitOverrides => Set<RacialTraitOverride>();
```

Add immediately after it:

```csharp
    public DbSet<RulebookDocumentOverride> RulebookDocumentOverrides => Set<RulebookDocumentOverride>();
```

Then find the `builder.Entity<RacialTraitOverride>(entity => { ... });` config block and add immediately after its closing `});`:

```csharp
        builder.Entity<RulebookDocumentOverride>(entity =>
        {
            entity.HasIndex(o => o.Slug).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(o => o.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
```

(`DeleteBehavior.Restrict` rather than `.Cascade` here, unlike `RacialTraitOverride`/`RacialAbilityOverride` — those two are scoped to one GM and make sense to vanish if that GM's account is deleted; a rulebook-wide override should outlive any one user account, so deleting the auditor's account must not delete everyone's rulebook edits. If deleting the referenced user is ever attempted while an override still points at them, EF will block it — acceptable for this feature; there is no user-deletion flow in this codebase today to worry about breaking.)

- [ ] **Step 5: Generate and inspect the migration**

Run: `dotnet ef migrations add AddRulebookDocumentOverrides --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`

Confirm it creates the `RulebookDocumentOverrides` table (Id, Slug, MarkdownText, UpdatedByUserId, UpdatedAt) with a unique index on `Slug` and an FK from `UpdatedByUserId` to `AspNetUsers` with `NO ACTION`/`Restrict` delete behavior.

- [ ] **Step 6: Run the test**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookDocumentOverrideMigrationTests"`
Expected: PASS.

- [ ] **Step 7: Build the whole solution**

Run: `dotnet build`
Expected: 0 warnings, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/RulebookDocumentOverride.cs src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs src/RuinaRPG.Infrastructure/Persistence/Migrations/*AddRulebookDocumentOverrides* tests/RuinaRPG.Tests.Integration/Persistence/RulebookDocumentOverrideMigrationTests.cs
git commit -m "feat: add RulebookDocumentOverride entity and table"
```

---

## Task 7: `RulebookDocumentsController` admin CRUD

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/RulebookDocumentOverrideResponse.cs`
- Create: `src/RuinaRPG.Contracts/Rules/UpdateRulebookDocumentOverrideRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/RulebookDocumentsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/RulebookDocumentsControllerTests.cs` (new)

**Interfaces:**
- Consumes: `RulebookDocumentOverride` (Task 6), `ApplicationUser.IsRulesAuditor` (Task 1).
- Produces: `RulebookDocumentOverrideResponse(string Slug, string MarkdownText, bool IsDefault)`, `UpdateRulebookDocumentOverrideRequest(string MarkdownText)`.
- The 3 valid slugs are the literal strings `"sistema-basico"`, `"graus-e-circulos"`, `"tabela-de-niveis"` — Task 8 (`RulebookRenderer`) reads overrides by these exact same strings, so do not rename them here.

- [ ] **Step 1: Write the failing tests**

Create `tests/RuinaRPG.Tests.Integration/Controllers/RulebookDocumentsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RulebookDocumentsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RulebookDocumentsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RuinaRPG.Contracts.Auth.RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_returns_the_3_documents_all_default_when_no_override_exists()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm1", "rulebookdocgm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        body!.Select(d => d.Slug).Should().BeEquivalentTo("sistema-basico", "graus-e-circulos", "tabela-de-niveis");
        body!.Should().OnlyContain(d => d.IsDefault);
        body!.Should().OnlyContain(d => !string.IsNullOrEmpty(d.MarkdownText)); // the embedded default text, non-empty
    }

    [Fact]
    public async Task List_by_a_GM_who_is_not_a_Rules_Auditor_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm2", "rulebookdocgm2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/sistema-basico", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Novo texto")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateDocument_persists_the_override_and_List_reflects_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm3", "rulebookdocgm3@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm3@teste.com");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/sistema-basico", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Sistema Básico Editado\n\nTexto novo do Auditor.")));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        var edited = body!.Single(d => d.Slug == "sistema-basico");
        edited.IsDefault.Should().BeFalse();
        edited.MarkdownText.Should().Contain("Texto novo do Auditor.");
    }

    [Fact]
    public async Task UpdateDocument_with_an_unknown_slug_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm4", "rulebookdocgm4@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/caracteristicas", gmToken,
            new UpdateRulebookDocumentOverrideRequest("# Não permitido")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteDocument_reverts_to_the_default()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookDocGm5", "rulebookdocgm5@teste.com");
        await GrantRulesAuditorAsync("rulebookdocgm5@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/rulebook-documents/tabela-de-niveis", gmToken,
            new UpdateRulebookDocumentOverrideRequest("| Custom |")));

        var deleteResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/rulebook-documents/tabela-de-niveis", gmToken));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook-documents", gmToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>();
        body!.Single(d => d.Slug == "tabela-de-niveis").IsDefault.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run to confirm the tests fail to compile**

Run: `dotnet build tests/RuinaRPG.Tests.Integration`
Expected: missing types (`RulebookDocumentOverrideResponse`, `UpdateRulebookDocumentOverrideRequest`) and missing route (`api/rulebook-documents`).

- [ ] **Step 3: Add the contracts**

Create `src/RuinaRPG.Contracts/Rules/RulebookDocumentOverrideResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record RulebookDocumentOverrideResponse(string Slug, string MarkdownText, bool IsDefault);
```

Create `src/RuinaRPG.Contracts/Rules/UpdateRulebookDocumentOverrideRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record UpdateRulebookDocumentOverrideRequest(string MarkdownText);
```

- [ ] **Step 4: Create the controller**

Create `src/RuinaRPG.Api/Controllers/RulebookDocumentsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// GM-editable overrides of the Livro de Regras' raw Markdown, for the 3 documents that aren't
/// DB-driven (Sistema Básico, Graus & Círculos, Tabela de Níveis — "caracteristicas" is excluded
/// on purpose, it's rebuilt from the live Traits table by RulebookRenderer instead, see
/// TraitsController). Display-only: nothing here feeds IRulesDataProvider or any gameplay
/// calculator — see Requisitos - Auditoria de Regras. Gated to the Rules Auditor, same inline
/// DB check as TraitsController (not a JWT claim, so a grant/revoke via `make grant-rules-auditor`
/// takes effect on the very next request).
/// </summary>
[ApiController]
[Authorize]
public class RulebookDocumentsController(RuinaRpgDbContext db) : ControllerBase
{
    private static readonly string[] ValidSlugs = ["sistema-basico", "graus-e-circulos", "tabela-de-niveis"];

    [HttpGet("api/rulebook-documents")]
    public async Task<ActionResult<List<RulebookDocumentOverrideResponse>>> List()
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var overrides = await db.RulebookDocumentOverrides.ToListAsync();

        return ValidSlugs.Select(slug =>
        {
            var over = overrides.FirstOrDefault(o => o.Slug == slug);
            return over is not null
                ? new RulebookDocumentOverrideResponse(slug, over.MarkdownText, false)
                : new RulebookDocumentOverrideResponse(slug, RulebookRenderer.ReadEmbeddedMarkdown(slug), true);
        }).ToList();
    }

    [HttpPut("api/rulebook-documents/{slug}")]
    public async Task<IActionResult> Update(string slug, UpdateRulebookDocumentOverrideRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!ValidSlugs.Contains(slug))
            return BadRequest("Slug de documento desconhecido.");

        var existing = await db.RulebookDocumentOverrides.FirstOrDefaultAsync(o => o.Slug == slug);
        if (existing is null)
        {
            db.RulebookDocumentOverrides.Add(new RulebookDocumentOverride
            {
                Id = Guid.NewGuid(), Slug = slug, MarkdownText = request.MarkdownText,
                UpdatedByUserId = CurrentUserId(), UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.MarkdownText = request.MarkdownText;
            existing.UpdatedByUserId = CurrentUserId();
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/rulebook-documents/{slug}")]
    public async Task<IActionResult> Delete(string slug)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!ValidSlugs.Contains(slug))
            return BadRequest("Slug de documento desconhecido.");

        var existing = await db.RulebookDocumentOverrides.FirstOrDefaultAsync(o => o.Slug == slug);
        if (existing is not null)
        {
            db.RulebookDocumentOverrides.Remove(existing);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

This references a not-yet-existing `RulebookRenderer.ReadEmbeddedMarkdown(string slug)` static helper — add it now so this compiles, ahead of Task 8's bigger `RulebookRenderer` rewrite (Task 8 will reuse it, not duplicate it). Open `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs` and add this public static method to the `RulebookRenderer` class (anywhere inside the class body, e.g. right after the `BuildTabelaDeNiveis` method):

```csharp
    /// <summary>
    /// Maps a document Slug to the exact embedded-resource filename RulesDataProvider.ReadResource
    /// expects, and returns its raw text. Exposed publicly (unlike the private Build* methods)
    /// specifically so RulebookDocumentsController can show the Rules Auditor the current default
    /// text for a document that has no override yet, without duplicating this mapping.
    /// </summary>
    public static string ReadEmbeddedMarkdown(string slug) => slug switch
    {
        "sistema-basico" => RulesDataProvider.ReadResource("Sistema Basico.md"),
        "graus-e-circulos" => RulesDataProvider.ReadResource("GRAUS e CIRCULOS.md"),
        "tabela-de-niveis" => RulesDataProvider.ReadResource("Tabela de Níveis.md"),
        _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "Slug de documento desconhecido."),
    };
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookDocumentsControllerTests"`
Expected: all 5 facts PASS.

- [ ] **Step 6: Build the whole solution**

Run: `dotnet build`
Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Contracts/Rules/RulebookDocumentOverrideResponse.cs src/RuinaRPG.Contracts/Rules/UpdateRulebookDocumentOverrideRequest.cs src/RuinaRPG.Api/Controllers/RulebookDocumentsController.cs src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs tests/RuinaRPG.Tests.Integration/Controllers/RulebookDocumentsControllerTests.cs
git commit -m "feat: RulebookDocumentsController — admin override CRUD for 3 rulebook documents"
```

---

## Task 8: `RulebookRenderer` reads overrides; `RulebookController` goes async

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs`
- Modify: `src/RuinaRPG.Api/Controllers/RulebookController.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (the `AddSingleton<IRulebookRenderer, ...>` line)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs` (existing — add to it)

**Interfaces:**
- Consumes: `RulebookDocumentOverride` (Task 6), `RulebookRenderer.ReadEmbeddedMarkdown` (Task 7).
- Produces: `IRulebookRenderer.GetDocuments()` becomes `Task<IReadOnlyList<RulebookDocument>>` (was sync) — any other caller of this interface must be updated; as of this plan's writing the only caller is `RulebookController.Get()`.

- [ ] **Step 1: Write the failing test**

Open `tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs` and add:

```csharp
    [Fact]
    public async Task Get_reflects_a_saved_RulebookDocumentOverride_immediately()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RulebookGmOverride1", "rulebookgmoverride1@teste.com");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "RULEBOOKGMOVERRIDE1@TESTE.COM");
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();

        var updateMessage = new HttpRequestMessage(HttpMethod.Put, "/api/rulebook-documents/tabela-de-niveis");
        updateMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        updateMessage.Content = JsonContent.Create(new UpdateRulebookDocumentOverrideRequest("# Texto Substituído Pelo Auditor"));
        await _client.SendAsync(updateMessage);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rulebook", gmToken));

        var body = await response.Content.ReadFromJsonAsync<List<RulebookDocumentResponse>>();
        var tabelaDeNiveis = body!.Single(d => d.Slug == "tabela-de-niveis");
        (tabelaDeNiveis.IntroHtml ?? "").Should().Contain("Texto Substituído Pelo Auditor");
    }
```

Add the necessary `using RuinaRPG.Contracts.Rules;` and `using Microsoft.Extensions.DependencyInjection;` and `using Microsoft.EntityFrameworkCore;` at the top of the file if not already present. (`RulebookControllerTests.cs` doesn't currently have an `AuthedRequest` overload accepting a body via `AuthedRequest(HttpMethod, url, token)` — check its existing helper signatures; if it only has a 3-arg `AuthedRequest(HttpMethod, url, token)` with no body parameter, that's why this test builds the PUT request manually above instead of reusing it — keep it that way, don't change the file's existing helper signature for this one test.)

- [ ] **Step 2: Run it to confirm it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Get_reflects_a_saved_RulebookDocumentOverride_immediately"`
Expected: FAIL — the override is saved but `GET /api/rulebook` still renders the embedded default (no override lookup exists yet).

- [ ] **Step 3: Rewrite `RulebookRenderer` to be DB-aware and async**

Open `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs`. Replace the top of the file (imports, the two records, the interface, and the class declaration through `BuildDocuments`) with:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// One navigable chunk of a rulebook document — the unit the client's table of contents and
/// per-section cards are built from. Grupo is set only for documents that split at a heading level
/// nested under a shallower "group" heading (currently only Características: Positivas/Negativas).
/// </summary>
public record RulebookSection(string Id, string Titulo, string Html, string? Grupo = null);

public record RulebookDocument(string Slug, string Titulo, string? IntroHtml, IReadOnlyList<RulebookSection> Sections);

public interface IRulebookRenderer
{
    Task<IReadOnlyList<RulebookDocument>> GetDocuments();
}

/// <summary>
/// Renders the Livro de Regras' 4 documents as displayable HTML. 3 of them (Sistema Básico, Graus
/// & Círculos, Tabela de Níveis) render a RulebookDocumentOverride's Markdown when the Rules
/// Auditor has saved one for that Slug (see RulebookDocumentsController), the embedded
/// Docs/Sistema RPG resource otherwise — display-only, this never affects IRulesDataProvider or
/// any gameplay calculator. The 4th (Características) is rebuilt straight from the live Traits
/// table instead of any Markdown at all (see BuildCaracteristicasAsync) — editing a Trait via
/// TraitsController is what changes that one.
///
/// Scoped (not Singleton — Program.cs registers it as such): it takes a RuinaRpgDbContext, and an
/// override can change between requests, so nothing here is cached across requests the way it used
/// to be with the old Lazy&lt;&gt; field.
/// </summary>
public class RulebookRenderer(RuinaRpgDbContext db) : IRulebookRenderer
{
    public async Task<IReadOnlyList<RulebookDocument>> GetDocuments() =>
    [
        await BuildCaracteristicasAsync(),
        await BuildSistemaBasicoAsync(),
        await BuildGrausECirculosAsync(),
        await BuildTabelaDeNiveisAsync(),
    ];
```

Then find the existing `private static readonly MarkdownPipeline Pipeline = ...` line — leave it as-is (still `static readonly`, it holds no request-specific state).

Replace `BuildSistemaBasico`/`BuildCaracteristicas`/`BuildGrausECirculos`/`BuildTabelaDeNiveis` (everything from the `Pipeline` field's doc comment down through the end of `BuildTabelaDeNiveis`, i.e. everything up to but not including `SplitIntoSections`) with:

```csharp
    // "## 1. Atributos" / "## 2. Perícias e Progressão" / ... — 7 numbered top-level sections,
    // no content before the first one.
    private async Task<RulebookDocument> BuildSistemaBasicoAsync()
    {
        var (intro, sections) = SplitIntoSections(await ReadMarkdownAsync("sistema-basico"), splitLevel: 2);
        return new RulebookDocument("sistema-basico", "Sistema Básico", intro, sections);
    }

    /// <summary>
    /// Unlike the other 3 documents, this one has no Markdown override at all — it is rebuilt
    /// directly from the live Traits table (Requisitos - Auditoria de Regras' unification: editing
    /// a Característica via TraitsController is immediately visible here too, instead of this tab
    /// being a second, disconnected copy of the same prose Características.md used to be). Grouped
    /// by Polaridade (Positivas/Negativas) — same Grupo shape the client's existing filterable-list
    /// UI (LivroDeRegras.razor's "caracteristicas" branch) already expects, unchanged by this.
    /// </summary>
    private async Task<RulebookDocument> BuildCaracteristicasAsync()
    {
        var traits = await db.Traits
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Polaridade)
            .ThenBy(t => t.Nome)
            .ToListAsync();

        var sections = traits.Select(t => new RulebookSection(
            Id: Slugify(t.Nome),
            Titulo: t.Nome,
            Html: WebUtility.HtmlEncode(t.Descricao).Replace("\n", "<br />") + $"<p><em>Custo: {t.Custo} ponto(s)</em></p>",
            Grupo: t.Polaridade == Polaridade.Positiva ? "Positivas" : "Negativas"
        )).ToList();

        return new RulebookDocument("caracteristicas", "Características", null, sections);
    }

    /// <summary>
    /// Escolas_de_Magia.png / Matriz_Elemental.png have no matching section in the source
    /// document — no "Escolas de Magia" heading exists at all, and the closest thing to
    /// "Matriz Elemental" (the "Encantamento Elemental" effect, 2º Grau) is just one of many
    /// effects, not a natural home for a document-wide reference image. Appended to IntroHtml
    /// (after the document's own lead-in cost table, before the per-Grau sections) instead —
    /// reference material for the whole document, not any one Grau/effect. The images live as
    /// static wwwroot assets (src/RuinaRPG.Client/wwwroot/rulebook/) rather than embedded in the
    /// source .md, so Docs/ stays untouched as the single source of truth.
    /// </summary>
    private async Task<RulebookDocument> BuildGrausECirculosAsync()
    {
        var (intro, sections) = SplitIntoSections(await ReadMarkdownAsync("graus-e-circulos"), splitLevel: 1);
        return new RulebookDocument("graus-e-circulos", "Graus & Círculos", (intro ?? "") + ReferenceImagesHtml, sections);
    }

    private const string ReferenceImagesHtml = """
        <div class="rulebook-reference-images" style="display:flex;gap:16px;flex-wrap:wrap;margin-bottom:16px">
            <figure style="margin:0">
                <img src="/rulebook/Escolas_de_Magia.png" alt="Escolas de Magia" style="max-width:100%" />
                <figcaption>Escolas de Magia</figcaption>
            </figure>
            <figure style="margin:0">
                <img src="/rulebook/Matriz_Elemental.png" alt="Matriz Elemental" style="max-width:100%" />
                <figcaption>Matriz Elemental</figcaption>
            </figure>
        </div>
        """;

    // No Markdown headings at all — one big GFM pipe table. Splitting finds nothing to split on, so
    // Sections stays empty and the whole rendered table lands in IntroHtml.
    private async Task<RulebookDocument> BuildTabelaDeNiveisAsync()
    {
        var (intro, sections) = SplitIntoSections(await ReadMarkdownAsync("tabela-de-niveis"), splitLevel: 2);
        return new RulebookDocument("tabela-de-niveis", "Tabela de Níveis", intro, sections);
    }

    /// <summary>
    /// Maps a document Slug to the exact embedded-resource filename RulesDataProvider.ReadResource
    /// expects, and returns its raw text. Exposed publicly (unlike the private Build* methods)
    /// specifically so RulebookDocumentsController can show the Rules Auditor the current default
    /// text for a document that has no override yet, without duplicating this mapping.
    /// </summary>
    public static string ReadEmbeddedMarkdown(string slug) => slug switch
    {
        "sistema-basico" => RulesDataProvider.ReadResource("Sistema Basico.md"),
        "graus-e-circulos" => RulesDataProvider.ReadResource("GRAUS e CIRCULOS.md"),
        "tabela-de-niveis" => RulesDataProvider.ReadResource("Tabela de Níveis.md"),
        _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "Slug de documento desconhecido."),
    };

    /// <summary>The RulebookDocumentOverride for this Slug, if the Rules Auditor saved one — the embedded default otherwise.</summary>
    private async Task<string> ReadMarkdownAsync(string slug)
    {
        var over = await db.RulebookDocumentOverrides.FirstOrDefaultAsync(o => o.Slug == slug);
        return over?.MarkdownText ?? ReadEmbeddedMarkdown(slug);
    }
```

(This removes the `ReadEmbeddedMarkdown` method Task 7 added as a one-off — it's now defined here instead, in its natural home; Task 7's copy must be deleted as part of this edit so there's exactly one definition. Since Task 7 added it to this same file, this whole Step 3 replaces that too — do not leave two copies.)

Leave `SplitIntoSections`, `RenderBlocks`, `ExtractText`, and `Slugify` exactly as they are — none of them change.

- [ ] **Step 4: Update `RulebookController`**

Open `src/RuinaRPG.Api/Controllers/RulebookController.cs`. It currently reads:

```csharp
    [HttpGet]
    public ActionResult<List<RulebookDocumentResponse>> Get() =>
        renderer.GetDocuments()
            .Select(d => new RulebookDocumentResponse(d.Slug, d.Titulo, d.IntroHtml,
                d.Sections.Select(s => new RulebookSectionResponse(s.Id, s.Titulo, s.Html, s.Grupo)).ToList()))
            .ToList();
```

Replace with:

```csharp
    [HttpGet]
    public async Task<ActionResult<List<RulebookDocumentResponse>>> Get() =>
        (await renderer.GetDocuments())
            .Select(d => new RulebookDocumentResponse(d.Slug, d.Titulo, d.IntroHtml,
                d.Sections.Select(s => new RulebookSectionResponse(s.Id, s.Titulo, s.Html, s.Grupo)).ToList()))
            .ToList();
```

- [ ] **Step 5: Fix the DI registration**

Open `src/RuinaRPG.Api/Program.cs` and find:

```csharp
builder.Services.AddSingleton<IRulebookRenderer, RulebookRenderer>();
```

Change to:

```csharp
builder.Services.AddScoped<IRulebookRenderer, RulebookRenderer>();
```

(It must not stay `Singleton` — it now depends on `RuinaRpgDbContext`, which is registered `Scoped`; a `Singleton` consuming a `Scoped` service throws at first resolution.)

- [ ] **Step 6: Run the new test, then the whole `RulebookControllerTests` file**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookControllerTests"`
Expected: all facts PASS, including the 4 pre-existing ones (`Get_returns_the_four_documents_split_into_sections`, `Caracteristicas_sections_are_tagged_...`, `GrausECirculos_IntroHtml_includes_...`, and whichever 401/other fact already existed) — re-read their assertions if any fail; the `Caracteristicas_sections_are_tagged_with_their_Positivas_or_Negativas_group` test in particular now exercises the DB-driven path instead of the Markdown-driven one, so its count assertion (`HaveCountGreaterThan(50)`) depends on how many non-deleted Traits exist in a freshly-seeded test database — if that count assertion fails, the actual seeded catalog size in this environment is smaller than 50; adjust the assertion to `HaveCountGreaterThan(20)` (still comfortably distinguishing "the real catalog" from "an empty or near-empty one") rather than deleting the test.

- [ ] **Step 7: Build the whole solution**

Run: `dotnet build`
Expected: 0 warnings, 0 errors.

- [ ] **Step 8: Run the broader Rulebook/Traits-adjacent integration suites once more**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookControllerTests|FullyQualifiedName~RulebookDocumentsControllerTests|FullyQualifiedName~TraitsControllerTests"`
Expected: all PASS.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs src/RuinaRPG.Api/Controllers/RulebookController.cs src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Integration/Controllers/RulebookControllerTests.cs
git commit -m "feat: RulebookRenderer reads document overrides; Características is DB-driven"
```

---

## Task 9: Client — nav entries + Livro de Regras editor page

**Files:**
- Create: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`
- Create: `src/RuinaRPG.Client/Pages/AuditoriaLivroDeRegras.razor`

**Interfaces:**
- Consumes: `GET/PUT/DELETE api/rulebook-documents` (Task 7), `GET api/auth/me` → `MeResponse.IsRulesAuditor` (Task 1).

- [ ] **Step 1: Add the nav entries, gated on `IsRulesAuditor`**

Per spec §7.1, these two links must be hidden from a GM who is not the designated Rules Auditor — not merely rely on the page's own 403. `NavMenu.razor` itself never calls the API today; `Painel.razor` is this codebase's existing precedent for a page resolving `MeResponse` via `Http.GetAsync("auth/me")` (search that file for `_me`/`auth/me` to see the exact pattern). Extract the check into its own small component rather than inlining an `OnInitializedAsync` fetch straight into `NavMenu.razor`, so it naturally re-fetches every time the surrounding `<AuthorizeView Roles="GM">` flips from not-rendered to rendered (i.e., right after a GM logs in) — `AuthorizeView` creates its `Authorized` subtree fresh on each such transition, so a child component's `OnInitializedAsync` runs again then, without NavMenu needing to listen for auth-state-changed events itself.

Create `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor`:

```razor
@inject HttpClient Http
@using RuinaRPG.Contracts.Auth

@if (_isRulesAuditor)
{
    <MudNavLink Href="auditoria/livro-de-regras">Auditoria: Livro de Regras</MudNavLink>
    <MudNavLink Href="auditoria/caracteristicas">Auditoria: Características</MudNavLink>
}

@code {
    private bool _isRulesAuditor;

    protected override async Task OnInitializedAsync()
    {
        var response = await Http.GetAsync("auth/me");
        if (!response.IsSuccessStatusCode)
            return;

        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        _isRulesAuditor = me?.IsRulesAuditor ?? false;
    }
}
```

Open `src/RuinaRPG.Client/Layout/NavMenu.razor`. Find the GM `<AuthorizeView Roles="GM">` block's `<Authorized>` section (the one already listing `painel/convites`, `catalogo`, etc.) and add the new component right before the closing `</Authorized>` of that inner block:

```razor
                    <RulesAuditorNavLinks />
```

- [ ] **Step 2: Create the Livro de Regras editor page**

Create `src/RuinaRPG.Client/Pages/AuditoriaLivroDeRegras.razor`:

```razor
@page "/auditoria/livro-de-regras"
@inject HttpClient Http
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Client.Services
@using MudBlazor

<Breadcrumbs Items="@Crumbs" />
<MudText Typo="Typo.h3">Auditoria: Livro de Regras</MudText>

<MudAlert Severity="Severity.Info" Class="mt-3">
    Editar aqui só muda o texto exibido nesta página do Livro de Regras — não afeta nenhum cálculo
    usado nas fichas (Vitalidade, Foco, Graduação, XP por nível, etc.). Essas fórmulas continuam
    fixas no sistema.
</MudAlert>

@if (_errorMessage is not null)
{
    <MudAlert Severity="Severity.Error" Class="mt-3">@_errorMessage</MudAlert>
}

@if (_forbidden)
{
    <MudAlert Severity="Severity.Warning" Class="mt-3">Você não é o Auditor de Regras designado — sem permissão para editar.</MudAlert>
}
else
{
    @foreach (var doc in _documents)
    {
        <Section Title="@Label(doc.Slug)">
            <MudTextField T="string" Value="@doc.MarkdownText" ValueChanged="@(v => UpdateAsync(doc.Slug, v))" Label="Markdown" Lines="20" />
            <MudButton Variant="Variant.Outlined" Color="Color.Secondary" Size="Size.Small" Class="mt-2" Disabled="@doc.IsDefault" OnClick="@(() => RestoreDefaultAsync(doc.Slug))">Restaurar padrão</MudButton>
        </Section>
    }
}

@code {
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Auditoria: Livro de Regras"),
    };

    private List<RulebookDocumentOverrideResponse> _documents = new();
    private string? _errorMessage;
    private bool _forbidden;

    private static string Label(string slug) => slug switch
    {
        "sistema-basico" => "Sistema Básico",
        "graus-e-circulos" => "Graus & Círculos",
        "tabela-de-niveis" => "Tabela de Níveis",
        _ => slug,
    };

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var response = await Http.GetAsync("rulebook-documents");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar os documentos.";
            return;
        }

        _documents = await response.Content.ReadFromJsonAsync<List<RulebookDocumentOverrideResponse>>() ?? new();
    }

    private async Task UpdateAsync(string slug, string? markdownText)
    {
        var response = await Http.PutAsJsonAsync($"rulebook-documents/{slug}", new UpdateRulebookDocumentOverrideRequest(markdownText ?? ""));
        if (!response.IsSuccessStatusCode)
            _errorMessage = "Não foi possível salvar o documento.";
        await LoadAsync();
    }

    private async Task RestoreDefaultAsync(string slug)
    {
        await Http.DeleteAsync($"rulebook-documents/{slug}");
        await LoadAsync();
    }
}
```

- [ ] **Step 3: Build the client**

Run: `dotnet build src/RuinaRPG.Client`
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor src/RuinaRPG.Client/Layout/NavMenu.razor src/RuinaRPG.Client/Pages/AuditoriaLivroDeRegras.razor
git commit -m "feat: client — nav entries + Livro de Regras auditor editor page"
```

---

## Task 10: Client — Características editor page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/AuditoriaCaracteristicas.razor`

**Interfaces:**
- Consumes: `GET api/traits`, `POST api/traits`, `PUT api/traits/{id}`, `DELETE api/traits/{id}` (Task 5).

- [ ] **Step 1: Create the page**

Create `src/RuinaRPG.Client/Pages/AuditoriaCaracteristicas.razor`:

```razor
@page "/auditoria/caracteristicas"
@inject HttpClient Http
@using RuinaRPG.Contracts.Rules
@using MudBlazor

<Breadcrumbs Items="@Crumbs" />
<MudText Typo="Typo.h3">Auditoria: Características</MudText>

@if (_errorMessage is not null)
{
    <MudAlert Severity="Severity.Error" Class="mt-3">@_errorMessage</MudAlert>
}

@if (_forbidden)
{
    <MudAlert Severity="Severity.Warning" Class="mt-3">Você não é o Auditor de Regras designado — sem permissão para editar.</MudAlert>
}
else
{
    <Section Title="Adicionar característica">
        <MudTextField T="string" @bind-Value="_newForm.Nome" Label="Nome" />
        <MudSelect T="string" @bind-Value="_newForm.Polaridade" Label="Polaridade">
            <MudSelectItem Value="@("Positiva")">Positiva</MudSelectItem>
            <MudSelectItem Value="@("Negativa")">Negativa</MudSelectItem>
        </MudSelect>
        <MudNumericField T="int" @bind-Value="_newForm.Custo" Label="Custo" />
        <MudCheckBox T="bool" @bind-Value="_newForm.RequerEspecificacao" Label="Exige Especificação?" />
        <MudTextField T="string" @bind-Value="_newForm.Descricao" Label="Descrição" Lines="3" />
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-2" OnClick="AddAsync">Adicionar</MudButton>
    </Section>

    <Section Title="Positivas">
        <MudSimpleTable Dense="true" Hover="true">
            <thead>
                <tr><th>Nome</th><th>Custo</th><th>Especificação?</th><th>Descrição</th><th></th></tr>
            </thead>
            <tbody>
                @foreach (var trait in _traits.Where(t => t.Polaridade == "Positiva"))
                {
                    <tr>
                        <td><MudTextField T="string" Value="@trait.Nome" ValueChanged="@(v => UpdateAsync(trait, nome: v))" /></td>
                        <td><MudNumericField T="int" Value="@trait.Custo" ValueChanged="@(v => UpdateAsync(trait, custo: v))" /></td>
                        <td><MudCheckBox T="bool" Value="@trait.RequerEspecificacao" ValueChanged="@(v => UpdateAsync(trait, requerEspecificacao: v))" /></td>
                        <td><MudTextField T="string" Value="@trait.Descricao" ValueChanged="@(v => UpdateAsync(trait, descricao: v))" /></td>
                        <td><MudIconButton Icon="@Icons.Material.Filled.Delete" OnClick="@(() => DeleteAsync(trait.Id))" /></td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
    </Section>

    <Section Title="Negativas">
        <MudSimpleTable Dense="true" Hover="true">
            <thead>
                <tr><th>Nome</th><th>Custo</th><th>Especificação?</th><th>Descrição</th><th></th></tr>
            </thead>
            <tbody>
                @foreach (var trait in _traits.Where(t => t.Polaridade == "Negativa"))
                {
                    <tr>
                        <td><MudTextField T="string" Value="@trait.Nome" ValueChanged="@(v => UpdateAsync(trait, nome: v))" /></td>
                        <td><MudNumericField T="int" Value="@trait.Custo" ValueChanged="@(v => UpdateAsync(trait, custo: v))" /></td>
                        <td><MudCheckBox T="bool" Value="@trait.RequerEspecificacao" ValueChanged="@(v => UpdateAsync(trait, requerEspecificacao: v))" /></td>
                        <td><MudTextField T="string" Value="@trait.Descricao" ValueChanged="@(v => UpdateAsync(trait, descricao: v))" /></td>
                        <td><MudIconButton Icon="@Icons.Material.Filled.Delete" OnClick="@(() => DeleteAsync(trait.Id))" /></td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>
    </Section>
}

@code {
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Auditoria: Características"),
    };

    private List<TraitResponse> _traits = new();
    private string? _errorMessage;
    private bool _forbidden;
    private readonly NewTraitFormModel _newForm = new();

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var response = await Http.GetAsync("traits");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar as características.";
            return;
        }

        _traits = await response.Content.ReadFromJsonAsync<List<TraitResponse>>() ?? new();

        // List (GET) has no auth restriction — this page can only tell "not the auditor" apart
        // from "no characteristics yet" by trying a write and reading its status. A cheap way to
        // surface that without a spurious write: attempt the Add button's own POST later, and let
        // its own 403 flip _forbidden — so nothing extra is needed here. (Left intentionally simple:
        // AddAsync/UpdateAsync/DeleteAsync below already set _forbidden on a 403 response.)
    }

    private async Task AddAsync()
    {
        var response = await Http.PostAsJsonAsync("traits",
            new CreateTraitRequest(_newForm.Nome, _newForm.Descricao, _newForm.Custo, _newForm.Polaridade, _newForm.RequerEspecificacao));
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar a característica — verifique Nome/Custo/Polaridade (podem já existir combinados, ou o sinal do Custo não bater com a Polaridade).";
            return;
        }

        _newForm.Nome = "";
        _newForm.Descricao = "";
        _newForm.Custo = 0;
        _newForm.RequerEspecificacao = false;
        await LoadAsync();
    }

    private async Task UpdateAsync(TraitResponse current, string? nome = null, int? custo = null, bool? requerEspecificacao = null, string? descricao = null)
    {
        var response = await Http.PutAsJsonAsync($"traits/{current.Id}", new UpdateTraitRequest(
            nome ?? current.Nome,
            descricao ?? current.Descricao,
            custo ?? current.Custo,
            current.Polaridade,
            requerEspecificacao ?? current.RequerEspecificacao));
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a alteração.";
            return;
        }

        await LoadAsync();
    }

    private async Task DeleteAsync(string id)
    {
        var response = await Http.DeleteAsync($"traits/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _errorMessage = "Esta característica está em uso em pelo menos uma ficha e não pode ser excluída.";
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível excluir a característica.";
            return;
        }

        await LoadAsync();
    }

    private class NewTraitFormModel
    {
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        public int Custo { get; set; }
        public string Polaridade { get; set; } = "Positiva";
        public bool RequerEspecificacao { get; set; }
    }
}
```

- [ ] **Step 2: Build the client**

Run: `dotnet build src/RuinaRPG.Client`
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/AuditoriaCaracteristicas.razor
git commit -m "feat: client — Características auditor editor page"
```

---

## Task 11: Docs

**Files:**
- Create: `Docs/Requisitos/Requisitos - Auditoria de Regras.md`
- Modify: `Docs/Requisitos/Requisitos - Compêndio de Regras.md`
- Modify: `Docs/Requisitos/Requisitos - Livro de Regras.md`
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`

**Interfaces:** none — documentation only, no code.

- [ ] **Step 1: Write the new requirements doc**

Create `Docs/Requisitos/Requisitos - Auditoria de Regras.md`:

```markdown
> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo.

  

> **Modelo de acesso**: pertence a um único usuário por vez na prática (embora nada no modelo de dados imponha isso), designado fora do app — um administrador do servidor roda um comando `make` que marca uma conta de GM existente como Auditor de Regras. Nenhum jogador, e nenhum GM que não tenha sido designado assim, alcança as páginas deste documento.

  

# **R0001** - O Auditor de Regras é designado por e-mail, via comando `make` no console do servidor.

**Descrição**: `make grant-rules-auditor EMAIL=...` marca a conta de GM com aquele e-mail (comparação sem diferenciar maiúsculas/minúsculas) como Auditor de Regras; `make revoke-rules-auditor EMAIL=...` desfaz. Só uma conta de **GM** pode ser concedida (uma tentativa contra um e-mail de Jogador, ou um e-mail inexistente, falha com uma mensagem clara no log do servidor); revogar não tem essa restrição. A concessão é verificada diretamente no banco a cada requisição — não fica embutida no token da sessão — então tem efeito imediato, sem o Auditor precisar deslogar e logar de novo.

  

# **R0002** - O Auditor de Regras pode editar o texto do Livro de Regras.

**Descrição**: Uma página lista os 3 documentos do "[[Requisitos - Livro de Regras]]" que não são a aba de Características — Sistema Básico, Graus & Círculos, Tabela de Níveis — cada um com o Markdown atual (a sobrescrita salva, ou o texto padrão do arquivo-fonte) em um campo de texto editável, e um botão "Restaurar padrão" que apaga a sobrescrita. A página exibe um aviso fixo: editar aqui só muda o texto exibido nesta página — nenhuma fórmula ou tabela usada nos cálculos das fichas (Vitalidade, Foco, Graduação, XP por nível, etc.) é afetada, essas continuam fixas no sistema.

  

# **R0003** - O Auditor de Regras tem CRUD completo sobre o catálogo de Características.

**Descrição**: Uma página separada lista todas as Características (Positivas e Negativas, ver "[[Características]]"), cada uma com Nome, Custo, Polaridade, Descrição e a flag "Exige Especificação?" editáveis, mais um botão de exclusão por linha e um formulário para adicionar uma nova. O sinal do Custo deve ser consistente com a Polaridade (Positiva ≥ 0, Negativa ≤ 0) — uma tentativa de salvar um valor inconsistente é rejeitada. Excluir uma característica já usada em alguma Ficha de Personagem/NPC é rejeitado, com uma mensagem indicando o conflito.

Diferente do Catálogo de Itens ou do Banco de Magias, esse catálogo não é por GM — a mudança vale para o servidor inteiro, todo GM e jogador vê o resultado. Editar ou excluir uma característica originalmente vinda do documento-fonte não é desfeito por uma futura atualização/reinicialização do servidor.

  

# **R0004** - A aba "Características" do Livro de Regras reflete esse catálogo em tempo real.

**Descrição**: Ao contrário dos outros 3 documentos (R0002), a aba de Características do "[[Requisitos - Livro de Regras]]" não tem uma sobrescrita de texto própria — ela é montada diretamente a partir do catálogo de R0003. Uma edição salva em R0003 aparece nessa aba imediatamente, sem precisar de nenhuma ação adicional do Auditor.
```

- [ ] **Step 2: Cross-reference from the existing rulebook docs**

Open `Docs/Requisitos/Requisitos - Compêndio de Regras.md` and add, right after the existing `> **Status**: ...` blockquote near the top:

```markdown
> **Edição**: o conteúdo deixou de ser 100% fixo — ver "[[Requisitos - Auditoria de Regras]]" para como um GM designado Auditor de Regras pode editá-lo.
```

Open `Docs/Requisitos/Requisitos - Livro de Regras.md` and add, right after the existing `> **Natureza do conteúdo**: ...` blockquote near the top:

```markdown
> **Edição**: o conteúdo deixou de ser 100% fixo — ver "[[Requisitos - Auditoria de Regras]]" para como um GM designado Auditor de Regras pode editá-lo (a aba de Características, em particular, passa a refletir o catálogo de "[[Características]]" em vez de uma cópia própria do texto).

  

> **Modelo de acesso**: GM e jogadores têm acesso de leitura — e, ao contrário do Compêndio, também visitantes não autenticados (ver "[[Requisitos - Login e Cadastro]]"), pelo menu lateral ou por um botão em destaque na Landing Page.
```

(If that second blockquote — "Modelo de acesso... visitantes não autenticados" — isn't present verbatim in the file when you open it, leave the file's existing "Modelo de acesso" blockquote exactly as it is and only add the new "Edição" blockquote above it; do not invent or restate a blockquote that doesn't already exist there.)

- [ ] **Step 3: Update the data-model doc**

Open `Docs/Requisitos/Requisitos - Modelo de Dados.md`. Find the `**CharacterTraits** (5.d)` table (search for it) and add 4 rows after its existing ones, matching the exact style already used elsewhere in this file for the `IsRacial`/`RacialVariante` columns added by a previous task (if those two rows are present, add the new ones right after them; if not, add all after `Polaridade`):

```markdown
| IsCustomized | bool — true depois de criada/editada pelo Auditor de Regras (5.d); protege a linha de ser sobrescrita pelo re-seed a partir de Características.md |
| IsDeleted | bool — soft delete pelo Auditor de Regras; oculta a linha de toda leitura, mas ela continua existindo para o re-seed nunca recriá-la |
| UpdatedByUserId | FK → Users, nullable |
| UpdatedAt | DateTime, nullable |
```

Find the `# 10. Habilidades Raciais` section (or wherever the last such admin-table section in the file is) and add a new section after it:

```markdown
# 11. Auditoria de Regras

*(ver "[[Requisitos - Auditoria de Regras]]")*

**RulebookDocumentOverrides** — sobrescrita do texto Markdown de um dos 3 documentos do Livro de Regras que não são a aba de Características (R0002); o padrão embutido no build vale quando a linha não existe. Uma linha por Slug (não por GM — vale pro servidor inteiro).

| Coluna | Tipo |
|---|---|
| Id | PK |
| Slug | string, único — "sistema-basico" \| "graus-e-circulos" \| "tabela-de-niveis" |
| MarkdownText | text |
| UpdatedByUserId | FK → Users |
| UpdatedAt | DateTime |
```

Also find wherever `ApplicationUser`/`AspNetUsers`'s own columns are documented (search for `InvitedByGmId`, which is the closest existing custom column on that table) and add:

```markdown
| IsRulesAuditor | bool — concedido/revogado via `make grant-rules-auditor`/`revoke-rules-auditor` (ver "[[Requisitos - Auditoria de Regras]]" R0001) |
```

- [ ] **Step 4: Commit**

```bash
git add "Docs/Requisitos/Requisitos - Auditoria de Regras.md" "Docs/Requisitos/Requisitos - Compêndio de Regras.md" "Docs/Requisitos/Requisitos - Livro de Regras.md" "Docs/Requisitos/Requisitos - Modelo de Dados.md"
git commit -m "docs: Requisitos - Auditoria de Regras + cross-references"
```

---

## Task 12: Full-solution verification pass

**Files:** none created/modified — this task only runs and reads test output.

- [ ] **Step 1: Full solution build**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 2: Full unit test suite**

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: all PASS, 0 failed.

- [ ] **Step 3: Full client bUnit test suite**

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: all PASS, 0 failed.

- [ ] **Step 4: Integration tests, scoped batches**

Docker/Testcontainers in this environment can't run the entire integration suite in one process without exhausting memory — run in the same scoped batches this codebase's own history uses, checking each batch passes before moving to the next:

```bash
dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulesAuditorMigrationTests|FullyQualifiedName~RulesAuditorCliTests|FullyQualifiedName~TraitAuditFieldsMigrationTests|FullyQualifiedName~RulebookDocumentOverrideMigrationTests"
dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~TraitsControllerTests|FullyQualifiedName~TraitSeederTests|FullyQualifiedName~CompendioControllerTests"
dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RulebookControllerTests|FullyQualifiedName~RulebookDocumentsControllerTests"
dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~AuthControllerTests"
dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterAffectionsAndTraitsControllerTests|FullyQualifiedName~CharacterRacialTraitsControllerTests|FullyQualifiedName~NpcRacialTraitsControllerTests"
```

Expected: every batch reports `Failed: 0`.

- [ ] **Step 5: Report**

State plainly, with the actual numbers from the runs above: build warning/error count, unit test pass count, client test pass count, and the integration pass count per batch. Do not claim success without having run every command in this task and read its output.
