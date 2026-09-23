# Equipagem — Construtor de Subcategoria Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace free-text Subcategoria matching (which silently fails Equipagem's choice slots when a GM's own wording doesn't match) with an Auditor-managed, closed per-Tipo vocabulary (Categoria + Família), a "Item Inicial" builder on the Catálogo item form that composes a predictable Subcategoria string from that vocabulary, and choice-slot matching that parses and compares the Família segment instead of the whole string — while widening choice slots from Arma-only to Arma/Armadura/Escudo/Artefato (Armadura routing to a specific `CharacterArmorSlot`/`NpcArmorSlot`).

**Architecture:** A new global (Auditor-managed, not per-GM) `SubcategoriaOption` catalog holds `Categoria`/`Família` values per `ItemTipo`. A shared pure `SubcategoriaBuilder` domain helper composes/parses the 4-segment string (`"Equipamento inicial - {Tipo} - {Categoria} - {Familia}"`) — used both server-side (matching) and client-side (the item form's builder UI). `Subcategoria` gains a per-subtype declaration on `Armadura`/`Escudo`/`Artefato` (mirroring the existing `ItemGeral`/`Arma` declarations — `Item`'s abstract base is deliberately untouched). Choice-slot resolution keeps matching the legacy raw-Subcategoria-string list (backward compat with already-shipped kits) in parallel with the new parsed-Família match.

**Tech Stack:** ASP.NET Core 8 / EF Core / PostgreSQL, Blazor WebAssembly 8 + MudBlazor, xUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-23-equipagem-subcategoria-builder-design.md`

## Global Constraints

- TDD mandatory (CLAUDE.md R0011): write the failing test, watch it fail for the right reason, then the minimal code to pass.
- `Item`'s abstract base class is never touched — `Subcategoria` stays a per-subtype declaration on `ItemGeral`/`Arma`/`Armadura`/`Escudo`/`Artefato` (explicit user correction during brainstorming; do not "clean this up" by moving it to the base).
- No new Auditoria page — the vocabulary-management UI is a new section on the existing `/auditoria/equipagem` page (explicit user correction during brainstorming).
- The composed Subcategoria string is exactly 4 segments joined by `" - "` with **no** grammar/pluralization logic: `"Equipamento inicial - {Tipo} - {Categoria} - {Familia}"`. Categoria and Família are raw Auditor-typed strings from their own lists.
- The `"Equipamento inicial"` prefix **is** the signal — there is no boolean "IsItemInicial" column anywhere. The item form's checkbox is pure client-side UI state, never sent to the server as a field.
- Choice-slot matching must support **both** the legacy exact-Subcategoria-string match (already-shipped kits use this) and the new parsed-Família match, in parallel, against the same stored value list — no migration of existing kit data required.
- Fixed `EquipmentKitItem` rows still never support `Armadura` — this plan only widens choice slots (`EquipmentKitChoiceSlot`), never fixed rows.
- All new positional-record fields are appended at the end of existing records — never inserted mid-list.

---

### Task 1: `Requisitos - Modelo de Dados.md` — new table and columns

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`

**Interfaces:**
- Produces: none (doc-only prerequisite).

- [ ] **Step 1: Read the file's `EquipmentKits`/`EquipmentKitItems`/`EquipmentKitChoiceSlots` section and the `Items` section to match existing formatting.**

- [ ] **Step 2: Add a new section, near the `EquipmentKitChoiceSlots` section, documenting:**

```markdown
### SubcategoriaOptions

Catálogo global (não por GM) de valores de Categoria/Família usados pelo construtor de Subcategoria do cadastro de Item (ver "[[Requisitos - Catálogo de Itens e Equipamentos]]") e pelos slots de escolha da Equipagem — editável pelo Auditor de Regras na página de Auditoria de Equipagem.

- `Id` (PK)
- `Tipo` (enum ItemTipo — Arma, Armadura, Escudo ou Artefato; nunca ItemGeral)
- `Facet` (enum SubcategoriaFacet — Categoria ou Familia)
- `Valor` (string, obrigatório)
- `IsDeleted` (bool — soft delete)

### Novas colunas

- `Items.Subcategoria` (string?, opcional) — já existia em Item Geral e Arma; passa a existir também em Armadura, Escudo e Artefato, com o mesmo comportamento (texto livre, ou o texto composto pelo construtor "Item Inicial" — ver "[[Requisitos - Catálogo de Itens e Equipamentos]]").
- `EquipmentKitChoiceSlots.ArmorSlot` (enum ArmorSlotType?, opcional) — obrigatório quando o slot é `Tipo=Armadura` (indica em qual slot de armadura da ficha — Capacete/Superior/Inferior — o item concedido é colocado); deve ficar vazio para os outros Tipos.
```

- [ ] **Step 3: Commit.**

```bash
git add "Docs/Requisitos/Requisitos - Modelo de Dados.md"
git commit -m "docs: modelo de dados do Construtor de Subcategoria da Equipagem"
```

---

### Task 2: Schema — `SubcategoriaOption`, `Subcategoria` on 3 item types, `ArmorSlot` on choice slots

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/SubcategoriaOption.cs`
- Create: `src/RuinaRPG.Domain/Items/SubcategoriaFacet.cs`
- Modify: `src/RuinaRPG.Infrastructure/Items/Armadura.cs`
- Modify: `src/RuinaRPG.Infrastructure/Items/Escudo.cs`
- Modify: `src/RuinaRPG.Infrastructure/Items/Artefato.cs`
- Modify: `src/RuinaRPG.Infrastructure/Rules/EquipmentKitChoiceSlot.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Create: migration under `src/RuinaRPG.Infrastructure/Persistence/Migrations/`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/SubcategoriaOptionSchemaTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.Items.ItemTipo` (existing), `RuinaRPG.Domain.CharacterSheets.ArmorSlotType` (existing, values `Capacete`/`Superior`/`Inferior`).
- Produces: `SubcategoriaOption { Id, Tipo (ItemTipo), Facet (SubcategoriaFacet), Valor, IsDeleted }`; `SubcategoriaFacet { Categoria, Familia }`; `Armadura.Subcategoria`/`Escudo.Subcategoria`/`Artefato.Subcategoria` (`string?`); `EquipmentKitChoiceSlot.ArmorSlot` (`ArmorSlotType?`); `RuinaRpgDbContext.SubcategoriaOptions` DbSet.

- [ ] **Step 1: Write the failing integration test.**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SubcategoriaOptionSchemaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public SubcategoriaOptionSchemaTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task SubcategoriaOption_round_trips_and_the_three_item_types_persist_Subcategoria()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();

        var option = new SubcategoriaOption { Id = Guid.NewGuid(), Tipo = ItemTipo.Arma, Facet = SubcategoriaFacet.Familia, Valor = "Varinha" };
        db.SubcategoriaOptions.Add(option);

        var gmId = Guid.NewGuid();
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Teste Armadura", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Peso = 1, Preco = 0 };
        var escudo = new Escudo { Id = Guid.NewGuid(), GmId = gmId, Nome = "Teste Escudo", Subcategoria = "Equipamento inicial - Escudo - Leve - Rodela", Peso = 1, Preco = 0 };
        var artefato = new Artefato { Id = Guid.NewGuid(), GmId = gmId, Nome = "Teste Artefato", Subcategoria = "Equipamento inicial - Artefato - Passivo - Anel", Peso = 0, Preco = 0 };
        db.AddRange(armadura, escudo, artefato);

        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit Teste", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, Qtd = 1, ArmorSlot = ArmorSlotType.Superior };
        db.EquipmentKitChoiceSlots.Add(slot);

        await db.SaveChangesAsync();

        (await db.SubcategoriaOptions.SingleAsync()).Valor.Should().Be("Varinha");
        (await db.Set<Armadura>().SingleAsync()).Subcategoria.Should().Be("Equipamento inicial - Armadura - Leve - Couro");
        (await db.Set<Escudo>().SingleAsync()).Subcategoria.Should().Be("Equipamento inicial - Escudo - Leve - Rodela");
        (await db.Set<Artefato>().SingleAsync()).Subcategoria.Should().Be("Equipamento inicial - Artefato - Passivo - Anel");
        (await db.EquipmentKitChoiceSlots.SingleAsync()).ArmorSlot.Should().Be(ArmorSlotType.Superior);
    }
}
```

- [ ] **Step 2: Run test to verify it fails.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SubcategoriaOptionSchemaTests -v`
Expected: FAIL to compile (`SubcategoriaOption`/`SubcategoriaFacet`/the new properties don't exist yet).

- [ ] **Step 3: Create the enum.** `src/RuinaRPG.Domain/Items/SubcategoriaFacet.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

public enum SubcategoriaFacet
{
    Categoria,
    Familia
}
```

- [ ] **Step 4: Create the entity.** `src/RuinaRPG.Infrastructure/Rules/SubcategoriaOption.cs`:

```csharp
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public class SubcategoriaOption
{
    public Guid Id { get; set; }
    public ItemTipo Tipo { get; set; }
    public SubcategoriaFacet Facet { get; set; }
    public required string Valor { get; set; }
    public bool IsDeleted { get; set; }
}
```

- [ ] **Step 5: Add `Subcategoria` to the 3 item subtypes.** In `src/RuinaRPG.Infrastructure/Items/Armadura.cs`, add right after the class declaration's opening brace (mirroring `Arma.cs`'s own `public string? Subcategoria { get; set; }` line):

```csharp
    public string? Subcategoria { get; set; }
