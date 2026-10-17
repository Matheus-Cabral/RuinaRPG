#!/usr/bin/env bash
# Roda NA PRODUCAO (192.168.80.10), uma vez, como root: sudo deploy/provision/install-gate.sh
# Instala o gate do SSH, o sudoers e o diretorio de backups. Idempotente.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
(( EUID == 0 )) || { echo "rode com sudo" >&2; exit 1; }
id deploy >/dev/null 2>&1 || { echo "usuario deploy nao existe (veja producao.md, passo 1)" >&2; exit 1; }

install -d -m 755 -o root -g root /opt/ruinarpg
install -m 755 -o root -g root "$here/../gate.sh" /opt/ruinarpg/gate.sh

# Valida antes de instalar: um sudoers quebrado trava o sudo da maquina inteira.
tmp="$(mktemp)"; trap 'rm -f "$tmp"' EXIT
install -m 440 -o root -g root "$here/sudoers-ruinarpg" "$tmp"
visudo -cf "$tmp"
install -m 440 -o root -g root "$tmp" /etc/sudoers.d/ruinarpg

# Os dumps contem os hashes de senha de todas as contas: so root le.
install -d -m 700 -o root -g root /var/backups/ruinarpg
echo "gate instalado em /opt/ruinarpg/gate.sh"
