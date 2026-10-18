# Versão 2.0.0 — D: Backend consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse the per-sheet-kind (Personagem/NPC/Criatura) controllers, stats services and integration tests into generic code parameterized by sheet kind, with identical HTTP behavior and an untouched database, and remove the `/api/rulebook` N+1.

**Architecture:** Per-kind child entities expose their FK as the C# property `SheetId` (column names unchanged via `HasColumnName`) and implement small per-feature interfaces. An `ITipoDeFicha<TSheet>` adapter per kind carries loading, authorization (Personagem → 403 via campaign GM; NPC/Criatura → 404 via `GrantedSheetAuthorization`) and rule flags. Abstract generic controllers in `Controllers/Fichas/` hold the logic; each kind keeps a sealed subclass with its current route and `Contracts` mapping. A route-inventory test and a rulebook frozen-output test, captured before any change, prove the HTTP surface did not move.

**Tech Stack:** .NET 8, ASP.NET Core MVC attribute routing, EF Core 8 / Npgsql, xUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-18-versao-2-0-0-d-backend-design.md`

## Global Constraints

- Same behavior for the end user: same URLs, HTTP methods, status codes, JSON bodies and error strings.
- `src/RuinaRPG.Contracts` and `src/RuinaRPG.Client` are **not modified** in D.
- No migration is added; the database schema does not change (`HasPendingModelChanges()` false; `tools/migrations/verificar-baseline.sh 729dac9` exits 0).
- Tasks 1–10 do not modify any existing test **except** the mechanical rename of Task 3 (`XxxSheetId` → `SheetId` on the renamed entities; identifier-only diff). A test that seems to need any other edit means behavior changed: revert the code change.
- Tasks 11–12 change tests only, never `src/`.
- Divergence rule (spec D.5): real difference → named hook/flag; cosmetic → unified; accidental → kept via hook and appended to the spec's "Divergências encontradas" list, never fixed in D.
- `dotnet build`: 0 warnings, 0 errors. TDD (Técnico R0011) for new behavior (Tasks 1, 2).
- Code comments and commit messages in Brazilian Portuguese, matching the repository; identifiers follow existing style (Portuguese domain names).
- Production (192.168.80.10) and the running `ruinarpg` stack are never touched; measurements use `tools/bench/` throwaway projects.
- Commit messages end with the session's attribution lines (given in the dispatch).

## Review Focus

1. **Status code per kind on denied access** — Personagem answers 403 (`Forbid`), NPC/Criatura answer 404, also for every endpoint moved into a generic base. Pinned by the route inventory (shape) and existing per-kind tests (codes); Task 4 adds one generic theory per moved controller family asserting the denied-status per kind.
2. **Check order** — unknown Perícia key (404) is checked before the sheet lookup; a sheet that exists but is denied must never reveal 400 validation errors. Base classes keep the original order; Task 4's theory covers "unknown key + foreign sheet → 404".
3. **Routes of `CharacterSheetsController`** use absolute templates (`api/campaigns/{campaignId}/character-sheets`, `~/api/character-sheets/mine`); putting a class-level `[Route]` on it without rewriting those as `/api/...` silently changes URLs. Caught by the route inventory (Task 10).
4. **Row order in `/api/rulebook` equipagem** — today each kit's items come from an unordered per-kit query; the batched query must not add an `OrderBy` and must keep the per-kit relative order of a single fetch. Pinned by the frozen-output test (Task 1/2).
5. **`SheetLogInterceptor` / `SheetLogMap`** read per-kind FK properties by name to attribute log entries to a sheet; renaming the CLR property must not break sheet logs. Task 3 runs `SheetLogs` tests and greps for string-based property names (`"CharacterSheetId"` etc.) before and after.

---

## File structure

- Create `src/RuinaRPG.Infrastructure/Fichas/IFicha.cs` — `IFicha` + per-feature child interfaces.
- Create `src/RuinaRPG.Api/Controllers/Fichas/ITipoDeFicha.cs`, `TipoPersonagem.cs`, `TipoNpc.cs`, `TipoCriatura.cs` — the adapter.
- Create `src/RuinaRPG.Api/Controllers/Fichas/FichaControllerBase.cs` — shared auth/user helpers.
- Create one abstract generic controller per feature in `src/RuinaRPG.Api/Controllers/Fichas/` (`PericiasDaFichaController.cs`, `AtributosDaFichaController.cs`, `MaestriasDaFichaController.cs`, `ArsenalDaFichaController.cs`, `InventarioDaFichaController.cs`, `ArtefatosDaFichaController.cs`, `AfeicoesDaFichaController.cs`, `CaracteristicasDaFichaController.cs`, `TracosRaciaisDaFichaController.cs`, `MagiasDaFichaController.cs`, `RunasDaFichaController.cs`, `AfinidadesDaFichaController.cs`, `EquipagemDaFichaController.cs`, `FichasController.cs`).
- Modify the per-kind controllers in `src/RuinaRPG.Api/Controllers/` into sealed subclasses (Possessions split per sub-resource; the old files are deleted when emptied).
- Create `src/RuinaRPG.Api/Services/FichaStats.cs`; slim `CharacterSheetStats.cs`, `NpcSheetStats.cs`, `CreatureSheetStats.cs`.
- Modify `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs` (`BuildEquipagemAsync`).
- Modify `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` and the per-kind child entities (rename).
- Create `tests/RuinaRPG.Tests.Integration/Controllers/RotasDaApiTests.cs` + `rotas-da-api.txt`.
- Create `tests/RuinaRPG.Tests.Integration/Rules/RulebookEquipagemCongeladoTests.cs` + `rulebook-equipagem.json`.
- Create `tests/RuinaRPG.Tests.Integration/Controllers/Fichas/` (generic test classes, Tasks 11–12) and `docs/superpowers/specs/2026-10-18-d-inventario-de-testes.md`.

---

### Task 1: Safety nets — route inventory and rulebook frozen output

**Files:**
- Create: `tests/RuinaRPG.Tests.Integration/Controllers/RotasDaApiTests.cs`
- Create: `tests/RuinaRPG.Tests.Integration/Controllers/rotas-da-api.txt` (generated, committed)
- Create: `tests/RuinaRPG.Tests.Integration/Rules/RulebookEquipagemCongeladoTests.cs`
- Create: `tests/RuinaRPG.Tests.Integration/Rules/rulebook-equipagem.json` (generated, committed)
- Modify: `tests/RuinaRPG.Tests.Integration/RuinaRPG.Tests.Integration.csproj` (copy the two data files to output)

**Interfaces:**
- Produces: `RotasDaApiTests.Inventario()` — static helper returning the sorted inventory lines; the committed `rotas-da-api.txt`; the committed `rulebook-equipagem.json`. Every later task runs both tests.

- [ ] **Step 1: Record "Before D" measurements** (no code change). Run on the current HEAD and keep the result files (git-ignored) for Task 13:

```bash
tools/bench/measure-code-size.sh antes-d
tools/bench/measure-build-and-tests.sh antes-d
tools/bench/measure-api-latency.sh antes-d
```

Expected: result files under `tools/bench/results/` containing `lines.RuinaRPG.Api`, build and test wall times, and `/api/rulebook` p50 ≈ 70 ms.

- [ ] **Step 2: Write the route inventory test**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace RuinaRPG.Tests.Integration.Controllers;

// Rede de segurança do sub-projeto D (spec D, "Route inventory"): a superfície HTTP da API —
// verbo, rota, parâmetros, corpo, resposta e autorização de cada ação — tem de continuar
// idêntica à capturada antes da consolidação. O nome do controller fica de fora de propósito:
// mudar de classe é permitido, mudar o contrato não.
public class RotasDaApiTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Arquivo = "Controllers/rotas-da-api.txt";

    [Fact]
    public void A_superficie_da_api_continua_a_mesma()
    {
        var esperado = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, Arquivo));
        var atual = Inventario(Factory.Services);
        Assert.Equal(esperado, atual);
    }

    public static string[] Inventario(IServiceProvider services) =>
        services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .SelectMany(Linhas)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> Linhas(ControllerActionDescriptor a)
    {
        var verbos = a.ActionConstraints?.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
            .SelectMany(c => c.HttpMethods).ToList() ?? [];
        if (verbos.Count == 0) verbos.Add("ANY");
        var parametros = string.Join(", ", a.Parameters.Select(p =>
            $"{p.Name}:{Nome(p.ParameterType)}:{p.BindingInfo?.BindingSource?.Id ?? "-"}"));
        var autorizacao = string.Join(";", a.EndpointMetadata.OfType<IAuthorizeData>()
                .Select(d => $"authorize(policy={d.Policy},roles={d.Roles})").Distinct().Order(StringComparer.Ordinal)
            .Concat(a.EndpointMetadata.OfType<IAllowAnonymous>().Any() ? ["anonymous"] : []));
        foreach (var verbo in verbos)
            yield return $"{verbo} /{a.AttributeRouteInfo?.Template} ({parametros}) -> {Nome(a.MethodInfo.ReturnType)} [{autorizacao}]";
    }

    private static string Nome(Type t) => t.IsGenericType
        ? $"{t.Name[..t.Name.IndexOf('`')]}<{string.Join(",", t.GetGenericArguments().Select(Nome))}>"
        : t.Name;
}
```

Add to the `.csproj` (inside an `<ItemGroup>`):

```xml
<None Update="Controllers/rotas-da-api.txt" CopyToOutputDirectory="PreserveNewest" />
<None Update="Rules/rulebook-equipagem.json" CopyToOutputDirectory="PreserveNewest" />
```

- [ ] **Step 3: Run it to verify it fails** (file missing)

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RotasDaApiTests`
Expected: FAIL with `FileNotFoundException` for `rotas-da-api.txt`.

