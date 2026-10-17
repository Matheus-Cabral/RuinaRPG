#!/usr/bin/env bash
# Usage: fingerprint.sh <compose-project> <out.jsonl>
# Writes one JSON line per row of every public base table (except __EFMigrationsHistory):
#   {"table": T, "pk": "<pk values joined by |>" | null, "row": {col: text | null}}
set -euo pipefail
(( $# == 2 )) || { echo "uso: $0 <compose-project> <out.jsonl>" >&2; exit 2; }
project="$1"; out="$2"
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cfile="${RUINA_COMPOSE_FILE:-$repo/docker-compose.yml}"

dc() { docker compose -p "$project" -f "$cfile" exec -T db "$@" </dev/null; }
dbuser="${RUINA_DB_USER:-$(dc printenv POSTGRES_USER | tr -d '\r')}"
dbname="${RUINA_DB_NAME:-$(dc printenv POSTGRES_DB | tr -d '\r')}"
psqlq() { dc psql -U "$dbuser" -d "$dbname" -At -v ON_ERROR_STOP=1 "$@"; }

tables="$(psqlq -F $'\t' -c "
  SELECT c.relname,
         COALESCE((SELECT string_agg(a.attname, ',' ORDER BY k.ord)
                   FROM pg_index i
                   CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY k(attnum, ord)
                   JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = k.attnum
                   WHERE i.indrelid = c.oid AND i.indisprimary), '')
  FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
  WHERE c.relkind IN ('r','p') AND c.relname <> '__EFMigrationsHistory'
  ORDER BY c.relname")"

: > "$out"
while IFS=$'\t' read -r table pkcols; do
  [[ -n "$table" ]] || continue
  if [[ -z "$pkcols" ]]; then
    pkexpr="NULL"
  else
    pkexpr="concat_ws('|'"
    IFS=',' read -ra cols <<< "$pkcols"
    for c in "${cols[@]}"; do pkexpr+=", x.\"$c\"::text"; done
    pkexpr+=")"
  fi
  psqlq -c "SELECT json_build_object('table', '$table', 'pk', $pkexpr, 'row', (SELECT jsonb_object_agg(key, value) FROM jsonb_each_text(to_jsonb(x))))::text FROM \"$table\" x" >> "$out"
done <<< "$tables"
