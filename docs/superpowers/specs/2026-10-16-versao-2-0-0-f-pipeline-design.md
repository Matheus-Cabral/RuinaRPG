# Versão 2.0.0 — Spec F: Validated pipeline + zero-downtime deploy — Design

**Status:** design approved by user 2026-10-16 (in chat). Version 2.0.0, branch `release/2.0.0`.
Sub-project F of the 2.0.0 roadmap (`2026-10-16-versao-2-0-0-roadmap-design.md`, section F — the decisions
recorded there still hold; this spec details them). Depends on 0+A (done).

## Overview

A PR to `main` is validated against a throwaway clone of production (real data); if anything breaks or any
data is lost, the PR is refused and goes back to draft with diagnostics; if it passes, it is auto-merged and
deployed to production **without taking the server down**. F builds the whole mechanism on
`release/2.0.0`; **production does not change during F**. The first real deploy (which also converts the
current stack to blue-green) belongs to H.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Production VM | `192.168.80.10`, repo at `/home/ruina/RuinaRPG/`, reached over **SSH on the LAN** from this dev VM (`192.168.80.16`). Docker there currently needs `sudo` (may change). |
| What the pipeline's key may do | **Restricted forced command**: a dedicated `deploy` user whose key can only run a root-owned gate script outside the repo, accepting three verbs (`dump`, `deploy <sha> [--window]`, `status`). Sudoers allows only that script. |
| Backups | On production (last 5, for fast rollback) **plus a copy pulled to this VM** (last 5). |
| Blue-green mechanism | **Two compose services `api-blue`/`api-green` + nginx `include` file switch + `nginx -s reload`.** No new components (no Traefik/Caddy). |
| Inherited from the roadmap | Self-hosted runner on this VM; public repo with locks; auto-merge on success; failing PR → diagnostic comment + draft; allowlist `deploy/data-changes.yml`; incompatible migration → invisible maintenance window (client holds and retries, no message). |

## Components

| Component | Location | Responsibility |
|---|---|---|
| `deploy/deploy.sh` | repo | The **only** deploy script, same code on the clone and in production. Subcommands `deploy <sha> [--window]`, `rollback`, `backup`, `status`. Runs as root, under `flock`. |
| `deploy/gate.sh` | source in repo; installed once to `/opt/ruinarpg/gate.sh` (root:root, 0755) | SSH forced command. Parses `$SSH_ORIGINAL_COMMAND`; accepts only the three verbs; validates arguments; calls `deploy.sh` with `sudo`. |
| `deploy/fingerprint.sql`, `deploy/compare.sh` | repo | Data-loss detection (see "Fingerprint"). |
| `deploy/data-changes.yml` | repo, per release | Allowlist of intentional data changes; empty by default. |
| `deploy/nginx/` | repo, bind-mounted into `web` | `default.conf` (static client from `webroot/current`, `/images/`, `/api/`, `/hubs/`, maintenance server, cache headers for `index.html`/`blazor.boot.json`) and `upstream.conf` (rewritten by `deploy.sh`). |
| `tools/RuinaRPG.Smoke` | repo, .NET console app + Dockerfile | Read-only smoke: given a base URL and a JWT signing key/issuer/audience, mints tokens for real users read from the DB, opens every campaign, sheet (all three kinds), encounter and catalog, asserts 200 + deserializable payload. Used by the clone job and by the post-deploy smoke **on production** (the key never leaves production). Non-zero exit + a JSON report on failure. |
| `.github/workflows/release.yml` | repo | PR → `main` (and `workflow_dispatch` for rehearsals): `gate` → `ci` → `clone` → report. |
| `.github/workflows/deploy.yml` | repo | Push to `main`: `deploy` job bound to the `production` Environment. |
| `deploy/provision/` | repo | One-time runbooks + scripts the user runs: production VM (user `deploy`, key, sudoers, gate install, backup dir), this VM (runner under a dedicated user without sudo, `~/.ssh` permissions fixed to 700/600), GitHub (Environment `production` restricted to `main`, secrets, branch protection, "require approval for fork PRs"). |

## Production stack after F (new `docker-compose.yml`)

- `db` — unchanged image and `pgdata` volume.
- `api-blue`, `api-green` — the same service definition (YAML anchor), image tag per color from an env file
  written by `deploy.sh` (`deploy/state/colors.env`: `BLUE_TAG`, `GREEN_TAG`, `ACTIVE`). Only the active color
  receives traffic outside a switch.
