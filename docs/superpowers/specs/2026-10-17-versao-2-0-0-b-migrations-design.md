# Versão 2.0.0 — Spec B: Migration squash — Design

**Status:** design approved by user 2026-10-17 (in chat). Version 2.0.0, branch `release/2.0.0`.
Detailed spec of sub-project **B** of the roadmap (`2026-10-16-versao-2-0-0-roadmap-design.md`). It
refines the roadmap in one point, with the user's approval: the history rewrite is not a separate deploy
step but runs inside `--migrate` (`DatabaseInitializer`), because `deploy.sh` only ever calls `--migrate`
(see `2026-10-17-versao-2-0-0-h-notas-do-cutover.md`).

## Overview

The 84 EF Core migrations of 1.5.0 (169 files with their `.Designer.cs` and the snapshot, 9.6 MB in
`src/RuinaRPG.Infrastructure/Persistence/Migrations/`) are replaced by **one baseline migration** that
creates the 2.0.0 schema from scratch. Databases already on 1.5.0 (production, the clone, the local
stacks) have their `__EFMigrationsHistory` rewritten automatically by `--migrate`; their schema and data
are not touched. Same behavior for the end user; nothing in the schema or data changes.

Gains: smaller build (the Infrastructure project compiles 169 fewer files), a faster template database in
the integration tests (one migration instead of 84), 50 test classes that only exercise history removed.

## Findings that drive B

1. **Only one migration seeds data:** `20261001100828_AddPericias` inserts the base Perícias with
   `migrationBuilder.InsertData` (table `Pericias`, columns `Id, Chave, Nome, Descricao,
   AtributoSugerido, DisponivelParaCriaturas, IsDeleted`). The model has no `HasData`, so a baseline
   generated from the model alone would leave `Pericias` empty on a new database. No later migration
   changes those rows or the `Pericias` columns (`PericiasComoTabela` only renames columns and adds FKs
   in the sheet tables).
2. **Raw SQL in 7 migrations is data backfill only** (`AddNormalizedNicknameUniqueIndex`,
   `AddAfinidadeSegundaEssencia`, `DropAfinidadeCaminhoNome`, `RemoveDurabilidadeMaximaDosItens`,
   `ConverteRequisitosLegadosDeEquipamento`, `AddEncounterPhasesAndLog`,
   `AddReadAtToDiaryEntryRecipients`): no triggers, functions or views. On an empty database it does
   nothing; on 1.5.0 databases it has already run. The baseline drops it.
3. **`deploy.sh` only calls `--migrate`**, and `rollback` never migrates. The rewrite therefore lives in
   `DatabaseInitializer`, which `--migrate`, Development startup and the test template all share.
4. Nothing outside `Migrations/` references `__EFMigrationsHistory`, except `deploy/fingerprint.sh`,
   which already excludes it from the fingerprint.
5. `tests/RuinaRPG.Tests.Integration/Persistence/` has 50 `*MigrationTests` plus a few schema/seed
   classes (`EquipmentKitSchemaTests`, `SubcategoriaOptionSchemaTests`, `TabelaDeAfinidadesSeederTests`,
   `*MigrationAndSeedTests`, the `SchemaInsert` helper). Some assert intermediate migrations or
   backfills; others assert constraints of the current schema (unique indexes, FKs, cascades) or seeders.

## Design

### B.1 Baseline migration

- All files in `Persistence/Migrations/` are deleted. `dotnet ef migrations add Baseline_2_0_0` generates
  one migration and a new snapshot from the current model, with the ID **`20261017120000_Baseline_2_0_0`**
  (the timestamp is set by renaming, so the ID is fixed and known before generation).
- The `InsertData` block for `Pericias` is copied **verbatim** from `AddPericias` into the baseline's
  `Up`, right after the `Pericias` table is created. `Down` is not used anywhere (no rollback by
  migration) and stays as generated.
- No `HasData` is added to the model: the seed stays a migration-time insert, exactly as today.
- The model is not changed in B. If generation shows any difference with the old chain's schema, that
  difference is the bug to fix (see B.3), never a reason to change the model.

### B.2 History rewrite inside `--migrate`

`DatabaseInitializer.InitializeAsync` gets a first step, before `MigrateAsync`:
`ConsolidarHistoricoDeMigracoesAsync(DbContext, ILogger)`. The 84 IDs of 1.5.0 are a fixed list in
`HistoricoDeMigracoes150` (Infrastructure), and the baseline ID is a constant next to it.

It reads `__EFMigrationsHistory` (a missing table counts as empty) and decides:

