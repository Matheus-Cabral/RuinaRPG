# Ficha de NPCs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a GM build and manage a bestiary of NPC sheets — the exact same 6-tab structure as Ficha de Personagem, with no player owner by default and no campaign link (that arrives via `CampaignAttachments` in the Campanha — Anexos plan), plus a filterable "NPCs do GM" listing page (R0003).

**Architecture:** `Requisitos - Ficha de NPCs.md` itself is a diff document — "todas as 5 abas... todos os campos, fórmulas e convenções... se aplicam aqui integralmente." This plan follows the same shape: the `NpcSheet` root entity mirrors `CharacterSheet` minus `CampaignId`, with `OwnerId` nullable; every one of the 14 child tables the three Ficha de Personagem plans built (`CharacterAttributes`, `CharacterSkills`, `CharacterAffinities`, `CharacterWeapons`, `CharacterArmorSlots`, `CharacterShields`, `CharacterSpellAbilities` + effects, `CharacterRunes`, `CharacterMasteries`, `CharacterInventoryItems`, `CharacterArtifacts`, `CharacterAffections`, `CharacterTraits`) gets an `Npc`-prefixed mirror with an identical column shape, FK'd to `NpcSheet` instead. Every formula (`AttributeTotalCalculator`, `SkillFormulas`, `SubAttributeFormulas`, `ResourceMaximumCalculator`, `SpellAbilityCostCalculator`, …) is reused as-is from `RuinaRPG.Domain` — none of it is Personagem-specific, it all just takes plain numbers in and out. Authorization is simpler than `CharacterSheet`'s: **GM-only, unconditionally** (R0001 — "não há a distinção de campos editáveis pelo jogador... O GM que a criou tem acesso total"), except once a sheet is granted to a player (Campanha R0010, next plan), which is explicitly out of scope here.

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Ficha de NPCs.md` (all 4 requirements), `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (the base structure this diffs against — already fully implemented by the three Personagem plans), `Docs/Requisitos/Requisitos - Modelo de Dados.md` §6.2.

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Every endpoint in this plan is `[Authorize(Roles = "GM")]` and scoped to `NpcSheet.GmId == caller` — no owner-or-GM split like `CharacterSheet` (that arrives only once a sheet is granted to a player, out of scope here).
- Field shapes, formulas, and validation rules for every mirrored tab are byte-for-byte identical to their `Character*` counterpart — a divergence here is a bug, not a design choice, unless this doc or its diff explicitly calls one out (there are none for tabs 1-5; R0004 is about visibility, not fields).
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Infrastructure — `NpcSheet` root entity, migration, and GM-only CRUD

**Files:**
- Create: `src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs`
- Create: `src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs`
- Create: `src/RuinaRPG.Contracts/NpcSheets/UpdateNpcSheetRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/NpcSheetMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`