- `web` — long-lived `nginx:1.27-alpine`, **never rebuilt on deploy**; config bind-mounted from `deploy/nginx/`.
  Serves the client from volume `webroot` (`releases/<sha>/`, symlink `current`), uploaded images from the
  `images` volume under `location /images/` (replacing today's mount over `wwwroot/images`, which shadowed
  static files), proxies `/api/` and `/hubs/` to the `app` upstream.
- `client-build` — one-shot build image that publishes the Blazor client and copies it into
  `webroot/releases/<sha>/`.
- Compose project name pinned (`name: ruinarpg`) so volume names stay identical to today's
  (`ruinarpg_pgdata`, `ruinarpg_images`) — verified in H before conversion.

## `deploy.sh deploy <sha> [--window]` (production and clone)

1. Take the `flock`; refuse if another deploy runs.
2. `git fetch` + `git checkout --detach <sha>` (as the repo owner, `ruina` in production).
3. **Backup**: `pg_dump -Fc` + tar of `images` to `/var/backups/ruinarpg/<UTC timestamp>-<sha7>/`; keep last 5.
4. **Build**: `ruinarpg-api:<sha>` (sha stamped into the assembly), client into `webroot/releases/<sha>/`
   (sha stamped into the client).
5. **Zero-downtime** (default): run `--migrate` with the new image as a one-off container → start the inactive
   color with `<sha>` → wait for `/health` (timeout 120 s) → switch `current` symlink → rewrite `upstream.conf`
   to the new color → `nginx -s reload`.
6. **Window** (`--window`): `upstream.conf` → `maintenance` (nginx answers 503 with `X-Ruina-Manutencao: 1`) →
   reload → second backup (consistent: no writes reach the API) → stop the old color → `--migrate` → start the
   new color → `/health` → switch `current` + upstream → reload.
7. **Post-deploy smoke**: run `ruinarpg-smoke:<sha>` on the compose network against the new color, with JWT
   settings from production's `.env`.
8. **Smoke passed**: wait 30 s (drain), stop the old color (image kept for `rollback`), prune releases/images
   older than the last 3, update `deploy/state/`.
9. **Smoke failed → automatic rollback** and non-zero exit:
   - zero-downtime: upstream + `current` back to the old color (still running; compatible schema);
   - window: restore the window backup (`pg_restore --clean`), start the old color, switch back.
10. Print a machine-readable summary (mode, colors, sha, backup path, smoke result) for the workflow.

`deploy.sh rollback` repeats step 9 on demand. `make deploy` / `make migrate` remain as the manual emergency
path and call `deploy.sh` locally.

## Gate (`/opt/ruinarpg/gate.sh`)

- `dump` → runs `deploy.sh backup` and streams the newest backup as a tar on stdout (for the clone and the
  off-machine copy).
- `deploy <sha> [--window]` → `<sha>` must match `^[0-9a-f]{40}$` and be an ancestor of `origin/main` after a
  fetch; otherwise refuse.
- `status` → active color, deployed sha, last 5 backups.
- Anything else → refuse with exit 2. `authorized_keys` line: `command="/opt/ruinarpg/gate.sh",no-pty,
  no-port-forwarding,no-agent-forwarding,no-X11-forwarding`.

## Fingerprint (data-loss detection)

- `fingerprint.sql` creates, in a separate database `fingerprint` on the clone's Postgres, one table per
  snapshot (`a`, `b`) holding `(table, pk_text, row_hash)` for every table of the app schema except
  `__EFMigrationsHistory`, where `row_hash = md5(row(<columns that exist in snapshot A>)::text)`, ordered by
  column name.
- Snapshot A after restoring the dump with the current `main` deployed; snapshot B after deploying the PR.
- `compare.sh` reports per table: rows present in A and missing in B (**lost**), rows whose hash changed
  (**altered**; for these, the differing column names). New tables, new rows and new columns are ignored.
- `deploy/data-changes.yml` lists intentional changes:
  `- { table: <name>, columns: [<col>, …] | "*", change: altered|lost, reason: <text> }`. A finding not
  covered by the allowlist fails the job. The 2.0.0 cutover uses an **empty** allowlist (H).

## `release.yml` (PR → `main`, and `workflow_dispatch`)

1. **gate** (`ubuntu-latest`): pass only if `github.event.pull_request.head.repo.full_name ==
   github.repository` and the author is the repo owner (for `workflow_dispatch`: the actor is the owner).
   Every later job `needs: gate`.
