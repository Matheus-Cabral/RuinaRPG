# Ambiente dos passos da virada que agem NA PRODUÇÃO (roteiro, seção 0.2). O Bash do agente não guarda
# variáveis nem funções entre chamadas: TODA chamada de um passo começa com
#
#   . /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
#
# Define ALVO=producao (somente leitura), as variáveis do roteiro e as funções prod, sqlprod, trazer,
# guardar e passo_longo (ambiente-comum.sh); carrega o estado dos passos anteriores de
# ~/.virada/estado-producao.env (600) e imprime o alvo. Recusa se este shell já carregou
# ambiente-ensaio.sh. Não escreve nada ao ser carregado.
# shellcheck shell=bash disable=SC2034  # as variáveis são lidas pelos comandos do roteiro

if ! (return 0 2>/dev/null); then
  echo "uso: . ${BASH_SOURCE[0]} (com source, não executado)" >&2
  exit 2
fi
if [[ -n "${ALVO:-}" && "$ALVO" != producao ]]; then
  echo "recusado: este shell já carregou o ambiente '$ALVO'; os passos da produção rodam em outra chamada" >&2
  return 1
fi
[[ -n "${ALVO:-}" ]] || readonly ALVO=producao

cd /home/pro/RuinaRPG || return 1
VIRADA_DIR="$HOME/.virada"
ESTADO="$VIRADA_DIR/estado-producao.env"
SENHA=/home/pro/.ruinarpg-virada/sudo                 # 600; nunca impresso
REPO=/home/ruina/RuinaRPG                             # checkout da produção
DONO="sudo -u ruina"                                  # git no checkout sempre como o dono
BKP=/var/backups/ruinarpg-virada                      # pacotes na produção (700)
FERR=/root/virada                                     # pacote.sh + contagens.sql na produção
LOCAL=/home/pro/backups/ruinarpg-virada               # cópias nesta VM (700)
LOGS="$LOCAL/logs"                                    # logs dos passos longos nesta VM (700)
V150=729dac9a8393383df01caa2720726454273d784a          # 1.5.0
# Padrões; o estado guarda o que o O1 e o P3 descobrirem (guardar CONTA …, guardar DB …).
CONTA="" URL="" SUDO_SEM_SENHA=""
DB=ruinarpg-db-1 API150=ruinarpg-api-1 NGINX150=ruinarpg-nginx-1 IMGAPI150="" IMGNGINX150=""
PACOTE="" BANCO="" MERGE="" HEAD200="" FP="" WD="" VOLTA_BKP=""
# shellcheck source=ambiente-comum.sh
. /home/pro/RuinaRPG/tools/virada/ambiente-comum.sh || return 1
_virada_carregar_estado || return 1

_ssh_prod() {
  ssh -i ~/.ssh/ruinarpg_virada -o IdentitiesOnly=yes -o BatchMode=yes -o ServerAliveInterval=30 "$CONTA@192.168.80.10" "$@"
}
# A senha entra pelo stdin do sudo -S. Com NOPASSWD (P1: guardar SUDO_SEM_SENHA 1) o sudo não lê o
# stdin, e a senha chegaria ao comando (ex.: um compose run): aí o stdin é /dev/null e o sudo é -n.
_prod_exec() {
  if [[ -z "$CONTA" ]]; then echo "CONTA não guardada: rode 'guardar CONTA <conta>' (O1)" >&2; return 2; fi
  if [[ "$SUDO_SEM_SENHA" == 1 ]]; then
    _ssh_prod "sudo -n bash -c $(printf %q "$1")" < /dev/null
  else
    if [[ ! -r "$SENHA" ]]; then echo "arquivo da senha ausente: $SENHA (passo 0.1)" >&2; return 2; fi
    _ssh_prod "sudo -S -p '' bash -c $(printf %q "$1")" < "$SENHA"
  fi
}

printf '== ALVO=producao: %s@192.168.80.10, checkout %s; estado %s\n' "${CONTA:-<CONTA não guardada>}" "$REPO" "$ESTADO" >&2