- [ ] **Step 4: Generate the snapshot from the current code.** Temporarily add this test to the class, run it once, then delete it (it must not be committed):

```csharp
[Fact]
public void Gerar() => File.WriteAllLines(
    Path.Combine(AppContext.BaseDirectory, "../../../", Arquivo), Inventario(Factory.Services));
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RotasDaApiTests.Gerar`, delete the `Gerar` method, then inspect the file: `wc -l tests/RuinaRPG.Tests.Integration/Controllers/rotas-da-api.txt` (expected several hundred lines) and `grep -c "npc-sheets" …` > 0, and confirm lines for `GET /api/npc-sheets/{sheetId}/skills` and `PUT /api/character-sheets/{sheetId}/skills/{pericia}` exist.

- [ ] **Step 5: Run it to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RotasDaApiTests`
Expected: PASS (1 test).

- [ ] **Step 6: Write the rulebook frozen-output test.** It seeds one extra kit that exercises every branch of `BuildEquipagemAsync` (legacy item without `FixedItemId`, item with a fixed item, choice slot with and without bonus, `Ciclos > 0`) on top of the seeded kits, then compares the `equipagem` document to the committed JSON. Read `src/RuinaRPG.Infrastructure/Items/` for the exact `EquipmentKit`, `EquipmentKitItem`, `EquipmentKitChoiceSlot`, `EquipmentKitFixedItem` property names (the renderer uses `KitId`, `FixedItemId`, `Nome`, `Qtd`, `Label`, `BonusFixedItemId`, `BonusNome`, `BonusQtd`, `BonusSubcategoria`, `Descricao`, `Ciclos`, `IsDeleted`) and fill any other required properties with fixed literal values (no `Guid.NewGuid()` in rendered text, fixed Guids for ids):

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Rules;

// Rede de segurança do D.6: o documento "equipagem" do Livro de Regras tem de sair byte a byte
// igual ao de antes da troca das consultas por kit por uma consulta só.
public class RulebookEquipagemCongeladoTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Arquivo = "Rules/rulebook-equipagem.json";

    [Fact]
    public async Task O_documento_equipagem_continua_identico()
    {
        await SemearKitDeTodosOsCasosAsync();
        var atual = await ObterAsync();
        var esperado = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, Arquivo));
        Assert.Equal(esperado, atual);
    }

    private async Task<string> ObterAsync()
    {
        var documento = await Client.GetFromJsonAsync<RulebookDocumentResponse>("/api/rulebook/equipagem");
        return JsonSerializer.Serialize(documento, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task SemearKitDeTodosOsCasosAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        // Kit "Zz Kit de Teste" (fica por último na ordenação por Nome) com:
        //  - um EquipmentKitFixedItem fixo (Id 00000000-0000-0000-0000-0000000000f1, Nome "Corda de teste");
        //  - um item ligado ao fixo (Qtd 2) e um item legado sem FixedItemId (Nome "Tocha legada", Qtd 3);
        //  - um slot de escolha sem bônus (Label "Arma simples") e um com bônus ligado ao fixo
        //    (Label "Armadura", BonusQtd 1, BonusSubcategoria "Leve");
        //  - Ciclos = 2, Descricao com uma quebra de linha ("Linha 1\nLinha 2 & <b>").
        // (Construa as entidades com esses valores; ids fixos 00000000-0000-0000-0000-0000000000a1..a9.)
        await db.SaveChangesAsync();
    }
}
```

