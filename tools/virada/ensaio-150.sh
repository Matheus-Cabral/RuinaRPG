#!/usr/bin/env bash
# Stack de ensaio da 1.5.0 NESTA VM (nunca na produção), com o mesmo formato da produção, carregada do
# "pacote da 1.5.0" (pacote.sh), para ensaiar o runbook da virada ao pé da letra.
#
#   ensaio-150.sh preparar <dir-pacote> <dir-checkout> [--remover-stack-atual]
#   ensaio-150.sh desfazer <dir-checkout> [--remover-stack-atual]
#
# preparar:
#  1. confere o SHA256SUMS do pacote (tem de ser o completo, com as imagens docker);
#  2. se o projeto compose já existe (nesta VM, a stack de desenvolvimento 2.0.0 usa o nome ruinarpg),
#     recusa — a não ser com --remover-stack-atual, que a derruba COM OS VOLUMES (dados apagados);
#  3. cria em <dir-checkout> um git worktree da 1.5.0 (729dac9) e copia o .env deste repositório com
#     ASPNETCORE_ENVIRONMENT=Production (a api nunca migra);
#  4. carrega imagens-docker.tar e marca as imagens da api e do nginx com os nomes que o compose da 1.5.0
#     deriva para os serviços com build: (ex.: ruinarpg-api, ruinarpg-nginx), sem build nenhum;
#  5. sobe o db pelo docker-compose.yml da 1.5.0, restaura db.dump e images.tar, confere as contagens;
#  6. sobe api e nginx e confere o formato: só os serviços db, api, nginx (contêineres <projeto>-db-1,
#     -api-1, -nginx-1), nginx na porta, api em Production com as imagens do pacote, /api/health "OK",
#     o nginx servindo os bytes do volume e as contagens ainda iguais às de contagens.tsv;
#  7. imprime "OK ensaio <projeto> em <dir-checkout> (porta N)".
# O runbook então roda dentro de <dir-checkout> como na produção (docker compose -p ruinarpg ...,
# docker stop ruinarpg-nginx-1 ruinarpg-api-1, make ...). O nome do diretório de <dir-checkout> tem de
# ser o projeto (sem maiúsculas), como /home/ruina/RuinaRPG -> ruinarpg na produção: o Makefile da 1.5.0
# roda "docker compose" sem -p.
#
# desfazer: derruba o projeto (contêineres, volumes, redes — também os de uma 2.0.0 que o ensaio tenha
# instalado ali) e remove o worktree. Sem --remover-stack-atual só age quando <dir-checkout> é um worktree
# deste repositório com a marca .ensaio-150 que preparar grava (o projeto foi criado por ele) e nenhum
# contêiner do projeto vem de outro diretório; senão recusa (código 2) sem apagar nada — um diretório
# errado ou um segundo desfazer nunca apaga os volumes de outra stack. As imagens da 1.5.0 ficam.
#
# Ambiente: RUINA_ENSAIO_PROJECT (ruinarpg; ou ruinarpg-bats-*/ruinarpg-ensaio-* descartáveis),
# RUINA_ENSAIO_PORTA (80; outra porta vai num docker-compose.override.yml em 127.0.0.1),
# RUINA_ENSAIO_ENV (o .env de origem, padrão <repo>/.env), RUINA_HEALTH_TIMEOUT (180).
# Códigos: 0 ok; 1 verificação falhou ou erro; 2 uso/configuração/recusa.
set -euo pipefail
shopt -s inherit_errexit

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$here/../.." && pwd)"
SQL="$here/contagens.sql"
# shellcheck source=comum.sh
source "$here/comum.sh"
REV150="729dac9"
PROJ="${RUINA_ENSAIO_PROJECT:-ruinarpg}"
PORT="${RUINA_ENSAIO_PORTA:-80}"
ENV_SRC="${RUINA_ENSAIO_ENV:-$REPO/.env}"
: "${RUINA_HEALTH_TIMEOUT:=180}"
# Variáveis que o docker-compose.yml da 1.5.0 lê do .env.
ENV_KEYS=(POSTGRES_USER POSTGRES_PASSWORD JWT_SIGNING_KEY JWT_ISSUER JWT_AUDIENCE JWT_ACCESS_TOKEN_MINUTES
          JWT_REFRESH_TOKEN_DAYS CORS_ALLOWED_ORIGINS IMG_MAX_SIZE_MB INVITE_CODE_EXPIRATION_HOURS)

