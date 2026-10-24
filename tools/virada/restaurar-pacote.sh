#!/usr/bin/env bash
# Restauração de teste do "pacote da 1.5.0" (gerado por pacote.sh), NESTA VM (nunca na produção).
#
#   restaurar-pacote.sh <dir-pacote> [--projeto ruinarpg-pacote] [--porta 8091]
#   restaurar-pacote.sh --remover [--projeto ruinarpg-pacote]
#
# 1. confere o SHA256SUMS do pacote (antes de subir qualquer coisa);
# 2. sobe um projeto compose descartável com postgres:16-alpine e, num pacote completo, as imagens da
#    1.5.0 de imagens-docker.tar (api em Production — nunca migra — e nginx em 127.0.0.1:<porta>);
# 3. restaura db.dump e images.tar;
# 4. confere as contagens de todas as tabelas contra contagens.tsv (qualquer diferença: código 1 com a
#    lista das tabelas) e grava o fingerprint do banco restaurado (deploy/fingerprint.sh) em
#    <dir-pacote>/fingerprint.jsonl — as duas coisas antes de a api subir, então refletem só o pacote;
# 5. num pacote completo: a api responde /api/health pela porta e o nginx serve o primeiro arquivo do
#    volume de imagens com os mesmos bytes do pacote;
# 6. imprime "OK pacote <dir>" (num pacote --so-banco: "OK pacote <dir> (só banco: ...)").
# O projeto fica de pé para inspeção (falha ou sucesso); --remover o derruba com os volumes.
#
# Nunca toca a stack de desenvolvimento (projeto ruinarpg) nem a produção: só aceita projetos que
# começam com ruinarpg-pacote ou ruinarpg-bats, e recusa um projeto que já exista.
# Códigos: 0 ok; 1 verificação falhou (checksum, contagens, api, imagem) ou erro; 2 uso/configuração.
set -euo pipefail
shopt -s inherit_errexit

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$here/../.." && pwd)"
SQL="$here/contagens.sql"
# shellcheck source=comum.sh
source "$here/comum.sh"
: "${RUINA_HEALTH_TIMEOUT:=180}"

usage() { echo "uso: $0 <dir-pacote> [--projeto ruinarpg-pacote] [--porta 8091] | $0 --remover [--projeto ruinarpg-pacote]" >&2; exit 2; }

