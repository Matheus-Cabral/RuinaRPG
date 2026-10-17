#!/usr/bin/env bash
# Blue-green deploy of RuinaRPG (Técnico R0013–R0016).
#
#   deploy.sh deploy <sha>         zero-downtime: builds, migrates, starts the idle color, switches nginx
#   deploy.sh deploy <sha> --window maintenance window: nginx answers 503, backup, old color stopped, then
#                                  migrate and switch (for migrations the old release cannot run against)
#   deploy.sh rollback             back to the previous release (a window rollback restores its backup)
#   deploy.sh backup               pg_dump + images volume into $RUINA_BACKUP_DIR/<timestamp>-<sha7>
#   deploy.sh restore <dir>        loads a backup back (database in one transaction, then images); with an
#                                  active color it runs under maintenance: nginx answers 503 and the color
#                                  stops during the restore, then comes back (even if the restore failed).
#                                  No safety copy is taken: run `deploy.sh backup` first if needed
#   deploy.sh status               color state and the newest backups
#   deploy.sh migrate              migrations + seeders with the active color's image
#   deploy.sh exec-api <args…>     dotnet RuinaRPG.Api.dll <args…> inside the active color
#
# Configuration comes from the environment; the defaults are production's.
#
# Everything lives in functions and `main` is called from the very last line: `deploy` runs
# `git checkout`, which rewrites this file, and bash reads a script lazily — by the time main runs,
# the whole file has been parsed, so the run finishes on the version that started it.
set -euo pipefail
# Without this, a failing command inside $( ) is ignored and the substitution just returns its output.
shopt -s inherit_errexit

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib.sh
source "$here/lib.sh"

: "${RUINA_REPO_DIR:=$(cd "$here/.." && pwd)}"
: "${RUINA_PROJECT:=ruinarpg}"
: "${RUINA_ENV_FILE:=$RUINA_REPO_DIR/.env}"
# Not configurable: docker-compose.yml bind-mounts ./deploy/state/upstream.conf.
RUINA_STATE_DIR="$RUINA_REPO_DIR/deploy/state"
: "${RUINA_BACKUP_DIR:=/var/backups/ruinarpg}"
: "${RUINA_KEEP_BACKUPS:=5}"
: "${RUINA_KEEP_RELEASES:=3}"
: "${RUINA_HEALTH_TIMEOUT:=120}"
: "${RUINA_DRAIN_SECONDS:=30}"
: "${RUINA_SKIP_FETCH:=0}"
# Images are host-wide: the tests turn this off so they never prune another deploy's images.
: "${RUINA_PRUNE_IMAGES:=1}"
export RUINA_REPO_DIR RUINA_PROJECT RUINA_ENV_FILE RUINA_STATE_DIR

STATE_FILE="$RUINA_STATE_DIR/colors.env"
UPSTREAM_FILE="$RUINA_STATE_DIR/upstream.conf"
STATE_KEYS=(ACTIVE BLUE_TAG GREEN_TAG CURRENT_SHA PREVIOUS_SHA PREVIOUS_COLOR LAST_BACKUP WINDOW_BACKUP MODE)
# nginx's maintenance server (deploy/nginx/default.conf): a valid upstream before any color exists.
MAINTENANCE_UPSTREAM="127.0.0.1:8081"
BUSYBOX="busybox:1.36"

# Set during a deploy, read by the EXIT trap (undo_on_failure). OLD_STOPPED: a window stopped the old
# color, so undoing means restoring RUN_WINDOW_BACKUP, not just pointing nginx back.
OLD="" NEW="" OLD_TAG="" UNDO_ARMED="" UPSTREAM_SWITCHED="" CLIENT_SWITCHED="" OLD_STOPPED=""
RUN_MODE=zero-downtime RUN_WINDOW_BACKUP="" DEPLOY_SHA=""

# No leading zeros: bash arithmetic reads 010 as octal.
validate_config() {
  [[ "$RUINA_KEEP_BACKUPS" =~ ^[1-9][0-9]*$ ]] || die "RUINA_KEEP_BACKUPS deve ser um inteiro >= 1"
  local var
  for var in RUINA_KEEP_RELEASES RUINA_HEALTH_TIMEOUT RUINA_DRAIN_SECONDS; do
    [[ "${!var}" =~ ^(0|[1-9][0-9]*)$ ]] || die "$var deve ser um inteiro >= 0"
  done
}

