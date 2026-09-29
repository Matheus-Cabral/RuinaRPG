# Evoluções de Arca Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each entry of the GM's Tabela de Arcas can hold level-gated evoluções; Humano Personagem/NPC sheets show the unlocked ones in a popup.

**Architecture:** New EF entity `ArcaEvolucao` (child of `ArcaEntry`, cascade). CRUD endpoints live in the existing GM-only `RacialAbilitiesController`; the two sheet `racial-ability` endpoints add the unlocked list, filtered by a pure Domain helper. Client gets two small reusable components (an editor for `/habilidades-raciais`, a button+dialog for the sheets), each bUnit-tested.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core + Npgsql, Blazor WASM + MudBlazor, xUnit + FluentAssertions, Testcontainers (integration), bUnit (client).

**Spec:** `docs/superpowers/specs/2026-09-29-evolucoes-de-arca-design.md`

## Global Constraints

- TDD is mandatory (Técnico R0011): failing test first, then minimum code.
- `dotnet build` must finish with 0 warnings, 0 errors.
- UI text in Brazilian Portuguese. **No emojis anywhere** — use MudBlazor icons (`Icons.Material.Filled.*`).
- Every new UI surface has an `InfoPopup` (ⓘ) explaining it (`Shared/InfoPopup.razor`).
- Evolution `Nivel` valid range: **1 to 50** (constant `ArcaEvolucaoRules.NivelMaximo = 50`; part 3 of 1.4.1 will replace it with the Tabela de Níveis' last level).
- Arca roll valid range: 1 to 18 (unchanged).
- Integration tests need Docker running; run them with `--filter` in batches (the full suite has a known ~2-3% timeout flake — rerun a failed test in isolation before treating it as real).
- Each integration test registers users with **unique** nicknames/emails (shared Postgres container per class).
- Commit messages in Portuguese, conventional prefix (`feat:`, `test:`, `docs:`), ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- Evolution belonging to another GM, or to a different roll of the same GM, addressed by id → must be 404, never edited/deleted.
- Adding the first evolution to a roll the GM never filled → must create the `ArcaEntry` (empty Nome/Descrição) and succeed; `GET api/arcas` then shows that row with empty strings, and the sheet shows "Arca não cadastrada."-style fallback only if Nome is empty (see Task 4 step on empty Nome).
- Sheet changes Linhagem away from Humano (ArcaRolada kept in DB) → `ArcaEvolucoes` must be empty.
- Two evolutions at the same level → both returned, stable order (Nivel, then creation order via `CriadaEm`).
- Blank/whitespace-only Descrição → 400, not stored.

---

## File Structure

| File | Responsibility |
|---|---|
| Create `src/RuinaRPG.Domain/CharacterSheets/ArcaEvolucaoRules.cs` | `NivelMaximo`, `Desbloqueadas(...)` pure filter/sort |
| Create `src/RuinaRPG.Infrastructure/CharacterSheets/ArcaEvolucao.cs` | EF entity |
| Modify `src/RuinaRPG.Infrastructure/CharacterSheets/ArcaEntry.cs` | `Evolucoes` navigation |
| Modify `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` | DbSet + config |
| Create migration `AddArcaEvolucoes` | schema |
| Create `src/RuinaRPG.Contracts/CharacterSheets/ArcaEvolucaoResponse.cs`, `ArcaEvolucaoRequest.cs` | DTOs |
| Modify `src/RuinaRPG.Contracts/CharacterSheets/ArcaEntryResponse.cs`, `RacialAbilityResponse.cs` | add lists |
| Modify `src/RuinaRPG.Api/Controllers/RacialAbilitiesController.cs` | CRUD endpoints, list includes evoluções |
| Modify `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, `NpcSheetsController.cs` | `racial-ability` adds unlocked list |
| Create `src/RuinaRPG.Client/Shared/ArcaEvolucoesEditor.razor` | per-roll evolution list editor |
| Create `src/RuinaRPG.Client/Shared/ArcaEvolucoesButton.razor` | "Evoluções da Arca (N)" button + dialog |
| Modify `src/RuinaRPG.Client/Pages/HabilidadesRaciais.razor` | own Section for Tabela de Arcas + InfoPopup + editor per row |
| Modify `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, `FichaDeNpc.razor` | button in Habilidade Racial section |
| Docs: `Requisitos - Habilidades Raciais.md`, `Requisitos - Ficha de Personagem.md`, `Requisitos - Modelo de Dados.md` | R0006, 4.a, table |

---

### Task 1: Domain rule — unlocked evolutions

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/ArcaEvolucaoRules.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/ArcaEvolucaoRulesTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  namespace RuinaRPG.Domain.CharacterSheets;
  public static class ArcaEvolucaoRules
  {
      public const int NivelMaximo = 50;
      public static bool NivelValido(int nivel);                 // 1..NivelMaximo
      public static IReadOnlyList<T> Desbloqueadas<T>(IEnumerable<T> evolucoes, Func<T, int> nivel, Func<T, DateTimeOffset> criadaEm, int nivelDaFicha);
  }
  ```

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class ArcaEvolucaoRulesTests
{
    private sealed record Ev(string Id, int Nivel, DateTimeOffset CriadaEm);

    private static readonly DateTimeOffset T0 = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Desbloqueadas_keeps_only_levels_up_to_the_sheet_level_ordered_by_level_then_creation()
    {
        var evolucoes = new[]
        {
            new Ev("c", 10, T0.AddMinutes(1)),
            new Ev("a", 5, T0.AddMinutes(2)),
            new Ev("b", 5, T0.AddMinutes(1)),
            new Ev("d", 11, T0),
        };

        var result = ArcaEvolucaoRules.Desbloqueadas(evolucoes, e => e.Nivel, e => e.CriadaEm, nivelDaFicha: 10);

        result.Select(e => e.Id).Should().Equal("b", "a", "c");
    }

    [Fact]
    public void Desbloqueadas_of_an_empty_list_is_empty()
    {
        ArcaEvolucaoRules.Desbloqueadas(Array.Empty<Ev>(), e => e.Nivel, e => e.CriadaEm, 50).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void NivelValido_accepts_1_to_50(int nivel, bool esperado)
    {
        ArcaEvolucaoRules.NivelValido(nivel).Should().Be(esperado);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~ArcaEvolucaoRulesTests`
Expected: build FAIL — `ArcaEvolucaoRules` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Regras das evoluções de Arca (Requisitos - Habilidades Raciais R0006): uma evolução vale para a
/// ficha a partir do seu Nível. Genérico sobre o tipo da evolução para servir tanto a entidade EF
/// quanto DTOs sem o Domain depender de nenhum dos dois.
/// </summary>
public static class ArcaEvolucaoRules
{
    /// <summary>Último nível da Tabela de Níveis hoje.</summary>
    public const int NivelMaximo = 50;

    public static bool NivelValido(int nivel) => nivel is >= 1 and <= NivelMaximo;

    public static IReadOnlyList<T> Desbloqueadas<T>(IEnumerable<T> evolucoes, Func<T, int> nivel, Func<T, DateTimeOffset> criadaEm, int nivelDaFicha) =>
        evolucoes.Where(e => nivel(e) <= nivelDaFicha)
            .OrderBy(nivel)
            .ThenBy(criadaEm)
            .ToList();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~ArcaEvolucaoRulesTests`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/ArcaEvolucaoRules.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/ArcaEvolucaoRulesTests.cs
git commit -m "feat(domain): regras de evoluções de Arca desbloqueadas por nível"
```

---

### Task 2: Entity, migration and CRUD endpoints

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/ArcaEvolucao.cs`
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/ArcaEntry.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` (DbSets near line 58; `ArcaEntry` config near line 187)
- Create: migration `src/RuinaRPG.Infrastructure/Persistence/Migrations/<timestamp>_AddArcaEvolucoes.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/ArcaEvolucaoResponse.cs`, `src/RuinaRPG.Contracts/CharacterSheets/ArcaEvolucaoRequest.cs`
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/ArcaEntryResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/RacialAbilitiesController.cs` (`ListArcas` ~line 118; add new actions after `UpdateArca`)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/RacialAbilitiesControllerTests.cs` (append)

**Interfaces:**
- Consumes: `ArcaEvolucaoRules.NivelValido`, `ArcaEvolucaoRules.Desbloqueadas` (Task 1).
- Produces:
  ```csharp
  // Contracts
  public record ArcaEvolucaoResponse(Guid Id, int Nivel, string Descricao);
  public record ArcaEvolucaoRequest(int Nivel, string Descricao);
  public record ArcaEntryResponse(int Roll, string? Nome, string? Descricao, List<ArcaEvolucaoResponse> Evolucoes);
  // Infrastructure
  public class ArcaEvolucao { Guid Id; Guid ArcaEntryId; int Nivel; string Descricao; DateTimeOffset CriadaEm; }
  // ArcaEntry gains: public List<ArcaEvolucao> Evolucoes { get; set; } = new();
  // DbContext gains: DbSet<ArcaEvolucao> ArcaEvolucoes
  // Routes: POST api/arcas/{roll}/evolucoes (201 + ArcaEvolucaoResponse), PUT api/arcas/{roll}/evolucoes/{id} (200 + ArcaEvolucaoResponse), DELETE api/arcas/{roll}/evolucoes/{id} (204)
  ```

- [ ] **Step 1: Write the failing tests** (append to `RacialAbilitiesControllerTests`; the class already has `AuthedRequest`, `RegisterGmAndGetTokenAsync`, `RegisterJogadorTokenAsync`)

```csharp
    [Fact]
    public async Task AddEvolucao_on_an_unfilled_roll_creates_the_Arca_row_and_List_returns_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm1", "arcaevo1@teste.com");

        var create = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/4/evolucoes", gmToken,
            new ArcaEvolucaoRequest(5, "A chama queima mais forte.")));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();
        created!.Nivel.Should().Be(5);

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        var row = list!.Single(a => a.Roll == 4);
        row.Nome.Should().Be("");
        row.Evolucoes.Should().ContainSingle().Which.Descricao.Should().Be("A chama queima mais forte.");
        list!.Single(a => a.Roll == 5).Evolucoes.Should().BeEmpty();
    }

    [Fact]
    public async Task List_returns_evolucoes_ordered_by_level_then_creation()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm2", "arcaevo2@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/2/evolucoes", gmToken, new ArcaEvolucaoRequest(10, "dez")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/2/evolucoes", gmToken, new ArcaEvolucaoRequest(3, "tres-a")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/2/evolucoes", gmToken, new ArcaEvolucaoRequest(3, "tres-b")));

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();

        list!.Single(a => a.Roll == 2).Evolucoes.Select(e => e.Descricao).Should().Equal("tres-a", "tres-b", "dez");
    }

    [Fact]
    public async Task UpdateEvolucao_changes_level_and_text()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm3", "arcaevo3@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/1/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "antes"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/1/evolucoes/{created!.Id}", gmToken, new ArcaEvolucaoRequest(7, "depois")));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        var ev = list!.Single(a => a.Roll == 1).Evolucoes.Single();
        ev.Nivel.Should().Be(7);
        ev.Descricao.Should().Be("depois");
    }

    [Fact]
    public async Task DeleteEvolucao_removes_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm4", "arcaevo4@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/9/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/arcas/9/evolucoes/{created!.Id}", gmToken));

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/arcas", gmToken)))
            .Content.ReadFromJsonAsync<List<ArcaEntryResponse>>();
        list!.Single(a => a.Roll == 9).Evolucoes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1, "texto")]
    [InlineData(19, 1, "texto")]
    [InlineData(1, 0, "texto")]
    [InlineData(1, 51, "texto")]
    [InlineData(1, 5, "   ")]
    public async Task AddEvolucao_with_invalid_input_returns_400(int roll, int nivel, string descricao)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"ArcaEvoGmV{roll}{nivel}{descricao.Length}", $"arcaevov{roll}{nivel}{descricao.Length}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/arcas/{roll}/evolucoes", gmToken, new ArcaEvolucaoRequest(nivel, descricao)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateEvolucao_with_invalid_level_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm5", "arcaevo5@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/1/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/1/evolucoes/{created!.Id}", gmToken, new ArcaEvolucaoRequest(51, "x")));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_or_delete_of_another_gms_evolucao_returns_404()
    {
        var ownerToken = await RegisterGmAndGetTokenAsync("ArcaEvoOwner", "arcaevoowner@teste.com");
        var otherToken = await RegisterGmAndGetTokenAsync("ArcaEvoOther", "arcaevoother@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", ownerToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/3/evolucoes/{created!.Id}", otherToken, new ArcaEvolucaoRequest(2, "y"))))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/arcas/3/evolucoes/{created.Id}", otherToken)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_of_an_evolucao_through_the_wrong_roll_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm6", "arcaevo6@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "x"))))
            .Content.ReadFromJsonAsync<ArcaEvolucaoResponse>();

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/arcas/4/evolucoes/{created!.Id}", gmToken, new ArcaEvolucaoRequest(2, "y")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddEvolucao_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ArcaEvoGm7", "arcaevo7@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "ArcaEvoJog7", "arcaevojog7@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", jogadorToken, new ArcaEvolucaoRequest(2, "x")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
```

Also update the existing `ArcaEntryResponse` usages in this test file if any construct it positionally (they only deserialize it — no change expected).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RacialAbilitiesControllerTests`
Expected: build FAIL — `ArcaEvolucaoRequest`/`ArcaEvolucaoResponse` don't exist.

- [ ] **Step 3: Contracts**

`src/RuinaRPG.Contracts/CharacterSheets/ArcaEvolucaoResponse.cs`:
```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record ArcaEvolucaoResponse(Guid Id, int Nivel, string Descricao);
```

`src/RuinaRPG.Contracts/CharacterSheets/ArcaEvolucaoRequest.cs`:
```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record ArcaEvolucaoRequest(int Nivel, string Descricao);
```

`ArcaEntryResponse.cs` becomes:
```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record ArcaEntryResponse(int Roll, string? Nome, string? Descricao, List<ArcaEvolucaoResponse> Evolucoes);
```

Fix every other positional construction of `ArcaEntryResponse` the compiler flags (search `new ArcaEntryResponse(` in `src/` and `tests/`, including `tests/RuinaRPG.Tests.Client`) by passing `new()` for `Evolucoes`.

- [ ] **Step 4: Entity + DbContext + migration**

`src/RuinaRPG.Infrastructure/CharacterSheets/ArcaEvolucao.cs`:
```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>Evolução de uma Arca, liberada a partir de Nivel (Requisitos - Habilidades Raciais R0006).</summary>
public class ArcaEvolucao
{
    public Guid Id { get; set; }
    public Guid ArcaEntryId { get; set; }
    public int Nivel { get; set; }
    public required string Descricao { get; set; }
    /// <summary>Desempate estável entre evoluções do mesmo nível.</summary>
    public DateTimeOffset CriadaEm { get; set; }
}
```

`ArcaEntry.cs` gains:
```csharp
    public List<ArcaEvolucao> Evolucoes { get; set; } = new();
```

`RuinaRpgDbContext.cs`: add `public DbSet<ArcaEvolucao> ArcaEvolucoes => Set<ArcaEvolucao>();` next to `ArcaEntries`, and inside `builder.Entity<ArcaEntry>(entity => { ... })` add:
```csharp
            entity.HasMany(a => a.Evolucoes)
                .WithOne()
                .HasForeignKey(e => e.ArcaEntryId)
                .OnDelete(DeleteBehavior.Cascade);
```
plus, after that block:
```csharp
        builder.Entity<ArcaEvolucao>(entity =>
        {
            entity.HasIndex(e => new { e.ArcaEntryId, e.Nivel });
        });
```

Run: `dotnet ef migrations add AddArcaEvolucoes --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`
Inspect the generated migration: it must only create table `ArcaEvolucoes` with FK to `ArcaEntries` (cascade) and the index. Nothing else.

- [ ] **Step 5: Controller**

In `RacialAbilitiesController.ListArcas`, load evolutions and map:
```csharp
    [HttpGet("api/arcas")]
    public async Task<ActionResult<List<ArcaEntryResponse>>> ListArcas()
    {
        var gmId = CurrentUserId();
        var entries = await db.ArcaEntries.Include(a => a.Evolucoes).Where(a => a.GmId == gmId).ToListAsync();

        var responses = new List<ArcaEntryResponse>();
        for (var roll = 1; roll <= 18; roll++)
        {
            var entry = entries.FirstOrDefault(a => a.Roll == roll);
            var evolucoes = entry is null
                ? new List<ArcaEvolucaoResponse>()
                : ArcaEvolucaoRules.Desbloqueadas(entry.Evolucoes, e => e.Nivel, e => e.CriadaEm, int.MaxValue).Select(ToResponse).ToList();
            responses.Add(new ArcaEntryResponse(roll, entry?.Nome, entry?.Descricao, evolucoes));
        }
        return responses;
    }
```

Add after `UpdateArca`:
```csharp
    [HttpPost("api/arcas/{roll:int}/evolucoes")]
    public async Task<ActionResult<ArcaEvolucaoResponse>> AddEvolucao(int roll, ArcaEvolucaoRequest request)
    {
        if (ValidateEvolucao(roll, request) is { } invalid)
            return invalid;

        var gmId = CurrentUserId();
        var arca = await db.ArcaEntries.FirstOrDefaultAsync(a => a.GmId == gmId && a.Roll == roll);
        if (arca is null)
        {
            arca = new ArcaEntry { Id = Guid.NewGuid(), GmId = gmId, Roll = roll, Nome = "", Descricao = "" };
            db.ArcaEntries.Add(arca);
        }

        var evolucao = new ArcaEvolucao { Id = Guid.NewGuid(), ArcaEntryId = arca.Id, Nivel = request.Nivel, Descricao = request.Descricao.Trim(), CriadaEm = DateTimeOffset.UtcNow };
        db.ArcaEvolucoes.Add(evolucao);
        await db.SaveChangesAsync();
        return Created($"/api/arcas/{roll}/evolucoes/{evolucao.Id}", ToResponse(evolucao));
    }

    [HttpPut("api/arcas/{roll:int}/evolucoes/{id:guid}")]
    public async Task<ActionResult<ArcaEvolucaoResponse>> UpdateEvolucao(int roll, Guid id, ArcaEvolucaoRequest request)
    {
        if (ValidateEvolucao(roll, request) is { } invalid)
            return invalid;

        var evolucao = await FindOwnEvolucaoAsync(roll, id);
        if (evolucao is null)
            return NotFound();

        evolucao.Nivel = request.Nivel;
        evolucao.Descricao = request.Descricao.Trim();
        await db.SaveChangesAsync();
        return ToResponse(evolucao);
    }

    [HttpDelete("api/arcas/{roll:int}/evolucoes/{id:guid}")]
    public async Task<IActionResult> DeleteEvolucao(int roll, Guid id)
    {
        var evolucao = await FindOwnEvolucaoAsync(roll, id);
        if (evolucao is null)
            return NotFound();

        db.ArcaEvolucoes.Remove(evolucao);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private ActionResult? ValidateEvolucao(int roll, ArcaEvolucaoRequest request)
    {
        if (roll is < 1 or > 18)
            return BadRequest("Roll deve estar entre 1 e 18.");
        if (!ArcaEvolucaoRules.NivelValido(request.Nivel))
            return BadRequest($"O nível da evolução deve estar entre 1 e {ArcaEvolucaoRules.NivelMaximo}.");
        if (string.IsNullOrWhiteSpace(request.Descricao))
            return BadRequest("Descreva a evolução.");
        return null;
    }

    // Scoped by GM *and* roll: an id from another GM, or from another roll of the same GM, is a 404.
    private Task<ArcaEvolucao?> FindOwnEvolucaoAsync(int roll, Guid id)
    {
        var gmId = CurrentUserId();
        return db.ArcaEvolucoes
            .Where(e => e.Id == id && db.ArcaEntries.Any(a => a.Id == e.ArcaEntryId && a.GmId == gmId && a.Roll == roll))
            .FirstOrDefaultAsync();
    }

    private static ArcaEvolucaoResponse ToResponse(ArcaEvolucao e) => new(e.Id, e.Nivel, e.Descricao);
```

`ArcaEvolucaoRules` lives in `RuinaRPG.Domain.CharacterSheets`, already imported by this controller.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RacialAbilitiesControllerTests`
Expected: PASS (all old + new).
Run: `dotnet build` → 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure src/RuinaRPG.Contracts src/RuinaRPG.Api/Controllers/RacialAbilitiesController.cs tests/
git commit -m "feat(api): evoluções por nível na tabela de Arcas"
```

---

### Task 3: Sheets return unlocked evolutions

**Files:**
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/RacialAbilityResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs:138-167` (`RacialAbility`)
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs:311-339` (`RacialAbility`)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs` (append next to the existing `RacialAbility_resolves_the_Arca_...` tests)

**Interfaces:**
- Consumes: `ArcaEvolucaoRules.Desbloqueadas` (Task 1); `ArcaEntry.Evolucoes`, `ArcaEvolucaoResponse`, `POST api/arcas/{roll}/evolucoes` (Task 2).
- Produces: `public record RacialAbilityResponse(string? Nome, string? Descricao, int? ArcaRolada, string? ArcaNome, string? ArcaDescricao, List<ArcaEvolucaoResponse> ArcaEvolucoes);`

- [ ] **Step 1: Write the failing tests**

In `NpcSheetsControllerTests` (NPC `Nivel` is directly editable through `ValidUpdate() with { Nivel = ... }`):
```csharp
    [Fact]
    public async Task RacialAbility_lists_only_Arca_evolucoes_unlocked_by_the_npc_level()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaEvo1", "npcarcaevo1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/3", gmToken, new UpdateArcaEntryRequest("Sombra Fugaz", "Some por 1 turno.")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(5, "cinco")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "dois")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(6, "seis")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 3, Nivel = 5 }));

        var body = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaEvolucoes.Select(e => e.Descricao).Should().Equal("dois", "cinco");
    }

    [Fact]
    public async Task RacialAbility_has_no_Arca_evolucoes_when_the_npc_is_not_Humano()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaEvo2", "npcarcaevo2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/3/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "um")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 3, Nivel = 5 }));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Nephrytes", Variante = "Yavos", ArcaRolada = 3, Nivel = 5 }));

        var body = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaEvolucoes.Should().BeEmpty();
    }

    [Fact]
    public async Task RacialAbility_treats_an_Arca_created_only_by_an_evolucao_as_not_registered_but_lists_the_evolucao()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmArcaEvo3", "npcarcaevo3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/8/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "um")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Laonir", ArcaRolada = 8, Nivel = 1 }));

        var body = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/racial-ability", gmToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaNome.Should().BeNull();
        body.ArcaDescricao.Should().BeNull();
        body.ArcaEvolucoes.Select(e => e.Descricao).Should().Equal("um");
    }
```

In `CharacterSheetsControllerTests` (Personagem `Nivel` derives from `ExperienciaAtual`; with 0 XP the level is 1 — use levels 1 and 2 so no XP math is needed):
```csharp
    [Fact]
    public async Task RacialAbility_lists_only_Arca_evolucoes_unlocked_by_the_character_level()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmArcaEvo1", "sheetarcaevo1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerArcaEvo1", "sheetplayerarcaevo1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Arca Evo 1");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/arcas/7", gmToken, new UpdateArcaEntryRequest("A Chama Eterna", "Resistência ao fogo.")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/7/evolucoes", gmToken, new ArcaEvolucaoRequest(1, "nível um")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/arcas/7/evolucoes", gmToken, new ArcaEvolucaoRequest(2, "nível dois")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Linhagem = "Humano", Variante = "Sinir", ArcaRolada = 7, ExperienciaAtual = 0 }));

        var body = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/racial-ability", playerToken)))
            .Content.ReadFromJsonAsync<RacialAbilityResponse>();

        body!.ArcaEvolucoes.Select(e => e.Descricao).Should().Equal("nível um");
    }
```
If `UpdateArcaEntryRequest`/`ArcaEvolucaoRequest` aren't imported in these files, use their fully-qualified `RuinaRPG.Contracts.CharacterSheets.` names as the neighbouring tests already do.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RacialAbility"`
Expected: build FAIL — `RacialAbilityResponse` has no `ArcaEvolucoes`.

- [ ] **Step 3: Implement**

`RacialAbilityResponse.cs`:
```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record RacialAbilityResponse(string? Nome, string? Descricao, int? ArcaRolada, string? ArcaNome, string? ArcaDescricao, List<ArcaEvolucaoResponse> ArcaEvolucoes);
```

In **both** controllers' `RacialAbility`:
- the early return becomes `return new RacialAbilityResponse(null, null, null, null, null, new());`
- the Arca block becomes (use `campaignGmId` in CharacterSheetsController, `sheet.GmId` in NpcSheetsController):
```csharp
        string? arcaNome = null;
        string? arcaDescricao = null;
        var arcaEvolucoes = new List<ArcaEvolucaoResponse>();
        if (sheet.Linhagem == Linhagem.Humano && sheet.ArcaRolada is not null)
        {
            var arca = await db.ArcaEntries.Include(a => a.Evolucoes)
                .FirstOrDefaultAsync(a => a.GmId == campaignGmId && a.Roll == sheet.ArcaRolada.Value);
            // An Arca row created only to hold evoluções has an empty Nome — the sheet treats it as not registered.
            arcaNome = string.IsNullOrEmpty(arca?.Nome) ? null : arca.Nome;
            arcaDescricao = string.IsNullOrEmpty(arca?.Descricao) ? null : arca.Descricao;
            if (arca is not null)
                arcaEvolucoes = ArcaEvolucaoRules.Desbloqueadas(arca.Evolucoes, e => e.Nivel, e => e.CriadaEm, sheet.Nivel)
                    .Select(e => new ArcaEvolucaoResponse(e.Id, e.Nivel, e.Descricao)).ToList();
        }

        return new RacialAbilityResponse(nome, descricao, sheet.ArcaRolada, arcaNome, arcaDescricao, arcaEvolucoes);
```
Fix any other `new RacialAbilityResponse(` the compiler flags (including in `tests/RuinaRPG.Tests.Client`) by appending `new()`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RacialAbility"`
Expected: PASS, including the pre-existing `RacialAbility_reports_a_null_Arca_when_the_GM_hasnt_registered_that_roll`.
Run: `dotnet build` → 0/0.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Contracts src/RuinaRPG.Api tests/
git commit -m "feat(api): fichas trazem as evoluções de Arca desbloqueadas pelo nível"
```

---

### Task 4: Client — editor on `/habilidades-raciais`

**Files:**
- Create: `src/RuinaRPG.Client/Shared/ArcaEvolucoesEditor.razor`
- Modify: `src/RuinaRPG.Client/Pages/HabilidadesRaciais.razor` (lines ~55-76: Tabela de Arcas; `@code` `UpdateArcaAsync` ~157)
- Test: `tests/RuinaRPG.Tests.Client/Shared/ArcaEvolucoesEditorTests.cs`

**Interfaces:**
- Consumes: `ArcaEvolucaoResponse`, `ArcaEvolucaoRequest`, routes from Task 2.
- Produces: `<ArcaEvolucoesEditor Roll="int" Evolucoes="List<ArcaEvolucaoResponse>" OnChanged="EventCallback" />` — the component calls the API itself (`@inject HttpClient Http`) and then invokes `OnChanged` so the page reloads `arcas`.

- [ ] **Step 1: Write the failing bUnit tests**

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.CharacterSheets;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ArcaEvolucoesEditorTests : MudBunitContext
{
    private readonly List<(HttpMethod Method, string Path, ArcaEvolucaoRequest? Body)> _log = new();

    private IRenderedComponent<ArcaEvolucoesEditor> RenderEditor(List<ArcaEvolucaoResponse> evolucoes, Action? onChanged = null)
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            var body = request.Content is null ? null : request.Content.ReadFromJsonAsync<ArcaEvolucaoRequest>().GetAwaiter().GetResult();
            _log.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            if (request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new ArcaEvolucaoResponse(Guid.NewGuid(), body!.Nivel, body.Descricao)) };
            if (request.Method == HttpMethod.Put)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ArcaEvolucaoResponse(Guid.NewGuid(), body!.Nivel, body.Descricao)) };
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        Services.AddScoped(_ => http);
        return Render<ArcaEvolucoesEditor>(p => p
            .Add(c => c.Roll, 4)
            .Add(c => c.Evolucoes, evolucoes)
            .Add(c => c.OnChanged, () => onChanged?.Invoke()));
    }

    [Fact]
    public void Lists_each_evolucao_level_and_text()
    {
        var cut = RenderEditor(new() { new(Guid.NewGuid(), 3, "três"), new(Guid.NewGuid(), 8, "oito") });

        // The last numeric/text pair is the blank "new evolução" row.
        cut.FindComponents<MudNumericField<int>>().Take(2).Select(f => f.Instance.Value).Should().Equal(3, 8);
        cut.FindComponents<MudTextField<string>>().Take(2).Select(f => f.Instance.Value).Should().Equal("três", "oito");
    }

    [Fact]
    public async Task Adicionar_posts_a_new_evolucao_and_notifies()
    {
        var changed = false;
        var cut = RenderEditor(new(), () => changed = true);

        await cut.InvokeAsync(() => cut.FindComponents<MudNumericField<int>>().Last().Instance.ValueChanged.InvokeAsync(6));
        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().Last().Instance.ValueChanged.InvokeAsync("nova"));
        cut.FindAll("button").Single(b => b.TextContent.Contains("Adicionar evolução")).Click();
        await Task.Delay(50);

        _log.Should().ContainSingle(l => l.Method == HttpMethod.Post && l.Path.EndsWith("/arcas/4/evolucoes") && l.Body!.Nivel == 6 && l.Body.Descricao == "nova");
        changed.Should().BeTrue();
    }

    [Fact]
    public async Task Editing_a_text_puts_it()
    {
        var id = Guid.NewGuid();
        var cut = RenderEditor(new() { new(id, 3, "três") });

        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First().Instance.ValueChanged.InvokeAsync("três editado"));
        await Task.Delay(50);

        _log.Should().ContainSingle(l => l.Method == HttpMethod.Put && l.Path.EndsWith($"/arcas/4/evolucoes/{id}") && l.Body!.Descricao == "três editado" && l.Body.Nivel == 3);
    }

    [Fact]
    public async Task Remove_button_deletes_it()
    {
        var id = Guid.NewGuid();
        var cut = RenderEditor(new() { new(id, 3, "três") });

        cut.Find($"button[aria-label='Remover evolução do nível 3']").Click();
        await Task.Delay(50);

        _log.Should().ContainSingle(l => l.Method == HttpMethod.Delete && l.Path.EndsWith($"/arcas/4/evolucoes/{id}"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ArcaEvolucoesEditorTests`
Expected: build FAIL — `ArcaEvolucoesEditor` doesn't exist.

- [ ] **Step 3: Implement the component**

`src/RuinaRPG.Client/Shared/ArcaEvolucoesEditor.razor`:
```razor
@inject HttpClient Http
@using RuinaRPG.Contracts.CharacterSheets
@using MudBlazor

@* Evoluções de uma linha da Tabela de Arcas (Requisitos - Habilidades Raciais R0006). As existentes
   salvam ao editar; a linha em branco no fim cria uma nova. *@
<DismissibleAlert @bind-Message="_errorMessage" />
@foreach (var ev in Evolucoes)
{
    <MudStack Row="true" AlignItems="AlignItems.Center" Class="mb-1">
        <MudNumericField T="int" Value="@ev.Nivel" ValueChanged="@(v => UpdateAsync(ev, v, ev.Descricao))" Label="Nível" Min="1" Max="50" Style="width:6em;" />
        <MudTextField T="string" Value="@ev.Descricao" ValueChanged="@(v => UpdateAsync(ev, ev.Nivel, v))" Label="Evolução" Lines="2" />
        <MudIconButton Icon="@Icons.Material.Filled.Delete" Color="Color.Error" Size="Size.Small"
                       aria-label="@($"Remover evolução do nível {ev.Nivel}")" title="Remover evolução"
                       OnClick="@(() => DeleteAsync(ev))" />
    </MudStack>
}
<MudStack Row="true" AlignItems="AlignItems.Center">
    <MudNumericField T="int" @bind-Value="_novoNivel" Label="Nível" Min="1" Max="50" Style="width:6em;" />
    <MudTextField T="string" @bind-Value="_novaDescricao" Label="Nova evolução" Lines="2" />
    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" StartIcon="@Icons.Material.Filled.Add"
               Disabled="@string.IsNullOrWhiteSpace(_novaDescricao)" OnClick="AddAsync">Adicionar evolução</MudButton>
</MudStack>

@code {
    [Parameter, EditorRequired] public int Roll { get; set; }
    [Parameter, EditorRequired] public List<ArcaEvolucaoResponse> Evolucoes { get; set; } = new();
    [Parameter] public EventCallback OnChanged { get; set; }

    private int _novoNivel = 1;
    private string _novaDescricao = "";
    private string? _errorMessage;

    private async Task AddAsync()
    {
        var response = await Http.PostAsJsonAsync($"arcas/{Roll}/evolucoes", new ArcaEvolucaoRequest(_novoNivel, _novaDescricao));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = await response.Content.ReadAsStringAsync();
            return;
        }
        _novaDescricao = "";
        _novoNivel = 1;
        await OnChanged.InvokeAsync();
    }

    private async Task UpdateAsync(ArcaEvolucaoResponse ev, int nivel, string descricao)
    {
        var response = await Http.PutAsJsonAsync($"arcas/{Roll}/evolucoes/{ev.Id}", new ArcaEvolucaoRequest(nivel, descricao));
        if (!response.IsSuccessStatusCode)
            _errorMessage = await response.Content.ReadAsStringAsync();
        await OnChanged.InvokeAsync();
    }

    private async Task DeleteAsync(ArcaEvolucaoResponse ev)
    {
        var response = await Http.DeleteAsync($"arcas/{Roll}/evolucoes/{ev.Id}");
        if (!response.IsSuccessStatusCode)
            _errorMessage = "Não foi possível remover a evolução.";
        await OnChanged.InvokeAsync();
    }
}
```
The tests use `.Last()` for the blank new row and `.First()`/`.Take(2)` for existing ones — keep the new row last in the markup.

- [ ] **Step 4: Wire it into `HabilidadesRaciais.razor`**

Close the `Sinir / Laonir (Humano)` Section before the Tabela de Arcas and give the table its own Section with an InfoPopup; each Arca row gets a second row with the editor:
```razor
</Section>

<Section Title="Tabela de Arcas">
    <TitleInfo>
        <InfoPopup Title="Como funcionam as Arcas e suas evoluções">
            Cada número de 1 a 18 é uma Arca, usada pelo Racial de Sinir/Laonir (Humano). Cada Arca pode ter evoluções, cada uma liberada a partir de um nível.
            As evoluções somam-se à descrição da Arca, não a substituem, e pode haver mais de uma no mesmo nível.
            Na ficha de um Humano, o botão "Evoluções da Arca" mostra todas as evoluções que o nível do personagem ou NPC já liberou; as futuras não aparecem.
        </InfoPopup>
    </TitleInfo>
    <ChildContent>
    <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
        <thead>
            <tr><th>Roll</th><th>Nome</th><th>Descrição</th></tr>
        </thead>
        <tbody>
            @foreach (var arca in _arcas)
            {
                <tr>
                    <td>@arca.Roll</td>
                    <td><MudTextField T="string" Value="@arca.Nome" ValueChanged="@(v => UpdateArcaAsync(arca.Roll, "Nome", v))" /></td>
                    <td><MudTextField T="string" Value="@arca.Descricao" ValueChanged="@(v => UpdateArcaAsync(arca.Roll, "Descricao", v))" /></td>
                </tr>
                <tr>
                    <td></td>
                    <td colspan="2">
                        <MudExpansionPanels Elevation="0">
                            <MudExpansionPanel Text="@($"Evoluções ({arca.Evolucoes.Count})")">
                                <ArcaEvolucoesEditor Roll="@arca.Roll" Evolucoes="@arca.Evolucoes" OnChanged="LoadArcasAsync" />
                            </MudExpansionPanel>
                        </MudExpansionPanels>
                    </td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
    </ChildContent>
</Section>
```
Check that `Section` requires `ChildContent` explicitly when `TitleInfo` is used (it does in `AuditoriaDurabilidadePorRank.razor`). Remove the old `<MudText Typo="Typo.h6">Tabela de Arcas</MudText>` heading and the old `</Section>` that followed the table. The page must build.

- [ ] **Step 5: Run tests and build**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ArcaEvolucoesEditorTests` → PASS.
Run: `dotnet build` → 0/0.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): editor de evoluções na tabela de Arcas"
```

---

### Task 5: Client — button + popup on the sheets

**Files:**
- Create: `src/RuinaRPG.Client/Shared/ArcaEvolucoesButton.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor:580-595` (Habilidade Racial section)
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` (the `Section Title="Habilidade Racial"` near line 551, same markup)
- Test: `tests/RuinaRPG.Tests.Client/Shared/ArcaEvolucoesButtonTests.cs`

**Interfaces:**
- Consumes: `RacialAbilityResponse.ArcaEvolucoes` (Task 3).
- Produces: `<ArcaEvolucoesButton Evolucoes="IReadOnlyList<ArcaEvolucaoResponse>" />`.

- [ ] **Step 1: Write the failing bUnit tests**

```csharp
using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class ArcaEvolucoesButtonTests : MudBunitContext
{
    private IRenderedComponent<ContainerFragment> RenderButton(List<ArcaEvolucaoResponse> evolucoes) =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ArcaEvolucoesButton>(1);
            builder.AddAttribute(2, nameof(ArcaEvolucoesButton.Evolucoes), evolucoes);
            builder.CloseComponent();
        });

    private static AngleSharp.Dom.IElement MainButton(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Contains("Evoluções da Arca"));

    [Fact]
    public void Shows_the_count_and_is_disabled_when_there_are_none()
    {
        var cut = RenderButton(new());

        MainButton(cut).TextContent.Should().Contain("Evoluções da Arca (0)");
        MainButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Clicking_opens_a_dialog_listing_each_level_and_text()
    {
        var cut = RenderButton(new() { new(Guid.NewGuid(), 2, "dois"), new(Guid.NewGuid(), 5, "cinco") });

        MainButton(cut).TextContent.Should().Contain("Evoluções da Arca (2)");
        MainButton(cut).Click();

        cut.Markup.Should().Contain("Nível 2").And.Contain("dois").And.Contain("Nível 5").And.Contain("cinco");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ArcaEvolucoesButtonTests`
Expected: build FAIL.

- [ ] **Step 3: Implement**

`src/RuinaRPG.Client/Shared/ArcaEvolucoesButton.razor`:
```razor
@using RuinaRPG.Contracts.CharacterSheets
@using MudBlazor

@* Ficha 4.a: evoluções da Arca rolada já liberadas pelo nível da ficha (a API já filtra e ordena). *@
<MudStack Row="true" AlignItems="AlignItems.Center" Class="mt-2">
    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" StartIcon="@Icons.Material.Filled.TrendingUp"
               Disabled="@(Evolucoes.Count == 0)" OnClick="@(() => _open = true)">
        Evoluções da Arca (@Evolucoes.Count)
    </MudButton>
    <InfoPopup Title="Evoluções da Arca">
        Evoluções liberadas pela Arca rolada até o nível atual. Novas evoluções aparecem sozinhas quando o personagem sobe de nível. Quem cadastra as evoluções é o GM.
    </InfoPopup>
</MudStack>

<MudDialog @bind-Visible="_open">
    <TitleContent>Evoluções da Arca</TitleContent>
    <DialogContent>
        <MudList T="string" Dense="true">
            @foreach (var ev in Evolucoes)
            {
                <MudListItem T="string">
                    <MudText><b>Nível @ev.Nivel</b> — @ev.Descricao</MudText>
                </MudListItem>
            }
        </MudList>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _open = false)">Fechar</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [Parameter, EditorRequired] public IReadOnlyList<ArcaEvolucaoResponse> Evolucoes { get; set; } = Array.Empty<ArcaEvolucaoResponse>();

    private bool _open;
}
```

In `FichaDePersonagem.razor` and `FichaDeNpc.razor`, inside the `@if (_racialAbility?.ArcaRolada is not null)` block, after the Arca description:
```razor
                    <ArcaEvolucoesButton Evolucoes="@_racialAbility.ArcaEvolucoes" />
```
(If FichaDeNpc names its field differently, use that field — read the section first.)

- [ ] **Step 4: Run tests and build**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~ArcaEvolucoes"` → PASS.
Run: `dotnet test tests/RuinaRPG.Tests.Client` → whole client suite PASS (sheet pages still render).
Run: `dotnet build` → 0/0.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): botão e popup de evoluções da Arca nas fichas de Personagem e NPC"
```

---

### Task 6: Requirements docs

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Habilidades Raciais.md` (append R0006)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md:263` (Número rolado bullet)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md:631-641` (after ArcaEntries table)

- [ ] **Step 1: Append R0006** to `Requisitos - Habilidades Raciais.md`:

```markdown

# **R0006** - Cada Arca pode ter evoluções liberadas por nível.

**Descrição**: Cada uma das 18 linhas da tabela de Arcas (R0002) tem uma lista de **evoluções**, cada uma com **Nível** (1 a 50) e **Descrição**. O GM adiciona, edita e remove evoluções na própria linha; pode haver várias no mesmo nível. Uma evolução **soma-se** à descrição da Arca, nunca a substitui. Adicionar uma evolução a um número ainda não preenchido cria a linha da Arca com Nome e Descrição vazios (a ficha continua exibindo "Arca não cadastrada." até o GM dar um Nome). Na Ficha de Personagem/NPC (ver "[[Requisitos - Ficha de Personagem]]" 4.a), só aparecem as evoluções com Nível menor ou igual ao da ficha.
```

- [ ] **Step 2: Extend 4.a** in `Requisitos - Ficha de Personagem.md` — append to the *Número rolado (1d18)* bullet:

```markdown
 Abaixo da Arca, um botão **Evoluções da Arca (N)** abre um popup listando, em ordem de nível, as evoluções dessa Arca já liberadas pelo Nível da ficha (ver "[[Requisitos - Habilidades Raciais]]" R0006); desabilitado quando N = 0.
```

- [ ] **Step 3: Add the table** in `Requisitos - Modelo de Dados.md` right after the ArcaEntries table:

```markdown

**ArcaEvolucoes** — evoluções de uma Arca liberadas por nível (Habilidades Raciais R0006). Apagar a Arca apaga suas evoluções.

| Coluna | Tipo |
|---|---|
| Id | PK |
| ArcaEntryId | FK → ArcaEntries (cascade) |
| Nivel | int, 1 a 50 |
| Descricao | text |
| CriadaEm | timestamptz — desempate entre evoluções do mesmo nível |
```

- [ ] **Step 4: Commit**

```bash
git add Docs/Requisitos
git commit -m "docs: requisitos das evoluções de Arca (Habilidades Raciais R0006)"
```
