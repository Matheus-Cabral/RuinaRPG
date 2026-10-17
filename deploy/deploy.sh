#!/usr/bin/env bash
# Blue-green deploy of RuinaRPG (Técnico R0013–R0016).
#
#   deploy.sh deploy <sha>         zero-downtime: builds, migrates, starts the idle color, switches nginx
#   deploy.sh backup               pg_dump + images volume into $RUINA_BACKUP_DIR/<timestamp>-<sha7>
#   deploy.sh restore <dir>        loads a backup back (database and images)
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

# Set during a deploy, read by the EXIT trap (undo_on_failure).
OLD="" NEW="" OLD_TAG="" UNDO_ARMED="" UPSTREAM_SWITCHED="" CLIENT_SWITCHED=""

validate_config() {
  [[ "$RUINA_KEEP_BACKUPS" =~ ^[1-9][0-9]*$ ]] || die "RUINA_KEEP_BACKUPS deve ser um inteiro >= 1"
  local var
  for var in RUINA_KEEP_RELEASES RUINA_HEALTH_TIMEOUT RUINA_DRAIN_SECONDS; do
    [[ "${!var}" =~ ^[0-9]+$ ]] || die "$var deve ser um inteiro >= 0"
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
  local deadline=$((SECONDS + 30))
  until [[ "$(compose exec -T web wget -qO- -T 2 http://127.0.0.1/api/health 2>/dev/null)" == '"OK"' ]]; do
    (( SECONDS < deadline )) || die "nginx não respondeu após a troca"
    sleep 1
  done
}

