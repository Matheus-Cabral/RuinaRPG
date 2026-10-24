# Banco de Magias e Habilidades Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a GM build a reusable library of Magia/Habilidade/Racial entries directly (R0002, R0004-R0006). The two requirements that reach INTO a character sheet's Magias & Habilidades tab — R0001 ("toda Magia/Habilidade criada em qualquer ficha é adicionada automaticamente ao banco") and R0003 ("ao montar uma Magia/Habilidade numa ficha, é possível partir de uma entrada existente do banco") — are deliberately **out of scope here**: neither can be built or tested until a character sheet's own Magias & Habilidades tab exists, so they're implemented as part of the Ficha de Personagem — Magias & Habilidades, Posses & Diário plan instead, which will consume the entity this plan builds.

**Architecture:** `SpellAbilityBankEntry` (root) + `SpellAbilityBankEffect` (its purchased effects) are two straightforward tables — `GastoEmPI` and `Custo` are stored, denormalized columns recomputed from the effect list on every create/update (Modelo de Dados lists them as plain `int` columns, not as a computed view), by a pure Domain calculator shared with whatever recomputes the same numbers later on a character sheet's copy of this same shape. GM-owned, GM-only CRUD — the doc's own preamble is explicit that "jogadores não têm uma tela própria para o banco."

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md` (all 6 requirements — R0001/R0003 deferred as noted above), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §4.

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Only the **GM** may create, list, edit, or delete bank entries — `[Authorize(Roles = "GM")]` on every endpoint in this plan (the doc's preamble: "jogadores não têm uma tela própria para o banco").
- Every entry is scoped to the GM who owns the bank — never return or act on another GM's entries.
- `GastoEmPI` = sum of the entry's effects' `CustoPI`. `Custo` = `ceil(1.25 × GastoEmPI)`. Both recomputed (not trusted from the client) on every create and update.
- Deleting a bank entry never affects any character-sheet copy that started from it (R0006) — there is no FK from a future `CharacterSpellAbilities` row back to this table that cascades; `SourceBankEntryId` (Modelo de Dados §6.1) is nullable and purely for traceability, added by the later Personagem plan.
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Domain — PI cost and Foco cost calculator

**Files:**
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/SpellAbilityCostCalculator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/SpellAbilityCostCalculatorTests.cs`

