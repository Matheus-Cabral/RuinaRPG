#!/usr/bin/env bash
# Runs the deploy bats tests with a pinned bats-core fetched into a git-ignored cache (nothing installed system-wide).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
bats_dir="$here/.bats"
if [[ ! -x "$bats_dir/bin/bats" ]]; then
  mkdir -p "$bats_dir"
  curl -fsSL https://github.com/bats-core/bats-core/archive/refs/tags/v1.11.0.tar.gz | tar -xz -C "$bats_dir" --strip-components=1
fi
if (( $# == 0 )); then set -- "$here"/*.bats; fi
exec "$bats_dir/bin/bats" "$@"
