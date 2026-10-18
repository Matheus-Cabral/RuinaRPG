#!/usr/bin/env bash
# Shared helpers for tools/bench/*. Source it; do not run it.
set -euo pipefail

BENCH_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BENCH_DIR="$BENCH_ROOT/tools/bench"
RESULTS_DIR="$BENCH_DIR/results"
mkdir -p "$RESULTS_DIR"

LABEL="${1:?usage: $0 <label> [ref]}"
BENCH_REF="${2:-HEAD}"
RESULT_FILE="$RESULTS_DIR/$LABEL.txt"

# record <key> <value> — prints and appends one result line.
record() {
  echo "$1: $2" | tee -a "$RESULT_FILE"
}

# timed <command...> — runs the command, sets ELAPSED (seconds, 1 decimal) and keeps its exit code in STATUS.
timed() {
  local start end
  start=$(date +%s.%N)
  set +e
  "$@"
  STATUS=$?
  set -e
  end=$(date +%s.%N)
  ELAPSED=$(echo "$end - $start" | bc -l | xargs printf '%.1f')
}

BENCH_URL="http://127.0.0.1:8088"

# bench_clone <ref> — a shared clone of this repo at <ref>, with a Production .env on 127.0.0.1:8088.
# Sets BENCH_TMP, BENCH_CLONE, BENCH_SHA. The stack is deployed by the clone's own deploy/deploy.sh, so
# "before" and "after" refs are measured with the deploy code of their own commit.
bench_clone() {
  BENCH_TMP="$(mktemp -d)"
  BENCH_CLONE="$BENCH_TMP/repo"
  BENCH_THROWAWAY_SHAS=()
  git clone --quiet --shared "$BENCH_ROOT" "$BENCH_CLONE"
  git -C "$BENCH_CLONE" checkout --quiet --detach "$1"
  BENCH_SHA="$(git -C "$BENCH_CLONE" rev-parse HEAD)"
  sed -e 's/^ASPNETCORE_ENVIRONMENT=.*/ASPNETCORE_ENVIRONMENT=Production/' \
      -e "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=$(openssl rand -hex 24)/" \
      -e 's#^CORS_ALLOWED_ORIGINS=.*#CORS_ALLOWED_ORIGINS=http://127.0.0.1:8088#' \
      "$BENCH_CLONE/.env.example" > "$BENCH_CLONE/.env"
  printf 'HTTP_PORT=8088\nHTTP_BIND=127.0.0.1\n' >> "$BENCH_CLONE/.env"
}

# bench_commit <message> [file-to-append-a-comment-to] — a commit that exists only in the clone, so its
# image tags are new and deploy.sh really builds. Sets BENCH_SHA.
bench_commit() {
  if [[ -n "${2:-}" ]]; then echo "// bench-touch $(date +%s%N)" >> "$BENCH_CLONE/$2"; git -C "$BENCH_CLONE" add -- "$2"; fi
  git -C "$BENCH_CLONE" -c user.name=bench -c user.email=bench@localhost commit --quiet --allow-empty -m "$1"
  BENCH_SHA="$(git -C "$BENCH_CLONE" rev-parse HEAD)"
  BENCH_THROWAWAY_SHAS+=("$BENCH_SHA")
}

# bench_deploy — deploys BENCH_SHA into the throwaway ruinarpg-bench project; never the user's stack.
bench_deploy() {
  RUINA_REPO_DIR="$BENCH_CLONE" RUINA_PROJECT=ruinarpg-bench RUINA_BACKUP_DIR="$BENCH_TMP/backups" \
  RUINA_SKIP_FETCH=1 RUINA_DRAIN_SECONDS=0 RUINA_PRUNE_IMAGES=0 RUINA_SMOKE_CMD=true \
    "$BENCH_CLONE/deploy/deploy.sh" deploy "$BENCH_SHA"
}

# bench_teardown — the bench stack with its volumes, the clone, and the images of throwaway commits
# (those tags exist nowhere else). Images of real commits are kept: a real deploy may reuse them.
bench_teardown() {
  docker compose -p ruinarpg-bench -f "$BENCH_CLONE/docker-compose.yml" --env-file "$BENCH_CLONE/.env" \
    --profile tools down --volumes --remove-orphans > /dev/null 2>&1 || true
  local s
  for s in "${BENCH_THROWAWAY_SHAS[@]}"; do
    docker rmi "ruinarpg-api:$s" "ruinarpg-client:$s" "ruinarpg-smoke:$s" > /dev/null 2>&1 || true
  done
  # Backups are written by containers and may be root-owned.
  docker run --rm -v "$BENCH_TMP:/t" alpine:3.20 rm -rf /t/backups > /dev/null 2>&1 || true
  rm -rf "$BENCH_TMP"
}

require_free_disk_gb() {
  local free_gb
  free_gb=$(df --output=avail -BG / | tail -1 | tr -dc '0-9')
  if (( free_gb < $1 )); then
    echo "Only ${free_gb} GB free on /; need $1 GB. Run 'docker builder prune -f' first." >&2
    exit 1
  fi
}
