#!/usr/bin/env bash
# SSH forced command for the pipeline's keys. The only things it accepts: dump, status, and
# deploy <40-hex sha that is on origin/main> [--window]. Everything else is refused before anything runs.
#
#   gate.sh             the deploy key (deploy.yml): dump, status, deploy
#   gate.sh dump-only   the runner's on-disk key (clone.sh): dump and status only — deploy is refused
#
# The argument comes from the forced command in authorized_keys, never from the client.
# Self-contained on purpose: it is a security boundary, so it sources nothing.
set -euo pipefail
# Bracket expressions are locale dependent (UTF-8 lets [0-9a-f] match é, ², fullwidth digits).
export LC_ALL=C

if (( EUID == 0 )); then
  # Production: ignore any override. The sudoers entry must env_keep SSH_ORIGINAL_COMMAND only, no SETENV.
  RUINA_REPO_DIR=/home/ruina/RuinaRPG
  RUINA_DEPLOY="$RUINA_REPO_DIR/deploy/deploy.sh"
  RUINA_SKIP_FETCH=0
else
  RUINA_REPO_DIR="${RUINA_REPO_DIR:-/home/ruina/RuinaRPG}"
  RUINA_DEPLOY="${RUINA_DEPLOY:-$RUINA_REPO_DIR/deploy/deploy.sh}"
  RUINA_SKIP_FETCH="${RUINA_SKIP_FETCH:-0}"
fi

refuse() { echo "comando recusado" >&2; exit 2; }

case "$#:${1-}" in
  0:) mode=full ;;
  1:dump-only) mode=dump-only ;;
  *) refuse ;;
esac

cmd="${SSH_ORIGINAL_COMMAND-}"
# The regex is the grammar, applied to the whole string before any splitting.
[[ "$cmd" =~ ^(dump|status|deploy\ [0123456789abcdef]{40}(\ --window)?)$ ]] || refuse

read -r -a words <<< "$cmd"
[[ "$mode" == full || "${words[0]}" != deploy ]] || refuse

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
    rc=0
    git_as_owner merge-base --is-ancestor "$sha" origin/main >&2 || rc=$?
    case "$rc" in
      0) ;;
      1) echo "sha não está na main" >&2; exit 3 ;;
      *) echo "git merge-base falhou (código $rc)" >&2; exit 1 ;;
    esac
    # The gate already fetched.
    RUINA_SKIP_FETCH=1 exec "$RUINA_DEPLOY" deploy "$sha" "${words[@]:2}"
    ;;
esac
refuse
