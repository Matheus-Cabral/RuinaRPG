# Versão 2.0.0 — H: production cutover 1.5.0 → 2.0.0 — Design

**Status:** design agreed with the user in chat on 2026-10-23; builder check done and the user's answers
incorporated; awaiting the user's review of this document.
Roadmap: `2026-10-16-versao-2-0-0-roadmap-design.md`, section "H — 1.5.0 → 2.0.0 cutover". Runbook input:
`2026-10-17-versao-2-0-0-h-notas-do-cutover.md` (its 12 steps, B and C notes are binding unless this
document changes them explicitly).

## Intent

Put 2.0.0 into production **without losing any data**. The user's worst outcome is losing the accounts of
GMs and players or the Rules Audit (Auditoria de Regras) data: hundreds of hours of rework. Everything below
serves that.

Today the production data (192.168.80.10, project `ruinarpg`, checkout `/home/ruina/RuinaRPG`, Docker via
`sudo`) exists in exactly one place: the `ruinarpg_pgdata` and `ruinarpg_images` volumes on the production
VM. There is no backup routine. This is the first and only cutover of this kind; from 2.0.1 on the
automated pipeline is the normal path.

## Goals

1. Before anything else, a complete, **tested** copy of today's production data exists **off the
   production VM**, and it is never deleted during 2.0.0.
2. The cutover changes no row and loses no row: a fingerprint comparison of every table and column against
   the last pre-cutover backup shows zero differences, with an empty allowlist (`deploy/data-changes.yml`).
3. A rehearsed, documented way back to 1.5.0 exists and stays available until the user's acceptance
   criterion is met.
4. The user follows every step that touches production.

## Non-goals

- A general backup system for production (scheduled, retained, monitored): **deferred to 2.2.0** by the
  user, after the 2.1.0 features. H only takes the backups the cutover needs.
- Any 2.1.0 feature or any behavior change.
- Changing the pipeline design (F) beyond what the rehearsals prove broken.

## Anti-goals (a failure even if the deploy "works")

- Any production row lost or changed, any uploaded image lost.
- A backup that was never restored and checked.
- A command run on production that the user didn't see, or a write to production outside the agreed
  window.
- Running 1.5.0's `--migrate` (`make migrate`, a 1.5.0 `make deploy`) against a database whose history was
  converted to the 2.0.0 baseline (B notes).
- Deleting anything from 1.5.0 (containers, images, volumes, backups) before the acceptance criterion.

## Decisions taken with the user