**Interfaces:**
- Consumes: every enum from the Ficha de Personagem plans (`Linhagem`, `Variante`, `Vocacao`, `AfinidadeElemental`, `Cobertura`), `LinhagemVarianteValidator`.
- Produces: `NpcSheet` — identical root columns to `CharacterSheet` (Ficha de Personagem — Fundação plan, Task 3) minus `CampaignId`, with `OwnerId` as `Guid?` instead of `Guid`. `RuinaRpgDbContext.NpcSheets`. `POST /api/npc-sheets` → `201`; `GET /api/npc-sheets/{id}` → `200`/`404`; `PUT /api/npc-sheets/{id}` → `204`/`404`; `DELETE /api/npc-sheets/{id}` → `204`/`404` — all `[Authorize(Roles = "GM")]`, scoped to `GmId`. `NpcSheetResponse`/`UpdateNpcSheetRequest` mirror `CharacterSheetResponse`/`UpdateCharacterSheetRequest` field-for-field, minus `CampaignId`/`OwnerId` semantics (here `OwnerId` is nullable and never set by this plan's endpoints — only by the future grant flow).

- [ ] **Step 1: Write the entity**

`src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs` — copy `CharacterSheet` (`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs`) verbatim into this new namespace/class, with exactly two changes: remove the `CampaignId` property, and change `OwnerId` from `Guid` to `Guid?`. Every other property (Nome, Linhagem, Variante, Vocacao, SubVocacao, Afinidade, Propriedade, Nivel, Circulo, Grau, PossuiCoracaoDeMana, ExperienciaAtual, EAPAtual, the 7 NucleosRank* fields, PontosDeIgnicaoAtual/Total, the 4 *Atual resource fields, Cobertura, Ciclos, LastDismissedLevelUpLevel) stays identical. Add `public Guid GmId { get; set; }` (replaces the ownership-via-campaign indirection `CharacterSheet` uses — an NPC sheet is directly GM-owned, not campaign-owned).

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/NpcSheetMigrationTests.cs` — same structure as `CharacterSheetMigrationTests` (Fundação plan, Task 3), minus the `Campaign`/member setup (no campaign needed), asserting a migration named `AddNpcSheets` and that `GmId`/`OwnerId: null` round-trip correctly.

- [ ] **Step 3: Run the test to verify it fails, register the DbSet, create the migration, verify it passes**

Add to `RuinaRpgDbContext`: `public DbSet<NpcSheet> NpcSheets => Set<NpcSheet>();`, and in `OnModelCreating`:

```csharp
builder.Entity<NpcSheet>(entity =>
{
    entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.GmId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.SetNull);
    entity.HasOne<Image>().WithMany().HasForeignKey(s => s.ImageId).OnDelete(DeleteBehavior.SetNull);
});
```

Two separate `HasOne<ApplicationUser>()` FKs on the same entity need EF Core to disambiguate — if a build error names an ambiguous shadow FK, add `.HasConstraintName("FK_NpcSheets_AspNetUsers_GmId")`/`"FK_NpcSheets_AspNetUsers_OwnerId"` explicitly to each.

```bash
dotnet ef migrations add AddNpcSheets --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter NpcSheetMigrationTests` — expect PASS.

- [ ] **Step 4: Write the contracts**

`NpcSheetResponse`/`UpdateNpcSheetRequest` — copy `CharacterSheetResponse`/`UpdateCharacterSheetRequest` (Fundação plan Task 5-6, as extended by the Atributos/Combate plan Task 8 and the Magias/Posses/Diário plan Task 6) field-for-field, replacing `CampaignId`/`OwnerId: string` with just `OwnerId: string?` (nullable) and no `CampaignId` at all.

- [ ] **Step 5: Write the failing controller tests**

`tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs` — mirror `CharacterSheetsControllerTests`'s create/get/update/delete cases (Fundação plan Task 5-6), simplified since there's no campaign/membership setup and no owner-vs-GM split to test — only: create-returns-201-with-Nivel-1, create-by-jogador-returns-403 (there's no such thing as a jogador calling this — assert 403 using a registered-but-unrelated Jogador token, same as every other GM-only endpoint in this codebase), get/update/delete-by-the-owning-GM-succeeds, update/delete-by-a-different-GM-returns-404.

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter NpcSheetsControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 7: Write `NpcSheetsController`**

Mirror `CharacterSheetsController`'s `Create`/`Get`/`Update`/`Delete` (Fundação plan Task 5-6) at route `api/npc-sheets`, `[Authorize(Roles = "GM")]` on the whole controller (no per-action role split needed, unlike `CharacterSheetsController`'s create/delete-only GM restriction), every query scoped by `s.GmId == CurrentGmId()` instead of the campaign-GM join `CharacterSheetsController` needs. No `CampanhaMember` check on create (nothing to validate against — an NPC sheet isn't created "for" anyone). Graduação computation reuses `GraduacaoCalculator` exactly as `CharacterSheetsController` does.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter NpcSheetsControllerTests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Infrastructure/NpcSheets src/RuinaRPG.Contracts/NpcSheets src/RuinaRPG.Api/Controllers/NpcSheetsController.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration
git commit -m "feat: add npc sheet root entity and GM-only CRUD"
```

