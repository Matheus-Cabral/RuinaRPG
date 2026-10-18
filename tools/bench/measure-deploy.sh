#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
require_free_disk_gb 10
bench_clone "$BENCH_REF"
trap bench_teardown EXIT

# seconds_between <log> <first-line-regex> <second-line-regex> — from deploy.sh's UTC timestamps.
seconds_between() {
  local a b
  a="$(grep -m1 -E "$2" "$1" | cut -d' ' -f1)"; b="$(grep -m1 -E "$3" "$1" | cut -d' ' -f1)"
  echo $(( $(date -d "$b" +%s) - $(date -d "$a" +%s) ))
}

# Cold: an empty commit (new tags) on an empty build cache. docker builder prune wipes the host-wide
# build cache, as the old script did.
bench_commit "bench cold"
docker builder prune -af > /dev/null
rm -f "$BENCH_CLONE/deploy/state/deploy.log"
timed bench_deploy > "$RESULTS_DIR/$LABEL-deploy-cold.log" 2>&1
(( STATUS == 0 )) || { echo "cold deploy failed; see $RESULTS_DIR/$LABEL-deploy-cold.log" >&2; exit 1; }
record "deploy.full.cold.seconds" "$ELAPSED"
record "deploy.full.cold.build.seconds" "$(seconds_between "$BENCH_CLONE/deploy/state/deploy.log" 'build de ruinarpg-api' 'publicando o cliente')"

# Warm: a one-line, comment-only change in an Api source file, committed in the clone only.
bench_commit "bench warm" src/RuinaRPG.Api/Program.cs
rm -f "$BENCH_CLONE/deploy/state/deploy.log"
timed bench_deploy > "$RESULTS_DIR/$LABEL-deploy-warm.log" 2>&1
(( STATUS == 0 )) || { echo "warm deploy failed; see $RESULTS_DIR/$LABEL-deploy-warm.log" >&2; exit 1; }
record "deploy.full.warm.seconds" "$ELAPSED"
record "deploy.full.warm.build.seconds" "$(seconds_between "$BENCH_CLONE/deploy/state/deploy.log" 'build de ruinarpg-api' 'publicando o cliente')"