# ---------------------------------------------------------------- state

# Both files must exist before the first compose call: compose reads colors.env as an env file, and a
# missing bind-mount source would be created by docker as a directory.
init_state() {
  mkdir -p "$RUINA_STATE_DIR"
  ACTIVE=none BLUE_TAG=none GREEN_TAG=none PREVIOUS_COLOR=none
  CURRENT_SHA="" PREVIOUS_SHA="" LAST_BACKUP="" WINDOW_BACKUP="" MODE=""
  if [[ -f "$STATE_FILE" ]]; then
    # shellcheck source=/dev/null
    source "$STATE_FILE"
  else
    save_state
  fi
  [[ -f "$UPSTREAM_FILE" ]] || write_upstream "$MAINTENANCE_UPSTREAM"
  give_state_to_owner
}

repo_owner() { stat -c %U "$RUINA_REPO_DIR"; }

# Run as root by the production gate, the files would end up root-owned and the owner's manual
# `make` path could no longer write them.
give_state_to_owner() {
  if (( EUID == 0 )); then
    chown -R "$(repo_owner):" "$RUINA_STATE_DIR"
  fi
}

# colors.env is only read, never bind-mounted, so an atomic replace is safe here.
save_state() {
  local key
  for key in "${STATE_KEYS[@]}"; do printf '%s=%s\n' "$key" "${!key}"; done > "$STATE_FILE.tmp"
  mv "$STATE_FILE.tmp" "$STATE_FILE"
  give_state_to_owner
}

# Rewritten in place (truncate + write), never mv: web bind-mounts this file and the mount pins the inode.
write_upstream() {
  printf 'upstream app { server %s; }\n' "$1" > "$UPSTREAM_FILE"
}

set_color_tag() {
  case "$1" in
    blue) BLUE_TAG="$2" ;;
    green) GREEN_TAG="$2" ;;
  esac
}

color_tag() { if [[ "$1" == blue ]]; then echo "$BLUE_TAG"; else echo "$GREEN_TAG"; fi; }

other_color() { if [[ "$1" == blue ]]; then echo green; else echo blue; fi; }

acquire_lock() {
  exec 9>"$RUINA_STATE_DIR/deploy.lock"
  flock -n 9 || die "outro deploy em andamento"
  # The state read at startup may predate a deploy that held the lock meanwhile.
  # shellcheck source=/dev/null
  source "$STATE_FILE"
  give_state_to_owner
}

# ---------------------------------------------------------------- git and images

# The checkout belongs to the repo owner; when the gate runs us as root, git must not leave root-owned files.
git_as_owner() {
  if (( EUID == 0 )); then
    sudo -H -u "$(repo_owner)" git -C "$RUINA_REPO_DIR" "$@"
  else
    git -C "$RUINA_REPO_DIR" "$@"
  fi
}

# Sets DEPLOY_SHA to the full sha, so image tags never depend on how the caller abbreviated it.
# Every step dies loudly: images are tagged with this sha and reused forever, so building the wrong
# tree under it would be permanent.
checkout_sha() {
  local full
  if [[ "$RUINA_SKIP_FETCH" != 1 ]]; then
    git_as_owner fetch --quiet origin || die "git fetch falhou"
  fi
  full="$(git_as_owner rev-parse --verify --quiet "$1^{commit}")" || die "commit desconhecido: $1"
  git_as_owner checkout --quiet --detach "$full" || die "checkout falhou: $full"
  [[ "$(git_as_owner rev-parse HEAD)" == "$full" ]] || die "checkout falhou: HEAD não é $full"
  DEPLOY_SHA="$full"
}

# A tag that already exists is reused: images are immutable per sha, and rebuilding takes minutes.
build_image() {
  local image="$1" dockerfile="$2" sha="$3"; shift 3
  if docker image inspect "$image:$sha" >/dev/null 2>&1; then
    log "imagem $image:$sha já existe, build pulado"
    return
  fi
  log "build de $image:$sha"
  docker build -q -f "$RUINA_REPO_DIR/$dockerfile" "$@" -t "$image:$sha" "$RUINA_REPO_DIR"
}