---

### Task 2: Infrastructure — mirror all 14 child tables (tabs 2-6)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/NpcSheets/NpcAttribute.cs`, `NpcSkill.cs`, `NpcAffinity.cs`, `NpcWeapon.cs`, `NpcArmorSlot.cs`, `NpcShield.cs`, `NpcSpellAbility.cs`, `NpcSpellAbilityEffect.cs`, `NpcRune.cs`, `NpcMastery.cs`, `NpcInventoryItem.cs`, `NpcArtifact.cs`, `NpcAffection.cs`, `NpcTrait.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/NpcChildTableMigrationTests.cs`

**Interfaces:**
- Consumes: `NpcSheet` (Task 1), every enum/type each `Character*` counterpart uses.
- Produces: 14 entities, each an exact copy of its `Character*` counterpart with `CharacterSheetId` renamed to `NpcSheetId` and the FK retargeted to `NpcSheet`. `RuinaRpgDbContext.NpcAttributes`/`NpcSkills`/`NpcAffinities`/`NpcWeapons`/`NpcArmorSlots`/`NpcShields`/`NpcSpellAbilities`/`NpcSpellAbilityEffects`/`NpcRunes`/`NpcMasteries`/`NpcInventoryItems`/`NpcArtifacts`/`NpcAffections`/`NpcTraits`. Task 3 depends on every shape exactly.

This is deliberately one batched task — all 14 tables are mechanical renames of already-built, already-tested entities (Ficha de Personagem — Atributos & Combate and — Magias, Posses & Diário plans), so implementing and reviewing them as 14 near-identical tasks would add process overhead without adding real risk. Treat it as data entry against a precise, exhaustive checklist, not 14 separate design decisions.

- [ ] **Step 1: Write all 14 entities**

For each pair below, copy the named `Character*` source file, rename the class and its `CharacterSheetId` property to the `Npc*` name and `NpcSheetId`, keep every other property identical:

| Copy from (Character* source, already built) | To (this task) |
|---|---|
| `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAttribute.cs` | `NpcAttribute.cs` |
| `.../CharacterSkill.cs` | `NpcSkill.cs` |
| `.../CharacterAffinity.cs` | `NpcAffinity.cs` |
| `.../CharacterWeapon.cs` | `NpcWeapon.cs` |
| `.../CharacterArmorSlot.cs` | `NpcArmorSlot.cs` |
| `.../CharacterShield.cs` | `NpcShield.cs` |
| `.../CharacterSpellAbility.cs` | `NpcSpellAbility.cs` (also rename its `List<CharacterSpellAbilityEffect> Efeitos` to `List<NpcSpellAbilityEffect> Efeitos`) |
| `.../CharacterSpellAbilityEffect.cs` | `NpcSpellAbilityEffect.cs` (rename `CharacterSpellAbilityId` to `NpcSpellAbilityId`) |
| `.../CharacterRune.cs` | `NpcRune.cs` |
| `.../CharacterMastery.cs` | `NpcMastery.cs` |
| `.../CharacterInventoryItem.cs` | `NpcInventoryItem.cs` |
| `.../CharacterArtifact.cs` | `NpcArtifact.cs` (rename `ArtifactItemId` — keep as-is, already generic) |
| `.../CharacterAffection.cs` | `NpcAffection.cs` |
| `.../CharacterTrait.cs` | `NpcTrait.cs` |