2. **ci** (self-hosted): `dotnet build --no-incremental` (0 warnings), unit, client, integration tests.
3. **clone** (self-hosted):
   - `ssh deploy@192.168.80.10 dump` → tar to a temp dir (deleted at the end, also on failure);
   - check out `main` into a temp worktree; in compose project `ruinarpg-clone` (port 8089, bound to
     127.0.0.1, own `.env` with generated secrets) start only `db`, restore the dump and the images into it,
     **then** run `deploy.sh deploy <main sha>` (production runs `main`, so no migration is pending — this only
     builds and starts the current version over the real data). The clone calls `deploy.sh` directly, not
     the gate, so the PR head sha does not need to be on `main`;
   - fingerprint A;
   - `deploy.sh deploy <PR head sha>` on the clone (same script as production);
   - fingerprint B + `compare.sh`;
   - smoke against the clone;
   - **compatibility check**: start the *old* API image against the migrated DB and run the smoke in
     read-only mode; failure → label the PR `deploy:janela` (window mode), success → remove the label;
   - copy the newest production backup to `/var/backups/ruinarpg-offsite/` on this VM (keep last 5);
   - `docker compose -p ruinarpg-clone down --volumes` + delete temp data (always, `if: always()`).
4. **report**: on failure, a PR comment with the failed step, the fingerprint diff (table/column), failing
   tests or smoke routes, and the API log tail; full logs as artifacts; `gh pr ready --undo`. On success,
   `gh pr merge --auto --merge`.

## `deploy.yml` (push to `main`)

`deploy` job (self-hosted, `environment: production`): find the PR of the pushed commit; `--window` if it
carries `deploy:janela`; `ssh deploy@192.168.80.10 deploy <sha> [--window]`; pull the newest backup copy;
post the summary (or failure + rollback result) on the PR.

## Client changes

- **`RetryHandler`** in the `Api` HttpClient chain (with `BearerTokenHandler`):
  - 503 with `X-Ruina-Manutencao: 1` → retry **any** method (the request never reached the API);
  - other 502/503, or a network failure → retry **GET/HEAD only** (a POST may have been processed);
  - backoff 1, 2, 4, 8 … s, total cap ~90 s, then surface the error as today. No UI message.
- **SignalR**: both connections get a retry policy of every 2–5 s for up to 5 minutes (default gives up at
  ~42 s).
- **New version notice**: `GET /api/version` → `{ versao, sha }` (`versao` = `AppVersionInfo.Current`). The
  client compares its own build sha with the API's on SignalR reconnect, on window focus and every 5 minutes;
  when different, a non-blocking MudBlazor snackbar with the `Refresh` icon, text "Nova versão disponível" and
  a **Recarregar** action.

## Testing (TDD)

- Client unit tests: `RetryHandler` (each status × method × header; total-time cap), version comparison and
  when the snackbar is raised.
- API integration test: `/api/version` returns the configured version and sha.
- Fingerprint: integration tests (Testcontainers) — deleted row detected; changed value in an existing column
  detected with column name; new column ignored; new table ignored; allowlisted change passes; non-allowlisted
  fails.
- `RuinaRPG.Smoke`: unit tests for token minting and the route list (every list endpoint it covers maps to a
  controller route).
- `deploy.sh`/`gate.sh`: **bats** tests run through the `bats/bats` Docker image (nothing installed on the
  host), against a throwaway local compose project: first deploy, blue→green, green→blue, rollback after a
  failing smoke (fake smoke image), window mode, gate refusing unknown verbs / malformed sha / sha not on
  `main`, `flock` refusing a concurrent deploy.
- End to end: `workflow_dispatch` rehearsals of `release.yml` against the real production dump, before H.

## Documentation

- `Requisitos - Técnico.md`: new **R0013** (validated release pipeline), **R0014** (zero-downtime blue-green
  deploy and invisible maintenance window), **R0015** (backups and rollback), **R0016** (forced-command deploy
  gate); R0007–R0010 updated where they describe the old deploy.
- `README.md` and `CLAUDE.md` deploy sections updated (G slims them later).
- Memory: release process now goes through the pipeline.

## Rollout

1. F is implemented and merged into `release/2.0.0` only; production untouched.
2. The user runs `deploy/provision/` once on both VMs and in GitHub settings.
3. Rehearsals via `workflow_dispatch` until clean.
4. First production deploy (stack conversion to blue-green, same volumes) is part of H's runbook.

## Out of scope

- Deploy notifications outside GitHub (e-mail, chat).
- Multiple environments (staging) besides the throwaway clone.
- Long-term cache headers and compression (sub-project C).
- Converting the running production stack (H).