build_images() {
  build_image ruinarpg-api src/RuinaRPG.Api/Dockerfile "$1" --build-arg "GIT_SHA=$1"
  build_image ruinarpg-client infra/client/Dockerfile "$1" --build-arg "GIT_SHA=$1"
  build_image ruinarpg-smoke tools/RuinaRPG.Smoke/Dockerfile "$1"
}

# A release directory is never replaced once it exists: it may be the one `current` points at.
publish_client() {
  log "publicando o cliente $1 em webroot/releases"
  CLIENT_TAG="$1" compose run --rm --no-deps client-build sh -c "
    mkdir -p /webroot/releases && cd /webroot/releases &&
    if [ ! -d '$1' ]; then rm -rf '$1.tmp' && cp -r /app/wwwroot '$1.tmp' && mv '$1.tmp' '$1'; fi"
}

# ---------------------------------------------------------------- database and colors

start_db() { compose up -d --wait db; }

# Runs with the target image while the old color still serves: migrations must stay backward
# compatible with the previous release (Técnico R0016).
run_migrations() {
  log "migrations e seeders com api-$1"
  compose run --rm --no-deps "api-$1" --migrate
}

start_color() {
  log "subindo api-$1"
  compose up -d --no-deps "api-$1"
}

# Asked from a throwaway container on the project network: the API image has no wget/curl, and the
# color may not be reachable through nginx yet.
wait_healthy() {
  local color="$1" deadline=$((SECONDS + RUINA_HEALTH_TIMEOUT)) body
  log "health check de api-$color (até ${RUINA_HEALTH_TIMEOUT}s)"
  while (( SECONDS < deadline )); do
    body="$(docker run --rm --network "${RUINA_PROJECT}_default" "$BUSYBOX" \
      wget -qO- -T 2 "http://api-$color:8080/health" 2>/dev/null || true)"
    [[ "$body" == '"OK"' ]] && return 0
    sleep 2
  done
  return 1
}

# ---------------------------------------------------------------- switching traffic

point_client_at() {
  log "webroot/current -> releases/$1"
  CLIENT_TAG="$1" compose run --rm --no-deps client-build sh -c \
    "ln -sfn 'releases/$1' /webroot/current.tmp && mv -Tf /webroot/current.tmp /webroot/current"
}

web_is_running() { [[ -n "$(compose ps -q --status running web)" ]]; }

# nginx resolves the upstream host when it loads the config, so this is only ever called with a color
# that is already up and healthy. nginx -t catches a bad upstream before the reload would.
reload_web() {
  if ! web_is_running; then
    compose up -d web
  else
    compose exec -T web nginx -t -q && compose exec -T web nginx -s reload
  fi
}

# The reload is asynchronous; wait until a request through nginx reaches an API again.
wait_web() {
  local deadline=$((SECONDS + 30)) body
  while (( SECONDS < deadline )); do
    body="$(compose exec -T web wget -qO- -T 2 http://127.0.0.1/api/health 2>/dev/null || true)"
    [[ "$body" == '"OK"' ]] && return 0
    sleep 1
  done
  return 1
}

# Returns instead of dying: the rollback paths call it and must report, not abort.
point_upstream_at() {
  log "nginx -> api-$1"
  UPSTREAM_SWITCHED=1
  write_upstream "api-$1:8080"
  reload_web || { log "ERRO: nginx recusou a nova configuração"; return 1; }
  wait_web || { log "ERRO: nginx não respondeu após a troca para api-$1"; return 1; }
}

# The maintenance server answers 503 with X-Ruina-Manutencao; the client retries on it.
enter_maintenance() {
  log "nginx -> manutenção"
  UPSTREAM_SWITCHED=1
  write_upstream "$MAINTENANCE_UPSTREAM"
  reload_web
}

run_smoke() {
  local color="$1" sha="$2"
  log "smoke contra api-$color"
  if [[ -n "${RUINA_SMOKE_CMD:-}" ]]; then
    RUINA_COLOR="$color" bash -c "$RUINA_SMOKE_CMD"
  else
    SMOKE_TAG="$sha" SMOKE_BASE_URL="http://api-$color:8080" compose run --rm --no-deps smoke
  fi
}