```

Same single-line addition, same relative position (first property), in `Escudo.cs` and `Artefato.cs`.

- [ ] **Step 6: Add `ArmorSlot` to `EquipmentKitChoiceSlot.cs`.** Add after the existing `BonusQtd` property:

```csharp
    public RuinaRPG.Domain.CharacterSheets.ArmorSlotType? ArmorSlot { get; set; }
```

- [ ] **Step 7: Wire up `RuinaRpgDbContext.cs`.** Add the DbSet near `public DbSet<EquipmentKit> EquipmentKits => Set<EquipmentKit>();`:

```csharp
    public DbSet<SubcategoriaOption> SubcategoriaOptions => Set<SubcategoriaOption>();
```

No new relationship config is needed for `SubcategoriaOption` (no FK to anything) or for `ArmorSlot` on `EquipmentKitChoiceSlot` (plain nullable enum column, same treatment as the existing `Tier` property on the same entity — no explicit Fluent config needed).

- [ ] **Step 8: Generate the migration.**

Run: `dotnet ef migrations add AddSubcategoriaOptionsAndArmorSlot --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`

- [ ] **Step 9: Read the generated migration file and confirm it creates `SubcategoriaOptions`, adds `Subcategoria` (nullable text) to `Items` (the shared TPH table), and adds `ArmorSlot` (nullable int) to `EquipmentKitChoiceSlots`.** If anything is missing or wrong, fix Steps 5-7 and regenerate (delete the migration files first).

- [ ] **Step 10: Run test to verify it passes.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SubcategoriaOptionSchemaTests -v`
Expected: PASS

- [ ] **Step 11: Run the full build to confirm 0 warnings/0 errors.**

Run: `dotnet build`

- [ ] **Step 12: Commit.**

```bash
git add src/RuinaRPG.Infrastructure/Rules/SubcategoriaOption.cs src/RuinaRPG.Domain/Items/SubcategoriaFacet.cs src/RuinaRPG.Infrastructure/Items/Armadura.cs src/RuinaRPG.Infrastructure/Items/Escudo.cs src/RuinaRPG.Infrastructure/Items/Artefato.cs src/RuinaRPG.Infrastructure/Rules/EquipmentKitChoiceSlot.cs src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs src/RuinaRPG.Infrastructure/Persistence/Migrations/ tests/RuinaRPG.Tests.Integration/Persistence/SubcategoriaOptionSchemaTests.cs
git commit -m "feat: schema do Construtor de Subcategoria (SubcategoriaOption, Subcategoria em 3 tipos, ArmorSlot)"
```

---

### Task 3: `SubcategoriaBuilder` domain helper (compose/parse)

**Files:**
- Create: `src/RuinaRPG.Domain/Items/SubcategoriaBuilder.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Items/SubcategoriaBuilderTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.Items.ItemTipo` (existing).
- Produces: `SubcategoriaBuilder.Prefix` (const string), `SubcategoriaBuilder.Compose(ItemTipo tipo, string categoria, string familia) : string`, `SubcategoriaBuilder.TryParse(string? subcategoria, out ItemTipo tipo, out string categoria, out string familia) : bool`. Consumed by Task 8 (`EquipmentKitGrantService`) and Task 10 (`CatalogoItemForm.razor`, via a new child component).