usage() {
  echo "uso: $0 preparar <dir-pacote> <dir-checkout> [--remover-stack-atual] | $0 desfazer <dir-checkout> [--remover-stack-atual]" >&2
  exit 2
}

(( $# >= 1 )) || usage
CMD="$1"; shift
REMOVER_ATUAL="" ARGS=()
for a in "$@"; do
  case "$a" in
    --remover-stack-atual) REMOVER_ATUAL=1 ;;
    -*) usage ;;
    *) ARGS+=("$a") ;;
  esac
done
case "$CMD" in
  preparar) (( ${#ARGS[@]} == 2 )) || usage; PKG="${ARGS[0]}" DIR="${ARGS[1]}" ;;
  desfazer) (( ${#ARGS[@]} == 1 )) || usage; DIR="${ARGS[0]}" ;;
  *) usage ;;
esac

# O projeto tem os volumes apagados por --remover-stack-atual e desfazer: só o nome real ou descartáveis,
# sem "_" (o prefixo "<projeto>_" dos volumes não pode casar com outro projeto).
[[ "$PROJ" == ruinarpg || "$PROJ" =~ ^ruinarpg-(bats|ensaio)[a-z0-9-]*$ ]] ||
  fatal "RUINA_ENSAIO_PROJECT deve ser ruinarpg, ruinarpg-bats-* ou ruinarpg-ensaio-*: $PROJ"
if ! [[ "$PORT" =~ ^[1-9][0-9]{0,4}$ ]] || (( PORT > 65535 )); then fatal "RUINA_ENSAIO_PORTA inválida: $PORT"; fi
[[ "$RUINA_HEALTH_TIMEOUT" =~ ^[1-9][0-9]*$ ]] || fatal "RUINA_HEALTH_TIMEOUT deve ser um inteiro >= 1"

# Caminho absoluto sem exigir que o diretório exista (o pai tem de existir).
parent="$(dirname "$DIR")"
[[ -d "$parent" ]] || fatal "diretório pai inexistente: $parent"
DIR="$(cd "$parent" && pwd)/$(basename "$DIR")"
[[ "$(basename "$DIR" | tr '[:upper:]' '[:lower:]')" == "$PROJ" ]] ||
  fatal "o diretório do checkout tem de se chamar como o projeto ($PROJ; ex.: .../RuinaRPG para ruinarpg): $DIR"

dc() { (cd "$DIR" && docker compose -p "$PROJ" "$@"); }

# Diretórios de trabalho de todos os contêineres do projeto (rótulo do compose), um por linha.
project_dirs() {
  local ids
  mapfile -t ids < <(docker ps -aq --filter "label=com.docker.compose.project=$PROJ")
  (( ${#ids[@]} == 0 )) ||
    docker inspect -f '{{index .Config.Labels "com.docker.compose.project.working_dir"}}' "${ids[@]}" | sort -u
}

MARCA=".ensaio-150"

is_worktree() {
  git -C "$REPO" worktree list --porcelain | grep -qxF "worktree $1"
}

# ================================================================ desfazer

if [[ "$CMD" == desfazer ]]; then
  [[ "$DIR" != "$REPO" ]] || fatal "recuso remover o checkout principal: $DIR"
  if [[ -e "$DIR" ]] && ! is_worktree "$DIR"; then fatal "$DIR não é um worktree deste repositório"; fi
  if [[ -z "$REMOVER_ATUAL" ]]; then
    # O projeto só é do ensaio se preparar criou este checkout (a marca tem o nome do projeto).
    if ! is_worktree "$DIR" || [[ "$(cat "$DIR/$MARCA" 2>/dev/null || true)" != "$PROJ" ]]; then
      fatal "$DIR não é um checkout de ensaio do projeto $PROJ criado por preparar (worktree com $MARCA); nada removido — passe --remover-stack-atual para remover o projeto $PROJ mesmo assim"
    fi
    outros="$(project_dirs | { grep -vxF "$DIR" || true; })"
    [[ -z "$outros" ]] ||
      fatal "o projeto $PROJ tem contêineres de outro diretório ($(echo "$outros" | tr '\n' ' ')); não é o ensaio — passe --remover-stack-atual para removê-lo mesmo assim"
  fi
  log "removendo o projeto $PROJ (contêineres, volumes, redes)"
  remove_project
  [[ -z "$(project_leftovers)" ]] || die "sobrou algo do projeto $PROJ"
  if is_worktree "$DIR"; then
    log "removendo o worktree $DIR"
    git -C "$REPO" worktree remove --force "$DIR" || die "não consegui remover o worktree $DIR (arquivos de root?)"
  fi
  git -C "$REPO" worktree prune
  log "ensaio desfeito; as imagens da 1.5.0 ficam (${PROJ}-api, ${PROJ}-nginx)"
  echo "OK desfeito $PROJ"
  exit 0
fi

# ================================================================ preparar

[[ -d "$PKG" ]] || fatal "pacote inexistente: $PKG"
PKG="$(cd "$PKG" && pwd)"
[[ -s "$SQL" ]] || fatal "contagens.sql não encontrado: $SQL"
[[ -f "$ENV_SRC" ]] || fatal ".env de origem não encontrado: $ENV_SRC"
for k in "${ENV_KEYS[@]}"; do
  grep -qE "^$k=" "$ENV_SRC" || fatal "o .env de origem não define $k (o compose da 1.5.0 precisa)"
done
[[ ! -e "$DIR" ]] || fatal "o checkout já existe: $DIR (rode $0 desfazer $DIR antes)"

# ---------------------------------------------------------------- 1. pacote

verify_package "$PKG"
[[ -n "$COMPLETO" ]] || fatal "o ensaio precisa do pacote completo (com imagens-docker.tar), não de um --so-banco"

# ---------------------------------------------------------------- 2. stack atual com o mesmo nome

if [[ -n "$(project_leftovers)" ]]; then
  [[ -n "$REMOVER_ATUAL" ]] ||
    fatal "já existe uma stack no projeto $PROJ; para apagá-la (contêineres E volumes) passe --remover-stack-atual"
  log "removendo a stack atual do projeto $PROJ, com os volumes (--remover-stack-atual)"
  remove_project
  [[ -z "$(project_leftovers)" ]] || die "sobrou algo do projeto $PROJ"
fi

# ---------------------------------------------------------------- 3. checkout da 1.5.0 e .env

log "criando o worktree da 1.5.0 ($REV150) em $DIR"
git -C "$REPO" worktree add -q --detach "$DIR" "$REV150" >&2
# Marca que este checkout e o projeto são do ensaio: desfazer sem --remover-stack-atual exige a marca.
echo "$PROJ" > "$DIR/$MARCA"
TMP=""
on_exit() {
  local rc=$?
  [[ -z "$TMP" ]] || rm -rf "$TMP"
  (( rc == 0 )) || log "preparar falhou (código $rc); stack e checkout ficam para inspeção — para limpar: $0 desfazer $DIR"
}
trap on_exit EXIT
trap 'exit 130' INT TERM HUP
(
  umask 077
  { grep -v '^ASPNETCORE_ENVIRONMENT=' "$ENV_SRC" || true; echo "ASPNETCORE_ENVIRONMENT=Production"; } > "$DIR/.env"
)
chmod 600 "$DIR/.env"
if (( PORT != 80 )); then
  printf '%s\n' "# Gerado por ensaio-150.sh: porta $PORT só para um ensaio descartável (a produção usa 80)." \
    'services:' '  nginx:' '    ports: !override' "      - \"127.0.0.1:$PORT:80\"" > "$DIR/docker-compose.override.yml"
fi

TMP="$(mktemp -d "$PKG/.ensaio.XXXXXX")"
chmod 700 "$TMP"
extract_dump "$PKG" "$TMP"

# ---------------------------------------------------------------- 4. imagens com os nomes do compose

load_images "$PKG"
dc config --images > "$TMP/imagens-compose"
for par in "api:$API_ID" "nginx:$NGINX_ID"; do
  svc="${par%%:*}" id="${par#*:}"
  # Compose v2 deriva <projeto>-<serviço> (compose antigo: <projeto>_<serviço>) para quem só tem build:.
  nome="$(grep -xE "${PROJ}[-_]${svc}" "$TMP/imagens-compose" || true)"
  [[ -n "$nome" && "$(wc -l <<<"$nome")" -eq 1 ]] || die "não achei o nome de imagem que o compose da 1.5.0 usa para $svc"
  docker tag "$id" "$nome:latest"
  log "imagem $nome:latest -> $id"
done

# ---------------------------------------------------------------- 5. banco e volume

docker image inspect "$PG_IMAGE" >/dev/null 2>&1 || docker pull -q "$PG_IMAGE" >/dev/null
log "subindo o db do projeto $PROJ pelo compose da 1.5.0"
dc up -d --no-build --wait db >&2 2>&1
DBUSER="$(dc exec -T db printenv POSTGRES_USER)"
DBNAME="$(dc exec -T db printenv POSTGRES_DB)"
restore_dump "$TMP/db.dump" "$TMP/images.tar" "$DBUSER" "$DBNAME"
check_counts "$PKG/contagens.tsv" "$DBUSER" "$DBNAME" "$TMP/contagens.tsv"

# ---------------------------------------------------------------- 6. api, nginx e formato

log "subindo api e nginx"
dc up -d --no-build >&2 2>&1
wait_health "$PORT" "$RUINA_HEALTH_TIMEOUT"
check_nginx_image "$PORT" "$TMP/images.tar" "$TMP"

log "conferindo o formato da stack"
esperado="$(printf '%s\n' "api ${PROJ}-api-1 running" "db ${PROJ}-db-1 running" "nginx ${PROJ}-nginx-1 running")"
atual="$(dc ps -a --format '{{.Service}} {{.Name}} {{.State}}' | LC_ALL=C sort)"
[[ "$atual" == "$esperado" ]] || die "serviços diferentes dos da produção: $(echo "$atual" | tr '\n' ';')"
[[ "$(docker ps -aq --filter "label=com.docker.compose.project=$PROJ" | wc -l)" -eq 3 ]] ||
  die "o projeto $PROJ tem contêineres além de db, api e nginx"
[[ "$(project_dirs)" == "$DIR" ]] || die "os contêineres não vêm do checkout $DIR"
docker port "${PROJ}-nginx-1" 80/tcp | grep -qE ":${PORT}\$" || die "o nginx não publica a porta $PORT"
docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' "${PROJ}-api-1" | grep -qx 'ASPNETCORE_ENVIRONMENT=Production' ||
  die "a api não está em Production"
[[ "$(docker inspect -f '{{.Image}}' "${PROJ}-api-1")" == "$API_ID" ]] || die "a api não roda a imagem do pacote"
[[ "$(docker inspect -f '{{.Image}}' "${PROJ}-nginx-1")" == "$NGINX_ID" ]] || die "o nginx não roda a imagem do pacote"
# A api da 1.5.0 já subiu: o banco tem de continuar exatamente como no pacote.
check_counts "$PKG/contagens.tsv" "$DBUSER" "$DBNAME" "$TMP/contagens.tsv"

echo "OK ensaio $PROJ em $DIR (porta $PORT)"
