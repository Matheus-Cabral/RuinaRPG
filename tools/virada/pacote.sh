#!/usr/bin/env bash
# Captura o "pacote da 1.5.0" da stack em execução (sub-projeto H, virada 1.5.0 → 2.0.0).
#
#   sudo ./pacote.sh <dir-destino>              pacote completo
#   sudo ./pacote.sh --so-banco <dir-destino>   só o banco (o dump final, dentro da janela)
#
# Roda NA PRODUÇÃO, como root. É autossuficiente: a produção roda a 1.5.0, que não tem deploy/; este
# script só precisa de docker (com compose), coreutils, tar e sha256sum, e do contagens.sql ao lado dele.
# Só LÊ a stack: nunca para, reinicia, marca (tag) ou apaga nada do docker, e nunca escreve no banco
# (a transação das contagens é READ ONLY).
#
# Escreve <dir-destino>/<AAAAMMDDTHHMMSSZ>-pacote-150/ (ou -banco-150/ com --so-banco), diretório 700 e
# arquivos 600:
#   dump.tar            tar com db.dump (pg_dump -Fc), images.tar (o volume de imagens) e sha — o formato
#                       que deploy/clone.sh lê
#   contagens.tsv       "tabela<TAB>linhas" de cada tabela (contagens.sql), no MESMO snapshot do pg_dump
#   imagens-docker.tar  docker save das imagens que os contêineres api e nginx estão rodando  (só completo)
#   imagens.tsv         "serviço<TAB>imagem<TAB>id" de db, api e nginx                         (só completo)
#   SHA256SUMS          sha256sum dos arquivos acima
# Tudo é escrito em <dir-destino>/.<nome>.partial e renomeado só no fim: uma falha sai com código != 0 e
# não deixa nenhum diretório com cara de pacote (o .partial é apagado). Na saída padrão vai só o caminho
# do pacote; o progresso vai para a saída de erro.
#
# Nomes (o padrão é a stack da 1.5.0 na produção; as variáveis existem só para os testes):
#   RUINA_PACOTE_PROJECT (ruinarpg), RUINA_PACOTE_VOLUME (<projeto>_images), RUINA_PACOTE_SERVICO_DB (db),
#   RUINA_PACOTE_SERVICO_API (api), RUINA_PACOTE_SERVICO_NGINX (nginx), RUINA_PACOTE_SEM_ROOT=1 (testes),
#   RUINA_PACOTE_SHA (o sha gravado em dump.tar/sha; padrão: HEAD do checkout da stack, se legível).
set -euo pipefail
shopt -s inherit_errexit

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
: "${RUINA_PACOTE_PROJECT:=ruinarpg}"
: "${RUINA_PACOTE_VOLUME:=${RUINA_PACOTE_PROJECT}_images}"
: "${RUINA_PACOTE_SERVICO_DB:=db}"
: "${RUINA_PACOTE_SERVICO_API:=api}"
: "${RUINA_PACOTE_SERVICO_NGINX:=nginx}"
SQL="$here/contagens.sql"

log() { printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2; }
die() { log "ERRO: $*"; exit 1; }
usage() { echo "uso: sudo $0 [--so-banco] <dir-destino>" >&2; exit 2; }

SO_BANCO="" DEST=""
for a in "$@"; do
  case "$a" in
    --so-banco) SO_BANCO=1 ;;
    -*) usage ;;
    *) [[ -z "$DEST" ]] || usage; DEST="$a" ;;
  esac
done
[[ -n "$DEST" ]] || usage

[[ "$(id -u)" == 0 || "${RUINA_PACOTE_SEM_ROOT:-}" == 1 ]] || die "precisa de root: rode com sudo"
for c in docker tar sha256sum; do command -v "$c" >/dev/null 2>&1 || die "comando obrigatório ausente: $c"; done
[[ -s "$SQL" ]] || die "contagens.sql não encontrado ao lado do script: $SQL"

# ---------------------------------------------------------------- descoberta (antes de escrever qualquer coisa)

