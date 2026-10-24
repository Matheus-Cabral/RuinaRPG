# Funções comuns de restaurar-pacote.sh e ensaio-150.sh (sub-projeto H). Só para `source`: não roda sozinho.
# Quem inclui define: set -euo pipefail, PROJ (projeto compose), SQL (contagens.sql) e, antes de chamar
# as funções, dc(), que roda `docker compose` no projeto certo.
# shellcheck shell=bash disable=SC2034  # COMPLETO, API_ID e NGINX_ID são lidos por quem inclui

PG_IMAGE="postgres:16-alpine"

log() { printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2; }
die() { log "ERRO: $*"; exit 1; }
fatal() { log "ERRO: $*"; exit 2; }

# Tudo o que pertence ao projeto $PROJ: contêineres, volumes (pelo rótulo e pelo prefixo "<projeto>_")
# e redes. Vazio = o projeto não existe.
project_leftovers() {
  docker ps -aq --filter "label=com.docker.compose.project=$PROJ"
  docker volume ls -q --filter "label=com.docker.compose.project=$PROJ"
  docker volume ls -q | { grep -E "^${PROJ}_" || true; }
  docker network ls -q --filter "label=com.docker.compose.project=$PROJ"
}

# Pelos rótulos e pelo prefixo dos volumes: não depende de arquivo compose nenhum.
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

# Confere o SHA256SUMS do pacote em $1 (antes de subir qualquer coisa). Define COMPLETO=1 quando o pacote
# traz as imagens docker (pacote completo), vazio num pacote --so-banco.
verify_package() {
  local dir="$1" f out
  [[ -f "$dir/SHA256SUMS" ]] || die "pacote sem SHA256SUMS: $dir"
  for f in dump.tar contagens.tsv; do
    awk '{print $2}' "$dir/SHA256SUMS" | grep -qx "$f" || die "SHA256SUMS não cobre $f"
  done
  COMPLETO=""
  if awk '{print $2}' "$dir/SHA256SUMS" | grep -qx imagens-docker.tar; then
    awk '{print $2}' "$dir/SHA256SUMS" | grep -qx imagens.tsv || die "SHA256SUMS cobre imagens-docker.tar mas não imagens.tsv"
    COMPLETO=1
  fi
  log "conferindo SHA256SUMS de $dir"
  if ! out="$(cd "$dir" && sha256sum -c --strict SHA256SUMS 2>&1)"; then
    printf '%s\n' "$out" | grep -v ': OK$' >&2 || true
    die "SHA256SUMS não confere: pacote corrompido ou incompleto"
  fi
}

# Extrai db.dump, images.tar e sha de $1/dump.tar para $2.
extract_dump() {
  tar -C "$2" -xf "$1/dump.tar" db.dump images.tar sha || die "dump.tar não tem db.dump, images.tar e sha"
  [[ "$(head -c 5 "$2/db.dump")" == PGDMP ]] || die "db.dump não é um pg_dump -Fc"
}

# docker load de $1/imagens-docker.tar; define API_ID e NGINX_ID a partir de imagens.tsv
# (serviço<TAB>nome<TAB>id; a api é a linha 2 e o nginx a 3 — db, api, nginx —, qualquer que seja o nome
# do serviço) e confere que os ids vieram no tar.
load_images() {
  local id
  log "carregando imagens-docker.tar"
  docker load -q -i "$1/imagens-docker.tar" >&2
  API_ID="$(awk -F'\t' 'NR==2{print $3}' "$1/imagens.tsv")"
  NGINX_ID="$(awk -F'\t' 'NR==3{print $3}' "$1/imagens.tsv")"
  for id in "$API_ID" "$NGINX_ID"; do
    [[ "$id" =~ ^sha256:[0-9a-f]{64}$ ]] || die "imagens.tsv inválido: '$id'"
    docker image inspect "$id" >/dev/null 2>&1 || die "imagem $id não veio em imagens-docker.tar"
  done
}

