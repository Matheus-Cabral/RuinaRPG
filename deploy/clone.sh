#!/usr/bin/env bash
# Validates a release against a copy of production (Técnico R0013), on the self-hosted runner.
#
#   clone.sh run <base-sha> <head-sha> <workdir>
#   clone.sh teardown <workdir>    removes what a run left behind (the workflow's `if: always()` step:
#                                  a cancelled job may kill the run before its EXIT trap finishes)
#
# Pulls a production dump through the gate, restores it into a throwaway compose project, deploys the
# base (main) and then the head with deploy.sh — this checkout's deploy.sh, the one under review —
# fingerprints the data before and after the head deploy, compares them against the head's
# deploy/data-changes.yml, and checks whether the base API still runs against the migrated database.
# The clone is always torn down (EXIT trap).
#
# Bootstrap: a base without deploy/deploy.sh (1.5.0, before the blue-green pipeline existed) cannot be
# deployed by deploy.sh. Then the worktree starts at the head, fingerprint A is taken from the restored
# dump itself, the base deploy and the compat check are skipped, and compat is forced to `janela`.
#
# Writes into <workdir>: report.md (Markdown for the PR comment, counts only), report-full.md (same with sampled pks: stays on this VM), compat (`ok` or `janela`), logs/<step>.log.
# Exit: 0 ok; 1 validation failure (head deploy/smoke failed, data findings); 2 infrastructure error.
#
# Configuration (environment):
#   RUINA_DUMP_CMD       the only way production is ever contacted (default: ssh to the gate's `dump`)
#   RUINA_CLONE_PROJECT  compose project, must start with ruinarpg-clone or ruinarpg-bats (ruinarpg-clone)
#   RUINA_CLONE_PORT     127.0.0.1 port of the clone's nginx (8089)
#   RUINA_OFFSITE_DIR    where each dump is kept as an off-machine backup (/var/backups/ruinarpg-offsite)
#   RUINA_KEEP_OFFSITE   how many of those to keep (5; deploy.yml's -pos-deploy copies are counted apart)
#   RUINA_HEAD_SMOKE_CMD test hook only: replaces the smoke of the head deploy (RUINA_SMOKE_CMD of deploy.sh)
set -euo pipefail
shopt -s inherit_errexit

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib.sh
source "$here/lib.sh"
SOURCE_REPO="$(cd "$here/.." && pwd)"

: "${RUINA_DUMP_CMD:=ssh -o BatchMode=yes deploy@192.168.80.10 dump}"
: "${RUINA_CLONE_PROJECT:=ruinarpg-clone}"
: "${RUINA_CLONE_PORT:=8089}"
: "${RUINA_OFFSITE_DIR:=/var/backups/ruinarpg-offsite}"
: "${RUINA_KEEP_OFFSITE:=5}"
: "${RUINA_HEALTH_TIMEOUT:=120}"
BUSYBOX="busybox:1.36"

BASE="" HEAD="" WD="" REPORT="" RC=0 FAILED_STEP="" TORN_DOWN="" BOOTSTRAP=""

usage() { echo "uso: clone.sh run <base-sha> <head-sha> <workdir> | clone.sh teardown <workdir>" >&2; exit 2; }
fatal() { echo "ERRO: $*" >&2; exit 2; }

validate_config() {
  # The teardown runs `down --volumes` on this project: never let it name production.
  [[ "$RUINA_CLONE_PROJECT" =~ ^ruinarpg-(clone|bats)[a-z0-9_-]*$ ]] ||
    fatal "RUINA_CLONE_PROJECT deve começar com ruinarpg-clone ou ruinarpg-bats: $RUINA_CLONE_PROJECT"
  [[ "$RUINA_CLONE_PORT" =~ ^[1-9][0-9]*$ ]] || fatal "RUINA_CLONE_PORT deve ser um número de porta"
  [[ "$RUINA_KEEP_OFFSITE" =~ ^[1-9][0-9]*$ ]] || fatal "RUINA_KEEP_OFFSITE deve ser um inteiro >= 1"
  [[ "$RUINA_HEALTH_TIMEOUT" =~ ^(0|[1-9][0-9]*)$ ]] || fatal "RUINA_HEALTH_TIMEOUT deve ser um inteiro >= 0"
}

