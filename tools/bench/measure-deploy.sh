#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
require_free_disk_gb 6

# Checked first: this script reverts $touched, so it must not start on top of local edits.
touched=src/RuinaRPG.Api/Program.cs
if ! git diff --quiet -- "$touched" || ! git diff --cached --quiet -- "$touched"; then
  echo "measure-deploy.sh: $touched has uncommitted changes; commit or stash them first (the script reverts that file)." >&2
  exit 1
fi
trap 'git checkout -- "$touched"' EXIT

docker builder prune -f > /dev/null
timed bench_compose build > "$RESULTS_DIR/$LABEL-deploy-cold.log" 2>&1
record "deploy.build.cold.seconds" "$ELAPSED"

# Warm: a one-line, comment-only change in an Api source file, then rebuild; reverted afterwards.
echo "// bench-touch" >> "$touched"
timed bench_compose build > "$RESULTS_DIR/$LABEL-deploy-warm.log" 2>&1
record "deploy.build.warm.seconds" "$ELAPSED"