The comment block above lists the exact values; write the entity construction lines with those values (the property names come from the renderer code quoted in this step).

- [ ] **Step 7: Run it to verify it fails** — `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RulebookEquipagemCongeladoTests` → FAIL, file missing.

- [ ] **Step 8: Generate the JSON snapshot** with a temporary `Gerar` test (same pattern as Step 4: seed, `ObterAsync()`, write to `../../../Rules/rulebook-equipagem.json`), run it, delete it. Inspect the file: it contains `"Zz Kit de Teste"`, `Tocha legada x3`, `Corda de teste x2`, `(escolha)`, `+2 Ciclos`, and `Linha 1&lt;br /&gt;`-free HTML (`Linha 1<br />Linha 2 &amp; &lt;b&gt;`).

- [ ] **Step 9: Run both tests 3 times in a row to make sure they are stable**

Run: `for i in 1 2 3; do dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RotasDaApiTests|FullyQualifiedName~RulebookEquipagemCongeladoTests" || break; done`
Expected: PASS 3×.

- [ ] **Step 10: Commit**

```bash
git add tests/RuinaRPG.Tests.Integration/Controllers/RotasDaApiTests.cs tests/RuinaRPG.Tests.Integration/Controllers/rotas-da-api.txt tests/RuinaRPG.Tests.Integration/Rules/RulebookEquipagemCongeladoTests.cs tests/RuinaRPG.Tests.Integration/Rules/rulebook-equipagem.json tests/RuinaRPG.Tests.Integration/RuinaRPG.Tests.Integration.csproj
git commit -m "test: inventário de rotas e documento equipagem congelados antes do D"
```

---

### Task 2: `/api/rulebook` — one query for all kits (D.6)

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs:209-236`
- Test: `tests/RuinaRPG.Tests.Integration/Rules/RulebookEquipagemCongeladoTests.cs` (Task 1), plus a query-count test below.

**Interfaces:**
- Consumes: the frozen JSON from Task 1.
- Produces: nothing new; `BuildEquipagemAsync` signature unchanged.

- [ ] **Step 1: Write the failing query-count test** in `RulebookEquipagemCongeladoTests` — the number of SQL commands for `/api/rulebook/equipagem` must not grow with the number of kits. Use an EF Core `DbCommandInterceptor` registered on the factory, or count via a scoped `DiagnosticListener`; the simplest form available in this repo is to resolve `IRulebookRenderer` in a scope whose `RuinaRpgDbContext` was built with a counting interceptor:

```csharp
[Fact]
public async Task A_quantidade_de_consultas_nao_cresce_com_os_kits()
{
    var antes = await ContarConsultasAsync();
    await SemearKitDeTodosOsCasosAsync();
    var depois = await ContarConsultasAsync();
    Assert.Equal(antes, depois);
}

private async Task<int> ContarConsultasAsync()
{
    var contador = new ContadorDeComandos();
    await using var scope = Factory.Services.CreateAsyncScope();
    var opcoes = new DbContextOptionsBuilder<RuinaRpgDbContext>()
        .UseNpgsql(scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>().Database.GetConnectionString())
        .AddInterceptors(contador).Options;
    await using var db = new RuinaRpgDbContext(opcoes);
    var renderer = ActivatorUtilities.CreateInstance<RulebookRenderer>(scope.ServiceProvider, db);
    await renderer.GetDocument("equipagem");
    return contador.Total;
}

private sealed class ContadorDeComandos : DbCommandInterceptor
{
    public int Total;
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken t = default)
    { Interlocked.Increment(ref Total); return base.ReaderExecutingAsync(c, e, r, t); }
}
```

If `RuinaRpgDbContext`'s constructor or `RulebookRenderer`'s dependencies differ (check `RuinaRpgDbContext` constructors and `RulebookRenderer`'s primary constructor), adapt only the construction lines; the assertion stays `Assert.Equal(antes, depois)`.

- [ ] **Step 2: Run to verify it fails** — `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~RulebookEquipagemCongeladoTests` → the new test FAILS (`depois` = `antes` + 3: one more kit means 3 more queries).

- [ ] **Step 3: Replace the per-kit loop** with three batched queries, keeping output identical. Do not add `OrderBy` to items or slots (Review Focus 4):

```csharp
private async Task<RulebookDocument> BuildEquipagemAsync()
{
    var (intro, _) = SplitIntoSections(RulesDataProvider.ReadResource("Equipagem.md"), splitLevel: 1);

    var kits = await db.EquipmentKits.Where(k => !k.IsDeleted).OrderBy(k => k.Nome).ToListAsync();
    var kitIds = kits.Select(k => k.Id).ToList();

    // Uma consulta por tabela para todos os kits (antes: três por kit). A ordem relativa dos itens
    // de cada kit é a da busca, como era na consulta por kit.
    var itensPorKit = (await db.EquipmentKitItems.Where(i => kitIds.Contains(i.KitId)).ToListAsync()).ToLookup(i => i.KitId);
    var slotsPorKit = (await db.EquipmentKitChoiceSlots.Where(s => kitIds.Contains(s.KitId)).ToListAsync()).ToLookup(s => s.KitId);

    // O nome vem do item fixo ligado; a linha legada (sem FixedItemId) cai no Nome antigo.
    var fixoIds = itensPorKit.SelectMany(g => g).Select(i => i.FixedItemId)
        .Concat(slotsPorKit.SelectMany(g => g).Select(s => s.BonusFixedItemId)).OfType<Guid>().Distinct().ToList();
    var nomesDosFixos = await db.EquipmentKitFixedItems.Where(f => fixoIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nome);
    string NomeDe(Guid? fixoId, string? legado) => fixoId is { } id && nomesDosFixos.TryGetValue(id, out var nome) ? nome : legado ?? "";

    var sections = new List<RulebookSection>();
    foreach (var kit in kits)
    {
        var items = itensPorKit[kit.Id];
        var slots = slotsPorKit[kit.Id];
        var html = WebUtility.HtmlEncode(kit.Descricao).Replace("\n", "<br />") + "<ul>"
            + string.Join("", items.Select(i => $"<li>{WebUtility.HtmlEncode(NomeDe(i.FixedItemId, i.Nome))} x{i.Qtd}</li>"))
            + string.Join("", slots.Select(s => $"<li>{WebUtility.HtmlEncode(s.Label)} (escolha){(s.BonusFixedItemId is not null || s.BonusNome is not null ? $" — +{s.BonusQtd} {WebUtility.HtmlEncode(NomeDe(s.BonusFixedItemId, s.BonusNome))} se escolher da subcategoria {WebUtility.HtmlEncode(s.BonusSubcategoria)}" : "")}</li>"))
            + "</ul>"
            + (kit.Ciclos > 0 ? $"<p><em>+{kit.Ciclos} Ciclos</em></p>" : "");

        sections.Add(new RulebookSection(Slugify(kit.Nome), kit.Nome, html));
    }

    return new RulebookDocument("equipagem", "Equipagem", intro, sections);
}
```

- [ ] **Step 4: Run tests** — `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Rulebook"` → all PASS, including the frozen JSON.