# deploy.sh and lib.sh's compose() read these; nothing inherited from the caller may point them at
# another stack.
export_clone_env() {
  export RUINA_REPO_DIR="$WD/repo"
  export RUINA_PROJECT="$RUINA_CLONE_PROJECT"
  export RUINA_ENV_FILE="$WD/repo/.env"
  export RUINA_STATE_DIR="$WD/repo/deploy/state"
  export RUINA_BACKUP_DIR="$WD/backups"
  export RUINA_SKIP_FETCH=1
  # Images are host-wide and the runner is shared: never prune them from here.
  export RUINA_PRUNE_IMAGES=0
  # Nobody uses the clone: no in-flight requests to drain.
  export RUINA_DRAIN_SECONDS="${RUINA_DRAIN_SECONDS:-0}"
  export RUINA_HEALTH_TIMEOUT
}

note() { printf '%s\n' "$*" >> "$REPORT"; }

# Runs step function $3 in a subshell with errexit (a plain call inside `if` would ignore it), logging to
# logs/$1.log. $2 says what a failure means: infra (exit 2), validation (exit 1) or passthrough (the
# step's own 1 is a validation failure, anything else infra).
step() {
  local name="$1" kind="$2" fn="$3" rc
  shift 3
  log "passo $name" 2>> "$WD/logs/$name.log"
  set +e
  ( set -e; "$fn" "$@" ) >> "$WD/logs/$name.log" 2>&1
  rc=$?
  set -e
  if (( rc == 0 )); then
    note "- $name: ok$(step_detail "$name")"
    return 0
  fi
  note "- $name: FALHOU (código $rc; veja logs/$name.log)$(step_detail "$name")"
  FAILED_STEP="$name"
  case "$kind" in
    validation) RC=1 ;;
    passthrough) if (( rc == 1 )); then RC=1; else RC=2; fi ;;
    *) RC=2 ;;
  esac
  exit "$RC"
}

# One extra line of context for the report, read from what the step left behind.
step_detail() {
  case "$1" in
    dump) [[ -f "$WD/offsite-name" ]] && printf ' (cópia fora da máquina: %s)' "$(cat "$WD/offsite-name")" ;;
    base|head) { grep '^RESULT ' "$WD/logs/$1.log" || true; } | tail -1 | sed 's/^/ — `/; s/$/`/' ;;
    fingerprint-a) [[ -f "$WD/fingerprint-a.jsonl" ]] && printf ' (%s linhas)' "$(wc -l < "$WD/fingerprint-a.jsonl")" ;;
    fingerprint-b) [[ -f "$WD/fingerprint-b.jsonl" ]] && printf ' (%s linhas)' "$(wc -l < "$WD/fingerprint-b.jsonl")" ;;
    compat) [[ -f "$WD/compat" ]] && printf ' — %s' "$(cat "$WD/compat")" ;;
  esac
  return 0
}

# ---------------------------------------------------------------- steps

do_dump() {
  local name
  bash -c "$RUINA_DUMP_CMD" > "$WD/dump.tar"
  mkdir "$WD/dump"
  tar -C "$WD/dump" -xf "$WD/dump.tar"
  [[ -s "$WD/dump/db.dump" && -f "$WD/dump/images.tar" ]] || { echo "dump inválido: falta db.dump ou images.tar" >&2; exit 2; }
  # The tar carries the backup's contents, not its directory name: name the copy like deploy.sh does.
  local sha; sha="$(head -c 7 "$WD/dump/sha" 2>/dev/null || true)"
  name="$(date -u +%Y%m%dT%H%M%SZ)-clone-${sha:-inicial}"
  mkdir -p "$RUINA_OFFSITE_DIR"
  chmod 700 "$RUINA_OFFSITE_DIR"
  rm -rf "$RUINA_OFFSITE_DIR/.$name.partial"
  cp -a "$WD/dump" "$RUINA_OFFSITE_DIR/.$name.partial"
  mv "$RUINA_OFFSITE_DIR/.$name.partial" "$RUINA_OFFSITE_DIR/$name"
  echo "cópia fora da máquina: $RUINA_OFFSITE_DIR/$name"
  echo "$name" > "$WD/offsite-name"
  # Names start with a UTC timestamp: reverse name order is newest first. Only clone copies count
  # here; deploy.yml keeps its own -pos-deploy copies, five of each.
  ls -1 "$RUINA_OFFSITE_DIR" | { grep -E '^[0-9]{8}T[0-9]{6}Z-clone-' || true; } | sort -r |
    tail -n +$((RUINA_KEEP_OFFSITE + 1)) | while IFS= read -r old; do
      echo "removendo cópia antiga $old"
      rm -rf "${RUINA_OFFSITE_DIR:?}/$old"
    done
  # A copy killed halfway stays hidden as .<name>.partial; drop it once it is over a day old.
  find "$RUINA_OFFSITE_DIR" -mindepth 1 -maxdepth 1 -type d -name '.*.partial' -mtime +0 -exec rm -rf {} +
}

