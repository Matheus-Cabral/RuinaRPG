#!/usr/bin/env bash
# Shared helpers for tools/bench/*. Source it; do not run it.
set -euo pipefail

BENCH_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BENCH_DIR="$BENCH_ROOT/tools/bench"
RESULTS_DIR="$BENCH_DIR/results"
mkdir -p "$RESULTS_DIR"

LABEL="${1:?usage: $0 <label>}"
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

# bench_compose <args...> — the throwaway bench stack; never the user's own project.
bench_compose() {
  docker compose -p ruinarpg-bench --env-file "$BENCH_DIR/bench.env" \
    -f "$BENCH_ROOT/docker-compose.yml" -f "$BENCH_DIR/compose.bench.yml" "$@"
}

require_free_disk_gb() {
  local free_gb
  free_gb=$(df --output=avail -BG / | tail -1 | tr -dc '0-9')
  if (( free_gb < $1 )); then
    echo "Only ${free_gb} GB free on /; need $1 GB. Run 'docker builder prune -f' first." >&2
    exit 1
  fi
}
