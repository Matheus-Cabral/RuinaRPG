# Versão 2.0.0 — G: Documentation slimming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Slim and align the project documentation with the 2.0.0 code, as the roadmap's section G defines, without changing any game rule, number or requirement meaning.

**Architecture:** Safety nets first (a wikilink checker and a frozen copy of everything the API renders from `Docs/Sistema RPG/`), then one task per documentation area, each measured. Content decisions that the roadmap leaves to the user (removing requirements, any rulebook content change) go to the user as a list before they are applied.

**Tech Stack:** Markdown (Obsidian vault), Python 3 for the checker, xUnit integration tests for the frozen rulebook/Compêndio output.

**Spec:** `docs/superpowers/specs/2026-10-16-versao-2-0-0-roadmap-design.md`, section "G — Documentation slimming" (binding; the user chose on 2026-10-23 to follow it as written, without a separate G design document).

## Global Constraints

- Everything in `Docs/` stays in Brazilian Portuguese and uses Obsidian `[[wikilink]]` syntax; renamed files get their links updated.
- **Requirement IDs are never renumbered.** A removed requirement keeps its heading and is marked removed (e.g. `# **R0007** — removido na 2.0.0` plus one line saying why or where it moved); it is not deleted.
- **`Docs/Sistema RPG/`: form only.** Formatting, duplicated text, broken wikilinks. Game rules, numbers and wording that carries a rule are untouched; any content change is written into a proposals list for the user, never applied.
- The 14 runtime-parsed files listed in `CLAUDE.md` keep their table structure. The parser tests and the frozen API output (Task 1) must stay green; any rendered-output difference must be explained line by line as formatting-only.
- No change to `src/` behavior. Code changes are limited to test files and `tools/docs/`.
- `CLAUDE.md` is for the agent and does not repeat the README; `README.md` is for humans (what it is, how to run, how releases flow through the pipeline).
- Commit messages in Portuguese, ending with the session's attribution lines. Push after each task.
- Production (192.168.80.10) and the dev stack are not touched.

## Review Focus

1. A "form-only" edit in a runtime-parsed rulebook file that changes a table (column order, header text, separator) breaks parsing or changes a number on screen — Task 1's frozen output and the parser tests must catch it.
2. A requirement removed as "narrates implementation" was in fact the only place a behavior is specified — Task 5's list goes to the user first.
3. A spec moved to the archive is still referenced by live docs, code comments, `CLAUDE.md`, memory notes or the 2.1.0 backlog — Task 6 greps and updates every reference.
4. `Modelo de Dados` drifts from the EF model — Task 3 checks every table and column against the model snapshot.
5. README/CLAUDE.md commands that no longer work — Task 7 runs each command it documents (except deploy).

---

### Task 1: Safety nets — wikilink checker and frozen rulebook/Compêndio output

**Files:**
- Create: `tools/docs/verificar-wikilinks.py` (+ `tools/docs/README.md`, 3 lines)
- Create: `tests/RuinaRPG.Tests.Integration/Rules/LivroDeRegrasCongeladoTests.cs` + `livro-de-regras.json`, `compendio.json` (generated, committed)

- [ ] **Step 1:** Write `verificar-wikilinks.py`: scans `Docs/**/*.md` and `*.canvas`, resolves `[[name]]`, `[[name|alias]]`, `[[name#section]]` against every file under `Docs/` (with and without extension, as Obsidian does), prints `file: [[link]]` for each unresolved link and exits 1 if any. Run it now and record the baseline count in the report (expected: 0 or a short list).
- [ ] **Step 2:** Write the frozen-output test: render every `/api/rulebook` document whose content comes from Markdown resources (exclude `equipagem`, which is database-driven and already frozen by `RulebookEquipagemCongeladoTests`, and any other document built from database rows — check `RulebookRenderer`), and the Compêndio search index/listing (`/api/compendio`, read `CompendioController` for what it serves). Compare to committed JSON files generated from the current code with a temporary `Gerar` test that is deleted before commit (same pattern as `RotasDaApiTests`). Run 3× for stability.
- [ ] **Step 3:** Commit `test: livro de regras e Compêndio congelados antes do G`.

### Task 2: `Docs/Sistema RPG/` — form only

**Files:** Modify: files in `Docs/Sistema RPG/` (Markdown only; images, `.docx`, `Fichas/` untouched). Create: `docs/superpowers/specs/2026-10-23-g-propostas-sistema-rpg.md`.

- [ ] **Step 1:** For each file: fix formatting (heading levels, list/table syntax, spacing), remove duplicated text (exact or near-exact repeats of a passage), fix broken wikilinks (checker from Task 1). Do not change a word that carries a rule or a number. For the 14 runtime-parsed files, do not touch tables at all unless a table is plainly malformed Markdown — and then show the before/after rendered row.
- [ ] **Step 2:** Anything that looks like a content problem (contradiction, typo inside a number/rule, outdated rule text) goes into the proposals file as `file — passage — what seems wrong — suggested change`. Nothing there is applied.
- [ ] **Step 3:** Run the checker (0 broken), the parser tests (`dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~Rules`), and the frozen-output test. If the frozen output changed, regenerate it only after writing, in the report, every changed line with its formatting-only reason; any non-formatting change is reverted.
- [ ] **Step 4:** Commit `docs: Sistema RPG — só forma`.

### Task 3: `Modelo de Dados` aligned with the 2.0.0 schema