| Topic | Decision |
|---|---|
| Access to production | A new SSH key from this VM (192.168.80.16) to production, used by the agent while the user follows along. Separate from the pipeline's dump/deploy keys of `deploy/provision/`. |
| First step | Protect today's data before any rehearsal: full backup, copy off the VM, test restore. |
| The "1.5.0 package" | Three parts, stored **in two places** (on production and on this VM): (1) the 1.5.0 Docker images (API and nginx, `docker save`); (2) the whole database (`pg_dump`); (3) the uploaded-images volume. Taken **before** the window, with the app online. |
| Maintenance window | Flexible: the app may be offline for this operation; the user schedules it once rehearsals are clean. Target: keep it short — inside the window only a final database dump (+ images), the deploy and the checks. |
| When to go back to 1.5.0 | **Only if data is lost** (user). Detected at the cutover's verification (fingerprint diff or the user's check) → full rollback restoring the final pre-cutover dump (H.5). If a loss is discovered later, after real use of 2.0.0, a full rollback would itself erase the newer data, so the recovery is instead to bring the missing rows back from the pre-cutover dump into 2.0.0 (agent's reading of the user's criterion — confirm). |
| sudo on production | The user provides the sudo password; the agent runs the commands over the new SSH key, including the `deploy/provision/` steps, with the user approving each one (agent's reading — the user offered the password and access). The password lives only in a mode-600 file on this VM for the duration of H, is never written to git, logs or reports, and is deleted at the end of H. |
| Rollback availability | Old containers stopped, not removed; 1.5.0 images re-tagged as `ruinarpg-legacy-*:1.5.0`; the package kept. |
| Acceptance (stop being able to go back) | 2.0.0 running with no data loss **and** 2.1.0 released. Only then are 1.5.0 artifacts removed. |
| Future | A robust backup system is a 2.2.0 item (recorded in the backlog). |

## Design

### H.1 — Protect the data (read-only on production)

- Generate a dedicated SSH key on this VM; the user authorizes it on production (an account that can run
  `sudo docker …`; the user decides which). Every command sent to production is shown to the user.
- Preflight, read-only: Docker version (≥ 23, C notes); `docker volume ls` shows `ruinarpg_pgdata` and
  `ruinarpg_images`; `.env` has `ASPNETCORE_ENVIRONMENT=Production` and every variable; the deployed sha
  (expected 1.5.0 = `729dac9`, the current `origin/main`); `__EFMigrationsHistory` has 84 rows ending in
  `20261014100935_AddAutomaticoToSheetLogEntries` (B notes); per-table row counts recorded as a reference.
- Take the 1.5.0 package with the app online: `pg_dump` of the whole database (custom format), a tar of
  the images volume, `docker save` of the running 1.5.0 API and nginx images; checksums for each file.
  Store it on production in a dated directory outside the repo, and copy it to this VM. Verify the
  checksums on both sides.
- Test restore on this VM, in a throwaway project: restore the dump and the images, start the 1.5.0 images
  from the package against it, and check: per-table row counts equal the production reference; a sample of
  accounts (users and roles, incl. Rules Auditor) and of Auditoria tables present; the app answers and an
  uploaded image is served. Also produce the data fingerprint (`deploy/fingerprint.sh`) of the restored
  database as the "A" reference for later comparisons.

### H.2 — Rehearse on copies, never on production

- **Pipeline rehearsals:** GitHub workflows only exist on `main` after the merge, and the PR's first clean
  run would auto-merge it. So rehearsals run `deploy/clone.sh` **by hand on this VM**, in bootstrap mode,
  fed from the H.1 package through `RUINA_DUMP_CMD` — production is never touched (agent's call). The real
  `release.yml` runs once, on the cutover PR (H.4). Rehearsals run against the restored package, with an **empty** allowlist, repeated until several consecutive clean runs
  (roadmap). Each run includes `tools/migrations/verificar-baseline.sh 729dac9` with its four OK lines.
- **Full runbook rehearsal:** on this VM, recreate a 1.5.0 stack shaped like production (project name,
  `nginx`/`api` services on port 80, Production mode, the restored data), then run the cutover steps exactly
  as they will run in production — including the window, the final dump, the manual `deploy.sh deploy`,
  the checks — and the **rollback to 1.5.0** (notes step 11), then the cutover again. Measure the time
  offline. The current dev stack (2.0.0 with an empty database) may be removed for this; nothing in it is
  worth keeping.
- Fix anything the rehearsals prove broken (scripts, runbook text) through the normal review process; the
  rehearsal repeats after a fix.

### H.3 — Prepare production (with the user)

- After the provisioning, build the 2.0.0 images on production and pre-pull `nginx:1.27-alpine` and
  `busybox` with the app still online (no data touched), so the cold build (~4 min) stays out of the
  window (agent's call).

- Run the `deploy/provision/` runbooks in their documented order (production part 0/A, runner, production
  B/C, GitHub), with each "Verificação" line checked.
- Add **Required reviewers** to the `production` Environment for this deploy only (removed after H).
- Re-tag the 1.5.0 images as `ruinarpg-legacy-api:1.5.0` / `ruinarpg-legacy-nginx:1.5.0`.

### H.4 — The cutover (scheduled with the user)

- Merge `release/2.0.0` to `main` through the pipeline (PR validated against the production clone with an
  empty allowlist), with the deploy held by the Required reviewers approval.
- Window: stop the 1.5.0 `nginx` and `api` containers, keep `db` running (no more writes); final `pg_dump`
  + images tar, copied off the VM with checksums; switch the checkout and run the deploy by hand, watching
  the terminal (notes steps 7–8); reject the redundant pending `deploy.yml` approval.
- Verify: notes step 9; `/api/version` shows the merge sha; C's `curl -sI` checks through Cloudflare;
  fingerprint of the production database compared to the final pre-cutover dump's fingerprint: **zero
  differences**; then the user's manual check (log in as GM and as a player, open sheets, Auditoria pages,
  images).
- Version shown to users: `AppVersionInfo` becomes 2.0.0 and the `ChangelogDialog` gets its text before the
  merge (release process); the text is proposed to the user.

### H.5 — Rollback to 1.5.0 (rehearsed in H.2)

Notes step 11, using the **final pre-cutover dump** (never 1.5.0's migrate): stop the 2.0.0 services,
restore the dump (and images if touched), check out the 1.5.0 sha, start the stopped 1.5.0 containers.
Decision to roll back: the user's, on any data difference or a broken critical path; the agent recommends.

### H.6 — After the cutover

- Old containers and legacy images stay until the acceptance criterion (2.0.0 without loss + 2.1.0
  released); the package and the final dump stay too.
- Remove the Required reviewers rule; the next deploy (2.0.1/2.1.0) goes through the automated path,
  after verifying notes step 12.
- Record the 2.2.0 backup-system item in the backlog.

## Constraints

- Nothing writes to production before the window except the H.3 provisioning steps the user runs or
  approves; H.1 is read-only.
- Every production command is shown to the user before it runs; destructive ones (stop, checkout, deploy,
  restore) need the user's explicit go in the moment.
- Never run 1.5.0's `--migrate` against a converted database.
- No 1.5.x migration may land on `main` before the cutover unless `HistoricoDeMigracoes150.Ids` is updated
  (B notes).
- Backups contain personal data (accounts): stored only on production and this VM, readable by root only
  (or the dedicated user), never pushed to git or any external service.
- The repository is public: the clone report posted to a PR on failure must carry counts only, not sample
  primary keys (user ids etc.); samples stay in the logs on this VM (agent's call — a small change to
  `compare.py`/`release.yml` reporting, made in H before the cutover PR).

## Success criteria

- H.1 package exists in both places with matching checksums, and its test restore passed every check.
- Several consecutive clean pipeline rehearsals; a full runbook rehearsal incl. rollback, with the offline
  time measured.
- Production on 2.0.0 with zero fingerprint differences against the final pre-cutover dump, the user's
  manual check passed, and the rollback path still available.

## Left to the builder

The exact scripts or commands for the package, the test-restore checks' implementation (reuse
`deploy.sh backup/restore`, `fingerprint.sh`, `compare.py`, `clone.sh` wherever they fit), how the 1.5.0
rehearsal stack is recreated on this VM, the order of the H.3 runbooks within their documented order, and
how each step's evidence is recorded (a cutover log the user can read).
