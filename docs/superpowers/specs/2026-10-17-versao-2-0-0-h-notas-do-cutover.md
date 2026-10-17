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