- [ ] **Step 1: Write the failing tests.**

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class SubcategoriaBuilderTests
{
    [Fact]
    public void Compose_joins_exactly_4_segments_with_no_grammar_transformation()
    {
        SubcategoriaBuilder.Compose(ItemTipo.Arma, "Mágica", "Varinha").Should().Be("Equipamento inicial - Arma - Mágica - Varinha");
        SubcategoriaBuilder.Compose(ItemTipo.Arma, "Distância", "Arcos").Should().Be("Equipamento inicial - Arma - Distância - Arcos");
    }

    [Fact]
    public void TryParse_round_trips_a_composed_string()
    {
        var composed = SubcategoriaBuilder.Compose(ItemTipo.Armadura, "Leve", "Couro");

        var ok = SubcategoriaBuilder.TryParse(composed, out var tipo, out var categoria, out var familia);

        ok.Should().BeTrue();
        tipo.Should().Be(ItemTipo.Armadura);
        categoria.Should().Be("Leve");
        familia.Should().Be("Couro");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Arcos")]
    [InlineData("Equipamento inicial - Arma - Só 3 partes")]
    [InlineData("Prefixo errado - Arma - Distância - Arcos")]
    [InlineData("Equipamento inicial - TipoInexistente - Distância - Arcos")]
    public void TryParse_returns_false_for_anything_that_does_not_match_the_pattern(string? input)
    {
        var ok = SubcategoriaBuilder.TryParse(input, out _, out _, out _);

        ok.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~SubcategoriaBuilderTests -v`
Expected: FAIL to compile (`SubcategoriaBuilder` doesn't exist yet).

- [ ] **Step 3: Write the helper.** `src/RuinaRPG.Domain/Items/SubcategoriaBuilder.cs`:

```csharp
namespace RuinaRPG.Domain.Items;

/// <summary>
/// Composes/parses the "Item Inicial" Subcategoria convention: exactly 4 segments joined by
/// " - ", no grammar/pluralization logic — Categoria and Família are raw Auditor-managed values
/// (see SubcategoriaOption). The literal Prefix segment is the signal Equipagem's choice-slot
/// matching (EquipmentKitGrantService) uses to recognize an item as built by this constructor,
/// in parallel with the legacy raw-Subcategoria-string match already shipped kits rely on.
/// </summary>
public static class SubcategoriaBuilder
{
    public const string Prefix = "Equipamento inicial";

    public static string Compose(ItemTipo tipo, string categoria, string familia) =>
        string.Join(" - ", [Prefix, tipo.ToString(), categoria, familia]);

    public static bool TryParse(string? subcategoria, out ItemTipo tipo, out string categoria, out string familia)
    {
        tipo = default;
        categoria = "";
        familia = "";

        if (string.IsNullOrEmpty(subcategoria))
            return false;

        var parts = subcategoria.Split(" - ");
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        if (!Enum.TryParse<ItemTipo>(parts[1], out tipo))
            return false;

        categoria = parts[2];
        familia = parts[3];
        return true;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~SubcategoriaBuilderTests -v`
Expected: PASS

- [ ] **Step 5: Commit.**

```bash
git add src/RuinaRPG.Domain/Items/SubcategoriaBuilder.cs tests/RuinaRPG.Tests.Unit/Items/SubcategoriaBuilderTests.cs
git commit -m "feat: SubcategoriaBuilder — compõe/analisa a Subcategoria do construtor Item Inicial"
```

---

### Task 4: Contracts — `SubcategoriaOption` + `ArmorSlot` on choice-slot contracts

**Files:**
- Create: `src/RuinaRPG.Contracts/Rules/SubcategoriaOptionResponse.cs`
- Create: `src/RuinaRPG.Contracts/Rules/CreateSubcategoriaOptionRequest.cs`
- Modify: `src/RuinaRPG.Contracts/Rules/EquipmentKitResponse.cs` (the `EquipmentKitChoiceSlotResponse` record in this file)
- Modify: `src/RuinaRPG.Contracts/Rules/CreateEquipmentKitRequest.cs` (the `EquipmentKitChoiceSlotInput` record in this file)

**Interfaces:**
- Produces: `SubcategoriaOptionResponse(string Id, string Tipo, string Facet, string Valor)`, `CreateSubcategoriaOptionRequest(string Tipo, string Facet, string Valor)`; `EquipmentKitChoiceSlotResponse`/`EquipmentKitChoiceSlotInput` both gain a trailing `string? ArmorSlot`.

- [ ] **Step 1: Create the new contracts.**

`src/RuinaRPG.Contracts/Rules/SubcategoriaOptionResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record SubcategoriaOptionResponse(string Id, string Tipo, string Facet, string Valor);
```

`src/RuinaRPG.Contracts/Rules/CreateSubcategoriaOptionRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Rules;

public record CreateSubcategoriaOptionRequest(string Tipo, string Facet, string Valor);
```

- [ ] **Step 2: Append `ArmorSlot` to the two choice-slot contracts.** In `EquipmentKitResponse.cs`, find:

```csharp
public record EquipmentKitChoiceSlotResponse(string Id, string Label, string Tipo, List<string>? Subcategorias,
    string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd);
```

Change the trailing `int? BonusQtd);` to:

```csharp
    string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd, string? ArmorSlot);
```

In `CreateEquipmentKitRequest.cs`, find:

```csharp
public record EquipmentKitChoiceSlotInput(string Label, string Tipo, List<string>? Subcategorias,
    string? Tier, int Qtd, string? BonusSubcategoria, string? BonusNome, int? BonusQtd);
```

Same edit: append `, string? ArmorSlot` after `int? BonusQtd`.

- [ ] **Step 3: Find every construction call site of these two records.**

Run: `grep -rn "new EquipmentKitChoiceSlotResponse(\|new EquipmentKitChoiceSlotInput(" src tests --include=*.cs --include=*.razor`

For every match, append a literal `null` as the trailing argument (Task 7 will later wire real values in `EquipmentKitsController`; every other site — client `AuditoriaEquipagem.razor`'s several `new EquipmentKitChoiceSlotInput(...)` calls, any test file — just needs `null` to keep compiling for now, except: `AuditoriaEquipagem.razor`'s calls that rebuild the input list from an existing `EquipmentKitChoiceSlotResponse` (e.g. `kit.ChoiceSlots.Select(s => new EquipmentKitChoiceSlotInput(s.Label, s.Tipo, s.Subcategorias, s.Tier, s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd))`) should pass `s.ArmorSlot` through instead of a literal `null`, so editing an existing kit doesn't silently drop its Armadura slots' target — Task 11 will further extend this file, but don't leave a real round-trip regression in the meantime.

- [ ] **Step 4: Build to confirm every call site was caught.**

Run: `dotnet build`
Expected: any missed call site fails with CS7036 naming the exact file/line — fix each until 0 errors.

- [ ] **Step 5: Run the full unit + client test suites.**

Run: `dotnet test tests/RuinaRPG.Tests.Unit && dotnet test tests/RuinaRPG.Tests.Client`

- [ ] **Step 6: Commit.**

```bash
git add src/RuinaRPG.Contracts/Rules/SubcategoriaOptionResponse.cs src/RuinaRPG.Contracts/Rules/CreateSubcategoriaOptionRequest.cs src/RuinaRPG.Contracts/Rules/EquipmentKitResponse.cs src/RuinaRPG.Contracts/Rules/CreateEquipmentKitRequest.cs
git add -u
git commit -m "feat: contratos do Construtor de Subcategoria e campo ArmorSlot no slot de escolha"
```

---

### Task 5: `SubcategoriaOptionsController` (Auditor CRUD)

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/SubcategoriaOptionsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/SubcategoriaOptionsControllerTests.cs`

**Interfaces:**
- Consumes: `SubcategoriaOption` (Task 2), `SubcategoriaOptionResponse`/`CreateSubcategoriaOptionRequest` (Task 4).
- Produces: `GET api/subcategoria-options` (open, optional `?tipo=&facet=` filters), `POST api/subcategoria-options` (Auditor-gated), `DELETE api/subcategoria-options/{id}` (Auditor-gated). No PUT — renaming a value is delete + re-add, matching this app's simple-flat-list precedent (no in-use guard on delete either: existing composed Subcategoria strings and existing choice-slot stored values are plain strings, not FKs to this table, so deleting an option never corrupts already-saved data — it only removes a future dropdown option).

- [ ] **Step 1: Write the failing tests.**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SubcategoriaOptionsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SubcategoriaOptionsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    // Copied verbatim from HistoricosControllerTests.cs's established pattern — no grant endpoint
    // exists, real grants happen via `make grant-rules-auditor`.
    private async Task GrantRulesAuditorAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_is_open_to_any_authenticated_caller_and_filters_by_Tipo_and_Facet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm1", "subcat1@teste.com");
        await GrantRulesAuditorAsync("subcat1@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Varinha")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Categoria", "Mágica")));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Armadura", "Familia", "Couro")));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/subcategoria-options?tipo=Arma&facet=Familia", gmToken));

        var options = await response.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>();
        options!.Should().ContainSingle(o => o.Valor == "Varinha");
        options.Should().NotContain(o => o.Valor is "Mágica" or "Couro");
    }

    [Fact]
    public async Task Create_by_a_non_Auditor_GM_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm2", "subcat2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "Familia", "Arcos")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_rejects_Tipo_ItemGeral_and_an_unknown_Facet()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm3", "subcat3@teste.com");
        await GrantRulesAuditorAsync("subcat3@teste.com");

        var badTipo = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("ItemGeral", "Familia", "X")));
        badTipo.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badFacet = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Arma", "NaoExiste", "X")));
        badFacet.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_by_a_non_Auditor_GM_returns_403_and_by_the_Auditor_soft_deletes()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SubcatGm4", "subcat4@teste.com");
        await GrantRulesAuditorAsync("subcat4@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/subcategoria-options", gmToken, new CreateSubcategoriaOptionRequest("Escudo", "Familia", "Rodela"))))
            .Content.ReadFromJsonAsync<SubcategoriaOptionResponse>();

        var otherGmToken = await RegisterGmAndGetTokenAsync("SubcatGm5", "subcat5@teste.com");
        var forbidden = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/subcategoria-options/{created!.Id}", otherGmToken));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleted = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/subcategoria-options/{created.Id}", gmToken));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/subcategoria-options?tipo=Escudo&facet=Familia", gmToken));
        var options = await listResponse.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>();
        options!.Should().NotContain(o => o.Id == created.Id);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SubcategoriaOptionsControllerTests -v`
Expected: FAIL to compile.

- [ ] **Step 3: Write the controller.** `src/RuinaRPG.Api/Controllers/SubcategoriaOptionsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Global (not per-GM) vocabulary of Categoria/Família values used by the Catálogo item form's
/// "Item Inicial" constructor and by Equipagem choice-slot authoring — same treatment as
/// HistoricosController/EquipmentKitsController. List (GET) stays open; Create/Delete are gated
/// to the Rules Auditor, checked directly against the DB, not a JWT claim. No PUT — renaming a
/// value is delete + re-add.
/// </summary>
[ApiController]
[Route("api/subcategoria-options")]
[Authorize]
public class SubcategoriaOptionsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SubcategoriaOptionResponse>>> List([FromQuery] string? tipo, [FromQuery] string? facet)
    {
        var query = db.SubcategoriaOptions.Where(o => !o.IsDeleted);

        if (tipo is not null && Enum.TryParse<ItemTipo>(tipo, out var tipoParsed))
            query = query.Where(o => o.Tipo == tipoParsed);
        if (facet is not null && Enum.TryParse<SubcategoriaFacet>(facet, out var facetParsed))
            query = query.Where(o => o.Facet == facetParsed);

        var options = await query.OrderBy(o => o.Valor).ToListAsync();
        return options.Select(ToResponse).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<SubcategoriaOptionResponse>> Create(CreateSubcategoriaOptionRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<ItemTipo>(request.Tipo, out var tipo) || tipo == ItemTipo.ItemGeral)
            return BadRequest($"Tipo inválido para Construtor de Subcategoria: \"{request.Tipo}\".");
        if (!Enum.TryParse<SubcategoriaFacet>(request.Facet, out var facet))
            return BadRequest($"Facet inválido: \"{request.Facet}\".");
        if (string.IsNullOrWhiteSpace(request.Valor))
            return BadRequest("Valor é obrigatório.");

        var option = new SubcategoriaOption { Id = Guid.NewGuid(), Tipo = tipo, Facet = facet, Valor = request.Valor };
        db.SubcategoriaOptions.Add(option);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(option));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var option = await db.SubcategoriaOptions.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
        if (option is null)
            return NotFound();

        option.IsDeleted = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static SubcategoriaOptionResponse ToResponse(SubcategoriaOption o) =>
        new(o.Id.ToString(), o.Tipo.ToString(), o.Facet.ToString(), o.Valor);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 4: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SubcategoriaOptionsControllerTests -v`
Expected: PASS

- [ ] **Step 5: Commit.**

```bash
git add src/RuinaRPG.Api/Controllers/SubcategoriaOptionsController.cs tests/RuinaRPG.Tests.Integration/Controllers/SubcategoriaOptionsControllerTests.cs
git commit -m "feat: CRUD de Auditoria para o vocabulário do Construtor de Subcategoria"
```

---

### Task 6: `ItemsController` — persist Subcategoria for Armadura/Escudo/Artefato

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/ItemsController.cs`
- Modify: `Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs` (add to the existing file)

**Interfaces:**
- Consumes: `Armadura.Subcategoria`/`Escudo.Subcategoria`/`Artefato.Subcategoria` (Task 2). `CreateItemRequest.Subcategoria`/`UpdateItemRequest.Subcategoria`/`ItemResponse.Subcategoria` already exist — no contract change.
- Produces: Create/Update/List/ToResponseAsync now round-trip Subcategoria for all 5 item types (previously only ItemGeral/Arma).

