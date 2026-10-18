#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
require_free_disk_gb 10
bench_clone "$BENCH_REF"
trap bench_teardown EXIT

# seconds_between <log> <first-line-regex> <second-line-regex> — from deploy.sh's UTC timestamps.
seconds_between() {
  local a b
  a="$(grep -m1 -E "$2" "$1" | cut -d' ' -f1)"; b="$(grep -m1 -E "$3" "$1" | cut -d' ' -f1)"
  if [[ -z "$a" || -z "$b" ]]; then
    echo "measure-deploy.sh: pattern not found in $1 (first='$2' -> '$a', second='$3' -> '$b')" >&2
    return 1
  fi
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
# Assigned first: a failure inside "$(...)" as an argument would not stop the script, but an assignment does (set -e).
build_seconds="$(seconds_between "$BENCH_CLONE/deploy/state/deploy.log" 'build de ruinarpg-api' 'publicando o cliente')"
record "deploy.full.cold.build.seconds" "$build_seconds"

# Warm: a one-line, comment-only change in an Api source file, committed in the clone only.
bench_commit "bench warm" src/RuinaRPG.Api/Program.cs
rm -f "$BENCH_CLONE/deploy/state/deploy.log"
timed bench_deploy > "$RESULTS_DIR/$LABEL-deploy-warm.log" 2>&1
(( STATUS == 0 )) || { echo "warm deploy failed; see $RESULTS_DIR/$LABEL-deploy-warm.log" >&2; exit 1; }
record "deploy.full.warm.seconds" "$ELAPSED"
# Assigned first: a failure inside "$(...)" as an argument would not stop the script, but an assignment does (set -e).
build_seconds="$(seconds_between "$BENCH_CLONE/deploy/state/deploy.log" 'build de ruinarpg-api' 'publicando o cliente')"
record "deploy.full.warm.build.seconds" "$build_seconds"
