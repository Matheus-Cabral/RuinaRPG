#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
require_free_disk_gb 4
project=tests/RuinaRPG.Tests.Integration

mapfile -t classes < <(dotnet test "$project" --no-build --list-tests 2>/dev/null \
  | grep -E '^\s+RuinaRPG\.' | sed -E 's/^\s+//; s/\.[^.]+$//; s/\(.*$//' | sort -u)
total=${#classes[@]}
per=$(( (total + 5) / 6 ))
grand=0
for slice in 0 1 2 3 4 5; do
  chunk=("${classes[@]:$((slice * per)):per}")
  (( ${#chunk[@]} )) || continue
  filter=$(printf 'FullyQualifiedName~%s.|' "${chunk[@]}"); filter=${filter%|}
  log="$RESULTS_DIR/$LABEL-slice-$slice.log"
  timed dotnet test "$project" --no-build --filter "$filter" > "$log" 2>&1
  grand=$(echo "$grand + $ELAPSED" | bc -l)
  record "slice.$slice.seconds" "$ELAPSED"
  record "slice.$slice.summary" "$(grep -E '(Passed|Failed)!' "$log" | tail -1 | sed 's/  */ /g')"
done
record "slices.total.seconds" "$grand"
