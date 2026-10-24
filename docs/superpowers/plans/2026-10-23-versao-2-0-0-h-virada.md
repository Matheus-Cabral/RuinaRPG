# Versão 2.0.0 — H: production cutover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put 2.0.0 into production with zero data loss, after protecting today's data off the production VM and rehearsing the whole cutover and the rollback on copies.

**Architecture:** Two kinds of work. **Code tasks (1–4)** build the tools and the runbook — package/restore scripts, a public-safe clone report, a 1.5.0 rehearsal stack, the Portuguese cutover runbook — through the normal subagent + review loop, never touching production. **Operation phases (O1–O5)** run those tools against production and against copies; they are run **by the controller in the main session with the user watching and approving**, never by subagents (subagents cannot talk to the user, and every production command must be shown first).

**Tech Stack:** bash, Docker Compose, PostgreSQL 16 (`pg_dump`/`pg_restore`), bats-core, Python 3 (`deploy/compare.py`), GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-23-versao-2-0-0-h-virada-design.md` (binding), with `docs/superpowers/specs/2026-10-17-versao-2-0-0-h-notas-do-cutover.md` (the 12-step runbook and B/C notes, binding unless the spec changes them).

## Global Constraints

- Production = `192.168.80.10`, compose project `ruinarpg`, checkout `/home/ruina/RuinaRPG`, Docker via `sudo`; 1.5.0 = `729dac9`.
- No code task touches production. Operation phases: nothing writes to production before the window except the H.3 steps the user approves; every production command is shown to the user before it runs; stop/checkout/deploy/restore need the user's explicit go in the moment.
- Never run 1.5.0's `--migrate` (`make migrate`, a 1.5.0 `make deploy`) against a database whose history holds the 2.0.0 baseline.
- Backups contain personal data: only on production and this VM, mode 700 directories / 600 files, never in git, never on GitHub (PR comments, artifacts) beyond counts.
- The sudo password lives only in a mode-600 file on this VM during H, is never echoed, logged or committed, and is deleted at the end of H.
- Nothing from 1.5.0 (containers, images, volumes, package, final dump) is deleted before the acceptance criterion: 2.0.0 with no data loss **and** 2.1.0 released.
- Go back to 1.5.0 **only if data is lost**; a loss found after real use is repaired by bringing the rows back from the pre-cutover dump into 2.0.0.
- The package format is the one `deploy/clone.sh` already reads: a tar with `db.dump` (pg_dump custom format), `images.tar` (the images volume) and `sha`.
- Comments, docs and commit messages in Portuguese; commit trailer as in the session.

## Review Focus

1. A package whose `pg_dump` silently missed data (wrong database name, role without access to a schema, a dump taken while 1.5.0 still writes during the window) — Task 2's restore check compares per-table row counts with the counts taken from production at the same moment, and fails on any difference.
2. A rehearsal stack that is not shaped like production (different project name, service names, port, environment) so the rehearsed runbook passes but production fails — Task 3 asserts the shape (`ruinarpg` project, `nginx`/`api`/`db` services, port 80, `ASPNETCORE_ENVIRONMENT=Production`) before restoring.
3. The public PR comment leaking primary keys when a clone validation fails — Task 1's test asserts the public report has counts only and the full report keeps samples.
4. A runbook command that only works on this VM (paths, users, sudo) and breaks on production — Task 4's runbook is executed verbatim in O2's full rehearsal, with production-only differences called out per step.
5. The rollback restoring an older backup than the final pre-cutover dump, or running 1.5.0 migrate — Task 4's rollback section names the final dump explicitly and O2 rehearses it end to end.

---

### Task 1: Public-safe clone report

**Files:**
- Modify: `deploy/compare.py`, `deploy/clone.sh` (the report it writes), `.github/workflows/release.yml` (which file is posted/uploaded)
- Test: `deploy/tests/test_compare.py`, `deploy/tests/clone.bats`

**Interfaces:**
- Produces: `compare.py` writes two outputs — the full report (unchanged content, with up to `SAMPLE` = 20 primary keys per table) and a public report with the same sections but **counts only** (no primary keys, no row values). `clone.sh` writes `report.md` (public) and `report-full.md` (full, kept only in the run's logs on this VM). `release.yml` posts and uploads only `report.md`; `report-full.md` never leaves the runner.

- [ ] **Step 1:** Add tests to `test_compare.py`: given a fingerprint pair with lost and altered rows, `test_public_report_has_counts_only` asserts the public report contains the per-table counts and changed column names and contains none of the pks; `test_full_report_keeps_samples` asserts the full report still lists up to 20 pks. Run `python3 -m unittest deploy/tests/test_compare.py` → FAIL.
- [ ] **Step 2:** Implement the split in `compare.py` (a flag or a second output path; keep the exit codes unchanged). Run → PASS.
- [ ] **Step 3:** In `clone.sh` write both files; in `release.yml` make sure the comment and the `clone-report` artifact use only the public `report.md`, and the `clone-logs` artifact excludes `report-full.md` (check the `find … -type f` list). Add a `clone.bats` assertion that after a failing run `report.md` has no pk from the fixture. Run `deploy/tests/run-bats.sh deploy/tests/clone.bats` → PASS.
- [ ] **Step 4:** Commit `fix(pipeline): relatório público do clone só com contagens`.

### Task 2: The 1.5.0 package — capture and test restore

**Files:**
- Create: `tools/virada/pacote.sh` (runs on production), `tools/virada/restaurar-pacote.sh` (runs on this VM), `tools/virada/contagens.sql`, `tools/virada/README.md`
- Test: `deploy/tests/virada.bats` (added to `deploy/tests/run-bats.sh`'s default list if it enumerates files)

**Interfaces:**
- `pacote.sh <dir-destino>`: needs root (run with `sudo`); reads the running 1.5.0 stack (project `ruinarpg`, service `db`, volume `ruinarpg_images`, images used by the running `api`/`nginx` containers). Writes into `<dir-destino>/<UTC timestamp>-pacote-150/`: `dump.tar` (tar of `db.dump` + `images.tar` + `sha`, the `clone.sh` format), `imagens-docker.tar` (`docker save` of the running api and nginx images), `contagens.tsv` (per-table `table<TAB>rows` from `contagens.sql`, taken in the same run, right after the dump), `SHA256SUMS`. Directory 700, files 600. `--so-banco` variant: only `dump.tar` + `contagens.tsv` + `SHA256SUMS` (the final dump inside the window). Exit non-zero on any failure, leaving no complete-looking directory behind (write to `.partial`, rename at the end — like `deploy.sh backup`).
- `restaurar-pacote.sh <dir-pacote> [--projeto ruinarpg-pacote] [--porta 8091]`: verifies `SHA256SUMS`; brings up a throwaway compose project on this VM from the 1.5.0 images in `imagens-docker.tar` + a postgres 16, restores `db.dump` and `images.tar`, then checks: per-table counts equal `contagens.tsv` (any difference → exit 1 with the table list); the API answers on the port; one uploaded image (first file in the volume) is served by nginx; prints `OK pacote <dir>` at the end. Writes the restored database's fingerprint (`deploy/fingerprint.sh`) to `<dir-pacote>/fingerprint.jsonl`. `--remover` tears the project down (volumes included — throwaway only; refuse any project name not starting with `ruinarpg-pacote` or `ruinarpg-bats`).
- `contagens.sql`: one row per user table in `public` (and Identity tables), exact `count(*)` (not estimates).

- [ ] **Step 1:** Write `virada.bats` against a throwaway stack built from `deploy/tests/fixtures` (as `clone.bats` does): `pacote.sh` on a seeded throwaway project produces the five files with 700/600 modes and valid `SHA256SUMS`; `--so-banco` produces three; a corrupted file makes `restaurar-pacote.sh` fail on the checksum; a modified `contagens.tsv` makes it fail naming the table; a good package restores and prints `OK pacote`. Make `pacote.sh`'s project/volume/service names overridable by env vars (`RUINA_PACOTE_PROJECT`, …) for the test only. Run → FAIL.
- [ ] **Step 2:** Implement the three files and the README (Portuguese: what each script does, where it runs, what it never does). Run `deploy/tests/run-bats.sh deploy/tests/virada.bats` → PASS, twice.
- [ ] **Step 3:** Commit `feat(virada): pacote da 1.5.0 e restauração de teste`.

### Task 3: A 1.5.0 rehearsal stack shaped like production

**Files:**
- Create: `tools/virada/ensaio-150.sh`
- Test: `deploy/tests/virada.bats` (new cases)

**Interfaces:**
- `ensaio-150.sh preparar <dir-pacote> <dir-checkout>`: creates a `git worktree` of `729dac9` at `<dir-checkout>`, copies this repo's `.env` with `ASPNETCORE_ENVIRONMENT=Production`, loads `imagens-docker.tar`, starts the 1.5.0 compose project **named `ruinarpg`** from that worktree on port 80 (stopping and, with the operator's confirmation flag `--remover-stack-atual`, removing the current dev stack and its volumes first — refuse otherwise), restores the package into it, and asserts the shape: services `nginx`, `api`, `db`; port 80; Production mode; counts equal the package's `contagens.tsv`. `ensaio-150.sh desfazer <dir-checkout>` removes the worktree and the stack. The rehearsal runbook then runs inside `<dir-checkout>` exactly as on production.

- [ ] **Step 1:** bats cases using a throwaway project name (the script takes `RUINA_ENSAIO_PROJECT`, default `ruinarpg`; tests use `ruinarpg-bats-ensaio` and a high port): refuses without `--remover-stack-atual` when a stack with that project name exists; prepares, asserts shape, and `desfazer` cleans up. Run → FAIL.
- [ ] **Step 2:** Implement. Run → PASS. Commit `feat(virada): stack de ensaio da 1.5.0 igual à produção`.

### Task 4: The cutover runbook, the cutover log, and the 2.0.0 version

**Files:**
- Create: `docs/superpowers/specs/2026-10-23-h-roteiro-da-virada.md` (Portuguese runbook), `docs/superpowers/specs/2026-10-23-h-registro-da-virada.md` (empty log template: one section per phase/step with "comando / saída relevante / quem aprovou / horário")
- Modify: `src/RuinaRPG.Domain/AppVersionInfo.cs` (2.0.0), the `ChangelogDialog` text (draft — the user approves the wording before the cutover PR)
- Test: existing tests covering `AppVersionInfo`/`ChangelogDialog` (find them; update expectations only for the version/text)

**Interfaces:**
- The runbook turns the spec's H.1–H.6 and the notes' 12 steps into numbered steps, each with: the exact command (as run on production: `ssh` + `sudo -S` reading the password file), what output means it passed, what to do if it doesn't, and whether the user's explicit go is needed. Sections: Preflight (spec H.1 list), Pacote, Ensaio, Preparação (H.3 incl. retag, Required reviewers, pre-build of the 2.0.0 images and pre-pull of `nginx:1.27-alpine`/`busybox`), Janela (stop `nginx`+`api`, keep `db`; `pacote.sh --so-banco`; copy off; checkout; `deploy.sh deploy <merge sha>`; reject the redundant deploy.yml approval), Verificação (notes step 9; C's `curl -sI`; fingerprint of production vs the final dump's `fingerprint.jsonl` = zero differences; user's manual check list), Volta (notes step 11 with the **final** dump), Depois (H.6, including how to recover rows lost and noticed only after real use: restore the final dump into a throwaway project, diff with `fingerprint.sh`/`compare.py`, and re-insert the missing rows into 2.0.0 — described, not scripted). Production-only differences from the rehearsal are marked per step.

- [ ] **Step 1:** Write the runbook and the log template. Every command that appears must exist in the repo (scripts from Tasks 2–3, `deploy/`), checked with a grep script listed in the report.
- [ ] **Step 2:** Bump `AppVersionInfo` to `2.0.0` and write a draft `ChangelogDialog` entry in Portuguese (user-facing: faster loading, the visible unifications from E, nothing internal) marked "rascunho — aprovação do usuário". Run the affected tests → PASS.
- [ ] **Step 3:** Commit `docs(virada): roteiro e registro da virada; versão 2.0.0`.

---

## Operation phases (controller + user, in the main session)

These are not subagent tasks. The controller runs them, showing each production command, recording each step in the cutover log, and stopping for the user's go where the runbook says so. Each phase ends with a short report to the user.

### O1 — Protect the data (spec H.1)

- [ ] Generate an ed25519 key on this VM (`~/.ssh/ruinarpg_virada`); the user authorizes it on production for the account they choose and gives the sudo password, stored in a mode-600 file. Verify: `ssh … true` works; `sudo -S true` works.
- [ ] Preflight (read-only): Docker ≥ 23; volumes; `.env`; deployed sha = `729dac9`; `__EFMigrationsHistory` = 84 rows ending in `20261014100935_AddAutomaticoToSheetLogEntries`; per-table counts recorded.
- [ ] Copy `tools/virada/pacote.sh` + `contagens.sql` to production (outside the repo checkout, e.g. `/root/virada/`), run it into `/var/backups/ruinarpg-virada/`, copy the package to this VM (`/home/pro/backups/ruinarpg-virada/`), verify `SHA256SUMS` on both sides.
- [ ] `restaurar-pacote.sh` on this VM → `OK pacote`; then `--remover`. Report to the user.

### O2 — Rehearse on copies (spec H.2)

- [ ] `clone.sh run 729dac9 <head of release/2.0.0> <workdir>` by hand with `RUINA_DUMP_CMD="cat <pacote>/dump.tar"`, empty `deploy/data-changes.yml`; plus `tools/migrations/verificar-baseline.sh 729dac9` (four OK). Repeat until **3 consecutive clean runs**; any failure → fix through a code task and restart the count.
- [ ] Full runbook rehearsal: `ensaio-150.sh preparar` (with the user's go to remove the current empty dev stack), then the runbook's Janela → Verificação → Volta → Janela again, timing the offline period. Record everything in the log. Fix the runbook/scripts through a code task if anything diverges; repeat until clean.
- [ ] Report to the user: offline time, results; agree the window.

### O3 — Prepare production (spec H.3, with the user)

- [ ] `deploy/provision/` runbooks in their documented order, each "Verificação" checked, each command approved by the user.
- [ ] GitHub: `production` Environment Required reviewers; retag 1.5.0 images as `ruinarpg-legacy-api:1.5.0` / `ruinarpg-legacy-nginx:1.5.0`; pre-build the 2.0.0 images on production (app online) and pre-pull `nginx:1.27-alpine`/`busybox`.
- [ ] The user approves the `ChangelogDialog` text; open the cutover PR `release/2.0.0` → `main`; `release.yml` must pass against the production clone with an empty allowlist; the deploy waits on the Required reviewers approval.

### O4 — The cutover (spec H.4, at the agreed time)

- [ ] The runbook's Janela and Verificação, step by step, with the user's go at each destructive step. Success = zero fingerprint differences + the user's manual check. Any data difference → the runbook's Volta, by the user's decision.

### O5 — After (spec H.6)

- [ ] Remove the Required reviewers rule; keep 1.5.0 artifacts until 2.1.0 ships; delete the sudo password file and revoke the virada key at the end of H (the user removes it from `authorized_keys`); update memory and the plan queue.