- [ ] **Step 5: Measure** — `tools/bench/measure-api-latency.sh depois-d-rulebook`. Expected `/api/rulebook` p50 < 30 ms. If not met, record the number and continue (reported in Task 13; do not tune beyond this task).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Rules/RulebookRenderer.cs tests/RuinaRPG.Tests.Integration/Rules/RulebookEquipagemCongeladoTests.cs
git commit -m "perf(rulebook): equipagem busca os itens de todos os kits numa consulta só"
```

---

### Task 3: `SheetId` rename and the shared entity interfaces (D.1)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Fichas/IFicha.cs`
- Modify: every per-kind child entity in `src/RuinaRPG.Infrastructure/CharacterSheets/`, `NpcSheets/`, `CreatureSheets/` whose FK is `CharacterSheetId`/`NpcSheetId`/`CreatureSheetId` (Skill, Attribute, Mastery, Weapon, ArmorSlot, Shield, InventoryItem, Spoil, Artifact, Affection, Trait, SpellAbility, SpellAbilityEffect if it has the FK, Rune, Affinity, SheetHistoriaImage); the three sheet classes implement `IFicha`.
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` (each renamed FK gets `.Property(x => x.SheetId).HasColumnName("<old name>")`)
- Modify: all `src/` and `tests/` usages of the renamed properties (mechanical).
- **Not renamed:** entities with several sheet FKs or non-per-kind meaning — `DiaryEntry`, `SheetLogEntry`, `EncounterParticipant`, `AddParticipantRequest` (Contracts), anything outside the three per-kind folders.

**Interfaces:**
- Produces (in `RuinaRPG.Infrastructure.Fichas`):

```csharp
namespace RuinaRPG.Infrastructure.Fichas;

/// <summary>Uma ficha (Personagem, NPC ou Criatura) vista pelo código genérico do D.</summary>
public interface IFicha
{
    Guid Id { get; }
    int Nivel { get; set; }
    Guid? ImageId { get; set; }
}

/// <summary>Uma linha de uma tabela por tipo de ficha (CharacterSkill, NpcSkill, …).</summary>
public interface ILinhaDaFicha
{
    Guid Id { get; set; }
    Guid SheetId { get; set; }
}

public interface IPericiaDaFicha<TAtributo> : ILinhaDaFicha where TAtributo : struct, Enum
{
    int PericiaId { get; set; }
    int Gasto { get; set; }
    TAtributo? AtributoEscolhido { get; set; }
}
```

  Later tasks add further feature interfaces to this file as they need them (each task lists its own). Every per-kind child entity implements `ILinhaDaFicha` in this task.

- [ ] **Step 1: Capture the pre-rename evidence**

```bash
grep -rn '"CharacterSheetId"\|"NpcSheetId"\|"CreatureSheetId"\|nameof(.*SheetId)' src --include=*.cs | grep -v Migrations > /tmp/d-nomes-por-string.txt; cat /tmp/d-nomes-por-string.txt
```

Every hit is a string-based reference (e.g. in `SheetLogMap`/`SheetLogInterceptor`) that a compiler-driven rename will not update; each must be reviewed by hand in Step 3 (Review Focus 5).

- [ ] **Step 2: Create `IFicha.cs`** with the code above; make `CharacterSheet`, `NpcSheet`, `CreatureSheet` implement `IFicha` (they already have the three members).

- [ ] **Step 3: Rename** on each per-kind child entity the FK property to `SheetId`, add `: ILinhaDaFicha` (and `IPericiaDaFicha<Atributo>` / `IPericiaDaFicha<AtributoCriatura>` on the three Skill entities), and in `RuinaRpgDbContext` add the column mapping inside the entity's existing configuration block, e.g.:

```csharp
builder.Entity<NpcSkill>(entity =>
{
    entity.Property(s => s.SheetId).HasColumnName("NpcSheetId");
    entity.HasIndex(s => new { s.SheetId, s.PericiaId }).IsUnique();
    entity.HasOne<NpcSheet>().WithMany().HasForeignKey(s => s.SheetId).OnDelete(DeleteBehavior.Cascade);
});
```

  Entities configured only by convention today (no `builder.Entity<…>` block, FK discovered by name) need an explicit block: `HasOne<XSheet>().WithMany().HasForeignKey(x => x.SheetId)` with the same `OnDelete` the snapshot shows (`src/RuinaRPG.Infrastructure/Persistence/Migrations/RuinaRpgDbContextModelSnapshot.cs` is the authority for each FK's delete behavior and index). Then fix every compile error in `src/` and `tests/` by replacing the old property name with `SheetId` on those entities only. Update the string-based references from Step 1.

- [ ] **Step 4: Prove the schema did not move**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet ef migrations has-pending-model-changes --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api` → "No changes have been made to the model since the last migration."
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~MigrationTests|FullyQualifiedName~SheetLog|FullyQualifiedName~RotasDaApiTests"` → PASS.
Run: `tools/migrations/verificar-baseline.sh 729dac9` → exit 0.

- [ ] **Step 5: Prove the test edits are identifier-only**

Run: `git diff -U0 tests | grep '^[-+][^-+]' | sed -E 's/(Character|Npc|Creature)SheetId/SheetId/g' | sort | uniq -u`
Expected: no output (every removed test line has an identical added line once the old names are normalized).

