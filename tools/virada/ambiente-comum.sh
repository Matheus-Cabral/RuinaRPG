# Parte comum de ambiente-producao.sh e ambiente-ensaio.sh (roteiro da virada, sub-projeto H, seção 0).
# Só para `source`, e só por um daqueles dois arquivos: nunca diretamente.
# Quem inclui já definiu ALVO, ESTADO, VIRADA_DIR, LOCAL, LOGS, BKP, DB e _prod_exec (roda uma string de
# comando como root no alvo, sem imprimi-la).
# shellcheck shell=bash

# As únicas variáveis que um passo pode guardar no arquivo de estado: nomes, shas, caminhos e o domínio
# público. Nunca senha, conteúdo do .env ou dado pessoal.
VIRADA_VARS=" CONTA URL SUDO_SEM_SENHA DB API150 NGINX150 IMGAPI150 IMGNGINX150 PACOTE BANCO MERGE HEAD200 FP WD VOLTA_BKP "
_VIRADA_VALOR='^[A-Za-z0-9._:/@+-]+$'

# Carrega $ESTADO linha a linha, sem executar nada dele: cada linha tem de ser VAR=valor, com VAR na lista
# acima e valor só com caracteres seguros. O arquivo tem de ser do usuário atual e ter modo 600.
_virada_carregar_estado() {
  local linha nome valor
  [[ -e "$ESTADO" ]] || return 0
  if [[ ! -O "$ESTADO" || "$(stat -c %a "$ESTADO")" != 600 ]]; then
    echo "recusado: $ESTADO tem de ser do usuário atual e ter modo 600" >&2
    return 1
  fi
  while IFS= read -r linha || [[ -n "$linha" ]]; do
    nome="${linha%%=*}" valor="${linha#*=}"
    if [[ "$linha" != *=* || "$VIRADA_VARS" != *" $nome "* || ! "$valor" =~ $_VIRADA_VALOR ]]; then
      echo "recusado: linha inesperada em $ESTADO (variável '${nome}')" >&2
      return 1
    fi
    printf -v "$nome" '%s' "$valor"
  done < "$ESTADO"
}

_virada_dirs() {
  (umask 077 && mkdir -p "$VIRADA_DIR" "$LOCAL" "$LOGS") && chmod 700 "$VIRADA_DIR" "$LOCAL" "$LOGS"
}

# guardar VAR valor: acrescenta VAR=valor ao arquivo de estado (600) e define VAR neste shell. A última
# linha de cada variável é a que vale nas próximas chamadas.
guardar() {
  if (( $# != 2 )); then echo "uso: guardar VAR valor" >&2; return 2; fi
  if [[ "$VIRADA_VARS" != *" $1 "* ]]; then echo "guardar: variável não prevista: $1" >&2; return 2; fi
  if [[ ! "$2" =~ $_VIRADA_VALOR ]]; then
    echo "guardar: valor vazio ou com caracteres inesperados para $1; nada guardado" >&2
    return 2
  fi
  _virada_dirs || return 1
  (umask 077 && printf '%s=%s\n' "$1" "$2" >> "$ESTADO") || return 1
  chmod 600 "$ESTADO"
  printf -v "$1" '%s' "$2"
  echo "[$ALVO] guardado: $1=$2" >&2
}

# prod 'comando': mostra o comando e o roda como root no alvo. Uma única string; as variáveis desta VM
# ($REPO, $BKP, …) são expandidas aqui antes, \$ fica para o lado de lá.
prod() {
  if (( $# != 1 )); then echo "uso: prod 'comando'" >&2; return 2; fi
  printf '[%s] $ %s\n' "$ALVO" "$1" >&2
  _prod_exec "$1"
}

# sqlprod 'SQL': consulta no banco do alvo ($DB). O SQL vai em base64 (sem problemas de aspas).
sqlprod() {
  local b
  if (( $# != 1 )); then echo "uso: sqlprod 'SQL'" >&2; return 2; fi
  b="$(printf '%s\n' "$1" | base64 -w0)"
  printf '[%s] sql em %s: %s\n' "$ALVO" "$DB" "$(printf '%s' "$1" | head -c 100 | tr '\n' ' ')" >&2
  _prod_exec "echo $b | base64 -d | docker exec -i $DB sh -c 'exec psql -X -q -At -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\${POSTGRES_DB:-\$POSTGRES_USER}\"'"
}

# trazer <nome>: copia o diretório $BKP/<nome> do alvo para $LOCAL, preservando 700/600.
trazer() {
  local -
  set -o pipefail
  if [[ $# != 1 || ! "$1" =~ ^[A-Za-z0-9._-]+$ ]]; then echo "uso: trazer <nome de um diretório de \$BKP>" >&2; return 2; fi
  _virada_dirs || return 1
  prod "tar -C $BKP -cf - $1" | (umask 077 && tar -C "$LOCAL" -xpf -)
}

# passo_longo <passo> <comando…>: roda o comando (uma função como prod serve) com stdout e stderr
# juntos em $LOGS/<passo>.log (600; um log anterior do mesmo passo é renomeado com o horário) e na
# tela, e termina o log com "== <passo> código N". Para os passos que passam de 2 minutos: a chamada
# roda com timeout de 600000 ms ou em segundo plano, e o log é lido depois.
passo_longo() {
  local -
  set -o pipefail
  local p="$1" f rc
  if (( $# < 2 )) || [[ ! "$p" =~ ^[A-Za-z0-9._-]+$ ]]; then echo "uso: passo_longo <passo> <comando…>" >&2; return 2; fi
  shift
  _virada_dirs || return 1
  f="$LOGS/$p.log"
  [[ ! -e "$f" ]] || mv "$f" "$f.$(date -u +%Y%m%dT%H%M%SZ)"
  (umask 077 && : > "$f")
  echo "[$ALVO] passo $p: log em $f" >&2
  { echo "== $p início $(date -u +%Y-%m-%dT%H:%M:%SZ) (alvo $ALVO)"; "$@"; } 2>&1 | tee -a "$f"
  rc="${PIPESTATUS[0]}"
  echo "== $p código $rc fim $(date -u +%Y-%m-%dT%H:%M:%SZ)" | tee -a "$f"
  return "$rc"
}