- [ ] **Step 1: Write the failing tests.** Add to `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs` (use this file's own established helpers — check it for the exact `RegisterGmAndGetTokenAsync`/`AuthedRequest`/`CreateItemRequest` construction pattern already in use, and match it):

```csharp
    [Fact]
    public async Task Create_and_Update_persist_Subcategoria_for_Armadura_Escudo_and_Artefato()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ItemsSubcatGm1", "itemssubcat1@teste.com");

        var armaduraResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            ValidCreateRequestFor("Armadura") with { Subcategoria = "Equipamento inicial - Armadura - Leve - Couro" }));
        var armadura = await armaduraResponse.Content.ReadFromJsonAsync<ItemResponse>();
        armadura!.Subcategoria.Should().Be("Equipamento inicial - Armadura - Leve - Couro");

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/items/{armadura.Id}", gmToken,
            ValidUpdateRequestFor(armadura) with { Subcategoria = "Equipamento inicial - Armadura - Pesada - Placas" }));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/items", gmToken));
        var items = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
        items!.Single(i => i.Id == armadura.Id).Subcategoria.Should().Be("Equipamento inicial - Armadura - Pesada - Placas");
    }
```

Note: this test uses `ValidCreateRequestFor(tipo)`/`ValidUpdateRequestFor(existing)` as illustrative helper names — **read the actual existing test file first** and either reuse its real equivalent helpers (if it already has per-Tipo valid-request builders for Armadura) or construct `CreateItemRequest`/`UpdateItemRequest` directly with the exact positional arguments the real contract requires, matching whatever pattern the file's other Armadura-related tests already use.

- [ ] **Step 2: Run test to verify it fails.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~ItemsControllerTests -v`
Expected: the new test FAILs (Subcategoria not yet persisted for Armadura); pre-existing tests in the file still pass.

- [ ] **Step 3: Wire `Subcategoria` into `ItemsController.Create`'s switch.** Add `Subcategoria = request.Subcategoria,` to the `ItemTipo.Armadura`, `ItemTipo.Escudo`, and `ItemTipo.Artefato` branches' object initializers (same line already present in the `ItemTipo.ItemGeral`/`ItemTipo.Arma` branches).

- [ ] **Step 4: Wire it into `ItemsController.Update`'s switch.** Add `ar.Subcategoria = request.Subcategoria;` / `e.Subcategoria = request.Subcategoria;` / `art.Subcategoria = request.Subcategoria;` as the first line inside the `case Armadura ar:`, `case Escudo e:`, `case Artefato art:` blocks respectively.

- [ ] **Step 5: Wire it into `ItemsController.ToResponseAsync`'s `item switch`.** For the `Armadura ar =>`, `Escudo e =>`, `Artefato ar =>` branches, change the `Subcategoria` argument (currently the literal `null` in each) to `ar.Subcategoria`/`e.Subcategoria`/`ar.Subcategoria` respectively — the argument position in each `new ItemResponse(...)` call is the 7th positional argument (right after `imageUrl`), already correctly positioned for ItemGeral/Arma; just swap the literal `null` for the real field read in these 3 branches.

- [ ] **Step 6: Run test to verify it passes.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~ItemsControllerTests -v`
Expected: PASS

- [ ] **Step 7: Update `Requisitos - Catálogo de Itens e Equipamentos.md`.** Add a new `**R0013**` (continuing the doc's sequence) documenting: Armadura, Escudo and Artefato now also have a Subcategoria field (same free-text-or-constructor behavior R0003 already describes for Item Geral), and introduce the "Item Inicial" constructor convention (checkbox swaps the free-text field for two selects — Categoria, Família — drawn from a vocabulary the Rules Auditor manages on the Auditoria: Equipagem page; the composed value is `"Equipamento inicial - {Tipo} - {Categoria} - {Familia}"`).

- [ ] **Step 8: Run the full build.**

Run: `dotnet build`

- [ ] **Step 9: Commit.**

```bash
git add src/RuinaRPG.Api/Controllers/ItemsController.cs "Docs/Requisitos/Requisitos - Catálogo de Itens e Equipamentos.md" tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs
git commit -m "feat: persiste Subcategoria para Armadura, Escudo e Artefato"
```

---

### Task 7: `EquipmentKitsController.ValidateRequest` — widen choice-slot Tipo, validate ArmorSlot

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/EquipmentKitsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/EquipmentKitsControllerTests.cs` (add to the existing file)

**Interfaces:**
- Consumes: `EquipmentKitChoiceSlotInput.ArmorSlot` (Task 4).
- Produces: choice-slot `Tipo` accepts Arma/Armadura/Escudo/Artefato; `ArmorSlot` required iff `Tipo==Armadura`; `ToResponseAsync` returns the real `ArmorSlot` value.

- [ ] **Step 1: Write the failing tests.** Add to `EquipmentKitsControllerTests.cs`:

```csharp
    [Fact]
    public async Task Create_accepts_a_choice_slot_of_Tipo_Armadura_with_an_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm1", "equipkitsarmor1@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor1@teste.com");

        var request = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, "Superior")] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var kit = await response.Content.ReadFromJsonAsync<EquipmentKitResponse>();
        kit!.ChoiceSlots.Should().ContainSingle(s => s.Tipo == "Armadura" && s.ArmorSlot == "Superior");
    }

    [Fact]
    public async Task Create_rejects_a_choice_slot_of_Tipo_Armadura_without_an_ArmorSlot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm2", "equipkitsarmor2@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor2@teste.com");

        var request = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", null, null, 1, null, null, null, null)] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_rejects_ArmorSlot_on_a_choice_slot_whose_Tipo_is_not_Armadura()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm3", "equipkitsarmor3@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor3@teste.com");

        var request = ValidCreate() with { ChoiceSlots = [new EquipmentKitChoiceSlotInput("Arma", "Arma", null, null, 1, null, null, null, "Superior")] };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_accepts_choice_slots_of_Tipo_Escudo_and_Artefato()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipKitsArmorGm4", "equipkitsarmor4@teste.com");
        await GrantRulesAuditorAsync("equipkitsarmor4@teste.com");

        var request = ValidCreate() with
        {
            ChoiceSlots =
            [
                new EquipmentKitChoiceSlotInput("Escudo", "Escudo", null, null, 1, null, null, null, null),
                new EquipmentKitChoiceSlotInput("Artefato", "Artefato", null, null, 1, null, null, null, null),
            ]
        };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken, request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