- [ ] **Step 6: Full suite** — `dotnet test` → all green, same counts as before (Unit 964, Client 936, Integration 1812 + the Task 1–2 tests).

- [ ] **Step 7: Commit**

```bash
git add -A src tests
git commit -m "refactor(dados): FK das linhas por tipo de ficha vira SheetId (colunas iguais)"
```

---

### Task 4: Adapter, base controller, and Skills/Attributes/Masteries (D.2, D.3)

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/Fichas/ITipoDeFicha.cs`, `TipoPersonagem.cs`, `TipoNpc.cs`, `TipoCriatura.cs`, `FichaControllerBase.cs`, `PericiasDaFichaController.cs`, `AtributosDaFichaController.cs`, `MaestriasDaFichaController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/{Character,Npc,Creature}{Skills,Attributes,Masteries}Controller.cs` (become sealed subclasses)
- Modify: `src/RuinaRPG.Api/Program.cs` (register the three adapters as scoped)
- Modify: `src/RuinaRPG.Infrastructure/Fichas/IFicha.cs` (add `IAtributoDaFicha<TAtributo>`, `IMaestriaDaFicha` with the fields the three kinds share — read the entities)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/Fichas/AcessoNegadoPorTipoTests.cs`

**Interfaces:**
- Produces (used by every later controller task):

```csharp
namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>O que muda entre Personagem, NPC e Criatura para o código genérico das fichas (spec D.2).</summary>
public interface ITipoDeFicha<TSheet> where TSheet : class, IFicha
{
    /// <summary>A ficha, ou null se não existe.</summary>
    Task<TSheet?> CarregarAsync(Guid sheetId);
    /// <summary>Se o usuário pode editar a ficha (Personagem: dono ou Mestre da campanha; NPC/Criatura: GrantedSheetAuthorization).</summary>
    Task<bool> PodeEditarAsync(TSheet sheet, Guid userId);
    /// <summary>Resposta para acesso negado: Personagem 403 (Forbid), NPC/Criatura 404 (NotFound).</summary>
    ActionResult Negado(ControllerBase controller);
    /// <summary>Condições ativas da ficha (CondicoesAtivasDaFicha.ParaPersonagemAsync / ParaNpcAsync / ParaCriaturaAsync).</summary>
    Task<EfeitosDeCondicao> CondicoesAsync(TSheet sheet);
    /// <summary>Limites de Nível (Tabela de Níveis da Auditoria) valem para este tipo? Personagem e NPC sim, Criatura não.</summary>
    bool AplicaLimitesDeNivel { get; }
    /// <summary>Só Perícias com DisponivelParaCriaturas (Auditoria de Regras R0005)? Só Criatura.</summary>
    bool SoPericiasDeCriatura { get; }
}

public abstract class FichaControllerBase<TSheet>(ITipoDeFicha<TSheet> tipo) : ControllerBase where TSheet : class, IFicha
{
    protected ITipoDeFicha<TSheet> Tipo { get; } = tipo;

    protected Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    /// <summary>Carrega e autoriza na ordem de sempre: ficha inexistente → 404; sem permissão → Negado.</summary>
    protected async Task<(TSheet? Ficha, ActionResult? Erro)> FichaEditavelAsync(Guid sheetId)
    {
        var ficha = await Tipo.CarregarAsync(sheetId);
        if (ficha is null)
            return (null, NotFound());
        if (!await Tipo.PodeEditarAsync(ficha, CurrentUserId()))
            return (null, Tipo.Negado(this));
        return (ficha, null);
    }
}
```

  `TipoPersonagem.PodeEditarAsync` reads the campaign's `GmId` exactly as the controllers do today (`db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync()`) and calls `CharacterSheetAuthorization.CanEdit(userId, sheet.OwnerId, campaignGmId)`. `TipoNpc`/`TipoCriatura` call `GrantedSheetAuthorization.CanEdit(userId, sheet.OwnerId, sheet.GmId)`. `CarregarAsync` = `db.<Kind>Sheets.FindAsync(sheetId)`. Each adapter is a scoped service registered as `ITipoDeFicha<CharacterSheet>` etc.

  `PericiasDaFichaController<TSheet, TSkill, TAtributo, TResponse, TUpdate>` (`TSkill : class, IPericiaDaFicha<TAtributo>, new()`), with abstract members:

```csharp
protected abstract DbSet<TSkill> Linhas { get; }
protected abstract Task<List<TResponse>> ListarAsync(TSheet ficha);
protected abstract (int Gasto, string? AtributoEscolhido) Ler(TUpdate request);
```

- [ ] **Step 1: Write the failing test.** One theory per kind × endpoint family asserting the denied status (Review Focus 1, 2). It must pass on the current code, so it is written and run **before** the move (it pins behavior; it is "failing" only in the sense of being new — run it to see it green on old code, then keep it green):

```csharp
namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

// Spec D, Review Focus 1–2: o código genérico mantém o status de acesso negado de cada tipo
// (Personagem 403, NPC/Criatura 404) e checa a chave de Perícia antes da ficha.
public class AcessoNegadoPorTipoTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    public static TheoryData<string, string, string, int> Casos => new()
    {
        // tipo, método, sufixo da rota, status esperado para um usuário sem acesso
        { "character", "GET", "skills", 403 }, { "npc", "GET", "skills", 404 }, { "creature", "GET", "skills", 404 },
        { "character", "PUT", "skills/atletismo", 403 }, { "npc", "PUT", "skills/atletismo", 404 }, { "creature", "PUT", "skills/atletismo", 404 },
        { "character", "GET", "attributes", 403 }, { "npc", "GET", "attributes", 404 }, { "creature", "GET", "attributes", 404 },
        { "character", "GET", "masteries", 403 }, { "npc", "GET", "masteries", 404 }, { "creature", "GET", "masteries", 404 },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Acesso_negado_tem_o_status_do_tipo(string tipo, string metodo, string sufixo, int status)
    {
        var sheetId = await CriarFichaDeOutroMestreAsync(tipo);
        var intruso = await RegisterGmAndGetTokenAsync($"intruso{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@x.com");
        var corpo = metodo == "PUT" ? new { Gasto = 1, AtributoEscolhido = (string?)null } : null;
        var resposta = await Client.SendAsync(AuthedRequest(new HttpMethod(metodo), $"/api/{tipo}-sheets/{sheetId}/{sufixo}", intruso, corpo));
        Assert.Equal(status, (int)resposta.StatusCode);
    }

    [Theory]
    [InlineData("character")] [InlineData("npc")] [InlineData("creature")]
    public async Task Pericia_desconhecida_em_ficha_alheia_e_404(string tipo)
    {
        var sheetId = await CriarFichaDeOutroMestreAsync(tipo);
        var intruso = await RegisterGmAndGetTokenAsync($"intruso{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@x.com");
        var resposta = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{tipo}-sheets/{sheetId}/skills/nao-existe", intruso, new { Gasto = 1 }));
        Assert.Equal(404, (int)resposta.StatusCode);
    }
}
```

  `CriarFichaDeOutroMestreAsync(tipo)`: register a GM, then create the sheet the way the existing per-kind test classes do (copy the creation calls from `CharacterSkillsControllerTests`, `NpcSkillsControllerTests`, `CreatureSkillsControllerTests` — character sheets need a campaign and a player via `CreateCampaignAsync`/`RegisterJogadorLinkedToAsync`). Use a Perícia key that exists in the seed for "atletismo" (check `Pericias` seed in the baseline migration; replace if the key differs).

