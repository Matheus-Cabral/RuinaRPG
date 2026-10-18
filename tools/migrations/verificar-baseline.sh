#!/usr/bin/env bash
# Prova que a baseline da 2.0.0 equivale à cadeia de migrations da 1.5.0 (spec B, B.3).
#   uso: tools/migrations/verificar-baseline.sh <ref da 1.5.0>     (ex.: 729dac9)
# Num Postgres descartável, monta quatro bancos:
#   a0 = cadeia antiga só com migrations      b0 = árvore atual só com migrations
#   a  = --migrate da 1.5.0 (migrations + seeders)
#   c  = cópia de a + --migrate da árvore atual (o caminho de upgrade da produção)
# e exige: schema e dump completo a0 = b0; dump completo a = c; histórico de c = histórico de b0.
# __EFMigrationsHistory fica fora dos dumps. Sai com 1 e imprime o diff se algo diferir.
set -euo pipefail
shopt -s inherit_errexit

ref="${1:?uso: verificar-baseline.sh <ref da 1.5.0>}"
root="$(git rev-parse --show-toplevel)"
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
name="ruina-verificar-baseline-$$"
image="postgres:16-alpine"

cleanup() {
  docker rm -f "$name" >/dev/null 2>&1 || true
  git -C "$root" worktree remove --force "$work/old" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

step() { printf '== %s\n' "$*" >&2; }

step "worktree de $ref"
git -C "$root" worktree add --detach "$work/old" "$ref" >/dev/null 2>&1

step "postgres descartável ($name)"
docker run -d --name "$name" -e POSTGRES_PASSWORD=verificar -p 127.0.0.1::5432 "$image" >/dev/null
port="$(docker port "$name" 5432/tcp | head -1 | sed 's/.*://')"
# Via TCP: o servidor temporário do initdb da imagem só escuta no socket Unix, então o pg_isready
# pelo socket podia passar antes da hora e falhar (calado) quando ele desligava.
for _ in $(seq 1 60); do
  docker exec "$name" pg_isready -h 127.0.0.1 -U postgres -q && break
  sleep 1
done
docker exec "$name" pg_isready -h 127.0.0.1 -U postgres >&2

psql_() { docker exec -i "$name" psql -v ON_ERROR_STOP=1 -U postgres -qAt "$@"; }
cs() { printf 'Host=127.0.0.1;Port=%s;Database=%s;Username=postgres;Password=verificar' "$port" "$1"; }

for db in a0 b0 a; do psql_ -c "CREATE DATABASE $db"; done

export ASPNETCORE_ENVIRONMENT=Production
export Jwt__SigningKey="verificar-baseline-chave-descartavel-0123456789"
export Jwt__Issuer=verificar Jwt__Audience=verificar

run_logged() {
  local log="$work/$1.log"; shift
  if ! "$@" >"$log" 2>&1; then
    cat "$log" >&2
    printf 'falhou: %s\n' "$*" >&2
    exit 1
  fi
}

ef_update() {
  step "dotnet ef database update ($2)"
  run_logged "$2-ef" dotnet ef database update \
    --project "$1/src/RuinaRPG.Infrastructure" --startup-project "$1/src/RuinaRPG.Api" \
    --connection "$(cs "$2")"
}

migrate() {
  step "--migrate ($2)"
  ConnectionStrings__Postgres="$(cs "$2")" run_logged "$2-migrate" \
    dotnet run --no-launch-profile --project "$1/src/RuinaRPG.Api" -- --migrate
}

# dotnet ef não restaura pacotes sozinho (o worktree novo não tem obj/).
for tree in "$work/old" "$root"; do
  step "dotnet restore ($tree)"
  run_logged "restore-$(basename "$tree")" dotnet restore "$tree/src/RuinaRPG.Api"
done

ef_update "$work/old" a0
ef_update "$root" b0
migrate "$work/old" a
psql_ -c "CREATE DATABASE c TEMPLATE a"
migrate "$root" c

dump() {
  docker exec "$name" pg_dump -U postgres --no-owner --no-privileges \
    --exclude-table='"__EFMigrationsHistory"' "$@" | python3 -I "$here/normalizar-dump.py"
}
historico() { psql_ -d "$1" -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1'; }

dump --schema-only a0 >"$work/a0.schema.sql"
dump --schema-only b0 >"$work/b0.schema.sql"
dump a0 >"$work/a0.sql"
dump b0 >"$work/b0.sql"
dump a >"$work/a.sql"
dump c >"$work/c.sql"
historico b0 >"$work/b0.hist"
historico c >"$work/c.hist"

status=0
compare() {
  if diff -u "$work/$1" "$work/$2" >"$work/diff" ; then
    printf 'OK    %s\n' "$3"
  else
    printf 'FALHA %s\n' "$3"
    cat "$work/diff"
    status=1
  fi
}
compare a0.schema.sql b0.schema.sql "schema: cadeia da 1.5.0 = baseline"
compare a0.sql b0.sql "dump completo (migrations): cadeia da 1.5.0 = baseline"
compare a.sql c.sql "dump completo (com seeders): 1.5.0 = 1.5.0 atualizada"
compare b0.hist c.hist "histórico: atualizado = novo"
exit "$status"
