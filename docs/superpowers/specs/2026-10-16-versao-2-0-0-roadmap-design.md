# Versão 2.0.0 — Roadmap — Design

**Status:** design approved by user 2026-10-16 (in chat). Version 2.0.0, branch `release/2.0.0` (from
`main` at `729dac9`, i.e. 1.5.0 merged).

This is the umbrella document. It records every decision taken while brainstorming 2.0.0 and splits the
work into sub-projects. Each sub-project gets its own detailed spec and plan when its turn comes; the
detailed spec may refine, but not contradict, what is decided here without going back to the user.

## Goal

A refactor of the whole project that:

1. **reduces and simplifies the code** (less duplication, smaller files);
2. **shortens response, load, deploy and automated-test times**;
3. **automates releases**: a PR to `main` is validated against a clone of production, refused with
   diagnostics if anything breaks or any data is lost, and otherwise deployed to production **without
   taking the server down**;
4. **slims the documentation** and makes `README.md` coherent with the rest;
5. ships the 1.5.0 → 2.0.0 upgrade **safely**, since it is a critical update touching production data.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| What may change | **Same behavior for the end user; internals free.** API routes, `Contracts` records and component structure may change freely (client and API always ship together). The DB schema may change only in **data-neutral** ways. |
| The 169 EF Core migrations | **Squash** into one 2.0.0 baseline migration. Production's `__EFMigrationsHistory` is rewritten by an explicit deploy step, after a backup, without touching data. Production must be on 1.5.0's last migration first. |
| Order | **0 → A → F → B → C → D → E → G → H** (see table below). |
| Where the CI runner and the production clone live | **This dev VM (192.168.80.16)**, as a self-hosted GitHub Actions runner. The clone is a throwaway `docker compose` project. Needs SSH from this VM to the production VM. |
| Runner security (the repo is **public**) | **Stay public, with locks**: workflows touching the runner only run for PRs whose head branch is in this repo and whose author is the owner; fork PRs need manual approval; the runner runs as a user without sudo; production secrets live in a GitHub *Environment* restricted to `main`. |
| Incompatible migration (old API can't run on the new schema) | **Short maintenance window, invisible to the user**: no message — requests are held and retried by the client, so loading just takes a little longer. Compatible migrations deploy with zero downtime. |
| On a passing PR | **Auto-merge** into `main`, which triggers the production deploy. |
| On a failing PR | Check fails, a diagnostic comment is posted, full logs attached as artifacts, PR converted back to **draft**. |
| Intentional data changes | Declared per PR in an allowlist file (`deploy/data-changes.yml`); anything not declared is data loss. |
| Docs in scope | `Docs/Requisitos/`, `README.md` + `CLAUDE.md`, `docs/superpowers/`, **and** `Docs/Sistema RPG/` (form only — see G). |

## Sub-projects

| # | Sub-project | Main gain | Why it sits here |
|---|---|---|---|
| 0 | Baseline measurement | Reference numbers | Everything else is measured against it |
| A | Test infrastructure | Test time, reliability | Speeds up every later sub-project |
| F | Validated pipeline + zero-downtime deploy | Release safety, no downtime | Validates B/D/E against real data |
| B | Migration squash | Build and test time | Validated by F's clone |
| C | Deploy and load performance | Deploy and load time | Builds on F's long-lived nginx |
| D | Backend consolidation | Code size | Big refactor, with A and F as safety net |
| E | Client consolidation | Code size, maintainability | Same |
| G | Documentation slimming | Docs size, coherence | Describes the final code |
| H | 1.5.0 → 2.0.0 cutover | Safe release | Last; rehearsed runbook |

### 0 — Baseline measurement

Measured on `origin/main` before any change, recorded in
`docs/superpowers/specs/2026-10-16-versao-2-0-0-baseline.md` and updated as each sub-project lands.
Detailed in the Spec 0+A.

### A — Test infrastructure

One API host per test class instead of one per test; a template database migrated and seeded once per
run and cloned per class; a `Testing` environment that skips startup migration/seeding; duplicated test
helpers moved into a shared base. Detailed in
`docs/superpowers/specs/2026-10-16-versao-2-0-0-0-a-testes-design.md`.

### F — Validated pipeline + zero-downtime deploy

**F.1 — PR flow (`release/*` → `main`), GitHub Actions:**

1. **Gate** (GitHub-hosted): stops unless the PR's head repo is this repo and the author is the owner.
2. **ci** (self-hosted): build with 0 warnings; unit, client and integration tests.
3. **clone** (self-hosted):
   - `pg_dump` of production + `rsync` of the `images` volume over SSH;
   - bring up the **current** version (`main`) in a throwaway compose project (`ruinarpg-clone`, own
     port, not reachable from outside) with that data;
   - **fingerprint A**: row count per table + hash of every row over its pre-existing columns;
   - deploy the PR's version onto it with the **same script used in production** (`deploy/deploy.sh`);
   - **fingerprint B** and compare: a lost row or a changed value in a pre-existing column fails,
     unless declared in `deploy/data-changes.yml`;
   - **smoke suite**: HTTP tests that log in as the clone's real accounts (temporary password created
     through the CLI) and open every sheet, campaign, encounter and catalog, asserting 200 + valid payload;
   - **compatibility check**: the *old* API against the migrated schema runs the read-only smoke; failure
     marks the deploy as "with window" instead of "zero downtime";
   - tear the clone down; production data never stays on disk.
4. **Failure**: check fails, a comment with the failed step, data diff (table/column), failing tests and
   API log tail; full logs as artifacts; PR back to draft.
5. **Success**: auto-merge.