**Interfaces:**
- Produces: `SpellAbilityCostCalculator.GastoEmPI(IEnumerable<int> efeitoCustosPI) : int`; `SpellAbilityCostCalculator.Custo(int gastoEmPI) : int`. Task 3 (create) and Task 5 (update) call both exact signatures. The Ficha de Personagem — Magias & Habilidades plan reuses this same calculator for the character-sheet copy of this shape, rather than reimplementing it.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/SpellAbilityCostCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class SpellAbilityCostCalculatorTests
{
    [Fact]
    public void GastoEmPI_sums_every_effects_CustoPI()
    {
        var result = SpellAbilityCostCalculator.GastoEmPI([2, 3, 4]);

        result.Should().Be(9);
    }

    [Fact]
    public void GastoEmPI_is_zero_for_an_entry_with_no_effects_yet()
    {
        var result = SpellAbilityCostCalculator.GastoEmPI([]);

        result.Should().Be(0);
    }

    [Fact]
    public void Custo_rounds_up_1_point_25_times_GastoEmPI()
    {
        // "Custo: 1,25 de arcana por PI (arredondado para cima)" — GRAUS & CÍRCULOS.md.
        SpellAbilityCostCalculator.Custo(gastoEmPI: 8).Should().Be(10);   // 8 * 1.25 = 10.0, exact
        SpellAbilityCostCalculator.Custo(gastoEmPI: 9).Should().Be(12);  // 9 * 1.25 = 11.25, rounds up to 12
        SpellAbilityCostCalculator.Custo(gastoEmPI: 0).Should().Be(0);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter SpellAbilityCostCalculatorTests`
Expected: FAIL to compile — `SpellAbilityCostCalculator` doesn't exist yet.

- [ ] **Step 3: Write `SpellAbilityCostCalculator`**

`src/RuinaRPG.Domain/SpellsAndAbilities/SpellAbilityCostCalculator.cs`:

```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

public static class SpellAbilityCostCalculator
{
    public static int GastoEmPI(IEnumerable<int> efeitoCustosPI) => efeitoCustosPI.Sum();

    public static int Custo(int gastoEmPI) => (int)Math.Ceiling(gastoEmPI * 1.25);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter SpellAbilityCostCalculatorTests`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/SpellsAndAbilities tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/SpellAbilityCostCalculatorTests.cs
git commit -m "feat: add spell/ability PI and foco cost calculator"
```

---

### Task 2: Infrastructure — `SpellAbilityBankEntry`/`SpellAbilityBankEffect` entities and migration

**Files:**
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/SpellAbilityTipo.cs`
- Create: `src/RuinaRPG.Infrastructure/SpellsAndAbilities/SpellAbilityBankEntry.cs`
- Create: `src/RuinaRPG.Infrastructure/SpellsAndAbilities/SpellAbilityBankEffect.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/SpellAbilityBankMigrationTests.cs`

**Interfaces:**
- Produces: `enum SpellAbilityTipo { Magia, Habilidade, Racial }`; `SpellAbilityBankEntry` (`Guid Id`, `Guid GmId`, `string Nome`, `SpellAbilityTipo Tipo`, `int Grau`, `int GastoEmPI`, `int Custo`, `string Descricao`, `List<SpellAbilityBankEffect> Efeitos`); `SpellAbilityBankEffect` (`Guid Id`, `Guid SpellAbilityBankEntryId`, `string EfeitoNome`, `int? Quantidade`, `int CustoPI`). `RuinaRpgDbContext.SpellAbilityBankEntries`/`SpellAbilityBankEffects`. Tasks 3-5 depend on this shape exactly.

- [ ] **Step 1: Write the enum and entities**

`src/RuinaRPG.Domain/SpellsAndAbilities/SpellAbilityTipo.cs`:

```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

public enum SpellAbilityTipo
{
    Magia,
    Habilidade,
    Racial
}
```

`src/RuinaRPG.Infrastructure/SpellsAndAbilities/SpellAbilityBankEntry.cs`:

```csharp
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.SpellsAndAbilities;

public class SpellAbilityBankEntry
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public SpellAbilityTipo Tipo { get; set; }
    public int Grau { get; set; }
    public int GastoEmPI { get; set; }
    public int Custo { get; set; }
    public required string Descricao { get; set; }
    public List<SpellAbilityBankEffect> Efeitos { get; set; } = [];
}
```

`src/RuinaRPG.Infrastructure/SpellsAndAbilities/SpellAbilityBankEffect.cs`:

```csharp
namespace RuinaRPG.Infrastructure.SpellsAndAbilities;

public class SpellAbilityBankEffect
{
    public Guid Id { get; set; }
    public Guid SpellAbilityBankEntryId { get; set; }
    public required string EfeitoNome { get; set; }
    public int? Quantidade { get; set; }
    public int CustoPI { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/SpellAbilityBankMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SpellAbilityBankMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SpellAbilityBankMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_bank_tables_with_a_cascading_effects_relationship()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddSpellAbilityBank"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@banktest.com", Email = "gm@banktest.com", Nickname = "BankTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var entry = new SpellAbilityBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = gm.Id,
            Nome = "Bola de Fogo",
            Tipo = SpellAbilityTipo.Magia,
            Grau = 3,
            GastoEmPI = 12,
            Custo = 15,
            Descricao = "Uma explosão de fogo."
        };
        entry.Efeitos.Add(new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = entry.Id, EfeitoNome = "Dano", Quantidade = 4, CustoPI = 8 });
        db.SpellAbilityBankEntries.Add(entry);
        await db.SaveChangesAsync();

        (await db.SpellAbilityBankEntries.CountAsync()).Should().Be(1);
        (await db.SpellAbilityBankEffects.CountAsync()).Should().Be(1);

        db.SpellAbilityBankEntries.Remove(entry);
        await db.SaveChangesAsync();

        // Deleting the entry cascades to its own effects (they're meaningless without their parent) —
        // this is NOT the R0006 "deleting doesn't affect a ficha's copy" guarantee, which is about a
        // DIFFERENT table (a future CharacterSpellAbilities row) never referencing this one by a
        // cascading FK at all.
        (await db.SpellAbilityBankEffects.CountAsync()).Should().Be(0);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankMigrationTests`
Expected: FAIL — no `AddSpellAbilityBank` migration exists yet.

- [ ] **Step 4: Register the DbSets and relationship in `RuinaRpgDbContext`**

Add `using RuinaRPG.Infrastructure.SpellsAndAbilities;` and:

```csharp
public DbSet<SpellAbilityBankEntry> SpellAbilityBankEntries => Set<SpellAbilityBankEntry>();
public DbSet<SpellAbilityBankEffect> SpellAbilityBankEffects => Set<SpellAbilityBankEffect>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<SpellAbilityBankEntry>(entity =>
{
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(e => e.GmId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasMany(e => e.Efeitos)
        .WithOne()
        .HasForeignKey(ef => ef.SpellAbilityBankEntryId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddSpellAbilityBank \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankMigrationTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Domain/SpellsAndAbilities/SpellAbilityTipo.cs src/RuinaRPG.Infrastructure/SpellsAndAbilities src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/SpellAbilityBankMigrationTests.cs
git commit -m "feat: add spell/ability bank entities and migration"
```

---

### Task 3: Contracts + Api — create a bank entry (R0002, R0005)

**Files:**
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEffectRequest.cs`
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/CreateSpellAbilityEntryRequest.cs`
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEffectResponse.cs`
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEntryResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs`

**Interfaces:**
- Consumes: `SpellAbilityCostCalculator` (Task 1), `SpellAbilityBankEntry`/`SpellAbilityBankEffect` (Task 2).
- Produces: `POST /api/spell-ability-bank` → `201` + `SpellAbilityEntryResponse`, `[Authorize(Roles = "GM")]`. `SpellAbilityEffectRequest(string EfeitoNome, int? Quantidade, int CustoPI)`; `CreateSpellAbilityEntryRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos)`; `SpellAbilityEffectResponse(string EfeitoNome, int? Quantidade, int CustoPI)`; `SpellAbilityEntryResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos)`. Tasks 4-5 and 6-7 (Client) depend on all four shapes exactly.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEffectRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record SpellAbilityEffectRequest(string EfeitoNome, int? Quantidade, int CustoPI);
```

`src/RuinaRPG.Contracts/SpellsAndAbilities/CreateSpellAbilityEntryRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record CreateSpellAbilityEntryRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos);
```

`src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEffectResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record SpellAbilityEffectResponse(string EfeitoNome, int? Quantidade, int CustoPI);
```

`src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEntryResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record SpellAbilityEntryResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao, List<SpellAbilityEffectResponse> Efeitos);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SpellAbilityBankControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SpellAbilityBankControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static CreateSpellAbilityEntryRequest BolaDeFogo() => new(
        "Bola de Fogo", "Magia", 3, "Uma explosão de fogo.",
        [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)]);

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/spell-ability-bank", BolaDeFogo());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_computes_GastoEmPI_and_Custo_from_the_effects_ignoring_any_client_supplied_totals()
    {
        var token = await RegisterGmAndGetTokenAsync("BankGm1", "bank1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", token, BolaDeFogo()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>();
        body!.GastoEmPI.Should().Be(14); // 8 + 6
        body.Custo.Should().Be(18); // ceil(14 * 1.25) = ceil(17.5) = 18
        body.Efeitos.Should().HaveCount(2);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankControllerTests`
Expected: FAIL — `/api/spell-ability-bank` doesn't exist yet.

- [ ] **Step 4: Write `SpellAbilityBankController` (create action only for now)**

`src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/spell-ability-bank")]
[Authorize(Roles = "GM")]
public class SpellAbilityBankController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SpellAbilityEntryResponse>> Create(CreateSpellAbilityEntryRequest request)
    {
        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo))
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        var entry = new SpellAbilityBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = CurrentGmId(),
            Nome = request.Nome,
            Tipo = tipo,
            Grau = request.Grau,
            GastoEmPI = gastoEmPI,
            Custo = SpellAbilityCostCalculator.Custo(gastoEmPI),
            Descricao = request.Descricao
        };
        entry.Efeitos = request.Efeitos
            .Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = entry.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
            .ToList();

        db.SpellAbilityBankEntries.Add(entry);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(entry));
    }

    private static SpellAbilityEntryResponse ToResponse(SpellAbilityBankEntry entry) => new(
        entry.Id.ToString(), entry.Nome, entry.Tipo.ToString(), entry.Grau, entry.GastoEmPI, entry.Custo, entry.Descricao,
        entry.Efeitos.Select(e => new SpellAbilityEffectResponse(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList());

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankControllerTests`
Expected: PASS (2/2).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/SpellsAndAbilities src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs
git commit -m "feat: add spell/ability bank entry creation"
```

---

### Task 4: Api — list bank entries with filters (R0004)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs`

**Interfaces:**
- Consumes: `SpellAbilityBankController` (Task 3).
- Produces: `GET /api/spell-ability-bank?nome=&tipo=&grau=` → `200` + `List<SpellAbilityEntryResponse>`, scoped to the authenticated GM's own entries, `[Authorize(Roles = "GM")]`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs`, inside the class:

```csharp
private async Task<HttpResponseMessage> CreateAsync(string token, CreateSpellAbilityEntryRequest request) =>
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", token, request));

[Fact]
public async Task List_returns_only_entries_created_by_the_authenticated_gm()
{
    var tokenA = await RegisterGmAndGetTokenAsync("BankGmA", "bankgma@teste.com");
    var tokenB = await RegisterGmAndGetTokenAsync("BankGmB", "bankgmb@teste.com");
    await CreateAsync(tokenA, BolaDeFogo());
    await CreateAsync(tokenB, BolaDeFogo());

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", tokenA));

    var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
    body!.Should().ContainSingle();
}

[Fact]
public async Task List_can_filter_by_Tipo_and_Grau_together()
{
    var token = await RegisterGmAndGetTokenAsync("BankGmFilter1", "bankfilter1@teste.com");
    await CreateAsync(token, BolaDeFogo()); // Magia, Grau 3
    await CreateAsync(token, new CreateSpellAbilityEntryRequest("Fúria", "Habilidade", 1, "Aumenta o dano.", []));

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?tipo=Magia&grau=3", token));

    var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
    body!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
}

[Fact]
public async Task List_can_filter_by_partial_Nome_case_insensitively()
{
    var token = await RegisterGmAndGetTokenAsync("BankGmFilter2", "bankfilter2@teste.com");
    await CreateAsync(token, BolaDeFogo());

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?nome=bola", token));

    var body = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
    body!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankControllerTests`
Expected: the 3 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 3: Add the `List` action**

In `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`, add:

```csharp
[HttpGet]
public async Task<ActionResult<List<SpellAbilityEntryResponse>>> List(
    [FromQuery] string? nome,
    [FromQuery] string? tipo,
    [FromQuery] int? grau)
{
    var gmId = CurrentGmId();
    var query = db.SpellAbilityBankEntries
        .Include(e => e.Efeitos)
        .Where(e => e.GmId == gmId);

    if (!string.IsNullOrWhiteSpace(nome))
        query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

    if (tipo is not null && Enum.TryParse<SpellAbilityTipo>(tipo, out var tipoParsed))
        query = query.Where(e => e.Tipo == tipoParsed);

    if (grau is not null)
        query = query.Where(e => e.Grau == grau);

    var entries = await query.ToListAsync();
    return entries.Select(ToResponse).ToList();
}
```

Add `using Microsoft.EntityFrameworkCore;` to the top of the file.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankControllerTests`
Expected: PASS (5/5).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs
git commit -m "feat: add spell/ability bank filtering"
```

---

### Task 5: Api — edit and delete a bank entry (R0006)

**Files:**
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/UpdateSpellAbilityEntryRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs`

**Interfaces:**
- Consumes: `SpellAbilityBankController` (Tasks 3-4).
- Produces: `PUT /api/spell-ability-bank/{id}` → `204`/`404`. `DELETE /api/spell-ability-bank/{id}` → `204`/`404`. `UpdateSpellAbilityEntryRequest` has the same shape as `CreateSpellAbilityEntryRequest` (no `Tipo` immutability rule stated in the spec for this doc, unlike Catálogo's Items — a GM may change Tipo on an existing entry).

- [ ] **Step 1: Write the request contract**

`src/RuinaRPG.Contracts/SpellsAndAbilities/UpdateSpellAbilityEntryRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

public record UpdateSpellAbilityEntryRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs`:

```csharp
[Fact]
public async Task Update_recomputes_GastoEmPI_and_Custo_from_the_new_effect_list()
{
    var token = await RegisterGmAndGetTokenAsync("BankGmUpdate1", "bankupdate1@teste.com");
    var createResponse = await CreateAsync(token, BolaDeFogo());
    var entryId = (await createResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;

    var update = new UpdateSpellAbilityEntryRequest("Bola de Fogo Maior", "Magia", 4, "Mais poderosa.",
        [new SpellAbilityEffectRequest("Dano", 6, 12)]);
    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/spell-ability-bank/{entryId}", token, update));

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", token));
    var body = await listResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
    var updated = body!.Single(e => e.Id == entryId);
    updated.Nome.Should().Be("Bola de Fogo Maior");
    updated.GastoEmPI.Should().Be(12);
    updated.Custo.Should().Be(15); // ceil(12 * 1.25) = 15
    updated.Efeitos.Should().ContainSingle();
}

[Fact]
public async Task Update_an_entry_owned_by_another_gm_returns_404()
{
    var tokenOwner = await RegisterGmAndGetTokenAsync("BankGmUpdateOwner", "bankupdateowner@teste.com");
    var tokenOther = await RegisterGmAndGetTokenAsync("BankGmUpdateOther", "bankupdateother@teste.com");
    var createResponse = await CreateAsync(tokenOwner, BolaDeFogo());
    var entryId = (await createResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;

    var update = new UpdateSpellAbilityEntryRequest("Hack", "Magia", 1, "", []);
    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/spell-ability-bank/{entryId}", tokenOther, update));

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task Delete_an_owned_entry_returns_204_and_it_no_longer_appears_on_list()
{
    var token = await RegisterGmAndGetTokenAsync("BankGmDelete1", "bankdelete1@teste.com");
    var createResponse = await CreateAsync(token, BolaDeFogo());
    var entryId = (await createResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/spell-ability-bank/{entryId}", token));

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", token));
    var body = await listResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
    body!.Should().BeEmpty();
}

[Fact]
public async Task Delete_a_nonexistent_entry_returns_404()
{
    var token = await RegisterGmAndGetTokenAsync("BankGmDelete2", "bankdelete2@teste.com");

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/spell-ability-bank/{Guid.NewGuid()}", token));

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankControllerTests`
Expected: the 4 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 4: Add `Update` and `Delete` actions**

In `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`, add:

```csharp
[HttpPut("{id}")]
public async Task<IActionResult> Update(Guid id, UpdateSpellAbilityEntryRequest request)
{
    if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo))
        return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");

    var gmId = CurrentGmId();
    var entry = await db.SpellAbilityBankEntries
        .Include(e => e.Efeitos)
        .FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
    if (entry is null)
        return NotFound();

    var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

    entry.Nome = request.Nome;
    entry.Tipo = tipo;
    entry.Grau = request.Grau;
    entry.Descricao = request.Descricao;
    entry.GastoEmPI = gastoEmPI;
    entry.Custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

    db.SpellAbilityBankEffects.RemoveRange(entry.Efeitos);
    entry.Efeitos = request.Efeitos
        .Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = entry.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
        .ToList();

    await db.SaveChangesAsync();
    return NoContent();
}

[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id)
{
    var gmId = CurrentGmId();
    var entry = await db.SpellAbilityBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
    if (entry is null)
        return NotFound();

    db.SpellAbilityBankEntries.Remove(entry);
    await db.SaveChangesAsync();
    return NoContent();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SpellAbilityBankControllerTests`
Expected: PASS (9/9).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/SpellsAndAbilities/UpdateSpellAbilityEntryRequest.cs src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs
git commit -m "feat: add spell/ability bank entry update and delete"
```

---

### Task 6: Client — bank list and filter page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/BancoDeMagias.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `SpellAbilityEntryResponse` (Task 3), the authenticated `HttpClient`.
- Produces: the `/banco-de-magias` route. Task 7 links from here for create/edit.

- [ ] **Step 1: Write the page**

`src/RuinaRPG.Client/Pages/BancoDeMagias.razor`:

```razor
@page "/banco-de-magias"
@inject HttpClient Http
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.SpellsAndAbilities

<h1>Banco de Magias e Habilidades</h1>

<button @onclick="@(() => Navigation.NavigateTo("/banco-de-magias/novo"))">Nova Entrada</button>

<div>
    <input @bind="_nomeFiltro" placeholder="Nome" />
    <select @bind="_tipoFiltro">
        <option value="">Todos os Tipos</option>
        <option value="Magia">Magia</option>
        <option value="Habilidade">Habilidade</option>
        <option value="Racial">Racial</option>
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
            <th>Grau</th>
            <th>Gasto em PI</th>
            <th>Custo</th>
            <th></th>
        </tr>
    </thead>
    <tbody>
        @foreach (var entry in _entries)
        {
            <tr>
                <td>@entry.Nome</td>
                <td>@entry.Tipo</td>
                <td>@entry.Grau</td>
                <td>@entry.GastoEmPI</td>
                <td>@entry.Custo</td>
                <td>
                    <button @onclick="@(() => Navigation.NavigateTo($"/banco-de-magias/{entry.Id}/editar"))">Editar</button>
                    <button @onclick="@(() => DeleteAsync(entry.Id))">Excluir</button>
                </td>
            </tr>
        }
    </tbody>
</table>

@code {
    private string _nomeFiltro = "";
    private string _tipoFiltro = "";
    private List<SpellAbilityEntryResponse> _entries = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var query = $"?nome={Uri.EscapeDataString(_nomeFiltro)}&tipo={_tipoFiltro}";
        var response = await Http.GetAsync($"spell-ability-bank{query}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar o banco de magias e habilidades.";
            return;
        }

        _entries = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>() ?? new();
        _errorMessage = null;
    }

    private async Task DeleteAsync(string id)
    {
        var response = await Http.DeleteAsync($"spell-ability-bank/{id}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível excluir a entrada.";
            return;
        }

        await LoadAsync();
    }
}
```

- [ ] **Step 2: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/banco-de-magias` following the file's established pattern — label it "Banco de Magias e Habilidades".

- [ ] **Step 3: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/BancoDeMagias.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add the banco de magias list and filter page"
```

---

### Task 7: Client — bank entry create/edit form

**Files:**
- Create: `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`

**Interfaces:**
- Consumes: `CreateSpellAbilityEntryRequest`/`UpdateSpellAbilityEntryRequest`/`SpellAbilityEntryResponse`/`SpellAbilityEffectRequest` (Tasks 3, 5), the authenticated `HttpClient`.
- Produces: the `/banco-de-magias/novo` and `/banco-de-magias/{id}/editar` routes. No later task depends on this file.

- [ ] **Step 1: Write the page**

`src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`:

```razor
@page "/banco-de-magias/novo"
@page "/banco-de-magias/{EntryId}/editar"
@inject HttpClient Http
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.SpellsAndAbilities

<h1>@(EntryId is null ? "Nova Entrada" : "Editar Entrada")</h1>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

<EditForm Model="_form" OnValidSubmit="SubmitAsync">
    <label>Nome <InputText @bind-Value="_form.Nome" /></label>
    <label>
        Tipo
        <select @bind="_form.Tipo">
            <option value="Magia">Magia</option>
            <option value="Habilidade">Habilidade</option>
            <option value="Racial">Racial</option>
        </select>
    </label>
    <label>Grau <InputNumber @bind-Value="_form.Grau" /></label>
    <label>Descrição <InputTextArea @bind-Value="_form.Descricao" /></label>

    <h2>Efeitos</h2>
    <button type="button" @onclick="AddEffect">Adicionar Efeito</button>
    @for (var i = 0; i < _form.Efeitos.Count; i++)
    {
        var index = i; // capture for the closures below
        <div>
            <label>Nome do Efeito <InputText @bind-Value="_form.Efeitos[index].EfeitoNome" /></label>
            <label>Quantidade <InputNumber @bind-Value="_form.Efeitos[index].Quantidade" /></label>
            <label>Custo em PI <InputNumber @bind-Value="_form.Efeitos[index].CustoPI" /></label>
            <button type="button" @onclick="@(() => _form.Efeitos.RemoveAt(index))">Remover</button>
        </div>
    }

    <p>Gasto em PI (calculado ao salvar): @_form.Efeitos.Sum(e => e.CustoPI)</p>

    <button type="submit">Salvar</button>
</EditForm>

@code {
    [Parameter] public string? EntryId { get; set; }

    private readonly EntryFormModel _form = new();
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        if (EntryId is null)
            return;

        var response = await Http.GetAsync("spell-ability-bank");
        var entries = await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>() ?? new();
        var existing = entries.SingleOrDefault(e => e.Id == EntryId);
        if (existing is null)
        {
            _errorMessage = "Entrada não encontrada.";
            return;
        }

        _form.Nome = existing.Nome;
        _form.Tipo = existing.Tipo;
        _form.Grau = existing.Grau;
        _form.Descricao = existing.Descricao;
        _form.Efeitos = existing.Efeitos
            .Select(e => new EffectFormModel { EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
            .ToList();
    }

    private void AddEffect() => _form.Efeitos.Add(new EffectFormModel());

    private async Task SubmitAsync()
    {
        var efeitos = _form.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();

        HttpResponseMessage response = EntryId is null
            ? await Http.PostAsJsonAsync("spell-ability-bank", new CreateSpellAbilityEntryRequest(_form.Nome, _form.Tipo, _form.Grau, _form.Descricao, efeitos))
            : await Http.PutAsJsonAsync($"spell-ability-bank/{EntryId}", new UpdateSpellAbilityEntryRequest(_form.Nome, _form.Tipo, _form.Grau, _form.Descricao, efeitos));

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a entrada.";
            return;
        }

        Navigation.NavigateTo("/banco-de-magias");
    }

    private class EntryFormModel
    {
        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "Magia";
        public int Grau { get; set; }
        public string Descricao { get; set; } = "";
        public List<EffectFormModel> Efeitos { get; set; } = new();
    }

    private class EffectFormModel
    {
        public string EfeitoNome { get; set; } = "";
        public int? Quantidade { get; set; }
        public int CustoPI { get; set; }
    }
}
```

- [ ] **Step 2: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor
git commit -m "feat: add the banco de magias entry create/edit form"
```

---

### Task 8: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files — this task only exercises what Tasks 1-7 built.

**Interfaces:**
- Consumes: the full stack, including everything this plan added.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers come up; migrations (including `AddSpellAbilityBank`) apply automatically in Development.

- [ ] **Step 2: Register a GM and get a token, through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/gm \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeBancoGm","email":"smokebancogm@teste.com","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' \
  | tee /tmp/gm-register.json
TOKEN=$(jq -r .accessToken /tmp/gm-register.json)
```

- [ ] **Step 3: Create a bank entry through nginx**

```bash
curl -sf -X POST http://localhost/api/spell-ability-bank \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"nome":"Bola de Fogo","tipo":"Magia","grau":3,"descricao":"Uma explosão de fogo.","efeitos":[{"efeitoNome":"Dano","quantidade":4,"custoPI":8}]}' \
  | tee /tmp/entry-create.json
```

Expected: HTTP 201 with `"gastoEmPI":8` and `"custo":10`.

- [ ] **Step 4: List entries through nginx and confirm the created entry is present**

```bash
curl -sf "http://localhost/api/spell-ability-bank" -H "Authorization: Bearer $TOKEN"
```

Expected: HTTP 200, JSON array containing the "Bola de Fogo" entry.

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the banco de magias flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- R0001 (auto-copy into the bank when a Magia/Habilidade is created on any sheet) and R0003 (start a sheet entry from an existing bank entry) — both depend on a character sheet's Magias & Habilidades tab existing; implemented by the Ficha de Personagem — Magias & Habilidades, Posses & Diário plan, which consumes `SpellAbilityBankEntry`/`SpellAbilityBankEffect` and `SpellAbilityCostCalculator` from this plan.
- Any validation of effect legality against Graus & Círculos rules (per-grade caps, prerequisite effects like "Duração must be bought before Aumentar Armadura") — the Catálogo doc frames the Bank as free-form reusable templates; the Ficha de Personagem doc explicitly places that enforcement ("a interface deve impedir a compra de um Efeito cujo pré-requisito não foi comprado") on the sheet's own 4.b tab, not here.