# ---------------------------------------------------------------- pruning

# Reads names newest first and prints those to remove: everything beyond the newest $1, except the
# current ($2) and previous ($3) releases, which a rollback needs.
select_prunable() {
  local keep="$1" cur="$2" prev="$3" n=0 name
  while IFS= read -r name; do
    [[ -n "$name" && "$name" != "<none>" ]] || continue
    n=$((n + 1))
    (( n <= keep )) && continue
    [[ "$name" == "$cur" || "$name" == "$prev" ]] && continue
    printf '%s\n' "$name"
  done
}

prune_releases() {
  local stale
  stale="$(CLIENT_TAG="$CURRENT_SHA" compose run --rm -T --no-deps client-build ls -1t /webroot/releases |
    select_prunable "$RUINA_KEEP_RELEASES" "$CURRENT_SHA" "$PREVIOUS_SHA")"
  [[ -n "$stale" ]] || return 0
  log "removendo releases antigas do cliente: $(echo $stale)"
  # shellcheck disable=SC2086 # names are shas, one word each
  CLIENT_TAG="$CURRENT_SHA" compose run --rm --no-deps client-build sh -c 'cd /webroot/releases && rm -rf -- "$@"' sh $stale
}

# Same rule for images; `docker image ls` lists newest first.
prune_images() {
  local tag
  [[ "$RUINA_PRUNE_IMAGES" == 1 ]] || return 0
  for tag in $(docker image ls ruinarpg-api --format '{{.Tag}}' |
               select_prunable "$RUINA_KEEP_RELEASES" "$CURRENT_SHA" "$PREVIOUS_SHA"); do
    log "removendo imagens de $tag"
    docker rmi "ruinarpg-api:$tag" "ruinarpg-client:$tag" "ruinarpg-smoke:$tag" >/dev/null 2>&1 || true
  done
}

# ---------------------------------------------------------------- backup / restore

# Writes into a hidden .partial directory and renames it at the end, so a failed backup never counts
# as one of the kept backups. Every step returns: a rollback calls this under ||, where errexit is off.
make_backup() {
  local label="inicial" dir partial
  [[ -n "$CURRENT_SHA" ]] && label="${CURRENT_SHA:0:7}"
  dir="$RUINA_BACKUP_DIR/$(date -u +%Y%m%dT%H%M%SZ)-$label"
  # Never reuse a name: it could be the protected window backup.
  while [[ -e "$dir" ]]; do sleep 1; dir="$RUINA_BACKUP_DIR/$(date -u +%Y%m%dT%H%M%SZ)-$label"; done
  partial="$RUINA_BACKUP_DIR/.$(basename "$dir").partial"
  log "backup em $dir"
  # Dumps hold every account's password hash: owner-only, even when root's umask is 022. The subshell
  # scopes the umask.
  (
    umask 077
    mkdir -p "$RUINA_BACKUP_DIR" && chmod 700 "$RUINA_BACKUP_DIR" && rm -rf "$partial" && mkdir "$partial" || exit 1
    start_db || exit 1
    compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -Fc ruinarpg' </dev/null > "$partial/db.dump" || exit 1
    # Before the first deploy the images volume does not exist yet (and creating it outside compose
    # would leave it unlabeled), so there is nothing to archive.
    if docker volume inspect "${RUINA_PROJECT}_images" >/dev/null 2>&1; then
      docker run --rm -v "${RUINA_PROJECT}_images:/images:ro" "$BUSYBOX" tar -C /images -cf - . > "$partial/images.tar" || exit 1
    else
      : > "$partial/images.tar" || exit 1
    fi
    printf '%s\n' "$CURRENT_SHA" > "$partial/sha" && mv "$partial" "$dir"
  ) || { log "ERRO: backup falhou: $dir"; return 1; }
  prune_backups || log "aviso: poda de backups falhou"
  BACKUP_DIR="$dir"
}