# Sets "$1"="$2" in the env file, replacing the line or appending it.
set_env() {
  local file="$1" key="$2" value="$3"
  if grep -q "^$key=" "$file"; then
    sed -i "s|^$key=.*|$key=$value|" "$file"
  else
    printf '%s=%s\n' "$key" "$value" >> "$file"
  fi
}

do_checkout() {
  git -C "$SOURCE_REPO" worktree add --detach "$WD/repo" "$1"
  cp "$WD/repo/.env.example" "$WD/repo/.env"
  chmod 600 "$WD/repo/.env"
  set_env "$WD/repo/.env" ASPNETCORE_ENVIRONMENT Production
  set_env "$WD/repo/.env" POSTGRES_PASSWORD "$(openssl rand -hex 24)"
  set_env "$WD/repo/.env" JWT_SIGNING_KEY "$(openssl rand -hex 24)"
  set_env "$WD/repo/.env" HTTP_BIND 127.0.0.1
  set_env "$WD/repo/.env" HTTP_PORT "$RUINA_CLONE_PORT"
}

do_restore() { "$here/deploy.sh" restore "$WD/dump"; }

do_deploy() { "$here/deploy.sh" deploy "$1"; }

# The smoke override applies to this deploy only: the base and the compat check keep the real smoke.
do_deploy_head() {
  if [[ -n "${RUINA_HEAD_SMOKE_CMD:-}" ]]; then
    RUINA_SMOKE_CMD="$RUINA_HEAD_SMOKE_CMD" "$here/deploy.sh" deploy "$HEAD"
  else
    "$here/deploy.sh" deploy "$HEAD"
  fi
}

do_fingerprint() {
  RUINA_COMPOSE_FILE="$WD/repo/docker-compose.yml" "$here/fingerprint.sh" "$RUINA_CLONE_PROJECT" "$WD/fingerprint-$1.jsonl"
}

# The allowlist is the head's: the worktree is at the head now.
do_compare() {
  python3 "$here/compare.py" "$WD/fingerprint-a.jsonl" "$WD/fingerprint-b.jsonl" \
    "$WD/repo/deploy/data-changes.yml" --markdown "$WD/compare.md" --full "$WD/compare-full.md"
}

wait_api() {
  local color="$1" deadline=$((SECONDS + RUINA_HEALTH_TIMEOUT)) body
  while (( SECONDS < deadline )); do
    body="$(docker run --rm --network "${RUINA_CLONE_PROJECT}_default" "$BUSYBOX" \
      wget -qO- -T 2 "http://api-$color:8080/health" 2>/dev/null || true)"
    [[ "$body" == '"OK"' ]] && return 0
    sleep 2
  done
  echo "api-$color não respondeu ao health check em ${RUINA_HEALTH_TIMEOUT}s" >&2
  return 1
}

# Can the base release run against the head's migrated schema? If so the production deploy can be
# zero-downtime; if not it needs a maintenance window. Never a failure of the PR.
compat_check() {
  local new old
  new="$(sed -n 's/^ACTIVE=//p' "$RUINA_STATE_DIR/colors.env")"
  old="$(sed -n 's/^PREVIOUS_COLOR=//p' "$RUINA_STATE_DIR/colors.env")"
  [[ "$new" =~ ^(blue|green)$ && "$old" =~ ^(blue|green)$ ]] || { echo "estado inesperado: ACTIVE=$new PREVIOUS_COLOR=$old" >&2; return 1; }
  echo "parando api-$new; subindo api-$old com a imagem de $BASE contra o banco migrado"
  compose stop "api-$new"
  # The shell environment wins over colors.env in compose's interpolation.
  if [[ "$old" == blue ]]; then
    BLUE_TAG="$BASE" compose up -d --no-deps api-blue
  else
    GREEN_TAG="$BASE" compose up -d --no-deps api-green
  fi
  wait_api "$old"
  echo "smoke de $BASE contra api-$old"
  if [[ -n "${RUINA_SMOKE_CMD:-}" ]]; then
    RUINA_COLOR="$old" bash -c "$RUINA_SMOKE_CMD"
  else
    SMOKE_TAG="$BASE" SMOKE_BASE_URL="http://api-$old:8080" compose run --rm --no-deps smoke
  fi
}

