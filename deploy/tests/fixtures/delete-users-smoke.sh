#!/usr/bin/env bash
# Stands in for the smoke of the head deploy in clone.bats (RUINA_HEAD_SMOKE_CMD): a "release" that loses
# data. Runs inside deploy.sh, which exports the clone's RUINA_PROJECT and RUINA_REPO_DIR.
set -euo pipefail
docker compose -p "$RUINA_PROJECT" -f "$RUINA_REPO_DIR/docker-compose.yml" exec -T db \
  sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d ruinarpg -c "DELETE FROM \"AspNetUsers\""' </dev/null
