#!/usr/bin/env bash
# Roda NO RUNNER (192.168.80.16) como ghrunner, semanalmente (cron, veja runner.md).
# O clone.sh nao faz prune (seria global no host), entao a limpeza periodica e feita aqui.
#
#   runner-cleanup.sh [colors.env…]
#
# Cada argumento e o deploy/state/colors.env de um checkout que faz deploy neste host (por exemplo
# /home/pro/RuinaRPG/deploy/state/colors.env, a stack de dev): as imagens de CURRENT_SHA e
# PREVIOUS_SHA desses arquivos nunca sao removidas, mesmo paradas ha mais de 7 dias, porque sao
# a release ativa e o alvo do rollback. Um arquivo listado que existe mas nao pode ser lido aborta tudo
# antes de remover qualquer imagem; um que nao existe (checkout que ainda nao fez deploy) e pulado.
set -euo pipefail

keep=" "
for f in "$@"; do
  [[ -e "$f" ]] || { echo "sem estado (ainda sem deploy): $f" >&2; continue; }
  [[ -r "$f" ]] || { echo "estado ilegivel: $f; nenhuma imagem removida" >&2; exit 1; }
  for key in CURRENT_SHA PREVIOUS_SHA; do
    v="$(sed -n "s/^$key=//p" "$f" | tail -1)"
    [[ -z "$v" ]] || keep+="$v "
  done
done

docker builder prune -f --filter until=168h

# Tags ruinarpg-api|client|smoke com mais de 7 dias e que nenhum container usa.
cutoff=$(( $(date +%s) - 7 * 86400 ))
used="$(docker ps -a --format '{{.Image}}' | sort -u)"
docker image ls --format '{{.Repository}}:{{.Tag}} {{.ID}}' |
  grep -E '^ruinarpg-(api|client|smoke)[:-]' |
  while read -r ref id; do
    [[ "$keep" == *" ${ref##*:} "* ]] && continue
    grep -qxF -e "$ref" -e "$id" <<< "$used" && continue
    created="$(date -d "$(docker image inspect -f '{{.Created}}' "$id")" +%s)" || continue
    (( created < cutoff )) || continue
    docker image rm "$ref" || true
  done