# Keeps the newest $RUINA_KEEP_BACKUPS plus the restore points the state still needs (the window
# backup, the last deploy's backup, the running deploy's window backup), which never count. Names
# start with a UTC timestamp, so reverse name order is newest first; anything else is left alone.
prune_backups() {
  local name n=0 protected=" "
  for name in "$WINDOW_BACKUP" "$LAST_BACKUP" "$RUN_WINDOW_BACKUP"; do
    [[ -n "$name" ]] && protected+="$(basename "$name") "
  done
  while IFS= read -r name; do
    [[ "$protected" == *" $name "* ]] && continue
    n=$((n + 1))
    (( n <= RUINA_KEEP_BACKUPS )) && continue
    log "removendo backup antigo $name"
    rm -rf "${RUINA_BACKUP_DIR:?}/$name"
  done < <(ls -1 "$RUINA_BACKUP_DIR" | { grep -E '^[0-9]{8}T[0-9]{6}Z-' || true; } | sort -r)
}

# ---------------------------------------------------------------- subcommands

cmd_backup() {
  acquire_lock
  make_backup
  echo "$BACKUP_DIR" >&3
}

# The schema is dropped first: --clean alone only drops what the dump contains, so tables a newer migration
# created would survive and break the next migrate. Every step returns: rollbacks call this under ||.
restore_backup() {
  local dir="$1"
  [[ -f "$dir/db.dump" && -f "$dir/images.tar" ]] || { log "ERRO: backup inválido: $dir"; return 1; }
  start_db || return 1
  log "restaurando o banco de $dir"
  # One transaction: the dump is first turned into SQL (a corrupt dump fails here, before anything is
  # touched), then schema drop + load commit together or not at all. psql's -1 only covers -c/-f
  # actions, never a script on stdin. The SQL file is removed whatever happens.
  compose exec -T db sh -c 'f=/tmp/ruina-restore.sql; rm -f "$f"
    pg_restore --clean --if-exists --no-owner -f "$f" &&
      psql -q -1 -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d ruinarpg \
        -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;" -f "$f" >/dev/null
    rc=$?; rm -f "$f"; exit $rc' < "$dir/db.dump" || return 1
  log "restaurando as imagens de $dir"
  if [[ -s "$dir/images.tar" ]]; then
    docker run --rm -i -v "${RUINA_PROJECT}_images:/images" "$BUSYBOX" sh -c 'rm -rf /images/* && tar -C /images -xf -' < "$dir/images.tar" || return 1
  else
    docker run --rm -v "${RUINA_PROJECT}_images:/images" "$BUSYBOX" sh -c 'rm -rf /images/*' || return 1
  fi
}

# Under maintenance with the active color stopped, so nothing writes while the data is replaced. The
# color comes back either way: a failed restore leaves the previous data (one transaction).
cmd_restore() {
  local dir="${1:-}" ok=1
  [[ -f "$dir/db.dump" && -f "$dir/images.tar" ]] || die "backup inválido: $dir"
  acquire_lock
  if [[ "$ACTIVE" == none ]]; then
    restore_backup "$dir" || die "restauração falhou"
    log "restauração concluída"
    return
  fi
  enter_maintenance || die "nginx recusou a configuração de manutenção; nada foi alterado"
  compose stop "api-$ACTIVE" || log "aviso: api-$ACTIVE não parou"
  restore_backup "$dir" || ok=""
  { start_color "$ACTIVE" && wait_healthy "$ACTIVE" && point_upstream_at "$ACTIVE"; } ||
    die "api-$ACTIVE não voltou após a restauração; o nginx está em manutenção. Rode deploy.sh status"
  [[ -n "$ok" ]] || die "restauração falhou; os dados anteriores foram mantidos e api-$ACTIVE voltou"
  log "restauração concluída"
}

cmd_status() {
  {
    echo "== $STATE_FILE"
    cat "$STATE_FILE"
    echo "== backups recentes em $RUINA_BACKUP_DIR"
    ls -1 "$RUINA_BACKUP_DIR" 2>/dev/null | grep -E '^[0-9]{8}T[0-9]{6}Z-' | sort -r | head -5 || true
  } >&3
}

cmd_migrate() {
  [[ "$ACTIVE" != none ]] || die "nenhuma cor ativa: faça o primeiro deploy"
  acquire_lock
  start_db
  run_migrations "$ACTIVE"
}

cmd_exec_api() {
  [[ "$ACTIVE" != none ]] || die "nenhuma cor ativa: faça o primeiro deploy"
  compose exec -T "api-$ACTIVE" dotnet RuinaRPG.Api.dll "$@" >&3
}

