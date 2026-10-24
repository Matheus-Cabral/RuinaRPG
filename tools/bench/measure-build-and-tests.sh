#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
require_free_disk_gb 4

timed dotnet build --no-incremental -v q -clp:NoSummary > "$RESULTS_DIR/$LABEL-build.log" 2>&1
record "build.seconds" "$ELAPSED"
record "build.warnings" "$(grep -c ' warning ' "$RESULTS_DIR/$LABEL-build.log" || true)"

for project in Unit Client Integration; do
  log="$RESULTS_DIR/$LABEL-test-$project.log"
  timed dotnet test "tests/RuinaRPG.Tests.$project" --no-build > "$log" 2>&1
  record "test.$project.seconds" "$ELAPSED"
  record "test.$project.summary" "$(grep -E '(Passed|Failed)!' "$log" | tail -1 | sed 's/  */ /g')"
  record "test.$project.timeouts" "$(grep -c 'HttpClient.Timeout' "$log" || true)"
done