| History state | Action |
|---|---|
| Empty or table missing (new database) | Nothing; `MigrateAsync` creates everything. |
| Contains the baseline ID (2.0.0 or later; later migrations may follow it) | Nothing. |
| **Exactly** the 84 IDs of 1.5.0, no more, no less | In **one transaction**, with `LOCK TABLE "__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE`: delete the 84 rows, insert the baseline row (with EF's current product version). Logs one line naming the old and new state. No other table is touched. |
| Anything else (part of the old chain, unknown IDs, old IDs mixed with the baseline) | Throws `InvalidOperationException` with a pt-BR message listing what was expected and what was found; nothing is changed, and `--migrate` exits non-zero, so `deploy.sh` aborts and undoes as for any failed migration. |

Properties: idempotent (a second run finds the baseline and does nothing); runs in every environment that
uses `DatabaseInitializer`; the pre-deploy backup that `deploy.sh` already takes precedes it in
production and in the clone.

**Rollback to 1.5.0 after the rewrite** (relevant only to H): `deploy.sh rollback` never runs
`--migrate`, so a 1.5.0 API keeps running on the identical schema. But 1.5.0's own `--migrate` would not
recognize the baseline row and would try to recreate the schema. The H runbook must therefore roll back
to 1.5.0 either without migrating or by restoring the pre-deploy backup. This goes into the H notes.

### B.3 Equivalence proof

`tools/migrations/verificar-baseline.sh <ref-1.5.0>` (committed, kept until H is done) runs in a
throwaway Postgres container (`docker run`, own name and port, removed at the end) and builds three
databases:

- **A**: `--migrate` from a temporary `git worktree` of the 1.5.0 ref (old chain + seeders);
- **B**: `--migrate` from the working tree (baseline + seeders);
- **C**: copy of A, then `--migrate` from the working tree (rewrite + seeders), i.e. the upgrade path.

Then it compares `pg_dump` outputs (plain format, `--no-owner --no-privileges`, with
`__EFMigrationsHistory` excluded from both schema and data):

- `--schema-only` A vs B: **must be identical** (the roadmap's byte-identical criterion);
- full dump (schema + data) A vs B and A vs C: **must be identical**;
- C's `__EFMigrationsHistory` holds only the baseline row.

The script exits non-zero on any difference and prints the diff. Rows are dumped in a deterministic order
(`pg_dump` with each table's data sorted, or a normalization step that sorts each `COPY` block) so that
physical row order never causes a false difference. The `Jwt__*` and connection variables it needs are
set to throwaway values inside the script. It is run during B and again in H's rehearsals; it is not part
of CI (it needs the old ref and is useless after the cutover).

### B.4 Tests

TDD order: the `ConsolidarHistoricoDeMigracoes` tests are written before the code, against a blank
database (`BlankDatabaseFixture`). The 1.5.0 state is built by applying the baseline and then replacing
the history rows with the 84 old IDs (schema and history are what matters; no old code needed). Cases:

1. new database → full migrate, history = baseline only, `Pericias` seeded;
2. 1.5.0 history with data in a few tables → history = baseline only, the data intact (row counts and a
   sample row compared before/after);
3. run twice → second run changes nothing;
4. baseline followed by a later (fake) migration ID → untouched;
5. old chain minus its last ID → throws, history and data unchanged;
6. 1.5.0 history plus an unknown ID → throws, unchanged;
7. old IDs mixed with the baseline → throws, unchanged.

Existing test triage, one rule per file in `Persistence/`:

- **Removed:** tests that migrate to an intermediate migration, assert that a named historical migration
  was applied, or assert a backfill's effect on pre-existing rows. They test history that no longer
  exists.
- **Kept, moved off history:** tests that assert the **current** schema (constraints, indexes, FKs,
  cascade/restrict behavior, column defaults) or a seeder's result. They stop calling `Migrate` to a
  named target and use the migrated template (`ApiFixture`'s database or `TemplateDatabase.CloneAsync`)
  or a full `Migrate()` on `BlankDatabaseFixture`. Assertions are not weakened.
- `MigrationTests` and `DatabaseInitializerTests` are rewritten around the baseline (e.g. "the model has
  no pending changes": `context.Database.GetPendingMigrations()` empty after migrate and
  `HasPendingModelChanges()` false).

The plan lists each file with its verdict before any deletion.

### B.5 Docs and measurements

- `Requisitos - Técnico.md` R0009 gets the rule: the 2.0.0 database starts from a single baseline
  migration; `--migrate` converts the history of a 1.5.0 database automatically and refuses any other
  old state. `CLAUDE.md` mentions that new migrations are added on top of the baseline as usual.
- `2026-10-16-versao-2-0-0-baseline.md` gets the 2.0.0 column for: `dotnet build --no-incremental`
  time, size of `Migrations/`, lines of code, and integration-test wall time (measured with
  `tools/bench/` as in 0).
- The H notes get the rollback constraint from B.2 and "run `verificar-baseline.sh` in every rehearsal".

## Validation against real data

F's clone validates B on the real production dump: the clone brings up 1.5.0 with that data, deploys
the branch with `deploy.sh` (bootstrap mode while `main` has no `deploy.sh`), and the fingerprint
comparison must show **no** lost or changed row with an **empty** `deploy/data-changes.yml`. This happens
in H's rehearsals, since production is never contacted while implementing.

## Success criteria

- `verificar-baseline.sh` exits 0: schema A = B byte for byte; full dumps A = B = C; C's history is the
  baseline row only.
- The seven rewrite tests pass; the kept persistence tests pass unweakened; the whole suite passes.
- `dotnet build --no-incremental`: 0 warnings, 0 errors, and faster than the baseline (number recorded).
- `Persistence/Migrations/` holds 3 files (migration, designer, snapshot).
- `dotnet ef migrations has-pending-model-changes` reports none.

## Out of scope

- Any model or schema change (D may make data-neutral ones later, as normal migrations on top of the
  baseline).
- Running the clone against production data (H).
- Changing `deploy.sh`, the fingerprint or the allowlist (F is done; B needs nothing new from it).