result() {
  echo "RESULT mode=$RUN_MODE sha=$1 active=$2 backup=$LAST_BACKUP smoke=$3 rollback=$4" >&3
}

# ---------------------------------------------------------------- rollback

# Zero-downtime undo, while the old color is still running: puts back the upstream and the client and
# stops the new color. Fails (after doing everything it can) if nginx does not serve the old color again.
undo_switch() {
  local rc=0
  if [[ -n "$UPSTREAM_SWITCHED" ]]; then
    if [[ "$OLD" != none ]]; then write_upstream "api-$OLD:8080"; else write_upstream "$MAINTENANCE_UPSTREAM"; fi
    if ! reload_web; then
      log "ERRO: nginx não recarregou o upstream restaurado"; rc=1
    elif [[ "$OLD" != none ]] && ! wait_web; then
      log "ERRO: o nginx não está servindo api-$OLD após desfazer a troca"; rc=1
    fi
  fi
  if [[ -n "$CLIENT_SWITCHED" && "$OLD" != none ]]; then
    point_client_at "$CURRENT_SHA" || { log "ERRO: webroot/current não foi restaurado"; rc=1; }
  fi
  compose stop "api-$NEW" || log "aviso: api-$NEW não parou"
  return "$rc"
}

# Brings api-$1 (release $2) back in front and stops api-$3. With a backup ($4, window mode) the
# database goes back to it under maintenance first; without one nothing is restored, so rows written
# since the switch survive. Every step returns: this runs under || (and from the EXIT trap).
# Before restoring, the data about to be discarded is copied. $5=required (on demand): if the copy
# fails, return 3 with nothing restored. Otherwise (a failing deploy) the copy is best effort and
# never blocks the restore.
return_to() {
  local color="$1" sha="$2" from="$3" backup="${4:-}" copy="${5:-best-effort}"
  log "voltando para api-$color ($sha)"
  if [[ -n "$backup" ]]; then
    enter_maintenance || { log "ERRO: nginx recusou a configuração de manutenção"; return 1; }
    compose stop "api-$from" || log "aviso: api-$from não parou"
    if make_backup; then
      log "dados descartados pelo rollback salvos em $BACKUP_DIR"
    elif [[ "$copy" == required ]]; then
      return 3
    else
      log "aviso: cópia de segurança falhou; restaurando mesmo assim"
    fi
    restore_backup "$backup" || return 1
  fi
  start_color "$color" || return 1
  wait_healthy "$color" || { log "ERRO: api-$color não respondeu ao health check"; return 1; }
  point_upstream_at "$color" || return 1
  point_client_at "$sha" || { log "ERRO: webroot/current não aponta para $sha"; return 1; }
  compose stop "api-$from" || log "aviso: api-$from não parou"
}

# Nothing was restored yet: put api-$1 back in front instead of leaving the site in maintenance.
safety_copy_failed() {
  local from="$1"
  log "ERRO: a cópia dos dados que o rollback descartaria falhou; voltando para api-$from"
  { start_color "$from" && wait_healthy "$from" && point_upstream_at "$from"; } ||
    rollback_failed "$CURRENT_SHA" none
  die "cópia de segurança falhou; nada foi alterado"
}

# A zero-downtime rollback never stops api-$2 before the switch succeeds, so it can keep serving.
zero_downtime_rollback_failed() {
  local back="$1" from="$2"
  if [[ -n "$UPSTREAM_SWITCHED" ]] && ! { point_upstream_at "$from" && point_client_at "$CURRENT_SHA"; }; then
    rollback_failed "$CURRENT_SHA" none
  fi
  compose stop "api-$back" || log "aviso: api-$back não parou"
  log "ERRO: rollback falhou; api-$from continua servindo"
  result "$CURRENT_SHA" "$ACTIVE" none failed
  exit 2
}

rollback_failed() {
  log "ERRO: rollback falhou; o nginx pode estar em manutenção. Rode deploy.sh status e restaure manualmente (deploy.sh restore <backup>)."
  result "$1" "$ACTIVE" "$2" failed
  exit 2
}

