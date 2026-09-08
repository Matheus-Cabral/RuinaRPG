# Inventory Weight and Capacity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a real "Peso Atual / Peso Máximo" on Personagem and NPC's Inventário (Posses tab), warn when it's exceeded, and let the GM add "Capacidade Extra" (backpack-like) items that raise the max without adding their own weight to the current total — while fixing the pre-existing bug where the weight aggregation has never included the Inventário list at all.

**Architecture:** A new pure Domain calculator (`CarryWeightCalculator`) plus a signature change to the existing `SubAttributeFormulas.Movimentacao`; a new nullable `CapacidadeExtra` column on `ItemGeral` threaded through the existing `CreateItemRequest`/`UpdateItemRequest`/`ItemResponse` contracts; corrected weight-aggregation queries in `CharacterSheetsController`/`NpcSheetsController.SubAttributes`; two new fields on the shared `SubAttributesResponse` (null for Creature); markup-only additions to the Inventário sections of `FichaDePersonagem.razor`/`FichaDeNpc.razor` and the Item Geral field group of `CatalogoItemForm.razor`.

**Tech Stack:** ASP.NET Core 8 (EF Core/Npgsql), Blazor WebAssembly 8 (MudBlazor), xUnit (Unit + Integration).

**Spec:** `docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md`

## Global Constraints