- [ ] **Step 2: Run it on the unchanged code** — `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~AcessoNegadoPorTipoTests` → PASS. (If any case fails, the table above is wrong about today's behavior: fix the expected value to today's, never the code.)

- [ ] **Step 3: Create the adapter, base, and the three generic controllers.** For each of Skills, Attributes, Masteries: put the logic of the NPC version in the generic base (the NPC copy sits between the two others), route every per-kind difference through `Tipo` flags or abstract members, and reduce each per-kind controller to a sealed subclass. Example for NPC Skills (the target shape of every subclass):

```csharp
[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/skills")]
public sealed class NpcSkillsController(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<NpcSheet> tipo, NpcSheetStats stats)
    : PericiasDaFichaController<NpcSheet, NpcSkill, Atributo, NpcSkillResponse, UpdateNpcSkillRequest>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override DbSet<NpcSkill> Linhas => db.NpcSkills;

    protected override async Task<List<NpcSkillResponse>> ListarAsync(NpcSheet ficha) =>
        (await stats.PericiasAsync(ficha, await Tipo.CondicoesAsync(ficha)))
            .Select(p => new NpcSkillResponse(p.Chave, p.Gasto, p.Modificador, p.Atributo, p.Total, p.BonusDeHistorico, p.Nome, p.Descricao))
            .ToList();

    protected override (int Gasto, string? AtributoEscolhido) Ler(UpdateNpcSkillRequest r) => (r.Gasto, r.AtributoEscolhido);
}
```

  and the base `Update` keeps today's order:

```csharp
[HttpPut("{pericia}")]
public async Task<IActionResult> Update(Guid sheetId, string pericia, TUpdate request)
{
    // Chave desconhecida ou Perícia removida: resolvida antes de tudo, como a antiga falha de binding da rota.
    var def = await pericias.AtivaPorChaveAsync(pericia);
    if (def is null)
        return NotFound();

    var (ficha, erro) = await FichaEditavelAsync(sheetId);
    if (erro is not null)
        return erro;

    var (gasto, atributoEscolhido) = Ler(request);
    if (Tipo.SoPericiasDeCriatura && !def.DisponivelParaCriaturas)
        // R0005's "lista fixa mais curta" — só Perícias com DisponivelParaCriaturas aparecem na Criatura.
        return BadRequest("Perícia fora da lista permitida para Criaturas.");
    if (Tipo.AplicaLimitesDeNivel)
    {
        var tabela = await tabelaDeNiveis.ObterAsync();
        var gastoAtual = await Linhas.Where(s => s.SheetId == sheetId && s.PericiaId == def.Id).Select(s => s.Gasto).SingleOrDefaultAsync();
        if (LimitesDeNivel.Gasto(def.Nome, gastoAtual, gasto, tabela.Limite(ChavesDeNivel.MaxPericia, ficha!.Nivel), ficha.Nivel) is { } erroDeLimite)
            return BadRequest(erroDeLimite);
    }

    var skill = await Linhas.SingleOrDefaultAsync(s => s.SheetId == sheetId && s.PericiaId == def.Id);
    if (skill is null)
    {
        skill = new TSkill { Id = Guid.NewGuid(), SheetId = sheetId, PericiaId = def.Id };
        Linhas.Add(skill);
    }
    skill.Gasto = gasto;
    skill.AtributoEscolhido = Enum.TryParse<TAtributo>(atributoEscolhido, out var parsed) ? parsed : null;
    await db.SaveChangesAsync();
    return NoContent();
}
```

  Character-only endpoints stay in the Character subclass (e.g. `GET skills/budget`, `PUT masteries/{id}`). Character's `ListarAsync` override keeps today's inline computation verbatim until Task 9. Before writing each base, diff the three versions (normalize kind names) and classify each hunk per the divergence rule; append accidental divergences to the spec's "Divergências encontradas" section (create it at the end of the spec if missing) as `- <feature>/<endpoint>: <what differs> (mantido via <hook>)`.

- [ ] **Step 4: Run** — `dotnet build` (0 warnings); `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Skills|FullyQualifiedName~Attributes|FullyQualifiedName~Masteries|FullyQualifiedName~AcessoNegadoPorTipoTests|FullyQualifiedName~RotasDaApiTests"` → PASS with no test file edited (`git status tests` shows only the new `Fichas/` test).

- [ ] **Step 5: Full suite** — `dotnet test` → all green.

- [ ] **Step 6: Commit**

```bash
git add -A src tests/RuinaRPG.Tests.Integration/Controllers/Fichas docs/superpowers/specs/2026-10-18-versao-2-0-0-d-backend-design.md
git commit -m "refactor(api): Perícias, Atributos e Maestrias das fichas em controllers genéricos"
```

---

### Tasks 5–8: the remaining feature controllers