**Files:** Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`

- [ ] **Step 1:** List every table and column from `src/RuinaRPG.Infrastructure/Persistence/Migrations/RuinaRpgDbContextModelSnapshot.cs` (the authority) and compare with the doc: missing tables/columns, columns documented but gone, wrong types/nullability/FK targets. Record the comparison table in the report.
- [ ] **Step 2:** Fix the doc to match the snapshot; keep its legend and linking-pattern conventions; remove text that restates code without adding meaning (keep column meanings). Run the wikilink checker.
- [ ] **Step 3:** Commit `docs: Modelo de Dados alinhado ao esquema da 2.0.0`.

### Task 4: `Técnico` aligned with the pipeline and the 2.0.0 code; the canvas

**Files:** Modify: `Docs/Requisitos/Requisitos - Técnico.md`, `Docs/Requisitos/01 - Visão Geral.canvas`

- [ ] **Step 1:** Update Técnico to the 2.0.0 reality without renumbering: solution layout (generic sheet controllers `Controllers/Fichas/`, `Contracts/Fichas/`, `Shared/Ficha/`), deploy (`deploy/` blue-green, the one `deploy/Dockerfile`, nginx compression/cache), the pipeline (R0013–R0016 as implemented), migrations baseline (R0009), tests (safety nets). Remove narration of implementation details that the code now states better, keeping the decision and its reason.
- [ ] **Step 2:** The canvas predates several pages (Banco de Magias, Encontros, Compêndio, Técnico…). Update it to show the current page/panel flow (all requirement docs as nodes, with the GM/Player flows); if that is not practical in the canvas JSON, replace it with a short Markdown flow list in a new `01 - Visão Geral.md` and keep the canvas file removed — every `[[01 - Visão Geral.canvas]]` link is then updated. Report which option and why.
- [ ] **Step 3:** Wikilink checker 0 broken. Commit `docs: Técnico e visão geral da 2.0.0`.

### Task 5: `Docs/Requisitos/` — redundancy and implementation narration (user checkpoint)

**Files:** Create: `docs/superpowers/specs/2026-10-23-g-requisitos-a-remover.md`. Modify: requirement docs.

- [ ] **Step 1 — list, don't edit:** For `Ficha de NPCs` and `Ficha de Criaturas` (defined as diffs of `Ficha de Personagem`), find passages that restate the Personagem doc instead of stating the difference. For every requirements doc, find requirements or paragraphs that only narrate implementation (class names, endpoints, internal steps) without a user-visible rule. Write each candidate as `doc — R#### or passage — category (restates/narrates) — proposed action (remove / mark removed / shorten to …)`.
- [ ] **Controller checkpoint:** the controller takes the list to the user in one message with a recommendation; only approved items are applied.
- [ ] **Step 2 — apply the approved items:** removed requirements keep their heading and are marked removed; restating passages are replaced by a link to the Personagem section; shortened text keeps every rule. Wikilink checker 0 broken.
- [ ] **Step 3:** Commit `docs: requisitos sem redundância e sem narração de implementação`.

### Task 6: `docs/superpowers/` — archive shipped work

**Files:** Move: shipped specs/plans to `docs/superpowers/archive/specs/` and `docs/superpowers/archive/plans/` (`git mv`). Modify: every reference.

- [ ] **Step 1:** Classify every file in `docs/superpowers/specs` and `plans`: **keep** (still-valid decisions: the 2.0.0 roadmap, baseline, H cutover notes, the 2.1.0 backlog, the D and E specs while their "Divergências encontradas"/"Differences decided" await the user, this plan until G ships) or **archive** (shipped work). Write the list into the report.
- [ ] **Step 2:** `git mv` the archived files. Grep the whole repo (`Docs/`, `src/`, `tests/`, `deploy/`, `tools/`, `.github/`, `README.md`, `CLAUDE.md`, the kept specs) for each moved path and update it. Report the list of updated references; the controller updates the agent memory notes itself.
- [ ] **Step 3:** Commit `docs: specs e planos já entregues no arquivo`.

### Task 7: README for humans, CLAUDE.md for the agent

**Files:** Modify: `README.md`, `CLAUDE.md`

- [ ] **Step 1:** README (Portuguese, like today): what Ruína RPG and the app are; how to run locally (`.env`, `make deploy`, health check); how to build and test (the three projects run one at a time); how releases flow (PR → clone of production → auto-merge → deploy); where the docs are. No content that only an agent needs.
- [ ] **Step 2:** CLAUDE.md (English, like today): shorter; keep only what an agent needs that the README doesn't give (Docs/ is the source of truth, TDD rule, the 14 parsed files and the Auditoria exceptions, Docs conventions, code map, safety nets, migration rules); link to the README for run/build instead of repeating it.
- [ ] **Step 3:** Run every command the README documents except `make deploy`/`make migrate`/rollback (build, test projects, the checker) and confirm they work. Commit `docs: README para pessoas e CLAUDE.md para o agente`.

### Task 8: Measurements

**Files:** Modify: `docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md` ("After G")

- [ ] **Step 1:** Word counts before (commit at G start) and after for: `Docs/Requisitos/`, `Docs/Sistema RPG/` (Markdown), live `docs/superpowers/` (excluding archive), `README.md`, `CLAUDE.md`; broken wikilinks before/after; number of requirements marked removed. Same counting method both sides, stated.
- [ ] **Step 2:** Full `dotnet build` (0/0) and the three test projects one at a time. Commit `docs: medições depois do G`.
