#!/usr/bin/env bash
# Shared helpers for the deploy scripts. Source it; do not execute it.

log() { printf '%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2; }
die() { log "ERRO: $*"; exit 1; }
require_cmd() { command -v "$1" >/dev/null 2>&1 || die "comando obrigatório ausente: $1"; }

compose() {
  docker compose -p "$RUINA_PROJECT" -f "$RUINA_REPO_DIR/docker-compose.yml" \
    --env-file "$RUINA_ENV_FILE" --env-file "$RUINA_STATE_DIR/colors.env" "$@"
}