point_upstream_at() {
  log "nginx -> api-$1"
  UPSTREAM_SWITCHED=1
  write_upstream "api-$1:8080"
  reload_web || die "nginx recusou a nova configuração"
  wait_web
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
# as one of the kept backups.
make_backup() {
  local label="inicial" dir partial saved_umask
  [[ -n "$CURRENT_SHA" ]] && label="${CURRENT_SHA:0:7}"
  # Dumps hold every account's password hash: owner-only, even when root's umask is 022.
  saved_umask="$(umask)"
  umask 077
  dir="$RUINA_BACKUP_DIR/$(date -u +%Y%m%dT%H%M%SZ)-$label"
  partial="$RUINA_BACKUP_DIR/.$(basename "$dir").partial"
  log "backup em $dir"
  mkdir -p "$RUINA_BACKUP_DIR"
  chmod 700 "$RUINA_BACKUP_DIR"
  rm -rf "$partial" && mkdir "$partial"
  start_db
  compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -Fc ruinarpg' </dev/null > "$partial/db.dump"
  # Before the first deploy the images volume does not exist yet (and creating it outside compose
  # would leave it unlabeled), so there is nothing to archive.
  if docker volume inspect "${RUINA_PROJECT}_images" >/dev/null 2>&1; then
    docker run --rm -v "${RUINA_PROJECT}_images:/images:ro" "$BUSYBOX" tar -C /images -cf - . > "$partial/images.tar"
  else
    : > "$partial/images.tar"
  fi
  printf '%s\n' "$CURRENT_SHA" > "$partial/sha"
  rm -rf "$dir" && mv "$partial" "$dir"
  umask "$saved_umask"
  prune_backups
  BACKUP_DIR="$dir"
}

# Names start with a UTC timestamp, so reverse name order is newest first; anything else in the
# directory is left alone.
prune_backups() {
  local name
  ls -1 "$RUINA_BACKUP_DIR" | { grep -E '^[0-9]{8}T[0-9]{6}Z-' || true; } | sort -r | tail -n +$((RUINA_KEEP_BACKUPS + 1)) |
    while IFS= read -r name; do
      log "removendo backup antigo $name"
      rm -rf "${RUINA_BACKUP_DIR:?}/$name"
    done
}

# ---------------------------------------------------------------- subcommands

cmd_backup() {
  acquire_lock
  make_backup
  echo "$BACKUP_DIR" >&3
}

cmd_restore() {
  local dir="${1:-}"
  [[ -f "$dir/db.dump" && -f "$dir/images.tar" ]] || die "backup inválido: $dir"
  acquire_lock
  start_db
  log "restaurando o banco de $dir"
  compose exec -T db sh -c 'pg_restore -U "$POSTGRES_USER" -d ruinarpg --clean --if-exists --no-owner' < "$dir/db.dump"
  log "restaurando as imagens de $dir"
  if [[ -s "$dir/images.tar" ]]; then
    docker run --rm -i -v "${RUINA_PROJECT}_images:/images" "$BUSYBOX" sh -c 'rm -rf /images/* && tar -C /images -xf -' < "$dir/images.tar"
  else
    docker run --rm -v "${RUINA_PROJECT}_images:/images" "$BUSYBOX" sh -c 'rm -rf /images/*'
  fi
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
  echo "RESULT mode=zero-downtime sha=$1 active=$2 backup=$LAST_BACKUP smoke=$3 rollback=none" >&3
}

# Puts back whatever this deploy already changed: the upstream, the client, the new color and its tag.
# The tag matters because the stopped color is the next rollback target (Task 9).
undo_switch() {
  if [[ -n "$UPSTREAM_SWITCHED" ]]; then
    if [[ "$OLD" != none ]]; then write_upstream "api-$OLD:8080"; else write_upstream "$MAINTENANCE_UPSTREAM"; fi
    reload_web || log "aviso: nginx não recarregou o upstream restaurado"
  fi
  if [[ -n "$CLIENT_SWITCHED" && "$OLD" != none ]]; then
    point_client_at "$CURRENT_SHA" || log "aviso: webroot/current não foi restaurado"
  fi
  compose stop "api-$NEW" || log "aviso: api-$NEW não parou"
  set_color_tag "$NEW" "$OLD_TAG"
  save_state
}

# EXIT trap: any failure between starting the new color and committing the switch (migrations,
# health check, nginx) leaves the old color serving.
undo_on_failure() {
  local rc=$?
  [[ -n "$UNDO_ARMED" && $rc -ne 0 ]] || return 0
  UNDO_ARMED=""
  log "deploy falhou: desfazendo a troca para api-$NEW"
  undo_switch
}

# Smoke failed after the switch: traffic goes back to the old color. Restoring the database is the
# rollback of Task 9; on a first deploy there is nothing to go back to, so the new color stays.
smoke_failed() {
  local sha="$1"
  log "smoke falhou em api-$NEW"
  UNDO_ARMED=""
  if [[ "$OLD" != none ]]; then
    undo_switch
  else
    ACTIVE="$NEW" CURRENT_SHA="$sha" MODE=zero-downtime
    save_state
  fi
  result "$sha" "$ACTIVE" failed
  exit 1
}

# Smoke passed: let in-flight requests on the old color finish, then stop it and record the switch.
# Pruning is housekeeping; its failure must not turn a finished deploy into a failed one.
finish_deploy() {
  local sha="$1"
  if [[ "$OLD" != none ]]; then
    log "drenando api-$OLD por ${RUINA_DRAIN_SECONDS}s"
    sleep "$RUINA_DRAIN_SECONDS"
    compose stop "api-$OLD"
  fi
  PREVIOUS_SHA="$CURRENT_SHA" PREVIOUS_COLOR="$OLD" CURRENT_SHA="$sha" ACTIVE="$NEW" MODE=zero-downtime
  save_state
  prune_releases || log "aviso: poda de releases do cliente falhou"
  prune_images || log "aviso: poda de imagens falhou"
  log "deploy concluído: api-$NEW servindo $sha"
}

cmd_deploy() {
  local sha
  [[ -n "${1:-}" ]] || die "uso: deploy.sh deploy <sha>"
  [[ "${2:-}" != --window ]] || die "--window ainda não implementado"
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
  OLD_TAG="$(color_tag "$NEW")"
  log "cor ativa: $OLD; nova cor: $NEW"
  set_color_tag "$NEW" "$sha"
  save_state

  start_db
  trap undo_on_failure EXIT
  UNDO_ARMED=1
  run_migrations "$NEW"
  start_color "$NEW"
  wait_healthy "$NEW" || die "health check falhou: api-$NEW não respondeu em ${RUINA_HEALTH_TIMEOUT}s; nada foi trocado"

  # Upstream first: if nginx refuses it, the client was never switched.
  point_upstream_at "$NEW"
  CLIENT_SWITCHED=1
  point_client_at "$sha"

  if ! run_smoke "$NEW" "$sha"; then smoke_failed "$sha"; fi
  UNDO_ARMED=""
  finish_deploy "$sha"
  result "$sha" "$NEW" ok
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
    backup) cmd_backup ;;
    restore) cmd_restore "$@" ;;
    status) cmd_status ;;
    migrate) cmd_migrate ;;
    exec-api) cmd_exec_api "$@" ;;
    *) die "uso: deploy.sh {deploy <sha>|backup|restore <dir>|status|migrate|exec-api <args…>}" ;;
  esac
}

# Sourcing (the bats unit tests do) only defines the functions.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then main "$@"; exit; fi
