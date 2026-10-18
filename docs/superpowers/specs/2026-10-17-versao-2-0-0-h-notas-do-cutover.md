# 2.0.0 — H: cutover notes (input for sub-project H's spec)

Source: the final review of sub-project F (validated pipeline + zero-downtime deploy). This file only
records the reviewer's runbook requirements for the cutover from 1.5.0; it implements nothing. Sub-project
H's spec must turn them into its runbook.

## H runbook requirements (from the final review, verbatim)

**What happens if deploy.yml runs first with no preparation.** The run cannot succeed:
- With Parte 0's untracked `deploy/` present, `git checkout --detach` aborts with "untracked working tree files would be overwritten" (verified; it happens even with identical content). The deploy dies with exit 1 and changes nothing.
- If `deploy/` was removed first, the gate's `exec deploy.sh` fails because the file doesn't exist.

**What happens if the checkout is done by hand but the old stack keeps running.**
- deploy.sh treats it as a first deploy (`ACTIVE=none`), and `--window` silently falls back to zero-downtime.
- It runs the 2.0.0 migrations while the 1.5.0 API is still serving and writing.
- It starts `api-blue`, then `compose up -d web` fails because `ruinarpg-nginx-1` holds port 80.
- The undo also fails, ending in exit 2 with "nginx pode estar em manutenção". In reality 1.5.0 keeps serving against a migrated schema.