# Takes this deploy back. Before the old color was stopped, undoing the switch is enough; after (window),
# the database goes back to the backup taken inside the window. The new color's tag goes back too: the
# stopped color is the next rollback target.
roll_back() {
  local sha="$1" smoke="$2" ok=1
  if [[ -n "$OLD_STOPPED" ]]; then
    return_to "$OLD" "$CURRENT_SHA" "$NEW" "$RUN_WINDOW_BACKUP" || ok=""
  else
    undo_switch || ok=""
  fi
  set_color_tag "$NEW" "$OLD_TAG"
  save_state
  [[ -n "$ok" ]] || rollback_failed "$sha" "$smoke"
  log "rollback concluído: api-$OLD servindo $CURRENT_SHA"
  result "$sha" "$OLD" "$smoke" ok
  exit 1
}

# EXIT trap: any failure between arming and committing the switch (migrations, health check, nginx)
# puts the old release back.
undo_on_failure() {
  local rc=$?
  [[ -n "$UNDO_ARMED" && $rc -ne 0 ]] || return 0
  UNDO_ARMED=""
  log "deploy falhou: desfazendo a troca para api-$NEW"
  roll_back "$DEPLOY_SHA" none
}

# Captures what the undo needs before anything changes, so no failure can leave the stopped color's
# tag at the new sha.
arm_undo() {
  OLD_TAG="$(color_tag "$NEW")"
  trap undo_on_failure EXIT
  UNDO_ARMED=1
}

# Smoke failed after the switch: back to the old release. On a first deploy there is nothing to go
# back to, so the new color stays.
smoke_failed() {
  local sha="$1"
  log "smoke falhou em api-$NEW"
  UNDO_ARMED=""
  [[ "$OLD" != none ]] || {
    ACTIVE="$NEW" CURRENT_SHA="$sha" MODE="$RUN_MODE"
    save_state
    result "$sha" "$ACTIVE" failed none
    exit 1
  }
  roll_back "$sha" failed
}

# ---------------------------------------------------------------- deploy

# Window mode, before migrating: nginx answers 503, the old color stops so it never sees the migrated
# schema, and the backup taken then is the restore point of a window rollback.
open_window() {
  enter_maintenance || die "nginx recusou a configuração de manutenção"
  # Stopped before the backup, so nothing writes after it is taken.
  log "parando api-$OLD"
  OLD_STOPPED=1
  compose stop "api-$OLD"
  make_backup
  RUN_WINDOW_BACKUP="$BACKUP_DIR" LAST_BACKUP="$BACKUP_DIR"
}

# A window needs an old color to stop and an nginx to answer 503; without them it is a first deploy.
choose_mode() {
  RUN_MODE="$1"
  if [[ "$RUN_MODE" == window ]] && { [[ "$OLD" == none ]] || ! web_is_running; }; then
    log "sem cor ativa atrás do nginx: deploy sem janela"
    RUN_MODE=zero-downtime
  fi
}

# Smoke passed: in zero-downtime mode let in-flight requests on the old color finish, then stop it.
# nginx and the client already serve the new color, so nothing after this may abort before save_state.
# Pruning is housekeeping; its failure must not turn a finished deploy into a failed one.
finish_deploy() {
  local sha="$1"
  if [[ "$OLD" != none && -z "$OLD_STOPPED" ]]; then
    log "drenando api-$OLD por ${RUINA_DRAIN_SECONDS}s"
    sleep "$RUINA_DRAIN_SECONDS"
    compose stop "api-$OLD" || log "aviso: api-$OLD não parou; pare manualmente"
  fi
  PREVIOUS_SHA="$CURRENT_SHA" PREVIOUS_COLOR="$OLD" CURRENT_SHA="$sha" ACTIVE="$NEW"
  MODE="$RUN_MODE" WINDOW_BACKUP="$RUN_WINDOW_BACKUP"
  save_state
  prune_releases || log "aviso: poda de releases do cliente falhou"
  prune_images || log "aviso: poda de imagens falhou"
  log "deploy concluído: api-$NEW servindo $sha"
}