**F.2 — Production deploy (push to `main`)**, job bound to the `production` Environment, running
`deploy/deploy.sh` on the production VM over SSH:

1. Pre-deploy backup (`pg_dump` + `images` snapshot), keeping the last N.
2. **Compatible**: build images; start `api-green`; migrate; health check; `nginx -s reload` onto green;
   drain and stop `api-blue`. Zero downtime.
3. **Incompatible**: nginx answers 503 (the client holds and retries); migrate; start `api-green`; health
   check; switch back. A few seconds of slower loading, no message.
4. **Post-deploy smoke** against production; on failure **automatic rollback** (old API back; in the
   incompatible case also restore the backup taken inside the window — no writes happened during it).
5. Result posted on the merged PR.

**F.3 — Structural changes it requires:**

- nginx becomes a **long-lived proxy**: config rendered from a template with the active upstream; the
  Blazor client's static files live in a versioned volume switched atomically (symlink), so serving a new
  client never restarts nginx.
- A client-side `DelegatingHandler` retries on 502/503/connection failure with backoff for up to ~90 s
  (this is what makes the maintenance window invisible). SignalR already reconnects on its own.
- The client compares its `AppVersionInfo` against `/api/version` and, when they differ, shows a discreet
  toast (MudBlazor icon) asking to reload.
- New requirements in `Requisitos - Técnico.md` (pipeline, blue-green, backup/rollback); R0009 and the
  Makefile updated. `make deploy` / `make migrate` stay as the manual emergency path, calling the same
  `deploy.sh`.

### B — Migration squash

Replace the 169 migrations with one 2.0.0 baseline generated from the current model; the schema must be
byte-identical (`pg_dump --schema-only` diff of a DB migrated with the old chain vs the new baseline is
empty). An explicit, idempotent deploy step rewrites `__EFMigrationsHistory` on databases that are on
1.5.0's last migration and refuses to run on any other state. The `Persistence/*MigrationTests` that test
individual historical migrations are removed with them. Validated end-to-end by F's clone.

### C — Deploy and load performance

- `.dockerignore`; Dockerfiles copy `*.csproj` and `restore` before copying the source, so NuGet layers
  are cached; a shared build stage so `Domain`/`Contracts` are not compiled twice per deploy.
- nginx: gzip/brotli (or pre-compressed `_framework` files), long-lived cache headers for fingerprinted
  assets, no-cache for `index.html`.
- Blazor WASM: trimming, `InvariantGlobalization` where it does not change behavior (pt-BR formatting must
  be checked), lazy-loading of rarely used assemblies; measure before and after.
- API response time: profile the slowest endpoints found in 0 (N+1 queries, missing `AsNoTracking`,
  missing indexes) — fixed only where measured.

### D — Backend consolidation

The Character/NPC/Creature sheets repeat the same feature groups (Arsenal, Attributes, Masteries,
Possessions, Skills, SpellAbilities, Runes, Affinities, Equipagem): 74 files in `Controllers/`, e.g.
`NpcSkillsController` vs `CharacterSkillsController` differ by ~75 diff lines once type names are
normalized. Consolidate into shared generic controllers/services parameterized by sheet kind; the
`*SheetStats` services likewise. Integration tests for the three kinds collapse into shared theories.
Schema changes, if any, are data-neutral and validated by F.

### E — Client consolidation

`FichaDePersonagem.razor` (2109 lines), `FichaDeNpc.razor` (1924), `FichaDeCriatura.razor` (1490) and
`CampanhaDetalhe.razor` (1034) are split into shared tab components parameterized by sheet kind; bUnit
tests follow.

### G — Documentation slimming

- **`Docs/Requisitos/`**: remove redundancy between docs (NPC/Criatura are already diffs but still
  restate); drop requirements that only narrate implementation better expressed by the 2.0.0 code; align
  `Modelo de Dados` with the 2.0.0 schema and `Técnico` with the pipeline; update or remove the outdated
  canvas. **Requirement IDs are never renumbered**; a removed requirement is marked as removed, not
  deleted.
- **`Docs/Sistema RPG/`**: **form only** — formatting, duplicated text, broken wikilinks. Game rules and
  numbers are untouched (content changes are proposed to the user, never applied). The 14 runtime-parsed
  files keep their table structure, guarded by the parser tests plus a before/after diff of the
  Compêndio output.
- **`docs/superpowers/`**: specs/plans of shipped work move to `docs/superpowers/archive/` (or are
  removed); only documents with still-valid decisions stay.
- **`README.md` + `CLAUDE.md`**: README for humans (what it is, how to run, how releases flow through the
  pipeline); CLAUDE.md for the agent, shorter, not repeating the README.

### H — 1.5.0 → 2.0.0 cutover

The riskiest release so far: the migration squash + history rewrite, any data-neutral schema change from
D, and the **first run** of the new pipeline (blue-green, long-lived nginx, versioned volumes). A
dedicated runbook:

- repeated rehearsals against the production clone until several consecutive clean runs;
- full fingerprint comparison of every table and column, with an **empty** allowlist;
- off-machine backup with a **tested restore** before the cutover;
- a chosen maintenance window, with **manual approval** in the GitHub Environment for this deploy only;
- a rehearsed rollback to 1.5.0.

From 2.0.1 on, the automated flow is the normal path.

## Out of scope

- Any user-visible behavior change (except the "new version available" toast and the invisible
  maintenance-window retry, both required by F).
- Changes to game rules or numbers in `Docs/Sistema RPG/`.
- Upgrading .NET 8 to a newer major version (can be proposed separately if 0 shows it matters).