All 14 files live in `src/RuinaRPG.Infrastructure/NpcSheets/`.

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/NpcChildTableMigrationTests.cs` — one test method per table (14 total, or a single parameterized/looping test if that's cleaner — implementer's choice, as long as every table gets a real assertion), each inserting one row against a real `NpcSheet` and confirming it round-trips. Assert the migration name ends `AddNpcChildTables`.

- [ ] **Step 3: Run the test to verify it fails, register all 14 DbSets and relationships, create the migration, verify it passes**

Mirror every `OnModelCreating` block the two Ficha de Personagem plans wrote for the `Character*` versions (unique indexes on `NpcSheetId`+enum for attributes/skills/armor-slots, cascade deletes to `NpcSheet`, `Restrict` deletes to `Item`/`Trait`) — same configuration, `Npc*` types instead of `Character*`.

```bash
dotnet ef migrations add AddNpcChildTables --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter NpcChildTableMigrationTests` — expect all 14 (or however many) assertions PASS.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Infrastructure/NpcSheets src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/NpcChildTableMigrationTests.cs
git commit -m "feat: mirror all character sheet child tables for npc sheets"
```

---

### Task 3: Api — mirror every child-table controller

**Files:**
- Create: `src/RuinaRPG.Contracts/NpcSheets/*` (response/request records mirroring every `CharacterSheets` contract used by Tasks 4-9 of the two Ficha de Personagem plans)
- Create: `src/RuinaRPG.Api/Controllers/NpcAttributesController.cs`, `NpcSkillsController.cs`, `NpcAffinitiesController.cs`, `NpcArsenalController.cs`, `NpcSpellAbilitiesController.cs`, `NpcRunesController.cs`, `NpcMasteriesController.cs`, `NpcPossessionsController.cs`
- Test: one integration test file per controller, mirroring the `Character*` equivalents

**Interfaces:**
- Consumes: `NpcSheet` (Task 1), all 14 child entities (Task 2), every Domain formula (`AttributeTotalCalculator`, `SkillFormulas`, `SubAttributeFormulas`, `ResourceMaximumCalculator`, `SpellAbilityCostCalculator`) — all reused unmodified.
- Produces: every route the two Ficha de Personagem plans built under `api/character-sheets/{sheetId}/...`, mirrored under `api/npc-sheets/{sheetId}/...` with the same verbs, same contract shapes (renamed `Character*` → `Npc*` where the type name itself is echoed in the contract, e.g. `CharacterAttributeResponse` → `NpcAttributeResponse`), and **one systematic authorization difference**: every check that was `CharacterSheetAuthorization.CanEdit(caller, sheet.OwnerId, campaignGmId)` becomes a direct `sheet.GmId == CurrentGmId()` comparison — no shared helper needed here since there's no owner-or-GM split to express, just single-owner-field equality. `NpcSpellAbilitiesController`'s `Add` action does **not** insert an independent bank copy the way `CharacterSpellAbilitiesController` does — R0001 of the Banco de Magias doc says the auto-copy applies when a Magia/Habilidade is created "em qualquer ficha" (Personagem, NPC, or Criatura), so it DOES apply here too; mirror that insert exactly, GM-attributed to `sheet.GmId`.

- [ ] **Step 1: Write every contract**

For each `Character*` contract used by the Atributos/Combate and Magias/Posses/Diário plans' endpoints (Attribute, Skill, Affinity, Weapon, ArmorSlot, Shield, SpellAbility, Rune, Mastery, InventoryItem, Artifact, Affection, Trait, TraitsList — request and response pairs), create an `Npc*`-named copy in `src/RuinaRPG.Contracts/NpcSheets/` with identical fields.

- [ ] **Step 2: Write the failing tests, one file per controller**

For each of the 8 controllers listed above, write an integration test file mirroring its `Character*` counterpart's tests (Atributos/Combate plan Tasks 4-7; Magias/Posses/Diário plan Tasks 4-7) — same case names, same assertions, GM-only setup (no campaign/member/jogador-token dance — just `RegisterGmAndGetTokenAsync` + a direct `POST /api/npc-sheets` call), plus one "acting as a different GM returns 404" case per controller replacing the "unrelated jogador returns 403" case (there's no owner-vs-GM ambiguity to probe here, only cross-GM isolation).

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Npc"`
Expected: FAIL — none of the routes exist yet.