# restore_dump <db.dump> <images.tar> <usuário> <banco>: pg_restore no serviço db (sem dono nem GRANTs da
# produção, numa transação só) e images.tar no volume <projeto>_images.
restore_dump() {
  log "restaurando db.dump"
  dc exec -T db pg_restore --no-owner --no-privileges --exit-on-error --single-transaction -U "$3" -d "$4" \
    < "$1" || die "pg_restore falhou"
  log "restaurando images.tar"
  docker run --rm -i --network none -v "${PROJ}_images:/images" --entrypoint tar "$PG_IMAGE" -C /images -xf - \
    < "$2" || die "restauração do volume de imagens falhou"
}

# check_counts <contagens.tsv do pacote> <usuário> <banco> <arquivo temporário>: as contagens de todas as
# tabelas do banco restaurado têm de ser iguais byte a byte às do pacote; senão lista as tabelas que
# divergem e sai com 1.
check_counts() {
  local esperado="$1" medido="$4"
  log "conferindo as contagens"
  dc exec -T db psql -X -q -At -v ON_ERROR_STOP=1 -U "$2" -d "$3" < "$SQL" > "$medido"
  if ! cmp -s "$medido" "$esperado"; then
    log "contagens divergentes (tabela: pacote -> restaurado):"
    join -t $'\t' -a 1 -a 2 -e '-' -o 0,1.2,2.2 <(LC_ALL=C sort "$esperado") <(LC_ALL=C sort "$medido") |
      awk -F'\t' '$2 != $3 { printf "  %s: %s -> %s\n", $1, $2, $3 }' >&2
    die "as contagens do banco restaurado não batem com contagens.tsv"
  fi
  log "contagens iguais ($(wc -l < "$esperado") tabelas)"
}

# wait_health <porta> <segundos>: espera "OK" em http://127.0.0.1:<porta>/api/health.
wait_health() {
  local deadline=$((SECONDS + $2)) body=""
  while (( SECONDS < deadline )); do
    body="$(curl -fsS -m 3 "http://127.0.0.1:$1/api/health" 2>/dev/null || true)"
    [[ "$body" == '"OK"' ]] && { log "api respondeu"; return 0; }
    sleep 2
  done
  dc logs --tail 50 api >&2 || true
  die "a api não respondeu em http://127.0.0.1:$1/api/health em ${2}s"
}

# check_nginx_image <porta> <images.tar> <dir temporário>: o nginx serve o primeiro arquivo do volume
# (ordem de bytes, nome seguro para URL) com os mesmos bytes do pacote — o nginx da 1.5.0 devolve
# index.html com 200 para um caminho que não existe, então o status sozinho mentiria.
check_nginx_image() {
  local porta="$1" tarf="$2" tmp="$3" n first
  n="$(tar -tf "$tarf" | grep -cv '/$' || true)"
  first="$(tar -tf "$tarf" | grep -v '/$' | sed 's|^\./||' | LC_ALL=C sort | { grep -E '^[A-Za-z0-9._/-]+$' || true; } | head -1)"
  if [[ -z "$first" ]]; then
    (( n == 0 )) || die "nenhum arquivo do volume de imagens tem nome seguro para URL; verifique o nginx à mão"
    log "aviso: o volume de imagens do pacote está vazio; verificação do nginx pulada"
    return 0
  fi
  mkdir "$tmp/img"
  tar -C "$tmp/img" -xf "$tarf" "./$first" 2>/dev/null || tar -C "$tmp/img" -xf "$tarf" "$first"
  curl -fsS -m 10 -o "$tmp/servida" "http://127.0.0.1:$porta/images/$first" || die "o nginx não serviu /images/$first"
  cmp -s "$tmp/servida" "$tmp/img/$first" || die "o nginx serviu /images/$first com bytes diferentes do pacote"
  log "nginx serviu /images/$first"
}