# Id do único contêiner EM EXECUÇÃO do serviço $1 no projeto.
container_of() {
  local ids
  mapfile -t ids < <(docker ps -q --no-trunc --filter "label=com.docker.compose.project=$RUINA_PACOTE_PROJECT" \
    --filter "label=com.docker.compose.service=$1" --filter status=running)
  (( ${#ids[@]} == 1 )) ||
    die "esperava 1 contêiner em execução do serviço '$1' no projeto '$RUINA_PACOTE_PROJECT'; achei ${#ids[@]}"
  printf '%s\n' "${ids[0]}"
}

# Nome e id da imagem que o contêiner $2 (serviço $1) está rodando. O nome tem de apontar hoje para o
# mesmo id: senão a imagem foi reconstruída depois que o contêiner subiu e o docker save do nome salvaria
# outra coisa — melhor parar e investigar do que gerar um pacote com a imagem errada.
image_of() {
  local svc="$1" cid="$2" ref id tagged
  ref="$(docker inspect -f '{{.Config.Image}}' "$cid")"
  id="$(docker inspect -f '{{.Image}}' "$cid")"
  tagged="$(docker image inspect -f '{{.Id}}' "$ref" 2>/dev/null || true)"
  [[ "$tagged" == "$id" ]] ||
    die "o contêiner '$svc' roda a imagem $id, mas '$ref' hoje aponta para '${tagged:-nada}'; investigue antes de empacotar"
  printf '%s\t%s\n' "$ref" "$id"
}

log "projeto $RUINA_PACOTE_PROJECT: procurando os contêineres"
DB="$(container_of "$RUINA_PACOTE_SERVICO_DB")"
docker volume inspect "$RUINA_PACOTE_VOLUME" >/dev/null 2>&1 || die "volume de imagens inexistente: $RUINA_PACOTE_VOLUME"
# O volume tem de ser o que a api monta em /images (contêiner da api em qualquer estado: na janela ela está parada).
mapfile -t api_any < <(docker ps -aq --no-trunc --filter "label=com.docker.compose.project=$RUINA_PACOTE_PROJECT" \
  --filter "label=com.docker.compose.service=$RUINA_PACOTE_SERVICO_API")
(( ${#api_any[@]} >= 1 )) || die "nenhum contêiner do serviço '$RUINA_PACOTE_SERVICO_API' no projeto '$RUINA_PACOTE_PROJECT'"
for cid in "${api_any[@]}"; do
  mounted="$(docker inspect -f '{{range .Mounts}}{{if eq .Destination "/images"}}{{.Name}}{{end}}{{end}}' "$cid")"
  [[ "$mounted" == "$RUINA_PACOTE_VOLUME" ]] ||
    die "a api monta '${mounted:-nada}' em /images, não $RUINA_PACOTE_VOLUME"
done
DB_IMAGE="$(docker inspect -f '{{.Image}}' "$DB")"
IMG_DB="$(docker inspect -f '{{.Config.Image}}' "$DB")	$DB_IMAGE"
if [[ -z "$SO_BANCO" ]]; then
  IMG_API="$(image_of "$RUINA_PACOTE_SERVICO_API" "$(container_of "$RUINA_PACOTE_SERVICO_API")")"
  IMG_NGINX="$(image_of "$RUINA_PACOTE_SERVICO_NGINX" "$(container_of "$RUINA_PACOTE_SERVICO_NGINX")")"
fi

SHA="${RUINA_PACOTE_SHA:-}"
if [[ -z "$SHA" ]]; then
  wd="$(docker inspect -f '{{index .Config.Labels "com.docker.compose.project.working_dir"}}' "$DB" 2>/dev/null || true)"
  if [[ -n "$wd" && -d "$wd/.git" ]] && command -v git >/dev/null 2>&1; then
    SHA="$(git -c safe.directory="$wd" -C "$wd" rev-parse HEAD 2>/dev/null || true)"
  fi
  [[ -n "$SHA" ]] || log "aviso: não consegui ler o sha do checkout; dump.tar/sha fica vazio"
fi
[[ -z "$SHA" ]] || log "sha do checkout: $SHA"

# ---------------------------------------------------------------- escrita

name="$(date -u +%Y%m%dT%H%M%SZ)-$([[ -n "$SO_BANCO" ]] && echo banco || echo pacote)-150"
umask 077
if [[ ! -d "$DEST" ]]; then mkdir -p "$DEST"; chmod 700 "$DEST"; fi
DIR="$DEST/$name"
PARTIAL="$DEST/.$name.partial"
[[ ! -e "$DIR" && ! -e "$PARTIAL" ]] || die "já existe: $DIR"
mkdir "$PARTIAL"
chmod 700 "$PARTIAL"
WORK="$PARTIAL/.trabalho"
mkdir "$WORK"
PSQL_PID=""

cleanup() {
  local rc=$?
  if [[ -n "$PSQL_PID" ]]; then kill "$PSQL_PID" 2>/dev/null || true; wait "$PSQL_PID" 2>/dev/null || true; fi
  if (( rc != 0 )); then
    rm -rf "$PARTIAL"
    log "ERRO: pacote NÃO gerado (código $rc); nada ficou em $DEST"
  fi
}
trap cleanup EXIT
trap 'exit 130' INT TERM HUP
# Sem isto, um SIGPIPE (ex.: stderr num pipe que fechou) mata o bash com o trap de EXIT vendo $?=0, e o
# .partial ficaria para trás.
trap 'exit 141' PIPE

log "gerando $DIR"

# Uma transação REPEATABLE READ READ ONLY exporta o snapshot; o pg_dump usa esse snapshot e as contagens
# rodam dentro da mesma transação depois do dump: dump e contagens enxergam exatamente os mesmos dados,
# mesmo com a aplicação recebendo escritas (pacote completo, fora da janela).
coproc PSQL { docker exec -i "$DB" sh -c 'exec psql -X -q -At -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}"' 2> "$WORK/psql.err"; }
psql_in="${PSQL[1]}" psql_out="${PSQL[0]}"
psql_fail() { die "psql da transação de snapshot falhou: $1 $(tr '\n' ' ' < "$WORK/psql.err" 2>/dev/null)"; }

printf '%s\n' "BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;" "SELECT pg_export_snapshot();" >&"$psql_in"
read -r -t 60 snap <&"$psql_out" || psql_fail "sem resposta ao exportar o snapshot"
[[ "$snap" =~ ^[0-9A-Fa-f-]+$ ]] || psql_fail "snapshot inválido: '$snap'"
log "snapshot $snap"

log "pg_dump"
docker exec -i "$DB" sh -c 'exec pg_dump -U "$POSTGRES_USER" -Fc --snapshot="$1" "${POSTGRES_DB:-$POSTGRES_USER}"' sh "$snap" \
  < /dev/null > "$WORK/db.dump"
[[ "$(head -c 5 "$WORK/db.dump")" == PGDMP ]] || die "db.dump vazio ou inválido"
docker exec -i "$DB" pg_restore -l < "$WORK/db.dump" > /dev/null || die "pg_restore -l não leu o db.dump"

log "contagens"
{ cat "$SQL"; printf '\n%s\n' "SELECT '#FIM-CONTAGENS';"; } >&"$psql_in"
: > "$PARTIAL/contagens.tsv"
fim=""
while IFS= read -r -t 3600 line <&"$psql_out"; do
  if [[ "$line" == '#FIM-CONTAGENS' ]]; then fim=1; break; fi
  [[ "$line" =~ ^[^[:space:]]+$'\t'[0-9]+$ ]] || psql_fail "linha de contagem inesperada: '$line'"
  printf '%s\n' "$line" >> "$PARTIAL/contagens.tsv"
done
[[ -n "$fim" ]] || psql_fail "as contagens não terminaram"
[[ -s "$PARTIAL/contagens.tsv" ]] || die "nenhuma tabela contada"
printf '%s\n' "COMMIT;" '\q' >&"$psql_in"
exec {psql_in}>&-
wait "$PSQL_PID" || psql_fail "psql terminou com erro"
PSQL_PID=""
log "$(wc -l < "$PARTIAL/contagens.tsv") tabelas contadas"

log "volume $RUINA_PACOTE_VOLUME"
# Com a imagem do próprio db (já está na máquina; nada é baixado) e o volume só leitura.
docker run --rm --network none -v "$RUINA_PACOTE_VOLUME:/images:ro" --entrypoint tar "$DB_IMAGE" -C /images -cf - . \
  < /dev/null > "$WORK/images.tar"
tar -tf "$WORK/images.tar" > /dev/null || die "images.tar inválido"
printf '%s\n' "$SHA" > "$WORK/sha"
tar -C "$WORK" -cf "$PARTIAL/dump.tar" db.dump images.tar sha

files=(contagens.tsv dump.tar)
if [[ -z "$SO_BANCO" ]]; then
  log "docker save de ${IMG_API%%$'\t'*} e ${IMG_NGINX%%$'\t'*}"
  docker save -o "$PARTIAL/imagens-docker.tar" "${IMG_API%%$'\t'*}" "${IMG_NGINX%%$'\t'*}"
  [[ -s "$PARTIAL/imagens-docker.tar" ]] || die "imagens-docker.tar vazio"
  printf '%s\t%s\n' "$RUINA_PACOTE_SERVICO_DB" "$IMG_DB" "$RUINA_PACOTE_SERVICO_API" "$IMG_API" \
    "$RUINA_PACOTE_SERVICO_NGINX" "$IMG_NGINX" > "$PARTIAL/imagens.tsv"
  files+=(imagens-docker.tar imagens.tsv)
fi
rm -rf "$WORK"

(cd "$PARTIAL" && sha256sum "${files[@]}" > SHA256SUMS && sha256sum -c --strict --quiet SHA256SUMS) ||
  die "SHA256SUMS não confere logo após ser escrito"
chmod 600 "$PARTIAL"/*
mv "$PARTIAL" "$DIR"
log "pacote pronto: $DIR"
printf '%s\n' "$DIR"