```

Note: `ValidCreate()`'s existing definition in this file constructs `EquipmentKitChoiceSlotInput` with 8 positional arguments (ending in `BonusQtd`) — since Task 4 appended `ArmorSlot` as a 9th trailing argument, `ValidCreate()`'s own body needs that same trailing `null` added (this should already be true from Task 4's mechanical call-site fix; if the build fails here, that fix was incomplete — apply it now).

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitsControllerTests -v`
Expected: the 4 new tests FAIL (current code only accepts `Tipo==Arma` for choice slots, `ArmorSlot` isn't validated or returned yet); pre-existing tests in the file still pass.

- [ ] **Step 3: Widen the choice-slot Tipo check in `ValidateRequest`.** Find:

```csharp
            // EquipmentKitGrantService.ResolveEligibleOptionsAsync/BuildPlanAsync only ever query
            // the Arma table for a choice slot, regardless of its declared Tipo (Requisitos - Modelo
            // de Dados: "sempre Arma nos dados de seed atuais") — accepting anything else here would
            // silently offer weapons as options and, on confirm, insert a row pointing at an Arma's
            // Id into the wrong sheet sub-table, a corrupt row the shared TPH Item base table's FK
            // never rejects.
            if (!Enum.TryParse<ItemTipo>(slot.Tipo, out var tipo) || tipo != ItemTipo.Arma)
                return BadRequest("Slots de escolha só suportam Tipo=Arma nos dados atuais.");
```

Replace with:

```csharp
            // EquipmentKitGrantService.ResolveEligibleOptionsAsync/BuildPlanAsync now dispatch on
            // slot.Tipo to the matching concrete Item subtype's DbSet — Armadura, Escudo, Artefato
            // and Arma are all real, resolvable choices; only Armadura also needs to know WHICH
            // ArmorSlot to fill (validated below), since armor doesn't insert a new row the way the
            // other 3 do.
            if (!Enum.TryParse<ItemTipo>(slot.Tipo, out var tipo) || tipo == ItemTipo.ItemGeral)
                return BadRequest($"Tipo de slot de escolha inválido: \"{slot.Tipo}\".");

            RuinaRPG.Domain.CharacterSheets.ArmorSlotType? armorSlot = null;
            if (tipo == ItemTipo.Armadura)
            {
                if (string.IsNullOrWhiteSpace(slot.ArmorSlot) || !Enum.TryParse<RuinaRPG.Domain.CharacterSheets.ArmorSlotType>(slot.ArmorSlot, out var parsedArmorSlot))
                    return BadRequest("Slots de escolha de Tipo=Armadura precisam de um ArmorSlot válido.");
                armorSlot = parsedArmorSlot;
            }
            else if (!string.IsNullOrWhiteSpace(slot.ArmorSlot))
            {
                return BadRequest("ArmorSlot só é aplicável a slots de escolha de Tipo=Armadura.");
            }
```

- [ ] **Step 4: Set `ArmorSlot` on the constructed `EquipmentKitChoiceSlot`.** Find the `slots2.Add(new EquipmentKitChoiceSlot { ... BonusQtd = slot.BonusQtd, });` object initializer and add `ArmorSlot = armorSlot,` as the last property.

- [ ] **Step 5: Return `ArmorSlot` from `ToResponseAsync`.** Find the `EquipmentKitChoiceSlotResponse` construction inside `ToResponseAsync` and append `s.ArmorSlot?.ToString()` as the trailing argument (after `s.BonusQtd`).

- [ ] **Step 6: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitsControllerTests -v`
Expected: PASS (including all pre-existing tests in the file — in particular the pre-existing `Create_rejects_an_item_of_Tipo_Armadura` test, which is about **fixed** `EquipmentKitItem` rows, not choice slots, and must remain unaffected by this task).

- [ ] **Step 7: Run the full build.**

Run: `dotnet build`

- [ ] **Step 8: Commit.**

```bash
git add src/RuinaRPG.Api/Controllers/EquipmentKitsController.cs tests/RuinaRPG.Tests.Integration/Controllers/EquipmentKitsControllerTests.cs
git commit -m "feat: slots de escolha aceitam Armadura/Escudo/Artefato, com ArmorSlot para Armadura"
```

---

### Task 8: `EquipmentKitGrantService` — Família-based matching, ArmorSlot in the grant plan

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/EquipmentKitGrantService.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/EquipmentKitGrantServiceTests.cs` (add to the existing file)

**Interfaces:**
- Consumes: `SubcategoriaBuilder` (Task 3), `EquipmentKitChoiceSlot.ArmorSlot` (Task 2), `Armadura.Subcategoria`/`Escudo.Subcategoria`/`Artefato.Subcategoria` (Task 2).
- Produces: `EquipmentGrantPlanItem` gains a trailing `ArmorSlotType? ArmorSlot`; `ResolveEligibleOptionsAsync` Tipo-dispatches and dual-matches (legacy raw-string OR parsed-Família); `BuildPlanAsync`'s bonus check gets the same dual-match treatment.

- [ ] **Step 1: Write the failing tests.** Add to `EquipmentKitGrantServiceTests.cs`:

```csharp
    [Fact]
    public async Task ResolveEligibleOptionsAsync_matches_by_parsed_Familia_for_items_built_by_the_constructor()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var varinha = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Varinha Composta", Subcategoria = "Equipamento inicial - Arma - Mágica - Varinha", Tier = Tier.F, Peso = 1, Preco = 0 };
        var machado = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Machado Composto", Subcategoria = "Equipamento inicial - Arma - Corpo a Corpo - Machado", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.AddRange(varinha, machado);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Condutor", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Varinha", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var options = await service.ResolveEligibleOptionsAsync(slot, gmId);

        options.Should().ContainSingle(o => o.ItemId == varinha.Id.ToString());
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_still_matches_the_legacy_raw_Subcategoria_string()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var arco = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arco Legado", Subcategoria = "Arcos", Tier = Tier.F, Peso = 1, Preco = 0 };
        db.Add(arco);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Arcos", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var options = await service.ResolveEligibleOptionsAsync(slot, gmId);

        options.Should().ContainSingle(o => o.ItemId == arco.Id.ToString());
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_resolves_Armadura_Escudo_and_Artefato_choice_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Armadura Teste", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Peso = 1, Preco = 0 };
        var escudo = new Escudo { Id = Guid.NewGuid(), GmId = gmId, Nome = "Escudo Teste", Subcategoria = "Equipamento inicial - Escudo - Leve - Rodela", Peso = 1, Preco = 0 };
        var artefato = new Artefato { Id = Guid.NewGuid(), GmId = gmId, Nome = "Artefato Teste", Subcategoria = "Equipamento inicial - Artefato - Passivo - Anel", Peso = 0, Preco = 0 };
        db.AddRange(armadura, escudo, artefato);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var armaduraSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, SubcategoriasCsv = "Couro", Qtd = 1, ArmorSlot = RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Superior };
        var escudoSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Escudo", Tipo = ItemTipo.Escudo, SubcategoriasCsv = "Rodela", Qtd = 1 };
        var artefatoSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Artefato", Tipo = ItemTipo.Artefato, SubcategoriasCsv = "Anel", Qtd = 1 };
        db.EquipmentKitChoiceSlots.AddRange(armaduraSlot, escudoSlot, artefatoSlot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);

        (await service.ResolveEligibleOptionsAsync(armaduraSlot, gmId)).Should().ContainSingle(o => o.ItemId == armadura.Id.ToString());
        (await service.ResolveEligibleOptionsAsync(escudoSlot, gmId)).Should().ContainSingle(o => o.ItemId == escudo.Id.ToString());
        (await service.ResolveEligibleOptionsAsync(artefatoSlot, gmId)).Should().ContainSingle(o => o.ItemId == artefato.Id.ToString());
    }

    [Fact]
    public async Task BuildPlanAsync_carries_the_slot_s_ArmorSlot_through_to_the_grant_for_a_chosen_Armadura()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Armadura Teste", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", DurabilidadeMaxima = 10, Peso = 1, Preco = 0 };
        db.Add(armadura);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, SubcategoriasCsv = "Couro", Qtd = 1, ArmorSlot = RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Capacete };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db);
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), armadura.Id.ToString())]);

        error.Should().BeNull();
        var grant = plan!.Grants.Single();
        grant.Tipo.Should().Be(ItemTipo.Armadura);
        grant.ArmorSlot.Should().Be(RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Capacete);
        grant.DurabilidadeMaxima.Should().Be(10);
    }
```

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitGrantServiceTests -v`
Expected: the 4 new tests FAIL to compile (`ResolveEligibleOptionsAsync` doesn't dispatch beyond Arma yet, `EquipmentGrantPlanItem` has no `ArmorSlot`).

- [ ] **Step 3: Add `ArmorSlot` to `EquipmentGrantPlanItem`.** Change:

```csharp
public record EquipmentGrantPlanItem(ItemTipo Tipo, Guid ItemId, int Qtd, int? DurabilidadeMaxima);
```

to:

```csharp
public record EquipmentGrantPlanItem(ItemTipo Tipo, Guid ItemId, int Qtd, int? DurabilidadeMaxima, RuinaRPG.Domain.CharacterSheets.ArmorSlotType? ArmorSlot);
```

- [ ] **Step 4: Rewrite `ResolveEligibleOptionsAsync` to dispatch per Tipo and dual-match.** Replace the whole method:

```csharp
    public async Task<List<EquipmentKitEligibleItemResponse>> ResolveEligibleOptionsAsync(EquipmentKitChoiceSlot slot, Guid gmId)
    {
        var allowedValues = slot.SubcategoriasCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];

        bool Matches(string? itemSubcategoria)
        {
            if (allowedValues.Length == 0)
                return true;
            if (allowedValues.Contains(itemSubcategoria))
                return true; // legacy: raw Subcategoria string equals one of the stored values
            if (SubcategoriaBuilder.TryParse(itemSubcategoria, out var tipo, out _, out var familia) && tipo == slot.Tipo && allowedValues.Contains(familia))
                return true; // new: parsed Família equals one of the stored values
            return false;
        }

        var candidates = new List<(Guid Id, string Nome)>();
        switch (slot.Tipo)
        {
            case ItemTipo.Arma:
                var armaQuery = db.Set<Arma>().Where(a => a.GmId == gmId);
                if (slot.Tier is not null)
                    armaQuery = armaQuery.Where(a => a.Tier == slot.Tier);
                var armas = await armaQuery.Select(a => new { a.Id, a.Nome, a.Subcategoria }).ToListAsync();
                candidates.AddRange(armas.Where(a => Matches(a.Subcategoria)).Select(a => (a.Id, a.Nome)));
                break;
            case ItemTipo.Armadura:
                var armaduras = await db.Set<Armadura>().Where(a => a.GmId == gmId).Select(a => new { a.Id, a.Nome, a.Subcategoria }).ToListAsync();
                candidates.AddRange(armaduras.Where(a => Matches(a.Subcategoria)).Select(a => (a.Id, a.Nome)));
                break;
            case ItemTipo.Escudo:
                var escudos = await db.Set<Escudo>().Where(e => e.GmId == gmId).Select(e => new { e.Id, e.Nome, e.Subcategoria }).ToListAsync();
                candidates.AddRange(escudos.Where(e => Matches(e.Subcategoria)).Select(e => (e.Id, e.Nome)));
                break;
            case ItemTipo.Artefato:
                var artefatos = await db.Set<Artefato>().Where(a => a.GmId == gmId).Select(a => new { a.Id, a.Nome, a.Subcategoria }).ToListAsync();
                candidates.AddRange(artefatos.Where(a => Matches(a.Subcategoria)).Select(a => (a.Id, a.Nome)));
                break;
        }

        return candidates.OrderBy(c => c.Nome).Select(c => new EquipmentKitEligibleItemResponse(c.Id.ToString(), c.Nome)).ToList();
    }
```

- [ ] **Step 5: Update `BuildPlanAsync`'s grant construction for the new `ArmorSlot` field.** Find:

```csharp
            var selectedItem = await db.Set<Arma>().SingleAsync(a => a.Id == selectedItemId);
            grants.Add(new EquipmentGrantPlanItem(slot.Tipo, selectedItemId, slot.Qtd, selectedItem.DurabilidadeMaxima));

            if (slot.BonusNome is not null && selectedItem.Subcategoria == slot.BonusSubcategoria)
```

Replace with a Tipo-dispatched lookup of the selected item's `Subcategoria`/`DurabilidadeMaxima` (mirroring Step 4's per-Tipo `switch` style — `Artefato` has no `DurabilidadeMaxima` column, so that branch always yields `null`):

```csharp
            string? selectedSubcategoria;
            int? selectedDurabilidade;
            switch (slot.Tipo)
            {
                case ItemTipo.Arma:
                    var arma = await db.Set<Arma>().Where(a => a.Id == selectedItemId).Select(a => new { a.Subcategoria, a.DurabilidadeMaxima }).SingleAsync();
                    selectedSubcategoria = arma.Subcategoria; selectedDurabilidade = arma.DurabilidadeMaxima;
                    break;
                case ItemTipo.Armadura:
                    var armadura = await db.Set<Armadura>().Where(a => a.Id == selectedItemId).Select(a => new { a.Subcategoria, a.DurabilidadeMaxima }).SingleAsync();
                    selectedSubcategoria = armadura.Subcategoria; selectedDurabilidade = armadura.DurabilidadeMaxima;
                    break;
                case ItemTipo.Escudo:
                    var escudo = await db.Set<Escudo>().Where(e => e.Id == selectedItemId).Select(e => new { e.Subcategoria, e.DurabilidadeMaxima }).SingleAsync();
                    selectedSubcategoria = escudo.Subcategoria; selectedDurabilidade = escudo.DurabilidadeMaxima;
                    break;
                case ItemTipo.Artefato:
                    selectedSubcategoria = await db.Set<Artefato>().Where(a => a.Id == selectedItemId).Select(a => a.Subcategoria).SingleAsync();
                    selectedDurabilidade = null;
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled choice-slot Tipo {slot.Tipo}.");
            }
            grants.Add(new EquipmentGrantPlanItem(slot.Tipo, selectedItemId, slot.Qtd, selectedDurabilidade, slot.ArmorSlot));

            var bonusMatches = slot.BonusNome is not null && (selectedSubcategoria == slot.BonusSubcategoria
                || (SubcategoriaBuilder.TryParse(selectedSubcategoria, out _, out _, out var selectedFamilia) && selectedFamilia == slot.BonusSubcategoria));
            if (bonusMatches)