- Creature (`CreatureSheetsController`/`FichaDeCriatura.razor`) is explicitly out of scope — its 5.a section is Espólios (loot), not a carried inventory. Its `SubAttributes` action's own weight-affecting behavior for Movimentação stays byte-for-byte unchanged; only its call site adapts to the new shared signature. Its `SubAttributesResponse.PesoAtual`/`PesoMaximo` are always `null`.
- `PesoAtual`/`PesoMaximo` are `decimal` end-to-end (never truncated to `int`) — `Item.Peso` is already `decimal` in the catalog.
- Armor (`CharacterArmorSlot`/`NpcArmorSlot`) never counts toward `PesoAtual` — an armor slot with an `ItemId` is inherently worn, there is no unequipped state for armor in this schema.
- Weapons/Shields count toward `PesoAtual` only when **not** equipped (`IsEquipped == false`).
- `CapacidadeExtra` lives on `ItemGeral` only — never on Arma/Armadura/Escudo/Artefato. A request that sends it for another Tipo is silently ignored, not rejected (matches this codebase's existing precedent for every other Tipo-specific field on the shared `CreateItemRequest`/`UpdateItemRequest`).
- An Inventário row whose item has a non-null, non-zero `CapacidadeExtra` is excluded from `PesoAtual` (its own `Peso × Qtd` doesn't count) and contributes `CapacidadeExtra × Qtd` to `PesoMaximo`. This exclusion is keyed on `CapacidadeExtra` being set, not on what `Peso` happens to be.
- Artefatos (5.b) stay outside the weight formula — unchanged from today, not something this plan adds.
- No changes to weapon/shield equip UI or endpoints — only how their weight is aggregated changes.

---

## Task 1: `CarryWeightCalculator` and `SubAttributeFormulas.Movimentacao`'s new signature

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/CarryWeightCalculator.cs`
- Create: `tests/RuinaRPG.Tests.Unit/CharacterSheets/CarryWeightCalculatorTests.cs`
- Modify: `src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs`
- Modify: `tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs`

**Interfaces:**
- Produces: `CarryWeightCalculator.PesoMaximo(int forca, int vigor, decimal capacidadeExtraTotal) -> decimal`
- Produces: `CarryWeightCalculator.CountsTowardPesoAtual(decimal? capacidadeExtra) -> bool`
- Produces: `SubAttributeFormulas.Movimentacao(int agilidade, int artefato, decimal pesoAtual, decimal pesoMaximo) -> int` (was `(int agilidade, int artefato, int pesoTotalCarregado, int forca, int vigor)` — the `piso((Força+Vigor)/2)` term moves into `CarryWeightCalculator.PesoMaximo`, computed by the caller before calling `Movimentacao`, since it now needs the Capacidade Extra term this pure formula function doesn't know about).
- Consumed by: Task 4 (`CharacterSheetsController`/`NpcSheetsController.SubAttributes`) and Task 5 (`CreatureSheetsController.SubAttributes`).

- [ ] **Step 1: Write the failing tests for `CarryWeightCalculator`**

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CarryWeightCalculatorTests
{
    [Fact]
    public void PesoMaximo_floors_Forca_plus_Vigor_over_2_and_adds_capacidade_extra()
    {
        // Limite de Carga = piso((Força+Vigor)/2) — Requisitos - Ficha de Personagem 2.b — plus
        // any Capacidade Extra from Inventário "mochila"-type items.
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 0m).Should().Be(4m); // floor(9/2)=4
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 10m).Should().Be(14m);
    }

    [Fact]
    public void CountsTowardPesoAtual_is_true_when_CapacidadeExtra_is_null()
    {
        CarryWeightCalculator.CountsTowardPesoAtual(null).Should().BeTrue();
    }

    [Fact]
    public void CountsTowardPesoAtual_is_true_when_CapacidadeExtra_is_zero()
    {
        CarryWeightCalculator.CountsTowardPesoAtual(0m).Should().BeTrue();
    }

    [Fact]
    public void CountsTowardPesoAtual_is_false_when_CapacidadeExtra_is_positive()
    {
        CarryWeightCalculator.CountsTowardPesoAtual(5m).Should().BeFalse();
    }
}
```

Create this file at `tests/RuinaRPG.Tests.Unit/CharacterSheets/CarryWeightCalculatorTests.cs`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~CarryWeightCalculatorTests"`
Expected: build error — `CarryWeightCalculator` does not exist. This is the correct red state for a new type (mirrors the same pattern this branch's earlier `AttributeDisplayOrder` task used) — confirm the error names the missing type, not something else.

- [ ] **Step 3: Implement `CarryWeightCalculator`**

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Limite de Carga (Requisitos - Ficha de Personagem 2.b) plus the "mochila" mechanic: an
/// Inventário item with a Capacidade Extra raises the max without adding its own weight to
/// PesoAtual (see docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md).
/// </summary>
public static class CarryWeightCalculator
{
    public static decimal PesoMaximo(int forca, int vigor, decimal capacidadeExtraTotal) =>
        Math.Floor((forca + vigor) / 2m) + capacidadeExtraTotal;

    public static bool CountsTowardPesoAtual(decimal? capacidadeExtra) =>
        capacidadeExtra is null or 0;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~CarryWeightCalculatorTests"`
Expected: 4/4 PASS.

- [ ] **Step 5: Update the 3 existing `Movimentacao` tests to the new signature, and add a fractional-rounding test**

Read `tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs` first — it has 6 existing test methods; only the 3 `Movimentacao_*` ones change. Replace those 3 methods and insert a 4th, in place:

```csharp
    [Fact]
    public void Movimentacao_applies_the_formula_with_no_sobrepeso()
    {
        // "Movimentação = (Agilidade × 2) + Artefato − Sobrepeso" — 2.b. PesoAtual 5 <= PesoMaximo
        // 10 → Sobrepeso 0.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoAtual: 5m, pesoMaximo: 10m);

        result.Should().Be(8); // (4*2) + 0 - 0
    }

    [Fact]
    public void Movimentacao_subtracts_sobrepeso_when_carried_weight_exceeds_the_limit()
    {
        // PesoAtual 10, PesoMaximo 4 → Sobrepeso = 10 - 4 = 6.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoAtual: 10m, pesoMaximo: 4m);

        result.Should().Be(2); // (4*2) + 0 - 6 = 2, above the floor of 1
    }

    [Fact]
    public void Movimentacao_never_goes_below_the_absolute_minimum_of_1()
    {
        var result = SubAttributeFormulas.Movimentacao(agilidade: 1, artefato: 0, pesoAtual: 100m, pesoMaximo: 2m);

        result.Should().Be(1);
    }

    [Fact]
    public void Movimentacao_rounds_a_fractional_sobrepeso_up_rather_than_truncating()
    {
        // PesoAtual 5.5, PesoMaximo 5 → Sobrepeso 0.5, rounded UP to 1 (not truncated to 0) — half a
        // kilo over the limit still costs a point. This is the precision fix over the old code's
        // `(int)` cast, which would have silently discarded the 0.5 and reported no penalty at all.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoAtual: 5.5m, pesoMaximo: 5m);

        result.Should().Be(7); // (4*2) + 0 - 1
    }
```

- [ ] **Step 6: Run the tests to verify the 3 updated ones and the new one fail (Movimentacao's signature hasn't changed yet)**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SubAttributeFormulasTests"`
Expected: build error — no overload of `Movimentacao` takes `pesoAtual`/`pesoMaximo` named arguments (the current signature is `pesoTotalCarregado, forca, vigor`).

- [ ] **Step 7: Change `Movimentacao`'s signature**

In `src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs`, replace:

```csharp
    public static int Movimentacao(int agilidade, int artefato, int pesoTotalCarregado, int forca, int vigor)
    {
        var limiteDeCarga = (forca + vigor) / 2;
        var sobrepeso = Math.Max(0, pesoTotalCarregado - limiteDeCarga);
        var raw = agilidade * 2 + artefato - sobrepeso;
        return Math.Max(1, raw);
    }
```

with:

```csharp
    public static int Movimentacao(int agilidade, int artefato, decimal pesoAtual, decimal pesoMaximo)
    {
        var sobrepeso = (int)Math.Ceiling(Math.Max(0m, pesoAtual - pesoMaximo));
        var raw = agilidade * 2 + artefato - sobrepeso;
        return Math.Max(1, raw);
    }
```

- [ ] **Step 8: Run the full Unit suite to verify everything passes**

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: this will currently FAIL to build — `CharacterSheetsController.cs`/`NpcSheetsController.cs`/`CreatureSheetsController.cs` still call `Movimentacao` with the old 5-argument shape. That's expected and gets fixed in Tasks 4 and 5; **do not** patch those controllers in this task. Instead, confirm narrowly:

Run: `dotnet build src/RuinaRPG.Domain tests/RuinaRPG.Tests.Unit/RuinaRPG.Tests.Unit.csproj -p:TreatWarningsAsErrors=false 2>&1 | grep -i "CharacterSheetsController\|NpcSheetsController\|CreatureSheetsController"` — the 3 controllers are outside these two projects, so a build scoped to just Domain + Tests.Unit succeeds; then run:

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SubAttributeFormulasTests|FullyQualifiedName~CarryWeightCalculatorTests"`
Expected: 8/8 PASS (4 `Movimentacao_*` + 4 `CarryWeightCalculator*`). Report in the task report that the full solution won't build again until Tasks 4/5 land — this is a deliberate, plan-ordered intermediate state, the same pattern the `ficha-de-personagem` Fase 1a plan used for its `MudTabs` shell.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/CarryWeightCalculator.cs \
        src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs \
        tests/RuinaRPG.Tests.Unit/CharacterSheets/CarryWeightCalculatorTests.cs \
        tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs
git commit -m "feat: add CarryWeightCalculator, give Movimentacao a decimal peso signature"
```

---

## Task 2: `CapacidadeExtra` — data model, contracts, `ItemsController`/`CampaignCatalogController`

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Items/ItemGeral.cs`
- Create: an EF Core migration (via `dotnet ef migrations add`)
- Modify: `src/RuinaRPG.Contracts/Items/CreateItemRequest.cs`
- Modify: `src/RuinaRPG.Contracts/Items/UpdateItemRequest.cs`
- Modify: `src/RuinaRPG.Contracts/Items/ItemResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/ItemsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs`
- Modify (mechanical — one more trailing arg per call site, no other change): `tests/RuinaRPG.Tests.Integration/Controllers/CreaturePossessionsControllerTests.cs`, `CreatureSheetsControllerTests.cs`, `CharacterArsenalControllerTests.cs`, `NpcPossessionsControllerTests.cs`, `CharacterPossessionsControllerTests.cs`, `CreatureArsenalControllerTests.cs`, `CharacterAttributesControllerTests.cs`, `NpcArsenalControllerTests.cs`, `ItemsControllerTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs` (3 new tests)

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `ItemGeral.CapacidadeExtra` (`decimal?`); `CreateItemRequest`/`UpdateItemRequest`/`ItemResponse`'s new trailing `decimal? CapacidadeExtra` parameter. Consumed by Task 3 (Catálogo UI) and Task 4 (`SubAttributes` aggregation, via `db.Set<ItemGeral>()`).

- [ ] **Step 1: Write the 3 new failing tests in `ItemsControllerTests.cs`**

- [ ] Read `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs` first (needed for the exact `PostItemAsync`/`MinimalItemGeral` helpers these tests call). Add these 3 tests anywhere after `Update_an_owned_item_returns_204_and_the_change_is_visible_on_list` (they use `PostItemAsync`, `MinimalItemGeral`, both already defined in this file):

```csharp
    [Fact]
    public async Task Create_an_ItemGeral_with_CapacidadeExtra_returns_it_in_the_response()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmCapExtra1", "itemcapextra1@teste.com");

        var request = new CreateItemRequest("ItemGeral", "Mochila de Couro", 1m, 40, null, "Equipamentos de Aventura", "Uma mochila resistente.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null, 10m);
        var response = await PostItemAsync(token, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.CapacidadeExtra.Should().Be(10m);
    }

    [Fact]
    public async Task Create_an_Arma_ignores_CapacidadeExtra_even_if_sent()
    {
        // CapacidadeExtra only makes sense for ItemGeral — sending it for another Tipo must be
        // silently ignored, matching how every other Tipo-specific field on this shared request
        // already behaves for a Tipo it doesn't apply to.
        var token = await RegisterGmAndGetTokenAsync("ItemGmCapExtra2", "itemcapextra2@teste.com");

        var request = new CreateItemRequest("Arma", "Espada Estranha", 1.5m, 50, null, "Espadas", null,
            "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, 10,
            null, null, null, null, null, null,
            null, null, null, null, 10m);
        var response = await PostItemAsync(token, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponse>();
        body!.CapacidadeExtra.Should().BeNull();
    }

    [Fact]
    public async Task Update_an_ItemGeral_changes_its_CapacidadeExtra()
    {
        var token = await RegisterGmAndGetTokenAsync("ItemGmCapExtra3", "itemcapextra3@teste.com");
        var createResponse = await PostItemAsync(token, MinimalItemGeral("Mochila"));
        var itemId = (await createResponse.Content.ReadFromJsonAsync<ItemResponse>())!.Id;

        var update = new UpdateItemRequest("Mochila", 1m, 40, null, "Equipamentos de Aventura", "Uma mochila resistente.",
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null,
            null, null, null, null, 8m);
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{itemId}") { Content = JsonContent.Create(update) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var listMessage = new HttpRequestMessage(HttpMethod.Get, "/api/items");
        listMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(listMessage);
        var body = await listResponse.Content.ReadFromJsonAsync<List<ItemResponse>>();
        body!.Single(i => i.Id == itemId).CapacidadeExtra.Should().Be(8m);
    }
```

- [ ] **Step 2: Run the tests to verify they fail to build**

Run: `dotnet build`
Expected: compile errors — `CreateItemRequest`/`UpdateItemRequest` don't have a 27th positional slot, and `ItemResponse` has no `CapacidadeExtra` member. This is the red state; the rest of this task's steps turn it green.

- [ ] **Step 3: Add the column to `ItemGeral`**

In `src/RuinaRPG.Infrastructure/Items/ItemGeral.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Items;

public class ItemGeral : Item
{
    public string? Subcategoria { get; set; }
    public string? Descricao { get; set; }
    public decimal? CapacidadeExtra { get; set; }
}
```

- [ ] **Step 4: Generate the migration**

Run: `dotnet ef migrations add AddItemGeralCapacidadeExtra --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`
Expected: a new migration file adding one nullable `numeric` column. No backfill needed — `NULL` is correct for every existing row (not a container).

- [ ] **Step 5: Extend `CreateItemRequest`**

In `src/RuinaRPG.Contracts/Items/CreateItemRequest.cs`, add one more parameter at the very end of the positional list:

```csharp
using System.ComponentModel.DataAnnotations;

namespace RuinaRPG.Contracts.Items;

public record CreateItemRequest(
    string Tipo,
    string Nome,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal Peso,
    [Range(0, int.MaxValue)] int Preco,
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
    [Range(0, int.MaxValue)] int? DurabilidadeMaxima,
    string? Categoria,
    int? Defesa,
    int? RF,
    int? RM,
    string? Penalidade,
    int? RequisitoVigor,
    int? BonusDefesa,
    string? TipoDeAlvo,
    string? Alvo,
    int? Valor,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? CapacidadeExtra);
```

- [ ] **Step 6: Extend `UpdateItemRequest`** the same way

In `src/RuinaRPG.Contracts/Items/UpdateItemRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace RuinaRPG.Contracts.Items;

public record UpdateItemRequest(
    string Nome,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal Peso,
    [Range(0, int.MaxValue)] int Preco,
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
    [Range(0, int.MaxValue)] int? DurabilidadeMaxima,
    string? Categoria,
    int? Defesa,
    int? RF,
    int? RM,
    string? Penalidade,
    int? RequisitoVigor,
    int? BonusDefesa,
    string? TipoDeAlvo,
    string? Alvo,
    int? Valor,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? CapacidadeExtra);
```

- [ ] **Step 7: Extend `ItemResponse`**

In `src/RuinaRPG.Contracts/Items/ItemResponse.cs`:

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
    int? Valor,
    decimal? CapacidadeExtra);
```

- [ ] **Step 8: Map it in `ItemsController.Create`**

In `src/RuinaRPG.Api/Controllers/ItemsController.cs`, in the `Item item = tipo switch { ... }` block, change:

```csharp
            ItemTipo.ItemGeral => new ItemGeral { Nome = request.Nome, Subcategoria = request.Subcategoria, Descricao = request.Descricao },
```

to:

```csharp
            ItemTipo.ItemGeral => new ItemGeral { Nome = request.Nome, Subcategoria = request.Subcategoria, Descricao = request.Descricao, CapacidadeExtra = request.CapacidadeExtra },
```

- [ ] **Step 9: Map it in `ItemsController.Update`**

In the same file's `Update` method, change:

```csharp
            case ItemGeral g:
                g.Subcategoria = request.Subcategoria;
                g.Descricao = request.Descricao;
                break;
```

to:

```csharp
            case ItemGeral g:
                g.Subcategoria = request.Subcategoria;
                g.Descricao = request.Descricao;
                g.CapacidadeExtra = request.CapacidadeExtra;
                break;
```

- [ ] **Step 10: Map it in `ItemsController.ToResponseAsync`**

In the same file's `ToResponseAsync`, every one of the 5 `switch` branches gains one more trailing argument — `g.CapacidadeExtra` for the `ItemGeral` branch, `null` for the other 4. Replace the whole `return item switch { ... }` block with:

```csharp
        return item switch
        {
            ItemGeral g => new ItemResponse(g.Id.ToString(), "ItemGeral", g.Nome, g.Peso, g.Preco, imageUrl,
                g.Subcategoria, g.Descricao, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, g.CapacidadeExtra),
            Arma a => new ItemResponse(a.Id.ToString(), "Arma", a.Nome, a.Peso, a.Preco, imageUrl,
                a.Subcategoria, null, a.Tier?.ToString(), a.Empunhadura?.ToString(), a.Dados, a.Dano, a.Critico, a.Alcance, a.TipoDeDano?.ToString(), a.RequisitoAtributo,
                a.DurabilidadeMaxima, null, null, null, null, null, null, null, null, null, null, null),
            Armadura ar => new ItemResponse(ar.Id.ToString(), "Armadura", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, ar.DurabilidadeMaxima,
                ar.Categoria?.ToString(), ar.Defesa, ar.RF, ar.RM, ar.Penalidade, ar.RequisitoVigor, null, null, null, null, null),
            Escudo e => new ItemResponse(e.Id.ToString(), "Escudo", e.Nome, e.Peso, e.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, e.DurabilidadeMaxima,
                e.Categoria?.ToString(), null, null, null, e.Penalidade, e.RequisitoVigor, e.BonusDefesa, null, null, null, null),
            Artefato ar => new ItemResponse(ar.Id.ToString(), "Artefato", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, ar.TipoDeAlvo?.ToString(), ar.Alvo, ar.Valor, null),
            _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
        };
```

- [ ] **Step 11: Apply the identical `ToItemResponseAsync` change in `CampaignCatalogController`**

Read `src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs` first — its `ToItemResponseAsync` private method is a byte-for-byte duplicate of `ItemsController.ToResponseAsync`'s switch (the file has a comment explaining why: "every controller in this codebase owns its own mapping"). Apply the exact same `return item switch { ... }` replacement as Step 10, in this file's `ToItemResponseAsync` method.

- [ ] **Step 12: Fix every other call site's positional argument count**

Every one of these already-existing `new CreateItemRequest(...)`/`new UpdateItemRequest(...)` calls gains one more trailing `null` (none of them need a real `CapacidadeExtra` value — only the 3 new tests from Step 1 do). For each file, find the exact line(s) below and insert `, null` immediately before the closing `)` of the constructor call (i.e. right before the last `)` that follows the final existing argument):

  - `tests/RuinaRPG.Tests.Integration/Controllers/CreaturePossessionsControllerTests.cs:56` — `new CreateItemRequest("ItemGeral", nome, peso, preco, null, "Diversos", "Um item qualquer", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)));` → append `, null` before the first `)`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CreaturePossessionsControllerTests.cs:63` — `new CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor)));` → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs:422` — `new CreateItemRequest("Armadura", "Peitoral de Testes", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, 12, "Medio", 5, rf, rm, "-1 Furtividade", 2, null, null, null, null)));` → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterArsenalControllerTests.cs:72` — `new CreateItemRequest("Arma", "Espada", 1.5m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, durabilidadeMaxima, null, null, null, null, null, null, null, null, null, null)));` → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterArsenalControllerTests.cs:79` — `new CreateItemRequest("Armadura", "Elmo de Ferro", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Medio", 5, 1, 1, "-1 Furtividade", 2, null, null, null, null)));` → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterArsenalControllerTests.cs:86` — `new CreateItemRequest("Escudo", "Broquel", 2m, 25, null, null, null, null, null, null, null, null, null, null, null, durabilidadeMaxima, "Leve", null, null, null, "-1 Agilidade", 1, 2, null, null, null)));` → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/NpcPossessionsControllerTests.cs:56` and `:63` — identical text to `CreaturePossessionsControllerTests.cs:56`/`:63` above, same fix.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterPossessionsControllerTests.cs:72` — identical text to `CreaturePossessionsControllerTests.cs:56`, same fix.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterPossessionsControllerTests.cs:79` — identical text to `CreaturePossessionsControllerTests.cs:63`, same fix.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterPossessionsControllerTests.cs:86` — `new CreateItemRequest("Arma", nome, 1.5m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, 20, null, null, null, null, null, null, null, null, null, null)));` → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CreatureArsenalControllerTests.cs:56`, `:63`, `:70` — identical text to `CharacterArsenalControllerTests.cs:72`/`:79`/`:86` respectively, same fixes.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAttributesControllerTests.cs:102` — identical text to `CreaturePossessionsControllerTests.cs:63`, same fix.
  - `tests/RuinaRPG.Tests.Integration/Controllers/NpcArsenalControllerTests.cs:56`, `:63`, `:70` — identical text to `CharacterArsenalControllerTests.cs:72`/`:79`/`:86` respectively, same fixes.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs:659` — `new RuinaRPG.Contracts.Items.CreateItemRequest("Armadura", "Peitoral de Testes", 3m, 30, null, null, null, null, null, null, null, null, null, null, null, 12, "Medio", 5, rf, rm, "-1 Furtividade", 2, null, null, null, null)));` (inside this file's own `CreateArmaduraItemAsync` helper) → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs:686` — `new RuinaRPG.Contracts.Items.CreateItemRequest("Artefato", nome, 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, tipoDeAlvo, alvo, valor)));` (inside `CreateArtefatoItemAsync`) → append `, null`.
  - `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs:469` — identical text to `CharacterSheetsControllerTests.cs:659` above (this file's own copy of the same helper) → append `, null`.

  In `tests/RuinaRPG.Tests.Integration/Controllers/ItemsControllerTests.cs`, fix these 7 sites (read the file first — 5 helper methods + 2 inline calls, all multi-line):
  - `MinimalItemGeral` (around line 52-56): the last line `null, null, null, null);` → `null, null, null, null, null);`
  - `MinimalArma` (around line 58-62): the last line `null, null, null, null);` → `null, null, null, null, null);`
  - `MinimalArmadura` (around line 64-68): the last line `null, null, null, null);` → `null, null, null, null, null);`
  - `MinimalEscudo` (around line 70-74): the last line `null, null, null, null);` → `null, null, null, null, null);`
  - `MinimalArtefato` (around line 76-80): the last line `null, "Atributo", "Força", 2);` → `null, "Atributo", "Força", 2, null);`
  - The inline `UpdateItemRequest` around line 296-299 (`Update_an_owned_item_returns_204...`): the last line `null, null, null, null);` → `null, null, null, null, null);`
  - The inline `UpdateItemRequest` at line 322 (`Update_a_item_owned_by_another_gm_returns_404`): `new UpdateItemRequest("Hack", 0m, 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);` → append `, null` before the final `);`.

- [ ] **Step 13: Run the tests to verify everything passes**

Run: `rm -rf $(find . -name obj -o -name bin | grep -v .worktrees); dotnet build`
Expected: 0 Warning(s), 0 Error(s).

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~ItemsControllerTests"`
Expected: all pass, including the 3 new ones from Step 1.

Run a broad scoped batch covering every file touched in Step 12, to catch any missed call site (2 batches to respect this sandbox's Testcontainers-contention limit):
`dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterPossessionsControllerTests|FullyQualifiedName~CharacterArsenalControllerTests|FullyQualifiedName~CharacterAttributesControllerTests|FullyQualifiedName~NpcPossessionsControllerTests|FullyQualifiedName~NpcArsenalControllerTests" -- xUnit.MaxParallelThreads=2`
then
`dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CreaturePossessionsControllerTests|FullyQualifiedName~CreatureArsenalControllerTests|FullyQualifiedName~CreatureSheetsControllerTests" -- xUnit.MaxParallelThreads=2`
Expected: all green, same pass counts as before this task (a regression here means a call site was fixed wrong, not just left broken — a wrong fix compiles but changes what these unrelated tests were asserting).

- [ ] **Step 14: Commit**

```bash
git add -A
git commit -m "feat: add CapacidadeExtra to Item Geral's catalog schema and contracts"
```

---

## Task 3: Capacidade Extra on the Catálogo UI

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`

**Interfaces:**
- Consumes: Task 2's `CreateItemRequest`/`UpdateItemRequest`/`ItemResponse.CapacidadeExtra`.
- Produces: nothing further consumed by later tasks (leaf task).

This page has no bUnit test file today for its Item Geral field group specifically (its existing `CatalogoItemFormTests.cs` covers auto-save wiring generically, not every field) — verification here is the build plus a manual read-through, matching this page's own established proportionality (see the autosave-on-blur plan's precedent for this exact file).

- [ ] **Step 1: Add the field to `ItemFormModel`**

Read `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor` first. In the `ItemFormModel` class, immediately after the `Descricao` property, add:

```csharp
        public string? Descricao { get; set; }
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal? CapacidadeExtra { get; set; }
```

(replacing just the `Descricao` line with these two lines — every other property in this class stays where it is).

- [ ] **Step 2: Populate it when loading an existing item**

In `LoadItemAsync` (or whatever the method populating `_form` from `existing` is called — confirm by reading the method this file's `_form.Descricao = existing.Descricao;` line lives in), immediately after that line, add:

```csharp
        _form.Descricao = existing.Descricao;
        _form.CapacidadeExtra = existing.CapacidadeExtra;
```

- [ ] **Step 3: Include it in `CreateAsync`'s request**

In `CreateAsync`, change:

```csharp
        var request = new CreateItemRequest(_form.Tipo, _form.Nome, _form.Peso, _form.Preco, _form.ImageId,
            _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
            _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
            _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
            _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor);
```

to:

```csharp
        var request = new CreateItemRequest(_form.Tipo, _form.Nome, _form.Peso, _form.Preco, _form.ImageId,
            _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
            _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
            _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
            _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor, _form.CapacidadeExtra);
```

- [ ] **Step 4: Include it in `SaveIfValidAsync`'s request**

In `SaveIfValidAsync`, apply the identical change to the `UpdateItemRequest` construction (same trailing `_form.CapacidadeExtra` argument):

```csharp
        var request = new UpdateItemRequest(_form.Nome, _form.Peso, _form.Preco, _form.ImageId,
            _form.Subcategoria, _form.Descricao, _form.Tier, _form.Empunhadura, _form.Dados, _form.Dano,
            _form.Critico, _form.Alcance, _form.TipoDeDano, _form.RequisitoAtributo, _form.DurabilidadeMaxima,
            _form.Categoria, _form.Defesa, _form.RF, _form.RM, _form.Penalidade, _form.RequisitoVigor,
            _form.BonusDefesa, _form.TipoDeAlvo, _form.Alvo, _form.Valor, _form.CapacidadeExtra);
```

- [ ] **Step 5: Add the field to the markup**

In the `@if (_form.Tipo == "ItemGeral")` block, change:

```razor
        <Section Title="Item Geral">
            <MudTextField T="string" @bind-Value="_form.Subcategoria" Label="Subcategoria" @bind-Value:after="NotifySavedAsync" />
            <MudTextField T="string" @bind-Value="_form.Descricao" Label="Descrição" Lines="3" @bind-Value:after="NotifySavedAsync" />
        </Section>
```

to:

```razor
        <Section Title="Item Geral">
            <MudTextField T="string" @bind-Value="_form.Subcategoria" Label="Subcategoria" @bind-Value:after="NotifySavedAsync" />
            <MudTextField T="string" @bind-Value="_form.Descricao" Label="Descrição" Lines="3" @bind-Value:after="NotifySavedAsync" />
            <MudNumericField T="decimal?" @bind-Value="_form.CapacidadeExtra" For="@(() => _form.CapacidadeExtra)" Label="Capacidade Extra" @bind-Value:after="NotifySavedAsync" />
        </Section>
```

- [ ] **Step 6: Build and verify**

Run: `rm -rf $(find . -name obj -o -name bin | grep -v .worktrees); dotnet build`
Expected: 0 Warning(s), 0 Error(s).

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~CatalogoItemFormTests"`
Expected: all still passing (this task is markup + trailing-argument only, no behavior these tests assert on changed).

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CatalogoItemForm.razor
git commit -m "feat: add Capacidade Extra field to the Catálogo's Item Geral form"
```

---

## Task 4: `CharacterSheetsController`/`NpcSheetsController.SubAttributes` — real `PesoAtual`/`PesoMaximo`

**Files:**
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs` (4 new tests)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs` (4 new tests, mirroring Character's)

**Interfaces:**
- Consumes: Task 1's `CarryWeightCalculator.PesoMaximo`/`CountsTowardPesoAtual`, `SubAttributeFormulas.Movimentacao`'s new signature; Task 2's `ItemGeral.CapacidadeExtra`.
- Produces: `SubAttributesResponse.PesoAtual`/`PesoMaximo` (`decimal?`, both non-null for Character/Npc). Consumed by Task 5 (Creature, which sets both `null`) and Task 6 (client display).

### Character side

- [ ] **Step 1: Write the 4 failing tests in `CharacterSheetsControllerTests.cs`**

Read `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs` first — it already has `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `CreateCampaignAsync`, `CreateSheetForMemberAsync`, `AuthedRequest`, and (further down) `CreateArmaduraItemAsync`/`CreateArtefatoItemAsync` private helpers. Add 3 new private helpers and 4 new `[Fact]`s near the existing `SubAttributes_*` tests:

```csharp
    private async Task<string> CreateItemGeralAsync(string gmToken, string nome, decimal peso, decimal? capacidadeExtra = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("ItemGeral", nome, peso, 5, null, "Diversos", "Um item qualquer",
                null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null,
                null, null, null, null, capacidadeExtra)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    private async Task<string> CreateArmaItemAsync(string gmToken, string nome, decimal peso)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Arma", nome, peso, 50, null, "Espadas", null,
                "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, 10,
                null, null, null, null, null, null,
                null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    private async Task<string> CreateEscudoItemAsync(string gmToken, string nome, decimal peso)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Escudo", nome, peso, 25, null, null, null,
                null, null, null, null, null, null, null, null, null,
                "Leve", null, null, null, "-1 Agilidade", 1,
                2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_includes_Inventario_items()
    {
        // The bug this task fixes: PesoAtual used to only sum Armas/Armaduras/Escudos, never the
        // Inventário list, contradicting Requisitos - Ficha de Personagem 2.b's own formula text.
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmPeso1", "sheetpeso1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerPeso1", "sheetplayerpeso1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Peso 1");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        var itemId = await CreateItemGeralAsync(gmToken, "Corda", peso: 2m);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken,
            new AddCharacterInventoryItemRequest(itemId, 3)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(6m); // 2 * 3
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_excludes_equipped_weapons_and_shields_but_includes_unequipped_ones()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmPeso2", "sheetpeso2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerPeso2", "sheetplayerpeso2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Peso 2");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var equippedWeaponItemId = await CreateArmaItemAsync(gmToken, "Espada Equipada", peso: 1.5m);
        var addEquippedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(equippedWeaponItemId)));
        var equippedWeaponId = (await addEquippedResponse.Content.ReadFromJsonAsync<CharacterWeaponResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/weapons/{equippedWeaponId}", playerToken, true));

        var reserveWeaponItemId = await CreateArmaItemAsync(gmToken, "Espada Reserva", peso: 1.5m);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/weapons", playerToken, new AddCharacterWeaponRequest(reserveWeaponItemId)));

        var shieldItemId = await CreateEscudoItemAsync(gmToken, "Escudo Guardado", peso: 2m);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/shields", playerToken, new AddCharacterShieldRequest(shieldItemId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(3.5m); // 1.5 (unequipped reserve weapon) + 2 (unequipped shield) — the equipped weapon is excluded
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_never_counts_armor()
    {
        // A CharacterArmorSlot with an ItemId is inherently worn — no unequipped state exists for
        // armor, so it never contributes to PesoAtual.
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmPeso3", "sheetpeso3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerPeso3", "sheetplayerpeso3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Peso 3");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, rf: 0, rm: 0);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/armor-slots/Capacete", playerToken,
            new UpdateCharacterArmorSlotRequest(armorItemId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(0m);
    }

    [Fact]
    public async Task SubAttributes_Capacidade_Extra_raises_PesoMaximo_and_its_own_Peso_is_excluded_from_PesoAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmPeso4", "sheetpeso4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerPeso4", "sheetplayerpeso4@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Peso 4");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken, new UpdateCharacterAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken, new UpdateCharacterAttributeRequest(4, 0, false)));

        var mochilaItemId = await CreateItemGeralAsync(gmToken, "Mochila", peso: 1m, capacidadeExtra: 10m);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/inventory", playerToken,
            new AddCharacterInventoryItemRequest(mochilaItemId, 2)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(0m); // the 2 mochilas' own Peso (1*2=2) is excluded
        body.PesoMaximo.Should().Be(24m); // floor((4+4)/2)=4, + (10*2)=20
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SubAttributes_PesoAtual|FullyQualifiedName~SubAttributes_Capacidade_Extra"`
Expected: build error — `SubAttributesResponse` has no `PesoAtual`/`PesoMaximo` members yet.

- [ ] **Step 3: Extend `SubAttributesResponse`**

In `src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs`, change:

```csharp
public record SubAttributesResponse(int Iniciativa, int Movimentacao, int EsquivaNatural, int DefesaNatural, int ReducaoFisica, int ReducaoMagica);
```

to:

```csharp
public record SubAttributesResponse(int Iniciativa, int Movimentacao, int EsquivaNatural, int DefesaNatural, int ReducaoFisica, int ReducaoMagica, decimal? PesoAtual, decimal? PesoMaximo);
```

- [ ] **Step 4: Fix `CharacterSheetsController.SubAttributes`'s weight aggregation and its `SubAttributesResponse` construction**

Read `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`'s `SubAttributes` method first. Replace:

```csharp
        var weapons = await db.CharacterWeapons.Where(w => w.CharacterSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var armorSlots = await db.CharacterArmorSlots.Where(a => a.CharacterSheetId == id && a.ItemId != null).Join(db.Items, a => a.ItemId!.Value, i => i.Id, (a, i) => i.Peso).ToListAsync();
        var shields = await db.CharacterShields.Where(s => s.CharacterSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => i.Peso).ToListAsync();
        var pesoTotalCarregado = weapons.Sum(w => w.Peso) + armorSlots.Sum() + shields.Sum();
```

with:

```csharp
        var weapons = await db.CharacterWeapons.Where(w => w.CharacterSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var shields = await db.CharacterShields.Where(s => s.CharacterSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => new { s.IsEquipped, i.Peso }).ToListAsync();
        var inventoryItems = await db.CharacterInventoryItems.Where(i => i.CharacterSheetId == id)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>(), i => i.ItemId, g => g.Id, (i, g) => new { i.Qtd, g.Peso, g.CapacidadeExtra })
            .ToListAsync();

        // Peso Total 2.b (corrected — see docs/superpowers/specs/2026-09-08-inventory-weight-and-
        // capacity-design.md): Inventário (5.a) + Armas/Escudos DESequipados. Armaduras never count
        // — a CharacterArmorSlot with an ItemId is inherently worn (no unequipped state exists for
        // armor in this schema). A Capacidade Extra ("mochila") item doesn't add its own Peso here —
        // it raises pesoMaximo instead.
        var pesoAtual = inventoryItems.Where(i => CarryWeightCalculator.CountsTowardPesoAtual(i.CapacidadeExtra)).Sum(i => i.Peso * i.Qtd)
            + weapons.Where(w => !w.IsEquipped).Sum(w => w.Peso)
            + shields.Where(s => !s.IsEquipped).Sum(s => s.Peso);
        var capacidadeExtraTotal = inventoryItems.Sum(i => (i.CapacidadeExtra ?? 0m) * i.Qtd);
        var pesoMaximo = CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal);
```

Then replace:

```csharp
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), (int)pesoTotalCarregado, forca, vigor),
```

with:

```csharp
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
```

And replace the final two lines of the `return new SubAttributesResponse(...)`:

```csharp
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm));
```

with:

```csharp
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo);
```

- [ ] **Step 5: Run the Character tests**

Run: `dotnet build`
Expected: `NpcSheetsController.cs`/`CreatureSheetsController.cs` still won't build (unfixed `Movimentacao` call + missing `SubAttributesResponse` args) — expected, fixed in this task's Npc steps below and Task 5.

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests" -- xUnit.MaxParallelThreads=2`
Expected: this specific class won't run yet either, since the whole `RuinaRPG.Tests.Integration` project must build first, and `NpcSheetsController`/`CreatureSheetsController` are still broken. Proceed directly to the Npc steps below before running any tests — this task's Character and Npc halves land together.

### Npc side (same task, same shape)

- [ ] **Step 6: Write the 4 mirrored failing tests in `NpcSheetsControllerTests.cs`**

Read `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs` first — it uses `RegisterGmAndGetTokenAsync`, `CreateSheetAsync(gmToken)` (no campaign/member needed), `AuthedRequest`. Add the same 3 helpers and 4 tests as Steps 1, with these mechanical differences: `npc-sheets` routes instead of `character-sheets`; `UpdateNpcAttributeRequest`/`AddNpcInventoryItemRequest`/`AddNpcWeaponRequest`/`AddNpcShieldRequest`/`NpcWeaponResponse`/`UpdateNpcArmorSlotRequest` instead of the Character contracts; `gmToken` in place of `playerToken` for the acting caller (NPC sheets are GM-owned, no separate player/campaign setup); no `campaignId`/`CreateCampaignAsync`/`AddCampaignMemberRequest` calls needed. Full text:

```csharp
    private async Task<string> CreateItemGeralAsync(string gmToken, string nome, decimal peso, decimal? capacidadeExtra = null)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("ItemGeral", nome, peso, 5, null, "Diversos", "Um item qualquer",
                null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null,
                null, null, null, null, capacidadeExtra)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    private async Task<string> CreateArmaItemAsync(string gmToken, string nome, decimal peso)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Arma", nome, peso, 50, null, "Espadas", null,
                "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, 10,
                null, null, null, null, null, null,
                null, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    private async Task<string> CreateEscudoItemAsync(string gmToken, string nome, decimal peso)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", gmToken,
            new RuinaRPG.Contracts.Items.CreateItemRequest("Escudo", nome, peso, 25, null, null, null,
                null, null, null, null, null, null, null, null, null,
                "Leve", null, null, null, "-1 Agilidade", 1,
                2, null, null, null, null)));
        return (await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Items.ItemResponse>())!.Id;
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_includes_Inventario_items()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmPeso1", "npcpeso1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var itemId = await CreateItemGeralAsync(gmToken, "Corda", peso: 2m);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmToken,
            new AddNpcInventoryItemRequest(itemId, 3)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(6m);
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_excludes_equipped_weapons_and_shields_but_includes_unequipped_ones()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmPeso2", "npcpeso2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var equippedWeaponItemId = await CreateArmaItemAsync(gmToken, "Espada Equipada", peso: 1.5m);
        var addEquippedResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken, new AddNpcWeaponRequest(equippedWeaponItemId)));
        var equippedWeaponId = (await addEquippedResponse.Content.ReadFromJsonAsync<NpcWeaponResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/weapons/{equippedWeaponId}", gmToken, true));

        var reserveWeaponItemId = await CreateArmaItemAsync(gmToken, "Espada Reserva", peso: 1.5m);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/weapons", gmToken, new AddNpcWeaponRequest(reserveWeaponItemId)));

        var shieldItemId = await CreateEscudoItemAsync(gmToken, "Escudo Guardado", peso: 2m);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/shields", gmToken, new AddNpcShieldRequest(shieldItemId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(3.5m);
    }

    [Fact]
    public async Task SubAttributes_PesoAtual_never_counts_armor()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmPeso3", "npcpeso3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var armorItemId = await CreateArmaduraItemAsync(gmToken, rf: 0, rm: 0);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/armor-slots/Capacete", gmToken,
            new UpdateNpcArmorSlotRequest(armorItemId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(0m);
    }

    [Fact]
    public async Task SubAttributes_Capacidade_Extra_raises_PesoMaximo_and_its_own_Peso_is_excluded_from_PesoAtual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmPeso4", "npcpeso4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Forca", gmToken, new UpdateNpcAttributeRequest(4, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/attributes/Vigor", gmToken, new UpdateNpcAttributeRequest(4, 0, false)));

        var mochilaItemId = await CreateItemGeralAsync(gmToken, "Mochila", peso: 1m, capacidadeExtra: 10m);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/inventory", gmToken,
            new AddNpcInventoryItemRequest(mochilaItemId, 2)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.PesoAtual.Should().Be(0m);
        body.PesoMaximo.Should().Be(24m);
    }
```

A `CreateArmaduraItemAsync(string gmToken, int rf, int rm)` helper already exists in this file (used by an existing armor-RF/RM test) — reuse it as-is, don't redefine it.

- [ ] **Step 7: Fix `NpcSheetsController.SubAttributes`**

Read `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`'s `SubAttributes` method first. Replace:

```csharp
        var weapons = await db.NpcWeapons.Where(w => w.NpcSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var armorSlots = await db.NpcArmorSlots.Where(a => a.NpcSheetId == id && a.ItemId != null).Join(db.Items, a => a.ItemId!.Value, i => i.Id, (a, i) => i.Peso).ToListAsync();
        var shields = await db.NpcShields.Where(s => s.NpcSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => i.Peso).ToListAsync();
        var pesoTotalCarregado = weapons.Sum(w => w.Peso) + armorSlots.Sum() + shields.Sum();
```

with:

```csharp
        var weapons = await db.NpcWeapons.Where(w => w.NpcSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
        var shields = await db.NpcShields.Where(s => s.NpcSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => new { s.IsEquipped, i.Peso }).ToListAsync();
        var inventoryItems = await db.NpcInventoryItems.Where(i => i.NpcSheetId == id)
            .Join(db.Set<RuinaRPG.Infrastructure.Items.ItemGeral>(), i => i.ItemId, g => g.Id, (i, g) => new { i.Qtd, g.Peso, g.CapacidadeExtra })
            .ToListAsync();

        // Peso Total 2.b (corrected — see docs/superpowers/specs/2026-09-08-inventory-weight-and-
        // capacity-design.md): Inventário (5.a) + Armas/Escudos DESequipados. Armaduras never count
        // — an NpcArmorSlot with an ItemId is inherently worn (no unequipped state exists for armor
        // in this schema). A Capacidade Extra ("mochila") item doesn't add its own Peso here — it
        // raises pesoMaximo instead.
        var pesoAtual = inventoryItems.Where(i => CarryWeightCalculator.CountsTowardPesoAtual(i.CapacidadeExtra)).Sum(i => i.Peso * i.Qtd)
            + weapons.Where(w => !w.IsEquipped).Sum(w => w.Peso)
            + shields.Where(s => !s.IsEquipped).Sum(s => s.Peso);
        var capacidadeExtraTotal = inventoryItems.Sum(i => (i.CapacidadeExtra ?? 0m) * i.Qtd);
        var pesoMaximo = CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal);
```

Then replace:

```csharp
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: 0, (int)pesoTotalCarregado, forca, vigor),
```

with:

```csharp
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: 0, pesoAtual, pesoMaximo),
```

And replace the final two lines of the `return new SubAttributesResponse(...)` (read the rest of the method to find its exact `ReducaoFisica`/`ReducaoMagica` lines — they mirror `CharacterSheetsController`'s exactly, with `artefato: 0` everywhere since NPC has no Artefatos term):

```csharp
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: 0, armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: 0, armaduraMagica: armaduraRm));
```

with:

```csharp
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: 0, armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: 0, armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo);
```

- [ ] **Step 8: Run the full build and the scoped tests**

Run: `rm -rf $(find . -name obj -o -name bin | grep -v .worktrees); dotnet build`
Expected: `CreatureSheetsController.cs` still won't build (unfixed in this task, deliberately — Task 5 fixes it). Confirm the only remaining errors are in that one file.

Run (Character + Npc together, both now use `CarryWeightCalculator`/the new `Movimentacao` signature and don't touch Creature):
`dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests|FullyQualifiedName~NpcSheetsControllerTests" -- xUnit.MaxParallelThreads=2`

If the build genuinely can't proceed with `CreatureSheetsController.cs` broken (a single-project build failure blocks every test in the solution, since `RuinaRPG.Tests.Integration` references `RuinaRPG.Api` as a whole), do Task 5's Step 1 (write Creature's failing test) now too, then Task 5's remaining steps, before running any tests here — report this ordering deviation in the task report; it doesn't change what either task delivers, only which commit contains the fix that lets the build succeed. Expected once both are done: all Character/Npc/Creature SubAttributes tests pass.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs \
        src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs \
        src/RuinaRPG.Api/Controllers/NpcSheetsController.cs \
        tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs \
        tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs
git commit -m "fix: PesoAtual now includes Inventário, add Capacidade Extra to PesoMaximo"
```

(If Task 5's files were touched too in order to get a green build, that's fine — Task 5 makes its own commit for its own files; don't commit `CreatureSheetsController.cs`/`CreatureSheetsControllerTests.cs` here even if you edited them out of necessity — `git add` only the files listed above.)

---

## Task 5: `CreatureSheetsController.SubAttributes` — mechanical adaptation, stays out of scope

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs` (extend 1 existing test)

**Interfaces:**
- Consumes: Task 1's `CarryWeightCalculator.PesoMaximo`, `Movimentacao`'s new signature.
- Produces: nothing further — leaf task for the backend half of this plan.

This is deliberately NOT a new feature for Creature — Espólios (5.a) is loot, not carried inventory (see the spec). Creature's own weight-affecting behavior for Movimentação (weapons + armor slots + shields, unfiltered by equip state) stays byte-for-byte what it computes today; only the call-site shape adapts to the new shared signature, and the two new response fields are explicitly `null`.

- [ ] **Step 1: Extend the existing `SubAttributes_computes_from_attributes_and_arsenal` test**

Read `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs`'s `SubAttributes_computes_from_attributes_and_arsenal` test. Immediately after its existing assertion:

```csharp
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.Movimentacao.Should().Be(8); // (4*2) + 0 artefato - 0 sobrepeso (nothing carried yet)
```

add:

```csharp
        body.PesoAtual.Should().BeNull(); // Espólios is loot, not carried inventory — see docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md
        body.PesoMaximo.Should().BeNull();
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SubAttributes_computes_from_attributes_and_arsenal&FullyQualifiedName~Creature"`
Expected: build error (the `PesoAtual`/`PesoMaximo` members exist on `SubAttributesResponse` from Task 4, but `CreatureSheetsController.SubAttributes` doesn't build yet — its `Movimentacao` call and its `SubAttributesResponse` construction are both still the old shape).

- [ ] **Step 3: Fix `CreatureSheetsController.SubAttributes`**

Read the method first — the weapons/armorSlots/shields/`pesoTotalCarregado` block stays **completely unchanged**. Only these two edits: replace

```csharp
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: 0, (int)pesoTotalCarregado, forca, vigor),
```

with:

```csharp
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: 0, pesoAtual: pesoTotalCarregado, pesoMaximo: CarryWeightCalculator.PesoMaximo(forca, vigor, capacidadeExtraTotal: 0m)),
```

and replace the final two lines:

```csharp
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: 0, armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: 0, armaduraMagica: armaduraRm));
```

with:

```csharp
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: 0, armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: 0, armaduraMagica: armaduraRm),
            // Espólios (5.a) is loot dropped when defeated, not a carried inventory — out of scope
            // for this feature. See docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md.
            PesoAtual: null,
            PesoMaximo: null);
```

- [ ] **Step 4: Run the full build and tests**

Run: `rm -rf $(find . -name obj -o -name bin | grep -v .worktrees); dotnet build`
Expected: 0 Warning(s), 0 Error(s) — the whole solution builds clean for the first time since Task 1.

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CreatureSheetsControllerTests" -- xUnit.MaxParallelThreads=2`
Expected: all pass.

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: all pass (confirms Task 1's `Movimentacao`/`CarryWeightCalculator` tests, unblocked now that every caller compiles).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs \
        tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs
git commit -m "fix: adapt CreatureSheetsController to Movimentacao's new signature, PesoAtual/Maximo stay null"
```

---

## Task 6: Client UI — Peso Atual/Máximo and overweight warning on Personagem/NPC's Inventário

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`

**Interfaces:**
- Consumes: Task 4's `SubAttributesResponse.PesoAtual`/`PesoMaximo`.
- Produces: nothing further — leaf task, closes this plan.

Both pages already load `SubAttributesResponse` into a page-level `_subAttributes` field during `OnInitializedAsync` (consumed today only by their "Sub-Atributos" section) — no new HTTP call needed, this task only adds markup reading the two new fields a second place. No bUnit coverage needed for either page (neither has a harness for this kind of full-page state today, matching this plan's Task 3 precedent).

- [ ] **Step 1: Add the display to `FichaDePersonagem.razor`'s Inventário section**

Read the file first. In the `<Section Title="Inventário">` block (inside the "Posses" tab panel), change:

```razor
        <Section Title="Inventário">
            <MudNumericField T="int?" Value="@_form.Ciclos" ValueChanged="@(v => UpdateCiclosAsync(v))" Label="Ciclos" Style="width:8em;" />
```

to:

```razor
        <Section Title="Inventário">
            @if (_subAttributes?.PesoAtual is not null && _subAttributes.PesoMaximo is not null)
            {
                <MudText>Peso: @_subAttributes.PesoAtual / @_subAttributes.PesoMaximo</MudText>
                @if (_subAttributes.PesoAtual > _subAttributes.PesoMaximo)
                {
                    <MudAlert Severity="Severity.Warning" Class="mt-1">Sobrecarregado — reduz Movimentação.</MudAlert>
                }
            }
            <MudNumericField T="int?" Value="@_form.Ciclos" ValueChanged="@(v => UpdateCiclosAsync(v))" Label="Ciclos" Style="width:8em;" />
```

- [ ] **Step 2: Apply the identical change to `FichaDeNpc.razor`**

Read the file first. In its `<Section Title="Inventário">` block, apply the exact same change as Step 1 (this page's `Ciclos` line is identical: `<MudNumericField T="int?" Value="@_form.Ciclos" ValueChanged="@(v => UpdateCiclosAsync(v))" Label="Ciclos" Style="width:8em;" />`).

- [ ] **Step 3: Build and verify**

Run: `rm -rf $(find . -name obj -o -name bin | grep -v .worktrees); dotnet build`
Expected: 0 Warning(s), 0 Error(s).

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: all still passing — markup-only change, no `@code` touched.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: show Peso Atual/Máximo and an overweight warning on Personagem/NPC's Inventário"
```

## Final check (after all 6 tasks)

Run the **full** suite before finishing the branch: `dotnet build` (0 Warning(s), 0 Error(s)) and `dotnet test` (Unit, Client, and a scoped Integration run covering at minimum `ItemsControllerTests`, `CharacterSheetsControllerTests`, `NpcSheetsControllerTests`, `CreatureSheetsControllerTests`, and every file touched in Task 2 Step 12 — the full Integration suite is chronically flaky in this sandbox from Testcontainers/Docker contention, see this repo's own standing note on that). Then follow `superpowers:finishing-a-development-branch`.
