# Isolated environment for deploy.bats: one throwaway compose project (ruinarpg-bats-<pid>) per test
# file, driven from a clone of the repo under test, so the real stack, its volumes and its
# deploy/state are never touched.
REPO="$(cd "$BATS_TEST_DIRNAME/../.." && pwd)"

# The clone is of HEAD, but the deploy code under test may not be committed yet: overlay the working
# tree's deploy/, docker-compose.yml and .dockerignore (never src/ — the images built from the clone
# are tagged with HEAD's sha and reused, so they must match the commit exactly).
overlay_code_under_test() {
  local clone="$1" f
  while IFS= read -r -d '' f; do
    [[ -e "$REPO/$f" ]] && cp --parents -t "$clone" -- "$f"
  done < <(cd "$REPO" && git ls-files -co --exclude-standard -z -- deploy docker-compose.yml .dockerignore)
}

setup_file() {
  local clone="$BATS_FILE_TMPDIR/repo"
  git clone --quiet --shared "$REPO" "$clone"
  (cd "$REPO" && overlay_code_under_test "$clone")
  # deploy.sh refuses a dirty tracked tree (the build context is the working tree); the overlay is the
  # code under test, not a local edit, so git is told to look past it.
  git -C "$clone" ls-files -m -z | xargs -0 -r git -C "$clone" update-index --skip-worktree --

  # BROKEN_SHA: an empty commit whose API image the tests fake. CONFLICT_SHA: adds conflict.txt, so
  # checking it out fails while an untracked conflict.txt is in the way.
  export SHA BROKEN_SHA CONFLICT_SHA
  SHA="$(git -C "$clone" rev-parse HEAD)"
  git -C "$clone" -c user.name=bats -c user.email=bats@localhost commit --quiet --allow-empty -m broken
  BROKEN_SHA="$(git -C "$clone" rev-parse HEAD)"
  git -C "$clone" checkout --quiet --detach "$SHA"
  echo conflict > "$clone/conflict.txt"
  git -C "$clone" add conflict.txt
  git -C "$clone" -c user.name=bats -c user.email=bats@localhost commit --quiet -m conflict
  CONFLICT_SHA="$(git -C "$clone" rev-parse HEAD)"
  git -C "$clone" checkout --quiet --detach "$SHA"

  sed -e 's/^ASPNETCORE_ENVIRONMENT=.*/ASPNETCORE_ENVIRONMENT=Production/' \
      -e "s/^JWT_SIGNING_KEY=.*/JWT_SIGNING_KEY=$(openssl rand -hex 24)/" \
      "$clone/.env.example" > "$clone/.env"
  printf 'HTTP_PORT=18089\nHTTP_BIND=127.0.0.1\n' >> "$clone/.env"

  export RUINA_REPO_DIR="$clone"
  export RUINA_STATE_DIR="$clone/deploy/state"   # fixed by deploy.sh; exported for the tests' own use
  export RUINA_PROJECT="ruinarpg-bats-$$"
  export RUINA_BACKUP_DIR="$BATS_FILE_TMPDIR/backups"
  export RUINA_SKIP_FETCH=1
  export RUINA_DRAIN_SECONDS=0
  # Images are host-wide, not per project: pruning them here could remove the ruinarpg-* images of a
  # real deploy on this host. The selection rule is unit-tested instead (select_prunable).
  export RUINA_PRUNE_IMAGES=0
  export BATS_FILE_TMPDIR

  docker image inspect busybox:1.36 >/dev/null 2>&1 || docker pull -q busybox:1.36 >/dev/null
}

# Images tagged with $SHA are NOT removed: the clone's HEAD is the working repo's HEAD, so they are the
# very images the next test run and the real deploy of this commit reuse (rebuilding takes minutes).
# Only tags for $BROKEN_SHA/$CONFLICT_SHA (commits that exist only in the clone) are dropped.
teardown_file() {
  docker compose -p "$RUINA_PROJECT" -f "$RUINA_REPO_DIR/docker-compose.yml" \
    --env-file "$RUINA_REPO_DIR/.env" --profile tools down --volumes --remove-orphans
  local s
  for s in "$BROKEN_SHA" "$CONFLICT_SHA"; do
    docker rmi "ruinarpg-api:$s" "ruinarpg-client:$s" "ruinarpg-smoke:$s" >/dev/null 2>&1 || true
  done
}