```

(the `if (slot.BonusNome is not null && selectedItem.Subcategoria == slot.BonusSubcategoria)` line's body — the bonus-item resolution block — stays exactly as it already is, just re-indented under the new `if (bonusMatches)` condition instead of the old inline check).

- [ ] **Step 6: Update the two fixed-item `EquipmentGrantPlanItem` constructions to pass a trailing `null` for `ArmorSlot`** (fixed rows never target armor — `EquipmentKitsController.ValidateRequest` still rejects `Tipo=Armadura` for `EquipmentKitItem`). Find both:

```csharp
            grants.Add(new EquipmentGrantPlanItem(kitItem.Tipo, resolved.Value.Id, kitItem.Qtd, resolved.Value.DurabilidadeMaxima));
```

and

```csharp
                    grants.Add(new EquipmentGrantPlanItem(ItemTipo.ItemGeral, bonusResolved.Value.Id, slot.BonusQtd ?? 1, null));
```

Append `, null` (trailing `ArmorSlot`) to each.

- [ ] **Step 7: Add `using RuinaRPG.Domain.CharacterSheets;` to the file's usings** (needed for `ArmorSlotType`).

- [ ] **Step 8: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~EquipmentKitGrantServiceTests -v`
Expected: PASS, including every pre-existing test in the file (the already-shipped Caçador-style conditional-bonus tests must keep passing unmodified — this is the explicit backward-compatibility proof named in the spec).

- [ ] **Step 9: Run the full build.**

Run: `dotnet build`

- [ ] **Step 10: Commit.**

```bash
git add src/RuinaRPG.Infrastructure/Rules/EquipmentKitGrantService.cs tests/RuinaRPG.Tests.Integration/Rules/EquipmentKitGrantServiceTests.cs
git commit -m "feat: EquipmentKitGrantService resolve Armadura/Escudo/Artefato e casa por Família"
```

---

### Task 9: `CharacterEquipagemController`/`NpcEquipagemController` — Armadura grants update an armor slot

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterEquipagemController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcEquipagemController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterEquipagemControllerTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/NpcEquipagemControllerTests.cs` (add to the existing files)

**Interfaces:**
- Consumes: `EquipmentGrantPlanItem.ArmorSlot` (Task 8).
- Produces: `AddGrant` updates the sheet's existing `CharacterArmorSlot`/`NpcArmorSlot` row for a granted Armadura, instead of inserting.

- [ ] **Step 1: Write the failing test.** Add to `CharacterEquipagemControllerTests.cs`:

```csharp
    [Fact]
    public async Task Choose_a_kit_with_an_Armadura_choice_slot_updates_the_target_armor_slot()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("EquipagemArmorGm1", "equipagemarmor1@teste.com");
        await GrantRulesAuditorAsync("equipagemarmor1@teste.com"); // this file's own copy of the Auditor-grant helper, per Task 6/9's established pattern
        var (_, sheetId) = await CreateCampaignAndSheetAsync(gmToken);

        var meResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));
        var gmId = (await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id;
        Guid armaduraId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            var armadura = new RuinaRPG.Infrastructure.Items.Armadura
            {
                Id = Guid.NewGuid(), GmId = Guid.Parse(gmId), Nome = "Armadura de Teste F", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro",
                DurabilidadeMaxima = 8, Peso = 1, Preco = 0,
            };
            db.Add(armadura);
            await db.SaveChangesAsync();
            armaduraId = armadura.Id;
        }

        var kitResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/equipment-kits", gmToken,
            new CreateEquipmentKitRequest("Kit com Armadura", "D", 0, [],
                [new EquipmentKitChoiceSlotInput("Armadura", "Armadura", ["Couro"], null, 1, null, null, null, "Superior")])));
        var kit = await kitResponse.Content.ReadFromJsonAsync<EquipmentKitResponse>();

        var chooseResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/equipagem/choose", gmToken,
            new ChooseEquipmentKitRequest(kit!.Id, [new ChoiceSlotSelectionRequest(kit.ChoiceSlots.Single().Id, armaduraId.ToString())])));
        chooseResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var slotsResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/armor-slots", gmToken));
        var slots = await slotsResponse.Content.ReadFromJsonAsync<List<CharacterArmorSlotResponse>>();
        var superior = slots!.Single(s => s.Slot == "Superior");
        superior.Nome.Should().Be("Armadura de Teste F");
        superior.DurabilidadeAtual.Should().Be(8);
    }
```

Mirror this test in `NpcEquipagemControllerTests.cs` using its own established Npc sheet-creation helper (`CreateSheetAsync(gmToken)`) and `GET .../npc-sheets/{sheetId}/armor-slots`.

- [ ] **Step 2: Run tests to verify they fail.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterEquipagemControllerTests|FullyQualifiedName~NpcEquipagemControllerTests" -v`
Expected: the 2 new tests FAIL (`AddGrant` has no `Armadura` case yet — the granted item is silently dropped, `armor-slots` stays empty for that slot).

- [ ] **Step 3: Add the `Armadura` case to `CharacterEquipagemController.AddGrant`.** This needs to become `async Task` (it currently is a synchronous `void` method) since it must query the existing `CharacterArmorSlot` row. Change the method signature and its call site:

```csharp
        foreach (var grant in plan.Grants)
            await AddGrantAsync(sheetId, grant);
```

```csharp
    private async Task AddGrantAsync(Guid sheetId, EquipmentGrantPlanItem grant)
    {
        switch (grant.Tipo)
        {
            case RuinaRPG.Domain.Items.ItemTipo.Arma:
                db.CharacterWeapons.Add(new CharacterWeapon { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.Armadura:
                var armorSlot = await db.CharacterArmorSlots.SingleAsync(s => s.CharacterSheetId == sheetId && s.Slot == grant.ArmorSlot);
                armorSlot.ItemId = grant.ItemId;
                armorSlot.DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0;
                break;
            case RuinaRPG.Domain.Items.ItemTipo.Escudo:
                db.CharacterShields.Add(new CharacterShield { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = grant.ItemId, IsEquipped = false, DurabilidadeAtual = grant.DurabilidadeMaxima ?? 0 });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.ItemGeral:
                db.CharacterInventoryItems.Add(new CharacterInventoryItem { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = grant.ItemId, Qtd = grant.Qtd });
                break;
            case RuinaRPG.Domain.Items.ItemTipo.Artefato:
                db.CharacterArtifacts.Add(new CharacterArtifact { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ArtifactItemId = grant.ItemId });
                break;
        }
    }
```

- [ ] **Step 4: Mirror Step 3 in `NpcEquipagemController.AddGrant`/`AddGrantAsync`** — same shape, `NpcArmorSlots`/`NpcSheetId` instead of `CharacterArmorSlots`/`CharacterSheetId`.

- [ ] **Step 5: Run tests to verify they pass.**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterEquipagemControllerTests|FullyQualifiedName~NpcEquipagemControllerTests" -v`
Expected: PASS

- [ ] **Step 6: Run the full build.**

Run: `dotnet build`

- [ ] **Step 7: Commit.**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterEquipagemController.cs src/RuinaRPG.Api/Controllers/NpcEquipagemController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterEquipagemControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcEquipagemControllerTests.cs
git commit -m "feat: concessão de Armadura via Equipagem atualiza o slot de armadura da ficha"
```

---

### Task 10: Client — "Item Inicial" builder on the Catálogo item form

**Files:**
- Create: `src/RuinaRPG.Client/Shared/ItemInicialSubcategoriaField.razor`
- Modify: `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`

**Interfaces:**
- Consumes: `SubcategoriaBuilder` (Task 3), `GET api/subcategoria-options?tipo=&facet=` (Task 5).
- Produces: a reusable field component swapped into the Arma/Armadura/Escudo/Artefato branches of `CatalogoItemForm.razor`, replacing (Arma) or adding (Armadura/Escudo/Artefato) the Subcategoria input.

- [ ] **Step 1: Create `ItemInicialSubcategoriaField.razor`.**

```razor
@using MudBlazor
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Domain.Items
@inject HttpClient Http

@* Requisitos - Catálogo de Itens e Equipamentos R0013 — "Item Inicial" checkbox swaps the free-text
   Subcategoria field for two selects (Categoria, Família) drawn from the Rules Auditor's vocabulary
   (Auditoria: Equipagem), composing "Equipamento inicial - {Tipo} - {Categoria} - {Familia}". The
   checkbox is pure UI state — Value/ValueChanged carry only the composed (or free-text) Subcategoria
   string, exactly like every other bound field on this form. *@
