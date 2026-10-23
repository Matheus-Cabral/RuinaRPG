# Versão 2.0.0 — E: Client consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the three near-duplicate sheet pages (`FichaDePersonagem`, `FichaDeNpc`, `FichaDeCriatura`) with short pages that assemble shared tab components, with no visible change except the user-decided unifications.

**Architecture:** A safety net of bUnit tests pins what each tab renders and requests today, for each kind. Then identical per-kind `Contracts` records are merged. A client-side kind description (`TipoDeFichaCliente`) and a shared, cascaded sheet state (`FichaEstado`, partial per tab) take over the data and HTTP logic, one tab at a time, while each tab's markup moves to a shared `Aba*.razor` component. Kind-only parts become their own components.

**Tech Stack:** .NET 8, Blazor WebAssembly, MudBlazor, bUnit + xUnit + FluentAssertions, ASP.NET Core API (type renames only).

**Spec:** `docs/superpowers/specs/2026-10-19-versao-2-0-0-e-cliente-design.md`. Read it first. Its "Differences decided" table is binding, and Task 1 may add rows to it.

## Global Constraints

- The user sees no change except the rows of the spec's "Differences decided" table.
- JSON on the wire does not change for any endpoint; routes don't change; `RotasDaApiTests` snapshot is never regenerated.
- Capability flags are named after the capability (`TemDiario`, `EditaNivelAManual`), never after the kind (`IsCriatura`).
- A shared component may branch on a flag for a small difference. A branch that grows beyond a few lines, or a second branch piling up in one place, becomes its own component.
- Each page keeps its `@page` route, tab titles and tab order.
- The E.1 safety-net tests (`tests/RuinaRPG.Tests.Client/Pages/Ficha/RedeDeSeguranca/`) are changed only in Task 4 (decided unifications), never together with the production code they guard.
- Safety-net tests fake HTTP with raw JSON (anonymous objects / string literals), never `Contracts` types.
- TDD (Técnico R0011): every new C# class gets its failing test first.
- `dotnet build` ends with 0 warnings, 0 errors after every task. Client suite green after every task. Whole `dotnet test` (needs Docker) green after Tasks 5 and 15.
- UI uses MudBlazor icons, never emojis.
- Commit after every task, push as work progresses (`git push`). Commit messages in Portuguese, like the history (`refactor: …`, `test: …`, `docs: …`).
- D's server follow-ups are out of scope. On the server, only type names change (Task 5).

## Review Focus

1. **Auto-save across moved fields:** editing a field inside a shared tab must still go through the page's `AutoSaveCoordinator`, so the `AutoSaveIndicator` shows the save. The deferred reload (`RecargaAdiada`, `@onfocusout` on the `ficha-condicoes-raiz` div) must still defer while focus is inside a child component. *Test in Task 10.*
2. **Live reload on `CondicoesAlteradas`:** a hub notification must re-render every tab, including the ones moved into child components, with fresh data: same GET count as today. *Test in Task 6.*
3. **Player vs GM:** controls that depend on `_isGmCaller`/`_campaignId` (e.g. `RuneOrigemFields CampaignId`, image pickers) behave the same for a player as today. *Test in Task 7.*
4. **Errors from a child tab:** a failed POST/PUT in a shared tab still shows its message in the page's `DismissibleAlert`. *Test in Task 6, using `FichaEstado.Erro`.*
5. **Creature manual weapon** (`ItemId` null, no `Peso`, `DurabilidadeMaximo`): it renders in the shared Combate tab without a crash and shows the same columns as today. *Test in Task 12.*

---

## File Structure

| Path | Responsibility |
|---|---|
| `src/RuinaRPG.Contracts/Fichas/*.cs` (new) | Merged per-kind records (Task 5) |
| `src/RuinaRPG.Client/Shared/Ficha/TipoDeFichaCliente.cs` | Kind description: API prefix + capability flags; static `Personagem`, `Npc`, `Criatura` |
| `src/RuinaRPG.Client/Shared/Ficha/FichaEstado.cs` (+ `FichaEstado.<Aba>.cs` partials) | Loaded sheet data and shared HTTP actions; `Erro`, versions, change notification |
| `src/RuinaRPG.Client/Shared/Ficha/AdaptadorDeCriatura.cs` | Maps the Creature records that stay separate onto the merged records |
| `src/RuinaRPG.Client/Shared/Ficha/Aba*.razor` | One shared component per tab |
| `src/RuinaRPG.Client/Shared/Ficha/<KindOnly>.razor` | Diário, Nível manual, Espólios, Creature-only fragments |
| `src/RuinaRPG.Client/Pages/FichaDe*.razor` | Short pages: route, page-level state, tab layout |
| `tests/RuinaRPG.Tests.Client/Pages/Ficha/RedeDeSeguranca/*.cs` | E.1 safety net |
| `tests/RuinaRPG.Tests.Client/Shared/Ficha/*.cs` | Tests of the new classes/components |
| `docs/superpowers/specs/2026-10-19-e-inventario-de-diferencas.md` | Per-tab difference inventory (Task 1) |

