#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
require_free_disk_gb 6

docker builder prune -f > /dev/null
timed bench_compose build > "$RESULTS_DIR/$LABEL-deploy-cold.log" 2>&1
record "deploy.build.cold.seconds" "$ELAPSED"

# Warm: a one-line, comment-only change in an Api source file, then rebuild; reverted afterwards.
touched=src/RuinaRPG.Api/Program.cs
echo "// bench-touch" >> "$touched"
timed bench_compose build > "$RESULTS_DIR/$LABEL-deploy-warm.log" 2>&1
git checkout -- "$touched"
record "deploy.build.warm.seconds" "$ELAPSED"