PROJ="ruinarpg-pacote" PORT=8091 DIR="" REMOVER=""
while (( $# )); do
  case "$1" in
    --projeto) (( $# >= 2 )) || usage; PROJ="$2"; shift 2 ;;
    --porta) (( $# >= 2 )) || usage; PORT="$2"; shift 2 ;;
    --remover) REMOVER=1; shift ;;
    -*) usage ;;
    *) [[ -z "$DIR" ]] || usage; DIR="$1"; shift ;;
  esac
done

# --remover apaga volumes deste projeto: nunca deixe o nome apontar para outra stack (sem "_", para o
# prefixo "<projeto>_" dos volumes não casar com outro projeto).
[[ "$PROJ" =~ ^ruinarpg-(pacote|bats)[a-z0-9-]*$ ]] ||
  fatal "--projeto deve começar com ruinarpg-pacote ou ruinarpg-bats (descartável): $PROJ"
if ! [[ "$PORT" =~ ^[1-9][0-9]{0,4}$ ]] || (( PORT > 65535 )); then fatal "--porta inválida: $PORT"; fi
[[ "$RUINA_HEALTH_TIMEOUT" =~ ^[1-9][0-9]*$ ]] || fatal "RUINA_HEALTH_TIMEOUT deve ser um inteiro >= 1"

if [[ -n "$REMOVER" ]]; then
  remove_project
  [[ -z "$(project_leftovers)" ]] || die "sobrou algo do projeto $PROJ"
  log "projeto $PROJ removido"
  exit 0
fi

[[ -n "$DIR" ]] || usage
[[ -d "$DIR" ]] || fatal "pacote inexistente: $DIR"
DIR="$(cd "$DIR" && pwd)"
[[ -s "$SQL" ]] || fatal "contagens.sql não encontrado: $SQL"
[[ -x "$REPO/deploy/fingerprint.sh" ]] || fatal "deploy/fingerprint.sh não encontrado em $REPO"

# ---------------------------------------------------------------- 1. checksum

verify_package "$DIR"

[[ -z "$(project_leftovers)" ]] || fatal "o projeto $PROJ já existe; rode $0 --remover --projeto $PROJ antes"

# ---------------------------------------------------------------- 2. projeto descartável

TMP="$(mktemp -d "$DIR/.restaurar.XXXXXX")"
chmod 700 "$TMP"
trap 'rm -rf "$TMP"' EXIT
trap 'exit 130' INT TERM HUP

extract_dump "$DIR" "$TMP"

API_ID="" NGINX_ID=""
[[ -z "$COMPLETO" ]] || load_images "$DIR"

docker image inspect "$PG_IMAGE" >/dev/null 2>&1 || docker pull -q "$PG_IMAGE" >/dev/null
DBPASS="$(head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n')"
JWTKEY="$(head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n')"
CF="$TMP/docker-compose.yml"
{
  cat <<EOF
# Gerado por restaurar-pacote.sh: a stack da 1.5.0 (serviços db, api, nginx) num projeto descartável.
services:
  db:
    image: $PG_IMAGE
    environment:
      POSTGRES_DB: ruinarpg
      POSTGRES_USER: ruinarpg
      POSTGRES_PASSWORD: $DBPASS
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -h 127.0.0.1 -U ruinarpg -d ruinarpg"]
      interval: 2s
      timeout: 5s
      retries: 60
EOF
  if [[ -n "$COMPLETO" ]]; then
    cat <<EOF
  api:
    image: $API_ID
    depends_on:
      db:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Postgres: "Host=db;Database=ruinarpg;Username=ruinarpg;Password=$DBPASS"
      Jwt__SigningKey: $JWTKEY
      Jwt__Issuer: RuinaRPG
      Jwt__Audience: RuinaRPG
      Jwt__AccessTokenMinutes: "15"
      Jwt__RefreshTokenDays: "30"
      Cors__AllowedOrigins: http://127.0.0.1:$PORT
      Storage__ImagesPath: /images
      Img__MaxSizeMb: "10"
      Invite__CodeExpirationHours: "48"
    volumes:
      - images:/images
  nginx:
    image: $NGINX_ID
    depends_on:
      - api
    ports:
      - "127.0.0.1:$PORT:80"
    volumes:
      - images:/usr/share/nginx/html/images:ro
EOF
  fi
  printf '%s\n' 'volumes:' '  pgdata:' '  images:'
} > "$CF"
dc() { docker compose -p "$PROJ" -f "$CF" "$@"; }

log "subindo o projeto $PROJ (db)"
dc create >&2 2>&1
dc up -d --wait db >&2 2>&1

# ---------------------------------------------------------------- 3. restauração

restore_dump "$TMP/db.dump" "$TMP/images.tar" ruinarpg ruinarpg

# ---------------------------------------------------------------- 4. contagens e fingerprint

check_counts "$DIR/contagens.tsv" ruinarpg ruinarpg "$TMP/contagens.tsv"

log "fingerprint"
RUINA_COMPOSE_FILE="$CF" RUINA_DB_USER=ruinarpg RUINA_DB_NAME=ruinarpg \
  "$REPO/deploy/fingerprint.sh" "$PROJ" "$TMP/fingerprint.jsonl" || die "fingerprint falhou"
chmod 600 "$TMP/fingerprint.jsonl"
mv "$TMP/fingerprint.jsonl" "$DIR/fingerprint.jsonl"
log "fingerprint em $DIR/fingerprint.jsonl ($(wc -l < "$DIR/fingerprint.jsonl") linhas)"

NIMG="$(tar -tf "$TMP/images.tar" | grep -cv '/$' || true)"
if [[ -z "$COMPLETO" ]]; then
  echo "OK pacote $DIR (só banco: api e nginx não verificados; $NIMG arquivos no volume de imagens)"
  exit 0
fi

# ---------------------------------------------------------------- 5. api e nginx

log "subindo api e nginx em 127.0.0.1:$PORT"
dc up -d >&2 2>&1
wait_health "$PORT" "$RUINA_HEALTH_TIMEOUT"
check_nginx_image "$PORT" "$TMP/images.tar" "$TMP"

echo "OK pacote $DIR"