<MudCheckBox T="bool" Value="_isChecked" ValueChanged="OnCheckedChangedAsync" Label="Item Inicial" />
@if (_isChecked)
{
    <MudSelect T="string" Value="_categoria" ValueChanged="OnCategoriaChangedAsync" Label="Categoria">
        @foreach (var opt in _categorias)
        {
            <MudSelectItem Value="@opt">@opt</MudSelectItem>
        }
    </MudSelect>
    <MudSelect T="string" Value="_familia" ValueChanged="OnFamiliaChangedAsync" Label="Família">
        @foreach (var opt in _familias)
        {
            <MudSelectItem Value="@opt">@opt</MudSelectItem>
        }
    </MudSelect>
}
else
{
    <MudTextField T="string" Value="Value" ValueChanged="OnFreeTextChangedAsync" Label="Subcategoria" />
}

@code {
    [Parameter] public string Tipo { get; set; } = "";
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    private bool _isChecked;
    private string? _categoria;
    private string? _familia;
    private List<string> _categorias = new();
    private List<string> _familias = new();
    private string? _loadedForTipo;
    private bool _parsedInitialValue;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedForTipo != Tipo)
        {
            _loadedForTipo = Tipo;
            await LoadOptionsAsync();
        }

        if (!_parsedInitialValue)
        {
            _parsedInitialValue = true;
            if (SubcategoriaBuilder.TryParse(Value, out var tipo, out var categoria, out var familia) && tipo.ToString() == Tipo)
            {
                _isChecked = true;
                _categoria = categoria;
                _familia = familia;
            }
        }
    }

    private async Task LoadOptionsAsync()
    {
        var categoriaResponse = await Http.GetAsync($"subcategoria-options?tipo={Tipo}&facet=Categoria");
        if (categoriaResponse.IsSuccessStatusCode)
            _categorias = (await categoriaResponse.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>() ?? new()).Select(o => o.Valor).ToList();

        var familiaResponse = await Http.GetAsync($"subcategoria-options?tipo={Tipo}&facet=Familia");
        if (familiaResponse.IsSuccessStatusCode)
            _familias = (await familiaResponse.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>() ?? new()).Select(o => o.Valor).ToList();
    }

    private async Task OnCheckedChangedAsync(bool value)
    {
        _isChecked = value;
        if (!_isChecked)
        {
            await ValueChanged.InvokeAsync(null);
        }
        else if (_categoria is not null && _familia is not null && Enum.TryParse<ItemTipo>(Tipo, out var tipo))
        {
            await ValueChanged.InvokeAsync(SubcategoriaBuilder.Compose(tipo, _categoria, _familia));
        }
    }

    private Task OnCategoriaChangedAsync(string value)
    {
        _categoria = value;
        return ComposeAndNotifyAsync();
    }

    private Task OnFamiliaChangedAsync(string value)
    {
        _familia = value;
        return ComposeAndNotifyAsync();
    }

    private Task ComposeAndNotifyAsync()
    {
        if (_categoria is null || _familia is null || !Enum.TryParse<ItemTipo>(Tipo, out var tipo))
            return Task.CompletedTask;
        return ValueChanged.InvokeAsync(SubcategoriaBuilder.Compose(tipo, _categoria, _familia));
    }

    private Task OnFreeTextChangedAsync(string? value) => ValueChanged.InvokeAsync(value);
}
```

- [ ] **Step 2: Wire it into `CatalogoItemForm.razor`'s Arma branch.** Find:

```razor
            <MudTextField T="string" @bind-Value="_form.Subcategoria" Label="Subcategoria" @bind-Value:after="NotifySavedAsync" />
            <MudTextField T="string" @bind-Value="_form.Tier" Label="Tier" @bind-Value:after="NotifySavedAsync" />
```

Replace the Subcategoria line with:

```razor
            <ItemInicialSubcategoriaField Tipo="@_form.Tipo" @bind-Value="_form.Subcategoria" @bind-Value:after="NotifySavedAsync" />
            <MudTextField T="string" @bind-Value="_form.Tier" Label="Tier" @bind-Value:after="NotifySavedAsync" />
```

- [ ] **Step 3: Add the same field to the Armadura/Escudo branch.** Find:

```razor
        <Section Title="@(_form.Tipo == "Armadura" ? "Armadura" : "Escudo")">
            <MudTextField T="string" @bind-Value="_form.Categoria" Label="Categoria" @bind-Value:after="NotifySavedAsync" />
```

Insert right after the opening `<Section ...>` tag, before the existing `_form.Categoria` line (which is the Armadura/Escudo protection-category enum, `CategoriaProtecao` — an unrelated field that keeps its existing name/behavior unchanged):

```razor
        <Section Title="@(_form.Tipo == "Armadura" ? "Armadura" : "Escudo")">
            <ItemInicialSubcategoriaField Tipo="@_form.Tipo" @bind-Value="_form.Subcategoria" @bind-Value:after="NotifySavedAsync" />
            <MudTextField T="string" @bind-Value="_form.Categoria" Label="Categoria" @bind-Value:after="NotifySavedAsync" />
```

- [ ] **Step 4: Add the same field to the Artefato branch.** Find:

```razor
        <Section Title="Artefato">
            <MudSelect T="string" @bind-Value="_form.TipoDeAlvo" @bind-Value:after="OnTipoDeAlvoChangedAsync" Label="Tipo de Alvo">
```

Replace with:

```razor
        <Section Title="Artefato">
            <ItemInicialSubcategoriaField Tipo="@_form.Tipo" @bind-Value="_form.Subcategoria" @bind-Value:after="NotifySavedAsync" />
            <MudSelect T="string" @bind-Value="_form.TipoDeAlvo" @bind-Value:after="OnTipoDeAlvoChangedAsync" Label="Tipo de Alvo">
```

- [ ] **Step 5: Confirm `_form.Subcategoria` is already loaded on edit for every Tipo.** Line 184 (`_form.Subcategoria = existing.Subcategoria;`) in `OnInitializedAsync` already runs unconditionally regardless of `Tipo` — no change needed there; `ItemInicialSubcategoriaField`'s own `OnParametersSetAsync` handles the parse-back the first time it receives that value.

- [ ] **Step 6: Build the client.**

Run: `dotnet build src/RuinaRPG.Client`
Expected: 0 warnings/0 errors.

- [ ] **Step 7: Manually verify in the browser.** Log in as a GM, go to Catálogo → Novo Item, pick Tipo=Arma, check "Item Inicial", confirm the two selects appear (populated from whatever `SubcategoriaOption` rows exist — if none yet, they'll be empty until Task 11's Auditoria section adds some; that's expected, verify the empty-select case doesn't error). Save, reopen the item for editing, confirm the checkbox is pre-checked and the two selects show the right values. Uncheck it, confirm it reverts to free text with the current (composed) string still shown for editing.

- [ ] **Step 8: Commit.**

```bash
git add src/RuinaRPG.Client/Shared/ItemInicialSubcategoriaField.razor src/RuinaRPG.Client/Pages/CatalogoItemForm.razor
git commit -m "feat: construtor de Subcategoria (checkbox Item Inicial) no cadastro de Item"
```

---

### Task 11: Client — Construtor de Subcategoria section + choice-slot UI on Auditoria: Equipagem

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/AuditoriaEquipagem.razor`
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md`

**Interfaces:**
- Consumes: `SubcategoriaOptionResponse`/`CreateSubcategoriaOptionRequest` (Task 4/5), `EquipmentKitChoiceSlotResponse.ArmorSlot`/`EquipmentKitChoiceSlotInput`'s new trailing param (Task 4/7).

- [ ] **Step 1: Add a "Construtor de Subcategoria" section.** Insert a new `<Section Title="Construtor de Subcategoria">` block right after the existing `<Section Title="Kits">...</Section>` closing tag (before the final `}` that closes the `else` block), with a Tipo selector and two add/remove lists:

```razor
    <Section Title="Construtor de Subcategoria">
        <MudSelect T="string" @bind-Value="_subcatTipo" Label="Tipo" @bind-Value:after="LoadSubcategoriaOptionsAsync">
            <MudSelectItem Value="@("Arma")">Arma</MudSelectItem>
            <MudSelectItem Value="@("Armadura")">Armadura</MudSelectItem>
            <MudSelectItem Value="@("Escudo")">Escudo</MudSelectItem>
            <MudSelectItem Value="@("Artefato")">Artefato</MudSelectItem>
        </MudSelect>

        <MudText Typo="Typo.h6" Class="mt-3">Categorias</MudText>
        <MudSimpleTable Dense="true">
            <tbody>
                @foreach (var opt in _categoriaOptions)
                {
                    <tr><td>@opt.Valor</td><td><MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" OnClick="@(() => DeleteSubcategoriaOptionAsync(opt))" /></td></tr>
                }
            </tbody>
        </MudSimpleTable>
        <div class="d-flex" style="gap:8px">
            <MudTextField T="string" @bind-Value="_newCategoriaValor" Label="Nova Categoria" />
            <MudButton Variant="Variant.Outlined" OnClick="@(() => AddSubcategoriaOptionAsync("Categoria", _newCategoriaValor))">Adicionar</MudButton>
        </div>

        <MudText Typo="Typo.h6" Class="mt-3">Famílias</MudText>
        <MudSimpleTable Dense="true">
            <tbody>
                @foreach (var opt in _familiaOptions)
                {
                    <tr><td>@opt.Valor</td><td><MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" OnClick="@(() => DeleteSubcategoriaOptionAsync(opt))" /></td></tr>
                }
            </tbody>
        </MudSimpleTable>
        <div class="d-flex" style="gap:8px">
            <MudTextField T="string" @bind-Value="_newFamiliaValor" Label="Nova Família" />
            <MudButton Variant="Variant.Outlined" OnClick="@(() => AddSubcategoriaOptionAsync("Familia", _newFamiliaValor))">Adicionar</MudButton>
        </div>
    </Section>
