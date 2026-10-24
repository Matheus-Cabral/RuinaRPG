#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
for p in src/* tests/*; do
  [[ -d "$p" ]] || continue
  cs=$(find "$p" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -not -path '*/Migrations/*' -print0 | xargs -0 cat 2>/dev/null | wc -l)
  razor=$(find "$p" -name '*.razor' -not -path '*/obj/*' -print0 | xargs -0 cat 2>/dev/null | wc -l)
  record "lines.$(basename "$p")" "cs=$cs razor=$razor"
done
record "migrations.files" "$(ls src/RuinaRPG.Infrastructure/Persistence/Migrations | wc -l)"
record "migrations.size" "$(du -sh src/RuinaRPG.Infrastructure/Persistence/Migrations | cut -f1)"