- [ ] **Step 4: Write every controller**

For each of the 8 controllers, mirror its `Character*` counterpart exactly (route prefix `api/npc-sheets/{sheetId}/...`, same actions, same formula calls), replacing the `CharacterSheetAuthorization.CanEdit` check with `sheet.GmId == CurrentGmId()` everywhere it appears, and swapping every `Character*` entity/contract type for its `Npc*` counterpart.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Npc"`
Expected: PASS across all 8 test files.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/NpcSheets src/RuinaRPG.Api/Controllers/Npc*.cs tests/RuinaRPG.Tests.Integration/Controllers/Npc*.cs
git commit -m "feat: mirror every character sheet child-table controller for npc sheets"
```

---

### Task 4: Api — NPC listing with advanced filters (R0003)

**Files:**
- Create: `src/RuinaRPG.Contracts/NpcSheets/NpcSheetSummaryResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `NpcSheetsController` (Task 1).
- Produces: `GET /api/npc-sheets?nome=&linhagem=&vocacao=&subVocacao=&nivel=` → `200` + `List<NpcSheetSummaryResponse>`, scoped to the caller's own NPCs. **"Campanha vinculada" filtering is explicitly out of scope** — it depends on `CampaignAttachments`, not built until the Campanha — Anexos plan; a `campaignId` query parameter is accepted but ignored (documented as a known no-op until that plan lands, not silently dropped without explanation). `NpcSheetSummaryResponse(string Id, string Nome, string? Linhagem, string? Vocacao, string? SubVocacao, int Nivel)` — a lighter shape than the full `NpcSheetResponse`, matching how a filterable list view doesn't need every field.

- [ ] **Step 1: Write the contract**

`src/RuinaRPG.Contracts/NpcSheets/NpcSheetSummaryResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.NpcSheets;

public record NpcSheetSummaryResponse(string Id, string Nome, string? Linhagem, string? Vocacao, string? SubVocacao, int Nivel);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`:

```csharp
[Fact]
public async Task List_returns_only_the_callers_own_npcs()
// create 2 NPCs under different GMs, list as GM A, assert only theirs appears.

[Fact]
public async Task List_can_filter_by_partial_Nome_Linhagem_and_Nivel_together()
// create 2 NPCs with different Nome/Linhagem/Nivel (via Update, mirroring CharacterSheetsControllerTests'
// ValidUpdate pattern), filter by all 3, assert only the matching one is returned.
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter NpcSheetsControllerTests`
Expected: the 2 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 4: Add the `List` action**

```csharp
[HttpGet]
public async Task<ActionResult<List<NpcSheetSummaryResponse>>> List(
    [FromQuery] string? nome, [FromQuery] string? linhagem, [FromQuery] string? vocacao, [FromQuery] string? subVocacao, [FromQuery] int? nivel)
{
    var gmId = CurrentGmId();
    var query = db.NpcSheets.Where(s => s.GmId == gmId);

    if (!string.IsNullOrWhiteSpace(nome))
        query = query.Where(s => s.Nome != null && EF.Functions.ILike(s.Nome, $"%{nome}%"));
    if (linhagem is not null && Enum.TryParse<Linhagem>(linhagem, out var linhagemParsed))
        query = query.Where(s => s.Linhagem == linhagemParsed);
    if (vocacao is not null && Enum.TryParse<Vocacao>(vocacao, out var vocacaoParsed))
        query = query.Where(s => s.Vocacao == vocacaoParsed);
    if (!string.IsNullOrWhiteSpace(subVocacao))
        query = query.Where(s => s.SubVocacao == subVocacao);
    if (nivel is not null)
        query = query.Where(s => s.Nivel == nivel);

    return await query.Select(s => new NpcSheetSummaryResponse(s.Id.ToString(), s.Nome ?? "", s.Linhagem.ToString(), s.Vocacao.ToString(), s.SubVocacao, s.Nivel)).ToListAsync();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter NpcSheetsControllerTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/NpcSheets/NpcSheetSummaryResponse.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs
git commit -m "feat: add npc sheet listing with filters"
```