```

- [ ] **Step 2: Add the backing state and methods.** In `@code`, near the other field declarations:

```csharp
    private string _subcatTipo = "Arma";
    private List<SubcategoriaOptionResponse> _categoriaOptions = new();
    private List<SubcategoriaOptionResponse> _familiaOptions = new();
    private string _newCategoriaValor = "";
    private string _newFamiliaValor = "";
```

Call `await LoadSubcategoriaOptionsAsync();` at the end of `OnInitializedAsync` (alongside the existing `LoadAsync()` call — both should run so the section has data on first load), and add:

```csharp
    private async Task LoadSubcategoriaOptionsAsync()
    {
        var categoriaResponse = await Http.GetAsync($"subcategoria-options?tipo={_subcatTipo}&facet=Categoria");
        if (categoriaResponse.IsSuccessStatusCode)
            _categoriaOptions = await categoriaResponse.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>() ?? new();

        var familiaResponse = await Http.GetAsync($"subcategoria-options?tipo={_subcatTipo}&facet=Familia");
        if (familiaResponse.IsSuccessStatusCode)
            _familiaOptions = await familiaResponse.Content.ReadFromJsonAsync<List<SubcategoriaOptionResponse>>() ?? new();
    }

    private async Task AddSubcategoriaOptionAsync(string facet, string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return;

        var response = await Http.PostAsJsonAsync("subcategoria-options", new CreateSubcategoriaOptionRequest(_subcatTipo, facet, valor));
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível adicionar o valor.";
            return;
        }

        _newCategoriaValor = "";
        _newFamiliaValor = "";
        await LoadSubcategoriaOptionsAsync();
    }

    private async Task DeleteSubcategoriaOptionAsync(SubcategoriaOptionResponse option)
    {
        var response = await Http.DeleteAsync($"subcategoria-options/{option.Id}");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _forbidden = true;
            return;
        }
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível remover o valor.";
            return;
        }

        await LoadSubcategoriaOptionsAsync();
    }
```

- [ ] **Step 3: Widen the choice-slot authoring form's Tipo select and add the Família/ArmorSlot pickers.** Find:

```razor
                <div class="d-flex" style="gap:8px">
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].Label" Label="Label" />
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].SubcategoriasText" Label="Subcategorias (vírgula, vazio = qualquer)" />
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].Tier" Label="Tier (vazio = qualquer)" />
                    <MudNumericField T="int" @bind-Value="_slotForm[kit.Id].Qtd" Label="Qtd" />
                    <MudButton Variant="Variant.Outlined" OnClick="@(() => AddSlotAsync(kit))">Adicionar slot</MudButton>
                </div>
```

Replace with (Tipo select drives whether the ArmorSlot picker shows; "Subcategorias" free text stays as-is — Auditors can still type raw legacy values or Família names directly, matching the dual-match backend, rather than forcing a dropdown; simplest change that satisfies the spec without a second live-filtered fetch inside this already-dense form):

```razor
                <div class="d-flex" style="gap:8px">
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].Label" Label="Label" />
                    <MudSelect T="string" @bind-Value="_slotForm[kit.Id].Tipo" Label="Tipo">
                        <MudSelectItem Value="@("Arma")">Arma</MudSelectItem>
                        <MudSelectItem Value="@("Armadura")">Armadura</MudSelectItem>
                        <MudSelectItem Value="@("Escudo")">Escudo</MudSelectItem>
                        <MudSelectItem Value="@("Artefato")">Artefato</MudSelectItem>
                    </MudSelect>
                    @if (_slotForm[kit.Id].Tipo == "Armadura")
                    {
                        <MudSelect T="string" @bind-Value="_slotForm[kit.Id].ArmorSlot" Label="Slot de Armadura">
                            <MudSelectItem Value="@("Capacete")">Capacete</MudSelectItem>
                            <MudSelectItem Value="@("Superior")">Superior</MudSelectItem>
                            <MudSelectItem Value="@("Inferior")">Inferior</MudSelectItem>
                        </MudSelect>
                    }
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].SubcategoriasText" Label="Famílias/Subcategorias (vírgula, vazio = qualquer)" />
                    <MudTextField T="string" @bind-Value="_slotForm[kit.Id].Tier" Label="Tier (vazio = qualquer)" />
                    <MudNumericField T="int" @bind-Value="_slotForm[kit.Id].Qtd" Label="Qtd" />
                    <MudButton Variant="Variant.Outlined" OnClick="@(() => AddSlotAsync(kit))">Adicionar slot</MudButton>
                </div>
```

- [ ] **Step 4: Update `NewSlotFormModel` and `AddSlotAsync`.** Add a `Tipo`/`ArmorSlot` field to the model:

```csharp
    private class NewSlotFormModel
    {
        public string Label { get; set; } = "";
        public string Tipo { get; set; } = "Arma";
        public string? ArmorSlot { get; set; }
        public string SubcategoriasText { get; set; } = "";
        public string Tier { get; set; } = "";
        public int Qtd { get; set; } = 1;
    }
```

In `AddSlotAsync`, find:

```csharp
            .Append(new EquipmentKitChoiceSlotInput(form.Label, "Arma", subcategorias, string.IsNullOrWhiteSpace(form.Tier) ? null : form.Tier, form.Qtd, null, null, null)).ToList();
```

Replace with:

```csharp
            .Append(new EquipmentKitChoiceSlotInput(form.Label, form.Tipo, subcategorias, string.IsNullOrWhiteSpace(form.Tier) ? null : form.Tier, form.Qtd, null, null, null,
                form.Tipo == "Armadura" ? form.ArmorSlot : null)).ToList();
```

- [ ] **Step 5: Update the display columns to show ArmorSlot.** Find:

```razor
                        @foreach (var slot in kit.ChoiceSlots)
                        {
                            <tr>
                                <td>@slot.Label</td><td>@(slot.Subcategorias is null ? "qualquer" : string.Join(", ", slot.Subcategorias))</td><td>@(slot.Tier ?? "qualquer")</td><td>@slot.Qtd</td>
                                <td><MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" OnClick="@(() => RemoveSlotAsync(kit, slot))" /></td>
                            </tr>
                        }
```

Add a Tipo/ArmorSlot column:

```razor
                        @foreach (var slot in kit.ChoiceSlots)
                        {
                            <tr>
                                <td>@slot.Label</td><td>@slot.Tipo@(slot.ArmorSlot is null ? "" : $" ({slot.ArmorSlot})")</td><td>@(slot.Subcategorias is null ? "qualquer" : string.Join(", ", slot.Subcategorias))</td><td>@(slot.Tier ?? "qualquer")</td><td>@slot.Qtd</td>
                                <td><MudIconButton Icon="@Icons.Material.Filled.Delete" Size="Size.Small" OnClick="@(() => RemoveSlotAsync(kit, slot))" /></td>
                            </tr>
                        }
```

(add a matching `<th>Tipo</th>` to that table's `<thead>` row too).

- [ ] **Step 6: Fix the remaining 3 places `EquipmentKitChoiceSlotInput` is constructed from an existing `EquipmentKitChoiceSlotResponse`** (`UpdateAsync`, `AddItemAsync`, `RemoveItemAsync`, `RemoveSlotAsync` — anywhere the pattern `new EquipmentKitChoiceSlotInput(s.Label, s.Tipo, s.Subcategorias, s.Tier, s.Qtd, s.BonusSubcategoria, s.BonusNome, s.BonusQtd)` appears) to pass `s.ArmorSlot` as the trailing argument instead of the `null` Task 4 left there — otherwise every one of those actions (which all re-PUT the kit's full child list) would silently strip `ArmorSlot` off every existing Armadura choice slot the moment any OTHER field on the kit changes.

- [ ] **Step 7: Build the client.**

Run: `dotnet build src/RuinaRPG.Client`
Expected: 0 warnings/0 errors.

- [ ] **Step 8: Manually verify in the browser.** As the Rules Auditor, open `/auditoria/equipagem`, add a few Categoria/Família values for Tipo=Arma, switch the Tipo selector to Armadura and add different values there, confirm the lists are independent. On an existing kit, add a choice slot with Tipo=Armadura, confirm the ArmorSlot select appears and is required-in-practice (test submitting without it and confirm the API's 400 surfaces as the page's error message). Edit the kit's Nome afterward (an unrelated field) and confirm the Armadura slot's ArmorSlot survives the round-trip (Step 6's fix).

- [ ] **Step 9: Update `Requisitos - Auditoria de Regras.md`.** Extend the existing R0009 (or add a new `**R0010**` right after it, whichever reads more naturally given the doc's existing structure) documenting: the Construtor de Subcategoria vocabulary (per-Tipo Categoria/Família lists, add/remove only) lives on this same page; choice slots now support Arma/Armadura/Escudo/Artefato, with Armadura slots also specifying an ArmorSlot (Capacete/Superior/Inferior).

- [ ] **Step 10: Commit.**

```bash
git add src/RuinaRPG.Client/Pages/AuditoriaEquipagem.razor "Docs/Requisitos/Requisitos - Auditoria de Regras.md"
git commit -m "feat: Construtor de Subcategoria e slots Armadura/Escudo/Artefato na Auditoria de Equipagem"
```
