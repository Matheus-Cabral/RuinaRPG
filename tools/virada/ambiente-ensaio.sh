# Ambiente dos passos da virada no ENSAIO, nesta VM (roteiro, seção 0.3): os passos das seções 4–7
# rodam com o mesmo texto da produção, trocando só estas variáveis e a função prod (sem ssh;
# RUINA_PACOTE_SEM_ROOT=1 porque o pro roda o docker sem root). TODA chamada de um passo do ensaio
# começa com
#
#   . /home/pro/RuinaRPG/tools/virada/ambiente-ensaio.sh || exit 1
#
# Define ALVO=ensaio (somente leitura), carrega ~/.virada/estado-ensaio.env (600) e imprime o alvo.
# Recusa se este shell já carregou ambiente-producao.sh. Não escreve nada ao ser carregado.
# shellcheck shell=bash disable=SC2034  # as variáveis são lidas pelos comandos do roteiro

if ! (return 0 2>/dev/null); then
  echo "uso: . ${BASH_SOURCE[0]} (com source, não executado)" >&2
  exit 2
fi
if [[ -n "${ALVO:-}" && "$ALVO" != ensaio ]]; then
  echo "recusado: este shell já carregou o ambiente '$ALVO'; os passos do ensaio rodam em outra chamada" >&2
  return 1
fi
[[ -n "${ALVO:-}" ]] || readonly ALVO=ensaio

cd /home/pro/RuinaRPG || return 1
VIRADA_DIR="$HOME/.virada"
ESTADO="$VIRADA_DIR/estado-ensaio.env"
ENSAIO="${RUINA_VIRADA_ENSAIO:-/home/pro/ensaio}"   # outro valor: só os testes (deploy/tests/virada-ambiente.bats)
REPO="$ENSAIO/RuinaRPG" DONO=""
BKP="$ENSAIO/producao-backups" FERR="$ENSAIO/virada" LOCAL="$ENSAIO/vm-backups"
LOGS="$LOCAL/logs"
V150=729dac9a8393383df01caa2720726454273d784a
CONTA="" SUDO_SEM_SENHA=""
DB=ruinarpg-db-1 API150=ruinarpg-api-1 NGINX150=ruinarpg-nginx-1 IMGAPI150="" IMGNGINX150=""
PACOTE="" BANCO="" MERGE="" HEAD200="" FP="" WD="" VOLTA_BKP=""
# shellcheck source=ambiente-comum.sh
. /home/pro/RuinaRPG/tools/virada/ambiente-comum.sh || return 1
_virada_carregar_estado || return 1
URL=http://127.0.0.1                                  # sem Cloudflare; o estado não muda isto

_prod_exec() {
  (umask 077 && mkdir -p "$BKP" "$FERR" "$ENSAIO/deploy-backups") || return 1
  chmod 700 "$ENSAIO" "$BKP" "$FERR" "$ENSAIO/deploy-backups" || return 1
  RUINA_PACOTE_SEM_ROOT=1 RUINA_BACKUP_DIR="$ENSAIO/deploy-backups" bash -c "$1"
}

printf '== ALVO=ensaio: esta VM, checkout %s; estado %s\n' "$REPO" "$ESTADO" >&2