---

### Task 5: Client — NPCs do GM list page and sheet page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/NpcsDoGm.razor`
- Create: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `NpcSheetSummaryResponse`/`NpcSheetResponse`/`CreateNpcSheetRequest` (Tasks 1, 4) and every child-table contract from Task 3, the authenticated `HttpClient`.
- Produces: the `/npcs` and `/npcs/{id}` routes. No later task in this plan depends on these files.

- [ ] **Step 1: Write the list page**

`src/RuinaRPG.Client/Pages/NpcsDoGm.razor` — same shape as `Catalogo.razor` (Catálogo plan, Task 9): a filter bar (Nome, Linhagem, Vocação, Nível), a "Novo NPC" button posting to `POST /api/npc-sheets` and navigating to the new sheet, a table of `NpcSheetSummaryResponse` rows linking to `/npcs/{id}`.

- [ ] **Step 2: Write the sheet page**

`src/RuinaRPG.Client/Pages/FichaDeNpc.razor` — same structure as `FichaDePersonagem.razor` (all three Personagem plans combined), with every `character-sheets/{SheetId}/...` URL changed to `npc-sheets/{SheetId}/...` and no level-up notice section (R0002 is a Personagem-only feature — nothing in the NPC doc mentions it, and NPCs don't gain levels through play the same way).

- [ ] **Step 3: Add a nav link**

In `src/RuinaRPG.Client/Layout/NavMenu.razor`, add a `NavLink` to `/npcs` following the file's established pattern — label it "NPCs do GM".

- [ ] **Step 4: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Pages/NpcsDoGm.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: add npcs do gm list and sheet pages"
```

---

### Task 6: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files.

**Interfaces:**
- Consumes: the full stack. Produces: nothing new.

- [ ] **Step 1: Boot the stack, register a GM through nginx**

- [ ] **Step 2: Create an NPC sheet and set its Linhagem/Nome through nginx**

```bash
curl -sf -X POST http://localhost/api/npc-sheets -H "Authorization: Bearer $GM_TOKEN"
# → NPC_ID
curl -sf -X PUT "http://localhost/api/npc-sheets/$NPC_ID" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d '{"imageId":null,"nome":"Goblin Batedor","linhagem":null,"variante":null,"vocacao":null,"subVocacao":null,"afinidade":null,"propriedade":"","nivel":3,...}'
```

Expected: both succeed (201, 204).

- [ ] **Step 3: Confirm the NPC appears on the filtered list, through nginx**

```bash
curl -sf "http://localhost/api/npc-sheets?nome=Goblin" -H "Authorization: Bearer $GM_TOKEN"
```

Expected: HTTP 200, contains "Goblin Batedor".

- [ ] **Step 4: Tear down**

```bash
make down
```

- [ ] **Step 5: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify npc sheet flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- R0004 (a jogador seeing, at most, Nome/Imagem of an NPC attached to their campaign, each toggle independently controlled by the GM) — depends on `CampaignAttachments`, built by the Campanha — Anexos e Concessões plan.
- Filtering the NPC list by "Campanha vinculada" — same `CampaignAttachments` dependency; the query parameter is accepted and silently ignored until then (see Task 4's Interfaces note).
- Granting an NPC sheet to a player, turning it owner-having and switching its edit model to owner-or-GM (Campanha R0010) — also the Campanha — Anexos plan's job; this plan's `OwnerId` column exists but nothing ever sets it.
