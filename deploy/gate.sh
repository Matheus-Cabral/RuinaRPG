#!/usr/bin/env bash
# SSH forced command for the pipeline's key. The only things it accepts: dump, status, and
# deploy <40-hex sha that is on origin/main> [--window]. Everything else is refused before anything runs.
# Self-contained on purpose: it is a security boundary, so it sources nothing.
set -euo pipefail

RUINA_REPO_DIR="${RUINA_REPO_DIR:-/home/ruina/RuinaRPG}"
RUINA_DEPLOY="${RUINA_DEPLOY:-$RUINA_REPO_DIR/deploy/deploy.sh}"
RUINA_SKIP_FETCH="${RUINA_SKIP_FETCH:-0}"

refuse() { echo "comando recusado" >&2; exit 2; }

cmd="${SSH_ORIGINAL_COMMAND-}"
# The regex is the grammar, applied to the whole string before any splitting.
[[ "$cmd" =~ ^(dump|status|deploy\ [0-9a-f]{40}(\ --window)?)$ ]] || refuse

read -r -a words <<< "$cmd"

# The checkout belongs to the repo owner; as root, git must not leave root-owned files in it.
git_as_owner() {
  if (( EUID == 0 )); then
    sudo -H -u "$(stat -c %U "$RUINA_REPO_DIR")" git -C "$RUINA_REPO_DIR" "$@"
  else
    git -C "$RUINA_REPO_DIR" "$@"
  fi
}

case "${words[0]}" in
  status)
    exec "$RUINA_DEPLOY" status
    ;;
  dump)
    # deploy.sh backup prints only the backup dir on stdout; its logs go to stderr.
    dir="$("$RUINA_DEPLOY" backup)" || { echo "backup falhou" >&2; exit 1; }
    [[ -d "$dir" ]] || { echo "backup falhou: diretório inexistente" >&2; exit 1; }
    exec tar -C "$dir" -cf - .
    ;;
  deploy)
    sha="${words[1]}"
    if [[ "$RUINA_SKIP_FETCH" != 1 ]]; then
      git_as_owner fetch --quiet origin >&2 || { echo "git fetch falhou" >&2; exit 1; }
    fi
    git_as_owner merge-base --is-ancestor "$sha" origin/main 2>/dev/null || { echo "sha não está na main" >&2; exit 3; }
    exec "$RUINA_DEPLOY" deploy "$sha" "${words[@]:2}"
    ;;
esac
refuse