cmd_deploy() {
  local sha mode=zero-downtime
  [[ -n "${1:-}" ]] || die "uso: deploy.sh deploy <sha> [--window]"
  case "${2:-}" in
    "") ;;
    --window) mode=window ;;
    *) die "opção desconhecida: $2" ;;
  esac
  acquire_lock
  checkout_sha "$1"
  sha="$DEPLOY_SHA"

  make_backup
  LAST_BACKUP="$BACKUP_DIR"
  save_state

  build_images "$sha"
  publish_client "$sha"

  OLD="$ACTIVE"
  NEW="$(other_color "$OLD")"
  choose_mode "$mode"
  log "modo $RUN_MODE; cor ativa: $OLD; nova cor: $NEW"
  arm_undo
  set_color_tag "$NEW" "$sha"
  save_state

  start_db
  [[ "$RUN_MODE" != window ]] || open_window
  run_migrations "$NEW"
  start_color "$NEW"
  wait_healthy "$NEW" || die "health check falhou: api-$NEW não respondeu em ${RUINA_HEALTH_TIMEOUT}s"

  # Upstream first: if nginx refuses it, the client was never switched.
  point_upstream_at "$NEW"
  CLIENT_SWITCHED=1
  point_client_at "$sha"

  if ! run_smoke "$NEW" "$sha"; then smoke_failed "$sha"; fi
  UNDO_ARMED=""
  finish_deploy "$sha"
  result "$sha" "$NEW" ok none
}

# On demand, from the state: back to PREVIOUS_SHA on PREVIOUS_COLOR. A window deploy is rolled back
# with its window backup (rows written since are lost); a zero-downtime one restores nothing. One level
# only: afterwards there is no previous release to go back to.
cmd_rollback() {
  acquire_lock
  [[ "$ACTIVE" != none && "$PREVIOUS_COLOR" != none && -n "$PREVIOUS_SHA" ]] || die "não há deploy anterior para voltar"
  local back="$PREVIOUS_COLOR" from="$ACTIVE" sha="$PREVIOUS_SHA" backup=""
  RUN_MODE="${MODE:-zero-downtime}"
  if [[ "$RUN_MODE" == window ]]; then
    backup="$WINDOW_BACKUP"
    [[ -n "$backup" ]] || die "o deploy em janela não registrou o backup da janela; nada foi alterado"
    [[ -f "$backup/db.dump" && -f "$backup/images.tar" ]] || die "backup da janela não existe mais: $backup; nada foi alterado"
  fi
  log "rollback ($RUN_MODE): api-$from -> api-$back ($sha)"
  # The stopped color's tag may be stale; compose starts it with whatever colors.env says.
  set_color_tag "$back" "$sha"
  save_state
  start_db
  local rc=0
  return_to "$back" "$sha" "$from" "$backup" required || rc=$?
  if (( rc == 3 )); then safety_copy_failed "$from"; fi
  if (( rc != 0 )); then
    [[ -z "$backup" ]] || rollback_failed "$sha" none
    zero_downtime_rollback_failed "$back" "$from"
  fi
  ACTIVE="$back" CURRENT_SHA="$sha" PREVIOUS_SHA="" PREVIOUS_COLOR=none MODE="" WINDOW_BACKUP=""
  save_state
  log "rollback concluído: api-$back servindo $sha"
  result "$sha" "$back" none ok
}

main() {
  local cmd="${1:-}"
  (( $# )) && shift
  # Results (RESULT line, backup dir, status) go to fd 3 = the real stdout; everything else, including
  # docker's own output, goes to stderr so the RESULT line stays the last thing printed.
  exec 3>&1 1>&2
  require_cmd docker
  require_cmd flock
  if (( EUID == 0 )); then require_cmd sudo; fi
  validate_config
  init_state
  case "$cmd" in
    deploy) cmd_deploy "$@" ;;
    rollback) cmd_rollback ;;
    backup) cmd_backup ;;
    restore) cmd_restore "$@" ;;
    status) cmd_status ;;
    migrate) cmd_migrate ;;
    exec-api) cmd_exec_api "$@" ;;
    *) die "uso: deploy.sh {deploy <sha> [--window]|rollback|backup|restore <dir> (em manutenção, sem cópia de segurança)|status|migrate|exec-api <args…>}" ;;
  esac
}

# Sourcing (the bats unit tests do) only defines the functions.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then main "$@"; exit; fi
