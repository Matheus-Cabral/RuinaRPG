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
PG_IMAGE="postgres:16-alpine"
: "${RUINA_HEALTH_TIMEOUT:=180}"

log() { printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2; }
die() { log "ERRO: $*"; exit 1; }
fatal() { log "ERRO: $*"; exit 2; }
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

project_leftovers() {
  docker ps -aq --filter "label=com.docker.compose.project=$PROJ"
  docker volume ls -q --filter "label=com.docker.compose.project=$PROJ"
  docker volume ls -q | { grep -E "^${PROJ}_" || true; }
  docker network ls -q --filter "label=com.docker.compose.project=$PROJ"
}

# Pelos rótulos e pelo prefixo dos volumes: não depende do arquivo compose (temporário) da restauração.
remove_project() {
  local ids
  mapfile -t ids < <(docker ps -aq --filter "label=com.docker.compose.project=$PROJ")
  (( ${#ids[@]} == 0 )) || docker rm -f "${ids[@]}" >/dev/null
  mapfile -t ids < <({ docker volume ls -q --filter "label=com.docker.compose.project=$PROJ"
                       docker volume ls -q | { grep -E "^${PROJ}_" || true; }; } | sort -u)
  (( ${#ids[@]} == 0 )) || docker volume rm -f "${ids[@]}" >/dev/null
  mapfile -t ids < <(docker network ls -q --filter "label=com.docker.compose.project=$PROJ")
  (( ${#ids[@]} == 0 )) || docker network rm "${ids[@]}" >/dev/null
}

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

[[ -f "$DIR/SHA256SUMS" ]] || die "pacote sem SHA256SUMS: $DIR"
for f in dump.tar contagens.tsv; do
  awk '{print $2}' "$DIR/SHA256SUMS" | grep -qx "$f" || die "SHA256SUMS não cobre $f"
done
COMPLETO=""
if awk '{print $2}' "$DIR/SHA256SUMS" | grep -qx imagens-docker.tar; then
  awk '{print $2}' "$DIR/SHA256SUMS" | grep -qx imagens.tsv || die "SHA256SUMS cobre imagens-docker.tar mas não imagens.tsv"
  COMPLETO=1
fi
log "conferindo SHA256SUMS de $DIR"
if ! out="$(cd "$DIR" && sha256sum -c --strict SHA256SUMS 2>&1)"; then
  printf '%s\n' "$out" | grep -v ': OK$' >&2 || true
  die "SHA256SUMS não confere: pacote corrompido ou incompleto"
fi

[[ -z "$(project_leftovers)" ]] || fatal "o projeto $PROJ já existe; rode $0 --remover --projeto $PROJ antes"

# ---------------------------------------------------------------- 2. projeto descartável

TMP="$(mktemp -d "$DIR/.restaurar.XXXXXX")"
chmod 700 "$TMP"
trap 'rm -rf "$TMP"' EXIT
trap 'exit 130' INT TERM HUP

tar -C "$TMP" -xf "$DIR/dump.tar" db.dump images.tar sha || die "dump.tar não tem db.dump, images.tar e sha"
[[ "$(head -c 5 "$TMP/db.dump")" == PGDMP ]] || die "db.dump não é um pg_dump -Fc"

API_ID="" NGINX_ID=""
if [[ -n "$COMPLETO" ]]; then
  log "carregando imagens-docker.tar"
  docker load -q -i "$DIR/imagens-docker.tar" >&2
  # imagens.tsv: serviço<TAB>nome<TAB>id. A api é a linha 2 e o nginx a 3 (db, api, nginx), qualquer que
  # seja o nome do serviço; o id carregado tem de bater.
  API_ID="$(awk -F'\t' 'NR==2{print $3}' "$DIR/imagens.tsv")"
  NGINX_ID="$(awk -F'\t' 'NR==3{print $3}' "$DIR/imagens.tsv")"
  for id in "$API_ID" "$NGINX_ID"; do
    [[ "$id" =~ ^sha256:[0-9a-f]{64}$ ]] || die "imagens.tsv inválido: '$id'"
    docker image inspect "$id" >/dev/null 2>&1 || die "imagem $id não veio em imagens-docker.tar"
  done
fi

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

log "restaurando db.dump"
dc exec -T db sh -c 'exec pg_restore --no-owner --no-privileges --exit-on-error --single-transaction -U ruinarpg -d ruinarpg' \
  < "$TMP/db.dump" || die "pg_restore falhou"
log "restaurando images.tar"
docker run --rm -i --network none -v "${PROJ}_images:/images" --entrypoint tar "$PG_IMAGE" -C /images -xf - \
  < "$TMP/images.tar" || die "restauração do volume de imagens falhou"

# ---------------------------------------------------------------- 4. contagens e fingerprint

log "conferindo as contagens"
dc exec -T db psql -X -q -At -v ON_ERROR_STOP=1 -U ruinarpg -d ruinarpg < "$SQL" > "$TMP/contagens.tsv"
if ! cmp -s "$TMP/contagens.tsv" "$DIR/contagens.tsv"; then
  log "contagens divergentes (tabela: pacote -> restaurado):"
  join -t $'\t' -a 1 -a 2 -e '-' -o 0,1.2,2.2 <(LC_ALL=C sort "$DIR/contagens.tsv") <(LC_ALL=C sort "$TMP/contagens.tsv") |
    awk -F'\t' '$2 != $3 { printf "  %s: %s -> %s\n", $1, $2, $3 }' >&2
  die "as contagens do banco restaurado não batem com contagens.tsv"
fi
log "contagens iguais ($(wc -l < "$DIR/contagens.tsv") tabelas)"

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
deadline=$((SECONDS + RUINA_HEALTH_TIMEOUT)) body=""
while (( SECONDS < deadline )); do
  body="$(curl -fsS -m 3 "http://127.0.0.1:$PORT/api/health" 2>/dev/null || true)"
  [[ "$body" == '"OK"' ]] && break
  sleep 2
done
[[ "$body" == '"OK"' ]] || { dc logs --tail 50 api >&2 || true; die "a api não respondeu em http://127.0.0.1:$PORT/api/health em ${RUINA_HEALTH_TIMEOUT}s"; }
log "api respondeu"

# O primeiro arquivo do volume (ordem de bytes) com nome seguro para URL. Compara os bytes: o nginx da
# 1.5.0 devolve index.html com 200 para um caminho que não existe.
first="$(tar -tf "$TMP/images.tar" | grep -v '/$' | sed 's|^\./||' | LC_ALL=C sort | { grep -E '^[A-Za-z0-9._/-]+$' || true; } | head -1)"
if [[ -z "$first" ]]; then
  if (( NIMG == 0 )); then
    log "aviso: o volume de imagens do pacote está vazio; verificação do nginx pulada"
  else
    die "nenhum arquivo do volume de imagens tem nome seguro para URL; verifique o nginx à mão"
  fi
else
  mkdir "$TMP/img"
  tar -C "$TMP/img" -xf "$TMP/images.tar" "./$first" 2>/dev/null || tar -C "$TMP/img" -xf "$TMP/images.tar" "$first"
  curl -fsS -m 10 -o "$TMP/servida" "http://127.0.0.1:$PORT/images/$first" || die "o nginx não serviu /images/$first"
  cmp -s "$TMP/servida" "$TMP/img/$first" || die "o nginx serviu /images/$first com bytes diferentes do pacote"
  log "nginx serviu /images/$first"
fi

echo "OK pacote $DIR"