do_compat() {
  local result=janela rc
  # Not `if ( … )`: errexit is ignored inside a condition.
  set +e
  ( set -e; compat_check )
  rc=$?
  set -e
  (( rc != 0 )) || result=ok
  echo "compat=$result"
  echo "$result" > "$WD/compat"
}

# ---------------------------------------------------------------- teardown

# Also used before the run, to clear what a killed run may have left under the same project name.
remove_project() {
  local p="$RUINA_CLONE_PROJECT" ids
  if [[ -f "$WD/repo/docker-compose.yml" && -f "$WD/repo/.env" ]]; then
    docker compose -p "$p" -f "$WD/repo/docker-compose.yml" --env-file "$WD/repo/.env" \
      --profile tools down --volumes --remove-orphans || true
  fi
  # Whatever compose did not know about: containers by label, and volumes that deploy.sh's busybox
  # runs created by name (without compose's labels).
  mapfile -t ids < <(docker ps -aq --filter "label=com.docker.compose.project=$p")
  (( ${#ids[@]} == 0 )) || docker rm -f "${ids[@]}" >/dev/null
  mapfile -t ids < <({ docker volume ls -q --filter "label=com.docker.compose.project=$p"
                       docker volume ls -q | { grep -E "^${p}_" || true; }; } | sort -u)
  (( ${#ids[@]} == 0 )) || docker volume rm -f "${ids[@]}" >/dev/null
  mapfile -t ids < <(docker network ls -q --filter "label=com.docker.compose.project=$p")
  (( ${#ids[@]} == 0 )) || docker network rm "${ids[@]}" >/dev/null
  [[ -z "$(docker ps -aq --filter "label=com.docker.compose.project=$p")" ]] &&
    [[ -z "$(docker volume ls -q | { grep -E "^${p}_" || true; })" ]]
}

do_teardown() {
  local ok=0
  remove_project || ok=1
  if [[ -e "$WD/repo" ]]; then
    git -C "$SOURCE_REPO" worktree remove --force "$WD/repo" || { rm -rf "$WD/repo"; }
  fi
  git -C "$SOURCE_REPO" worktree prune || true
  # Copies of production data (dumps hold every account's password hash) never outlive the run.
  rm -rf "$WD/dump" "$WD/dump.tar" "$WD/backups" "$WD"/fingerprint-*.jsonl "$WD/offsite-name"
  [[ ! -e "$WD/repo" ]] || ok=1
  return "$ok"
}

on_exit() {
  local rc=$?
  # Ignore further signals (not reset them): a cancelled job sends more than one, and a second one
  # must not kill the teardown halfway.
  trap - EXIT
  trap '' INT TERM
  [[ -n "$TORN_DOWN" ]] && exit "$rc"
  TORN_DOWN=1
  (( RC != 0 )) || RC="$rc"
  # Anything but a validation failure (signals, an unexpected error) is an infrastructure error.
  (( RC == 0 || RC == 1 )) || RC=2
  if ( do_teardown ) >> "$WD/logs/teardown.log" 2>&1; then
    note "- teardown: ok"
  else
    note "- teardown: FALHOU (veja logs/teardown.log)"
    (( RC != 0 )) || RC=2
  fi
  note ""
  if (( RC == 0 )); then
    note "**Resultado: OK**"
  else
    local why="erro de infraestrutura"
    (( RC == 1 )) && why="validação"
    note "**Resultado: FALHOU — ${FAILED_STEP:-interrompido} ($why)**"
  fi
  # report-full.md (pks sampled) stays on this VM; report.md (counts only) is what goes to GitHub.
  cp "$REPORT" "$WD/report-full.md"
  if [[ -s "$WD/compare.md" ]]; then
    note ""
    cat "$WD/compare.md" >> "$REPORT"
    { echo; cat "$WD/compare-full.md" 2>/dev/null || cat "$WD/compare.md"; } >> "$WD/report-full.md"
  fi
  exit "$RC"
}

cmd_run() {
  (( $# == 3 )) || usage
  BASE="$1" HEAD="$2" WD="$3"
  validate_config
  require_cmd docker
  require_cmd git
  require_cmd python3
  require_cmd flock
  require_cmd openssl
  umask 077
  mkdir -p "$WD/logs"
  WD="$(cd "$WD" && pwd)"
  REPORT="$WD/report.md"
  [[ ! -e "$WD/repo" ]] || fatal "$WD/repo já existe: use um diretório de trabalho novo"
  rm -f "$WD/compat" "$WD/compare.md" "$WD/compare-full.md" "$WD/report-full.md"
  export_clone_env

  # One clone per project at a time: the project name and the port are shared by every run.
  exec 8> "${TMPDIR:-/tmp}/$RUINA_CLONE_PROJECT.lock"
  log "aguardando o lock de $RUINA_CLONE_PROJECT"
  flock 8

  {
    echo "## Validação em clone de produção"
    echo "- base: \`$BASE\`"
    echo "- head: \`$HEAD\`"
  } > "$REPORT"
  trap on_exit EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM

  ( remove_project ) >> "$WD/logs/teardown.log" 2>&1 || true

  # An unknown base would otherwise look like a base without deploy.sh.
  if ! git -C "$SOURCE_REPO" rev-parse --verify --quiet "$BASE^{commit}" >/dev/null; then
    note "- base: FALHOU (commit desconhecido)"
    FAILED_STEP=base RC=2
    exit 2
  fi
  git -C "$SOURCE_REPO" cat-file -e "$BASE:deploy/deploy.sh" 2>/dev/null || BOOTSTRAP=1

  step dump infra do_dump
  if [[ -n "$BOOTSTRAP" ]]; then
    note "- bootstrap: a versão base não tem deploy/deploy.sh — fingerprint A tirado do dump restaurado; compat forçado para janela"
    step checkout infra do_checkout "$HEAD"
    step restore infra do_restore
  else
    step checkout infra do_checkout "$BASE"
    step restore infra do_restore
    step base infra do_deploy "$BASE"
  fi
  step fingerprint-a infra do_fingerprint a
  # Every failure of the head deploy counts as a validation failure (exit 1), including infrastructure
  # causes such as a failed image build or pull: deploy.sh's exit code does not tell them apart.
  step head validation do_deploy_head
  step fingerprint-b infra do_fingerprint b
  step compare passthrough do_compare
  if [[ -n "$BOOTSTRAP" ]]; then
    echo janela > "$WD/compat"
    note "- compat: janela (bootstrap: não verificada)"
  else
    step compat infra do_compat
  fi
  log "clone validado: base $BASE, head $HEAD, compat $(cat "$WD/compat")"
}

cmd_teardown() {
  (( $# == 1 )) || usage
  WD="$1"
  validate_config
  require_cmd docker
  require_cmd git
  require_cmd flock
  [[ -d "$WD" ]] || { log "nada a remover: $WD não existe"; WD=""; }
  if [[ -n "$WD" ]]; then
    WD="$(cd "$WD" && pwd)"
    mkdir -p "$WD/logs"
  fi
  export_clone_env
  # A run still tearing itself down holds the lock; wait for it, but never forever.
  exec 8> "${TMPDIR:-/tmp}/$RUINA_CLONE_PROJECT.lock"
  flock -w 600 8 || log "aviso: lock de $RUINA_CLONE_PROJECT ocupado; removendo mesmo assim"
  if [[ -z "$WD" ]]; then
    remove_project || fatal "o projeto $RUINA_CLONE_PROJECT não foi removido por completo"
    return
  fi
  local rc=0
  set +e
  ( do_teardown ) >> "$WD/logs/teardown.log" 2>&1
  rc=$?
  set -e
  (( rc == 0 )) || fatal "teardown incompleto; veja $WD/logs/teardown.log"
  log "clone $RUINA_CLONE_PROJECT removido"
}

main() {
  case "${1:-}" in
    run) shift; cmd_run "$@" ;;
    teardown) shift; cmd_teardown "$@" ;;
    *) usage ;;
  esac
}

main "$@"