**Required steps:**
1. **Prerequisite (B).** The migration-history rewrite must run inside `--migrate` (DatabaseInitializer), because deploy.sh only ever calls `--migrate`.
2. **Rehearse on the dev stack first.** 192.168.80.16 has the same 1.5.0 layout: project `ruinarpg`, api/nginx on port 80, Production mode. Rehearse the full conversion and the rollback to 1.5.0 there. Then run several clean `workflow_dispatch` rehearsals of release.yml in bootstrap mode with an empty allowlist.
3. **Before merging.** Add **Required reviewers** to the `production` Environment (the spec's "manual approval for this deploy only"). Remove it after H.
4. **Production pre-checks.**
   - `docker volume ls` shows `ruinarpg_pgdata` and `ruinarpg_images`.
   - `.env` has `ASPNETCORE_ENVIRONMENT=Production` and every variable.
   - Record the 1.5.0 sha.
   - Retag the rollback images: `docker tag ruinarpg-api:latest ruinarpg-legacy-api:1.5.0` and `docker tag ruinarpg-nginx:latest ruinarpg-legacy-nginx:1.5.0`.
5. **Open the window.** Run `sudo docker stop ruinarpg-nginx-1 ruinarpg-api-1` and keep `db` running. This frees port 80 and stops writes.
6. **Back up.** Run `sudo deploy/deploy.sh backup` (the Parte 0 copy). Pull an off-machine copy and test-restore it in a clone.
7. **Switch the checkout.**
   - `sudo -u ruina rm -rf /home/ruina/RuinaRPG/deploy`.
   - `sudo -u ruina git -C /home/ruina/RuinaRPG fetch`, then `checkout --detach <merge sha>`.
   - Re-run `sudo deploy/provision/install-gate.sh`, since /opt holds the Parte 0 copy of gate.sh.
8. **Deploy by hand, watching the terminal.**
   - Run `sudo deploy/deploy.sh deploy <merge sha>`.
   - Expect the "sem cor ativa…" fallback log and `RESULT … active=blue smoke=ok`.
   - Then **reject** the pending deploy.yml approval. Approving it would only redeploy the same sha to green in window mode: harmless, but pointless.
9. **Verify.**
   - `deploy.sh status`.
   - `/api/version` returns the sha.
   - The site works through the Cloudflare tunnel.
   - `/images/…` is served.
   - The snackbar doesn't loop.
10. **Keep the orphans for a soak period.** Leave `ruinarpg-api-1` and `ruinarpg-nginx-1` stopped but not removed until you're confident. Then `docker rm` them; the legacy tags keep the images.
11. **Rollback to 1.5.0** (rehearsed; `deploy.sh rollback` can't do it because `PREVIOUS_COLOR=none`):
    - `docker compose -p ruinarpg stop web api-blue api-green`.
    - Restore the pre-cutover backup. Either set `ACTIVE=none` in `deploy/state/colors.env` first so `deploy.sh restore` doesn't restart blue, or run drop-schema plus pg_restore by hand.
    - `git checkout <1.5.0 sha>` (`deploy/state` is gitignored and survives).
    - `docker start ruinarpg-api-1 ruinarpg-nginx-1`.
12. **Before the first automated deploy (2.0.1).** Critical #1 (the dropped-SSH undo) is fixed in this F fix wave. Verify it is merged before relying on deploy.yml.

## From sub-project B (migration squash)

- Requirement 1 above is done: `DatabaseInitializer` calls `HistoricoDeMigracoes150.ConsolidarAsync` before `MigrateAsync`, so the `--migrate` that `deploy.sh` runs converts the 1.5.0 history to the baseline `20261017120000_Baseline_2_0_0` in one transaction, and refuses any other old state without changing anything.
- Every rehearsal runs `tools/migrations/verificar-baseline.sh <1.5.0 sha>` and requires four `OK` lines.
- Rollback to 1.5.0: after the cutover the history holds only the baseline row, which 1.5.0 does not know. A 1.5.0 API still runs (Production never migrates at startup and the schema is identical), but 1.5.0's `--migrate` (`make migrate`, `make deploy` of 1.5.0) would try to recreate the whole schema and fail. So the rollback in step 11 must restore the pre-cutover backup (as written) and must never run 1.5.0's migrate against the converted database.
- A failed 2.0.0 deploy can leave the history already converted while 1.5.0 keeps serving: the consolidation commits in its own transaction before the seeders, health check and smoke, and `undo_switch` does not restore the database. 1.5.0 keeps running (identical schema, Production never migrates at startup), and retrying 2.0.0 is safe (it sees the baseline). But never run `make migrate` / `deploy.sh migrate` while 1.5.0 is the active color (it uses the 1.5.0 image, whose `--migrate` would try `InitialCreate` and fail); restore the pre-deploy backup if the history must go back.
- Preflight, first thing, before the window: `SELECT count(*), max("MigrationId") FROM "__EFMigrationsHistory"` on production (or a fresh clone) must return 84 and `20261014100935_AddAutomaticoToSheetLogEntries`; otherwise the cutover's `--migrate` refuses (safe, but a surprise inside the window). No 1.5.x hotfix migration may land on main before the cutover unless `HistoricoDeMigracoes150.Ids` is updated to match.

## From sub-project C (deploy and load performance)

- Preflight: `sudo docker version --format '{{.Server.Version}}'` on production must be ≥ 23 (BuildKit by default; `deploy/Dockerfile` uses `RUN --mount=type=cache`). Older: upgrade Docker before the window.
- The first 2.0.0 deploy on production builds with an empty build cache: expect the cold build time of the baseline doc's "After C" table, not the warm one, and size the window for it.
- `deploy/nginx/default.conf` changes with 2.0.0 and is picked up by the color switch's `nginx -s reload`; the `web` container is not recreated. It also serves 1.5.0's client correctly, so a rollback needs nothing here.
- After the cutover, through Cloudflare: `curl -sI -H 'Accept-Encoding: gzip' https://<host>/` and the same for `/_framework/blazor.boot.json`, one `/_framework/*.wasm` (name from `blazor.boot.json`) and `/css/app.css`: each shows `cache-control` as in Técnico's deploy section and the `.wasm`/`.css` show `content-encoding` (`gzip`, or `br` if Cloudflare re-encodes). A missing `cache-control` means a Cloudflare rule overrides it; check the panel.
