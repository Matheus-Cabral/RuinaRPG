#!/usr/bin/env bash
# Shared helpers for the deploy scripts. Source it; do not execute it.

# Never fatal: stderr may be an SSH channel that went away mid-run. RUINA_LOG_FD (default 2) is where
# the line goes; RUINA_LOG_FILE, when set, keeps a copy that survives the channel (deploy.sh sets both).
log() {
  local line
  line="$(date -u +%Y-%m-%dT%H:%M:%SZ) $*"
  # Left to right: the line goes to the log fd first, then printf's own error message goes nowhere.
  # shellcheck disable=SC2261
  printf '%s\n' "$line" >&"${RUINA_LOG_FD:-2}" 2>/dev/null || true
  if [[ -n "${RUINA_LOG_FILE:-}" ]]; then printf '%s\n' "$line" >> "$RUINA_LOG_FILE" 2>/dev/null || true; fi
}
die() { log "ERRO: $*"; exit 1; }
require_cmd() { command -v "$1" >/dev/null 2>&1 || die "comando obrigatório ausente: $1"; }

# As words, for callers that wrap compose in another command (timeout); compose() is the usual way.
compose_cmd() {
  COMPOSE_CMD=(docker compose -p "$RUINA_PROJECT" -f "$RUINA_REPO_DIR/docker-compose.yml"
    --env-file "$RUINA_ENV_FILE" --env-file "$RUINA_STATE_DIR/colors.env")
}

compose() {
  compose_cmd
  "${COMPOSE_CMD[@]}" "$@"
}