---

### Task 1: Difference inventory + safety-net harness + header and Informações Básicas net

**Files:**
- Create: `docs/superpowers/specs/2026-10-19-e-inventario-de-diferencas.md`
- Create: `tests/RuinaRPG.Tests.Client/Pages/Ficha/RedeDeSeguranca/RedeDeSegurancaBase.cs`
- Create: `tests/RuinaRPG.Tests.Client/Pages/Ficha/RedeDeSeguranca/RedeCabecalhoEInformacoesBasicasTests.cs`

**Interfaces:**
- Produces: `abstract class RedeDeSegurancaBase : MudBunitContext` with:
  - `protected IReadOnlyList<string> Requisicoes`: entries `"{METHOD} {path}"` plus, for non-GET, the body as compact JSON (`"PUT /npc-sheets/s1/skills/Atletismo {\"gasto\":2,...}"`);
  - `protected void Responder(string sufixoOuRegex, object json)` to override one endpoint;
  - `protected Task<IRenderedComponent<IComponent>> RenderFichaAsync(string tipo, string papel = "GM")` with `tipo ∈ {"Personagem","NPC","Criatura"}`, rendering the matching page with `SheetId = "s1"` and rich default JSON per kind: one row in every list (attribute, skill, weapon with item, armor slot, shield, spell, rune, mastery, inventory item, artifact, affection, trait, spoil, diary entry), so every row-rendering branch shows;
  - `protected Task IrParaAbaAsync(IRenderedComponent<IComponent> cut, string aba)`;
  - `protected static string TextoNormalizado(IElement e)` (reuse `TextNormalization`).
  Later net tasks extend it.

- [ ] **Step 1: Write the difference inventory.** For each section in the spec's findings table, diff the three pages with type names normalized. Classify every difference as **real** (backed by a different endpoint/record, a capability, or an earlier spec), **decided** (already in "Differences decided"), or **accidental?** (open). One table per tab, columns `Difference | P | N | C | Class | Proposed flag/component`. End with the proposed capability flag list for `TipoDeFichaCliente` and the proposed kind-only components.
- [ ] **Step 2: Write the harness and the net tests for the header (breadcrumbs, title, level-up notice, condições alert, auto-save indicator) and Informações Básicas, for each kind as GM and as player.** Assert visible labels/values in DOM order, and the requests made by: page load (full ordered GET list), one auto-saved field edit, image change, Linhagem/Variante change.
- [ ] **Step 3: Run** `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~RedeDeSeguranca`. Expected: PASS on the current code. A net test that fails on unchanged code is wrong; fix the test.
- [ ] **Step 4: Commit** `test: rede de segurança do E — cabeçalho e Informações Básicas` with the inventory doc.

> **Controller checkpoint after Task 1:** take every "accidental?" row to the user in one message, with a recommendation each. Record answers in the spec's "Differences decided" table and commit before Task 4.

### Task 2: Safety net — Atributos & Perícias and Combate

**Files:**
- Create: `tests/RuinaRPG.Tests.Client/Pages/Ficha/RedeDeSeguranca/RedeAtributosEPericiasTests.cs`, `RedeCombateTests.cs`
- Modify: `RedeDeSegurancaBase.cs` only to add fixtures

**Interfaces:** Consumes `RedeDeSegurancaBase` (Task 1).

- [ ] **Step 1: Write net tests** `[Theory]` over the kinds that have each section. Rendered text: attributes table, budgets ("Gasto / Disponível" on Personagem, "Pontos de Perícia Gastos: N" on NPC/Criatura), skills, sub-attributes, modificador de dano, affinities, weapons/armor/shields, maestrias, Vitalidade/Foco order as it is **today**. Requests made by: attribute edit, skill gasto edit, skill atributo edit, add/delete weapon, durability edit, armor slot assign, add shield, add/delete affinity, and every follow-up reload GET in order.
- [ ] **Step 2: Run** the filter from Task 1. Expected: PASS.
- [ ] **Step 3: Commit** `test: rede de segurança do E — Atributos & Perícias e Combate`.