Each of Tasks 5–8 follows **exactly** the procedure of Task 4 Steps 3–6 (diff the versions, classify per divergence rule, generic base in `Controllers/Fichas/`, sealed per-kind subclasses with today's `[Route]`, feature interfaces added to `IFicha.cs`, no test edited, route inventory green, full suite green, one commit). Each also extends `AcessoNegadoPorTipoTests.Casos` with one GET row per kind for each moved endpoint family, run on the code **before** the move first (Step 2 of Task 4).

### Task 5: Arsenal (weapons, armor slots, shields — three kinds)

**Files:** Create `src/RuinaRPG.Api/Controllers/Fichas/ArsenalDaFichaController.cs`; modify `{Character,Npc,Creature}ArsenalController.cs`; extend `IFicha.cs` with `IArmaDaFicha` (ItemId, IsEquipped, DurabilidadeAtual …), `ISlotDeArmaduraDaFicha`, `IEscudoDaFicha` — the fields read by the shared code.

Known differences to route through hooks (from the normalized diffs): authorization (adapter); Character's `DesabilitadoPorAsync` takes the sheet id, NPC/Creature load the sheet first (adapter `CondicoesAsync(ficha)` covers both); comments only otherwise. Endpoints: all 13 identical across kinds → all in the base.

Commit: `refactor(api): Arsenal das fichas em controller genérico`.

### Task 6: Possessions split by sub-resource

**Files:** Create `InventarioDaFichaController.cs` (Character, NPC: `inventory`, 4 endpoints), `ArtefatosDaFichaController.cs` (3 kinds: `artifacts`, 3 endpoints), `AfeicoesDaFichaController.cs` (3 kinds: `affections` POST/GET/DELETE; Character subclass adds `PUT affections/{id}`), `CaracteristicasDaFichaController.cs` (3 kinds: `traits` POST/GET/DELETE), `TracosRaciaisDaFichaController.cs` (Character, NPC: `racial-traits/pending`, `racial-traits/resolve`). Per kind, sealed subclasses named `<Kind>InventoryController`, `<Kind>ArtifactsController`, `<Kind>AffectionsController`, `<Kind>TraitsController`, `<Kind>RacialTraitsController`, each with `[Route("api/<kind>-sheets/{sheetId}")]` and the action templates unchanged (`"inventory"`, `"inventory/{id}/qtd"`, …). Creature spoils (`spoils`, 4 endpoints) move to `CreatureSpoilsController` (no generic base: one kind). Delete the three `*PossessionsController.cs` files when empty.

Known differences: Creature traits use the exclusive catalog (`CreatureExclusiveTrait`) on POST, with the "Esta característica não é do catálogo de criatura." 400, and `SumExistingCustoAsync` over both catalogs — keep as an override of the base's "resolve the trait to add" and "sum existing cost" members. Character/NPC exclude racial grants (`!t.IsRacial`) from the budget sum — a flag or the default implementation. Check `ListTraits` and `DeleteTrait` for the same split.

Commit: `refactor(api): Posses das fichas divididas por sub-recurso em controllers genéricos`.

### Task 7: SpellAbilities (three kinds)

**Files:** Create `MagiasDaFichaController.cs`; modify `{Character,Npc,Creature}SpellAbilitiesController.cs`; interfaces for spell-ability rows and their effects as the shared code needs. Endpoints: POST, GET, `GET passivas-disponiveis`, `PUT {id}`, `DELETE {id}` — identical across kinds. Read the 79-line C↔N and 36-line N↔Cr normalized diffs first; requisitos checks go through each kind's stats (`FichaParaRequisitosAsync`), exposed by an abstract member.

Commit: `refactor(api): Magias e Habilidades das fichas em controller genérico`.

### Task 8: Runes, Affinities, Equipagem (Character + NPC)

**Files:** Create `RunasDaFichaController.cs`, `AfinidadesDaFichaController.cs`, `EquipagemDaFichaController.cs`; modify `{Character,Npc}{Runes,Affinities,Equipagem}Controller.cs`. Endpoints identical between the two kinds (Runes: POST/GET/DELETE; Affinities: POST/GET/PUT/DELETE; Equipagem: `GET kits`, `POST choose`).

Commit: `refactor(api): Runas, Afinidades e Equipagem das fichas em controllers genéricos`.

---

### Task 9: Shared stats core (D.4)

**Files:**
- Create: `src/RuinaRPG.Api/Services/FichaStats.cs`
- Modify: `src/RuinaRPG.Api/Services/{Character,Npc,Creature}SheetStats.cs` (become subclasses of `FichaStats<TSheet>`; registrations in `Program.cs` unchanged)
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs` and `CharacterAttributesController.cs` (their `ListarAsync` overrides switch to the stats methods, if outputs are identical — the existing tests decide)

**Interfaces:**
- Produces: `FichaStats<TSheet>` with the public methods the three classes expose today (`SubAtributosAsync`, `FichaParaRequisitosAsync`, `ArtefatosAsync`, `PenalidadesAtivasAsync`, `ModificadoresAsync`, `MaximosAsync`, `AtributosAsync`, `PericiasAsync`, `EapAtualAsync` where it exists). Public signatures of `CharacterSheetStats`/`NpcSheetStats`/`CreatureSheetStats` used by other services (`ResumoDeFichaDoParticipante`, `CondicoesDoEncontro`, controllers) stay callable with the same arguments — including Character's `MaximosAsync(sheet, efeitos, vocacao, nivel)` overload.

Known differences (normalized diffs): Character reads the campaign `BonusDeCarga` and returns it in `SubAttributesResponse.BonusDeCargaDaCampanha`, NPC/Creature use `0m` and omit it → hook `Task<decimal?> BonusDeCargaAsync(TSheet)` where null means "omit". Character lacks `AtributosAsync`/`PericiasAsync` today (computed inline in its controllers) → adding them to the base is allowed only if the Character Skills/Attributes lists stay identical (existing tests). Creature attributes use `AtributoCriatura` and a different sub-attribute formula set: read the 185-line N↔Cr diff; whatever is not shared stays in `CreatureSheetStats`. `VocacaoTabelaName` and `SubAtributosPorEnum` move to `FichaStats` as `internal static`.

Steps: as Task 4 Steps 3–6 (no test edited; full suite green; route inventory green). Commit: `refactor(api): núcleo comum de estatísticas das fichas`.

---

### Task 10: Shared Sheets endpoints

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/Fichas/FichasController.cs`
- Modify: `CharacterSheetsController.cs`, `NpcSheetsController.cs`, `CreatureSheetsController.cs`

Shared candidates (present in all three, or in two with the same template): `GET {id}/level-up-notice`, `POST {id}/dismiss-level-up-notice`, `GET {id}/condicoes-ativas`, `GET {id}/equipment-penalties`, `GET {id}/modificador-de-dano`, `GET {id}/sub-attributes`, `PUT {id}/pontos-de-ignicao`, `DELETE {id}`; Character+NPC: `GET {id}/variantes-liberadas`, `GET {id}/racial-ability`, `PUT {id}/historia`, `GET|PUT {id}/historia/imagens`; NPC+Creature: `PUT {id}/nivel`, `PUT {id}/experiencia-atual`. Move only those whose normalized bodies match (or differ only through adapter hooks); everything else stays in the subclass.

`CharacterSheetsController` has no class-level route today: give it `[Route("api/character-sheets")]` and rewrite its non-prefixed templates as absolute (`"/api/campaigns/{campaignId}/character-sheets"`, `"/api/character-sheets/mine"`) — Review Focus 3; the route inventory must stay green. A Personagem-only base for the two-kind groups is a second abstract layer only if it saves code; otherwise keep those actions in the subclasses.

Steps: as Task 4 Steps 3–6, extending `AcessoNegadoPorTipoTests.Casos` with `GET {id}/sub-attributes` and `GET {id}/condicoes-ativas` per kind first. Commit: `refactor(api): endpoints comuns das fichas em controller genérico`.

---

### Task 11: Test inventory, then consolidate the per-kind feature tests (part 1)

**Files:**
- Create: `docs/superpowers/specs/2026-10-18-d-inventario-de-testes.md`
- Create: generic test classes in `tests/RuinaRPG.Tests.Integration/Controllers/Fichas/` for Skills, Attributes, Masteries, Arsenal, EquipmentPenalties
- Modify/Delete: the corresponding per-kind test files in `tests/RuinaRPG.Tests.Integration/Controllers/`

- [ ] **Step 1: Capture the before-list** — `dotnet test tests/RuinaRPG.Tests.Integration --list-tests > /tmp/d-testes-antes.txt` and per kind counts: `grep -c "Character" …`, `grep -c "Npc" …`, `grep -c "Creature" …`.

- [ ] **Step 2: Write the inventory** `docs/superpowers/specs/2026-10-18-d-inventario-de-testes.md`: one table per per-kind test class covered by Tasks 11–12, rows = test method, columns = kinds where it exists and its destination (`<GenericClass>.<Method>` run for kinds X/Y/Z, or `stays in <KindClass>`). This is the spec's "the plan lists every current test" list, generated here from the live code.

- [ ] **Step 3: Consolidate one feature at a time.** Shape:

```csharp
public abstract class PericiasDaFichaTestes<TTipo>(ApiFixture fixture) : ApiTestBase(fixture) where TTipo : ITipoDeTeste, new()
{
    protected static readonly TTipo Tipo = new();
    [Fact] public async Task Update_grava_o_gasto() { /* corpo comum, usando Tipo.CriarFichaAsync, Tipo.Rota */ }
}
public sealed class PericiasDaFichaTestesPersonagem(ApiFixture f) : PericiasDaFichaTestes<TipoDeTestePersonagem>(f) { /* casos só de Personagem */ }
public sealed class PericiasDaFichaTestesNpc(ApiFixture f) : PericiasDaFichaTestes<TipoDeTesteNpc>(f) { }
public sealed class PericiasDaFichaTestesCriatura(ApiFixture f) : PericiasDaFichaTestes<TipoDeTesteCriatura>(f) { /* casos só de Criatura */ }
```

  `ITipoDeTeste` (in `Controllers/Fichas/TiposDeTeste.cs`) carries `string Rota` (`"character-sheets"` …), `Task<(string Token, Guid SheetId)> CriarFichaAsync(ApiTestBase t)` and the per-kind expected values the tests assert (e.g. `int StatusNegado`). A kind-specific expectation becomes a property, never an `if (tipo == …)` in a test body. Each assertion moves verbatim; where two kinds asserted different literal values, the value becomes a property of the kind.

- [ ] **Step 4: Check** — `dotnet test tests/RuinaRPG.Tests.Integration --list-tests > /tmp/d-testes-depois.txt`; per kind, the number of tests in the consolidated features is ≥ the before count; `git diff --stat src` is empty; full integration suite green.

- [ ] **Step 5: Commit** — `test: testes por tipo de ficha de Perícias, Atributos, Maestrias, Arsenal e penalidades em classes genéricas`.

### Task 12: Consolidate the per-kind feature tests (part 2)

Same procedure as Task 11 Steps 3–5 (inventory already written; update its destination column) for Possessions / AffectionsAndTraits, SpellAbilities, Runes, Affinities, Equipagem, Passivas, and the shared parts of the three `*SheetsControllerTests`. Commit: `test: demais testes por tipo de ficha em classes genéricas`.

---

### Task 13: Measurements and docs

**Files:**
- Modify: `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md` ("After D" section)
- Modify: `CLAUDE.md` (Code section: one line on `Controllers/Fichas/` and the adapter)
- Modify: `Docs/Requisitos/Requisitos - Técnico.md` only if it names per-kind controller classes (check with grep; otherwise no change)
- Modify: `docs/superpowers/specs/2026-10-18-versao-2-0-0-d-backend-design.md` ("Divergências encontradas" final)

- [ ] **Step 1: Measure** — `tools/bench/measure-code-size.sh depois-d`, `tools/bench/measure-build-and-tests.sh depois-d`, `tools/bench/measure-api-latency.sh depois-d`. Lines in `src/RuinaRPG.Api/Controllers/` + `Services/`: `find src/RuinaRPG.Api/Controllers src/RuinaRPG.Api/Services -name '*.cs' | xargs cat | wc -l`.

- [ ] **Step 2: Write the "After D" section** with before/after/target for: Controllers+Services lines (15 813 → target ≤ 11 500), integration test lines (40 139 → ≤ 34 000), `/api/rulebook` p50 (69.8 ms → < 30 ms), integration wall time and `dotnet build --no-incremental` (no regression). Mark each target met/missed explicitly; a miss is stated plainly, not tuned.

- [ ] **Step 3: Final checks** — `dotnet build` (0 warnings), `dotnet test` (all green), `tools/migrations/verificar-baseline.sh 729dac9` (exit 0), `git diff ed7a447 --stat -- src/RuinaRPG.Contracts src/RuinaRPG.Client` (empty).

- [ ] **Step 4: Commit** — `docs: medições depois do D e divergências encontradas`.
