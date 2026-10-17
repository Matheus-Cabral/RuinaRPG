#!/usr/bin/env bash
# Roda NO RUNNER (192.168.80.16) como ghrunner, semanalmente (systemd timer, veja runner.md).
# O clone.sh nao faz prune (seria global no host), entao a limpeza periodica e feita aqui.
set -euo pipefail

docker builder prune -f --filter until=168h

# Tags ruinarpg-api|client|smoke com mais de 7 dias e que nenhum container usa.
cutoff=$(( $(date +%s) - 7 * 86400 ))
used="$(docker ps -a --format '{{.Image}}' | sort -u)"
docker image ls --format '{{.Repository}}:{{.Tag}} {{.ID}}' |
  grep -E '^ruinarpg-(api|client|smoke)[:-]' |
  while read -r ref id; do
    grep -qxF -e "$ref" -e "$id" <<< "$used" && continue
    created="$(date -d "$(docker image inspect -f '{{.Created}}' "$id")" +%s)"
    (( created < cutoff )) || continue
    docker image rm "$ref" || true
  done