### Task 3: Safety net — Magias, Posses, Antecedentes, Diário and kind-only parts

**Files:**
- Create: `RedeMagiasTests.cs`, `RedePossesTests.cs`, `RedeAntecedentesEDiarioTests.cs`, `RedePartesPorTipoTests.cs` in the same folder

- [ ] **Step 1: Write net tests** for: spell list + add from zero/from bank + edit + delete; passivas section URL; runes (table on Personagem, line on NPC, **today's** form) add/delete; inventory/spoils, artifacts, affections add/qty/delete; Equipagem dialog open; Antecedentes (Personagem full, NPC short); Diário add/edit/delete; Nível/Experiência manual (NPC, Criatura); XP/PI/crítico deltas (Personagem); Espólios (Criatura); racial trait choice dialog (Personagem/NPC).
- [ ] **Step 2: Run** the filter. Expected: PASS.
- [ ] **Step 3: Commit** `test: rede de segurança do E — Magias, Posses, Antecedentes, Diário e partes por tipo`.

### Task 4: Apply the decided unifications (the only visible change)

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`, `FichaDeCriatura.razor` (and Personagem only if a decision says so)
- Modify: the safety-net tests that pin a unified difference

- [ ] **Step 1: Change the net assertions** for each "Differences decided" row whose decision is "Personagem version for all": Vitalidade/Foco and other swapped orders, Runas table, and the rows added at the checkpoint. Assert the Personagem version for every kind. Run: the changed tests FAIL for NPC/Criatura.
- [ ] **Step 2: Change the pages** to match. Run the whole client suite. Expected: PASS.
- [ ] **Step 3: Commit** `feat: unifica diferenças acidentais entre as fichas (decididas pelo usuário)`. List each row in the body. Add a line to `ChangelogDialog` only if the user asks; not by default.

### Task 5: Merge the identical per-kind `Contracts` records

**Files:**
- Create: `src/RuinaRPG.Contracts/Fichas/<Record>.cs`, one per merged record
- Delete: the per-kind duplicates
- Modify: API (`Controllers/Fichas/*`, per-kind subclasses, `Services/*`), client pages, `tools/RuinaRPG.Smoke`, tests that name the old types

**Interfaces:**
- Produces: shared record names = the Character name minus the kind word, in namespace `RuinaRPG.Contracts.Fichas` (e.g. `CharacterWeaponResponse` → `WeaponResponse`, `AddCharacterWeaponRequest` → `AddWeaponRequest`, `CharacterSkillResponse` → `SkillResponse`). Where a name already exists elsewhere in `Contracts` (check `Runes`, `SpellsAndAbilities`), suffix `DaFicha` (e.g. `AddRuneDaFichaRequest`).
- Merge rule: a record is merged for the kinds whose normalized definition (type names removed, comments ignored) is identical. Creature records that differ (weapon, armor slot, skill, trait, sheet response/update, add weapon) stay in `CreatureSheets`. `NpcSheetResponse`/`UpdateNpcSheetRequest` stay.
- D generic controllers: a type parameter that now always receives the same type is removed.

- [ ] **Step 1: Write a unit test** `ContractsDasFichasTests.Each_per_kind_record_left_has_no_identical_twin` in `tests/RuinaRPG.Tests.Unit`. Reflect over `RuinaRPG.Contracts` and fail if two records in `CharacterSheets`/`NpcSheets`/`CreatureSheets` have identical normalized constructor signatures (parameter names + types, kind words removed). Run: FAIL (27+ pairs).
- [ ] **Step 2: Merge** until the test passes. Fix every compile error by renaming types only; no logic change.
- [ ] **Step 3: Run** `dotnet build` (0/0) and the whole `dotnet test`. Expected: PASS, `RotasDaApiTests` unchanged, safety net unchanged.
- [ ] **Step 4: Commit** `refactor: unifica os records de Contracts iguais entre os tipos de ficha`.

### Task 6: `TipoDeFichaCliente`, `FichaEstado` core and `AdaptadorDeCriatura`, cascaded by the pages

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Ficha/TipoDeFichaCliente.cs`, `FichaEstado.cs`, `AdaptadorDeCriatura.cs`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Ficha/TipoDeFichaClienteTests.cs`, `FichaEstadoTests.cs`, `AdaptadorDeCriaturaTests.cs`
- Modify: the three pages, so each creates one `FichaEstado` and wraps its tabs in `<CascadingValue Value="_estado">` (`IsFixed="false"`)

**Interfaces:**
- Produces:
  - `public sealed record TipoDeFichaCliente(string Nome, string Rota, …flags)`. `Rota` is `"character-sheets"`, `"npc-sheets"` or `"creature-sheets"`. Flags are the list approved after Task 1, at least `TemDiario`, `TemAntecedentes`, `AntecedentesCompletos`, `EditaNivelAManual`, `TemOrcamentoDePericias`, `TemEspolios`, `TemInventario`, `TemRunas`, `TemAfinidades`, `TemTracosRaciais`, `EditaMaestriaNaLinha`, `EditaAfeicaoNaLinha`, `TemArmaManual`. Static `Personagem`, `Npc`, `Criatura`. `string Url(string sheetId, string sufixo = "")` → `$"{Rota}/{sheetId}{(sufixo == "" ? "" : "/" + sufixo)}"`.
  - `public sealed partial class FichaEstado(HttpClient http, TipoDeFichaCliente tipo, string sheetId)` with `TipoDeFichaCliente Tipo`, `string SheetId`, `HttpClient Http`, `string? Erro { get; set; }`, `int PenalidadesVersao`, `int CondicoesVersao`, `bool IsGmCaller`, `string? CampaignId`, `List<ImageSummaryResponse> MinhasImagens`, `event Action? Alterado`, and `void NotificarAlteracao()`. Data lists and actions move in later tasks as partials.
  - `public static class AdaptadorDeCriatura` with one `Para…(CreatureXResponse) → XResponse` per Creature record that stays separate. Missing fields get neutral values (`Peso` 0, `ContaHistorico` false, `IsRacial` false). The Creature `DurabilidadeMaximo` maps to the shared `DurabilidadeMaxima`.
- The page subscribes to `Alterado` with `InvokeAsync(StateHasChanged)` and unsubscribes in `Dispose`. The page's `_errorMessage` alert binds to `_estado.Erro`.

- [ ] **Step 1: Write failing tests:** `TipoDeFichaClienteTests` (`Rota` and `Url` per kind; each flag's value per kind per the inventory); `FichaEstadoTests.Notificar_alteracao_raises_Alterado`; `AdaptadorDeCriaturaTests` (every field mapped, null `ItemId` kept, `DurabilidadeMaximo` → `DurabilidadeMaxima`). Run: FAIL (types missing).
- [ ] **Step 2: Implement** the three classes. Change the pages to create/cascade the state and bind the error alert to it. Run the tests: PASS.
- [ ] **Step 3: Add the Review Focus tests** in `FichaEstadoNaPaginaTests`: (2) a `CondicoesAlteradas` notification re-renders a moved field with the same GET count as before; (4) setting `Erro` from a child shows it in the page alert. Run: PASS.
- [ ] **Step 4: Run** the client suite; the net is unchanged. Commit `refactor: TipoDeFichaCliente e FichaEstado nas três fichas`.

### Tasks 7–12: One shared tab per task

Every tab task follows the same steps. Only the tab, its files and its kind-only parts differ.

- [ ] **Step 1: Write failing tests** for any new C# logic of the tab in `tests/RuinaRPG.Tests.Client/Shared/Ficha/FichaEstado<Aba>Tests.cs` (request URL/body per kind; reload calls in today's order). Run: FAIL.
- [ ] **Step 2: Move the tab's data fields and actions** from the three pages into `FichaEstado.<Aba>.cs`. Use `Tipo.Url(...)` for addresses and the merged records; map Creature records through `AdaptadorDeCriatura`. The request order and count stay exactly as today.
- [ ] **Step 3: Create `Aba<Aba>.razor`** with `[CascadingParameter] FichaEstado Estado`. It holds the markup from Personagem, with flag branches only where the anti-goal allows; anything bigger goes into a kind-only component. Replace the tab body in each page with `<Aba… />`.
- [ ] **Step 4: Run** the client suite. Expected: all PASS, safety net untouched. Then `dotnet build` (0/0).
- [ ] **Step 5: Commit** `refactor: aba <Aba> compartilhada entre as fichas`.

| Task | Tab | Files | Kind-only parts / notes |
|---|---|---|---|
| 7 | Magias & Habilidades | `AbaMagiasEHabilidades.razor`, `FichaEstado.Magias.cs` | Runas section only where `TemRunas`. Review Focus (3): player `RuneOrigemFields CampaignId` test in `AbaMagiasEHabilidadesTests` |
| 8 | Posses | `AbaPosses.razor`, `FichaEstado.Posses.cs` | `EspoliosSection.razor` (`TemEspolios`); inventory where `TemInventario`; Equipagem dialog |
| 9 | Antecedentes | `AbaAntecedentes.razor`, `FichaEstado.Antecedentes.cs` | short vs full via `AntecedentesCompletos`; tab not rendered where `!TemAntecedentes` |
| 10 | Informações Básicas | `AbaInformacoesBasicas.razor`, `FichaEstado.InformacoesBasicas.cs` | `NivelManualSection.razor` (`EditaNivelAManual`); the auto-saved form stays page-owned and is passed in. Review Focus (1): `AbaInformacoesBasicasTests.Edit_inside_tab_shows_auto_save_and_defers_reload` |
| 11 | Atributos & Perícias | `AbaAtributosEPericias.razor`, `FichaEstado.Atributos.cs` | `CarregarAtributosECombateAsync()` = today's `LoadTabs2And3Async` (same GETs, same order); Creature body as its own component if the inventory says it differs that much |
| 12 | Combate | `AbaCombate.razor`, `FichaEstado.Combate.cs` | `ArmaManualForm.razor` (`TemArmaManual`); inline edit of Maestrias/Afeições by flag. Review Focus (5): `AbaCombateTests.Creature_manual_weapon_renders_like_today` |

### Task 13: Kind-only parts and slim pages

**Files:**
- Create: `Shared/Ficha/DiarioDaFicha.razor` + `FichaEstado.Diario.cs` (Personagem); a component for XP/PI/crítico deltas if it is still inline in a page
- Modify: the three pages

- [ ] **Step 1: Write failing tests** for any new logic (Diário URLs, delta requests). Run: FAIL.
- [ ] **Step 2: Extract** what is still inline in the pages. Each page keeps only: route, injected services, `FichaEstado` creation and lifecycle, page-level state (breadcrumbs, header, auto-save, `RecargaAdiada`, condições/SignalR, level-up notice) and the tab layout. A common page base or a header component (`CabecalhoDaFicha.razor`) is extracted if the three headers still repeat after the move.
- [ ] **Step 3: Check** with `grep` that no `Pages/FichaDe*.razor` contains markup of a tab a shared component provides. Run the client suite: PASS.
- [ ] **Step 4: Commit** `refactor: partes exclusivas de cada ficha e páginas enxutas`.

### Task 14: Collapse per-kind client tests into theories

**Files:**
- Modify: `tests/RuinaRPG.Tests.Client/Pages/Ficha*Tests.cs` (the pre-E ones; **not** `RedeDeSeguranca/`)

- [ ] **Step 1: Find** tests repeated per kind (same body, different page type or prefix) and turn them into `[Theory]` over the kinds that have the feature. Tests that only render one shared component may move to `tests/RuinaRPG.Tests.Client/Shared/Ficha/`. No test loses an assertion; the test count may drop only by merged duplicates.
- [ ] **Step 2: Run** the client suite: PASS. Commit `test: testes das fichas como teorias por tipo`.

### Task 15: Measurements, docs and deploy to the dev stack

**Files:**
- Modify: `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md` (new "After E" section), the E spec status line, `CLAUDE.md` (code map: `Shared/Ficha/`, `Contracts/Fichas/`)

- [ ] **Step 1: Measure** on `30a8fa4` (before; use a `git worktree`) and HEAD (after): lines of the three pages, of `Shared/Ficha/`, the client `.razor`/`.cs` total, `Contracts` lines, client test count and time (3 runs), the whole-solution build time, and `_framework` size. Report against the spec's reference numbers (guess 2 500–3 000 for pages + shared components). No number is a gate.
- [ ] **Step 2: Run** `dotnet build` (0/0) and the whole `dotnet test`: PASS. Commit `docs: medições depois do E`.
- [ ] **Step 3: Deploy to the dev stack** (192.168.80.16, this server): `make deploy`, then `make migrate`. Check that `curl -s http://localhost/api/health` prints `OK`. Report to the user that the stack is ready for the manual check of the three sheets.
- [ ] **Step 4: Push.**
