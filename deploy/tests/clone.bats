# clone.sh (Técnico R0013): restores a production-like dump into a throwaway project, deploys base then
# head, compares the data and checks old-API compatibility. The "production" is a throwaway stack from
# helpers.bash (ruinarpg-bats-<pid>, port 18089); the clone runs as ruinarpg-bats-clone-<pid> on 18090.
# The tests build on each other and run in order.
load helpers

clone_project() { echo "ruinarpg-bats-clone-${RUINA_PROJECT##*-}"; }

# Everything the clone needs; RUINA_REPO_DIR/RUINA_PROJECT/RUINA_BACKUP_DIR from helpers belong to the
# source stack and must be ignored by clone.sh.
clone_env() {
  export RUINA_DUMP_CMD="tar -C $(cat "$BATS_FILE_TMPDIR/dumpdir") -cf - ."
  export RUINA_OFFSITE_DIR="$BATS_FILE_TMPDIR/offsite"
  export RUINA_CLONE_PROJECT="$(clone_project)"
  export RUINA_CLONE_PORT=18090
}

leftovers() {
  local p; p="$(clone_project)"
  docker ps -aq --filter "label=com.docker.compose.project=$p"
  docker volume ls -q --filter "label=com.docker.compose.project=$p"
  docker volume ls -q | grep "^${p}_" || true
  docker network ls -q --filter "label=com.docker.compose.project=$p"
}

eval "helpers_teardown_file() $(declare -f teardown_file | tail -n +2)"
teardown_file() {
  local p; p="$(clone_project)"
  docker rm -f $(docker ps -aq --filter "label=com.docker.compose.project=$p") >/dev/null 2>&1 || true
  docker volume rm "${p}_pgdata" "${p}_images" "${p}_webroot" >/dev/null 2>&1 || true
  docker network rm "${p}_default" >/dev/null 2>&1 || true
  helpers_teardown_file
}

@test "setup: production-like stack with a user, and its backup" {
  RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh" run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  echo "$output"
  [ "$status" -eq 0 ]
  run curl -fsS -H 'Content-Type: application/json' \
    -d '{"nickname":"clonegm","email":"clonegm@example.com","senha":"Senha-Forte-123!","confirmacaoSenha":"Senha-Forte-123!"}' \
    "http://127.0.0.1:18089/api/auth/register/gm"
  echo "$output"
  [ "$status" -eq 0 ]
  dir="$("$RUINA_REPO_DIR/deploy/deploy.sh" backup)"
  [ -s "$dir/db.dump" ]
  echo "$dir" > "$BATS_FILE_TMPDIR/dumpdir"
  # A "1.5.0-like" base: $SHA without deploy/deploy.sh, made without moving the test repo's HEAD.
  export GIT_INDEX_FILE="$BATS_FILE_TMPDIR/old.index"
  git -C "$RUINA_REPO_DIR" read-tree "$SHA"
  git -C "$RUINA_REPO_DIR" rm -q --cached deploy/deploy.sh
  tree="$(git -C "$RUINA_REPO_DIR" write-tree)"
  unset GIT_INDEX_FILE
  git -C "$RUINA_REPO_DIR" -c user.name=bats -c user.email=bats@localhost commit-tree "$tree" -p "$SHA" -m "sem deploy.sh" > "$BATS_FILE_TMPDIR/oldbase"
  ! git -C "$RUINA_REPO_DIR" cat-file -e "$(cat "$BATS_FILE_TMPDIR/oldbase"):deploy/deploy.sh"
}

@test "clone refuses a project name that is not a throwaway one" {
  clone_env
  RUINA_CLONE_PROJECT=ruinarpg run "$RUINA_REPO_DIR/deploy/clone.sh" run "$SHA" "$SHA" "$BATS_FILE_TMPDIR/wd0"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"RUINA_CLONE_PROJECT"* ]]
}

@test "clone run reports an infrastructure error when the dump fails, and leaves nothing behind" {
  clone_env
  wd="$BATS_FILE_TMPDIR/wd-dumpfail"
  RUINA_DUMP_CMD="echo sem acesso >&2; exit 255" run "$RUINA_REPO_DIR/deploy/clone.sh" run "$SHA" "$SHA" "$wd"
  echo "$output"; cat "$wd/report.md" "$wd"/logs/*.log || true
  [ "$status" -eq 2 ]
  grep -q "dump: FALHOU" "$wd/report.md"
  grep -q "Resultado: FALHOU" "$wd/report.md"
  grep -q "sem acesso" "$wd/logs/dump.log"
  [ ! -e "$wd/repo" ] && [ ! -e "$wd/dump" ] && [ ! -e "$wd/dump.tar" ]
  [ -z "$(leftovers)" ]
}

@test "clone teardown refuses a project name that is not a throwaway one" {
  clone_env
  RUINA_CLONE_PROJECT=ruinarpg run "$RUINA_REPO_DIR/deploy/clone.sh" teardown "$BATS_FILE_TMPDIR/wd0"
  [ "$status" -eq 2 ]
  [[ "$output" == *"RUINA_CLONE_PROJECT"* ]]
}

@test "clone teardown removes what a killed run left behind" {
  clone_env
  wd="$BATS_FILE_TMPDIR/wd-killed"
  mkdir -p "$wd/dump" "$wd/backups" "$wd/logs"; echo x > "$wd/dump.tar"; echo x > "$wd/fingerprint-a.jsonl"
  docker volume create "$(clone_project)_images" >/dev/null
  docker run -d --name "$(clone_project)-stray" --label "com.docker.compose.project=$(clone_project)" busybox:1.36 sleep 600 >/dev/null
  run "$RUINA_REPO_DIR/deploy/clone.sh" teardown "$wd"
  echo "$output"
  [ "$status" -eq 0 ]
  [ -z "$(leftovers)" ]
  [ ! -e "$wd/dump" ] && [ ! -e "$wd/dump.tar" ] && [ ! -e "$wd/backups" ] && [ ! -e "$wd/fingerprint-a.jsonl" ]
  [ -d "$wd/logs" ]
}

@test "clone run passes for an unchanged version and reports compat ok" {
  clone_env
  wd="$BATS_FILE_TMPDIR/wd1"
  run "$RUINA_REPO_DIR/deploy/clone.sh" run "$SHA" "$SHA" "$wd"
  echo "$output"; cat "$wd/report.md" "$wd"/logs/*.log || true
  [ "$status" -eq 0 ]
  grep -q "Resultado: OK" "$wd/report.md"
  grep -qx '\*\*Resultado: OK\*\*' "$wd/report.md"
  [ "$(cat "$wd/compat")" = ok ]
  for s in dump checkout restore base fingerprint-a head fingerprint-b compare compat teardown; do
    [ -s "$wd/logs/$s.log" ]
  done
  [ -z "$(docker compose -p "$(clone_project)" ps -q)" ]
  [ -z "$(leftovers)" ]
  [ ! -e "$wd/repo" ] && [ ! -e "$wd/dump" ] && [ ! -e "$wd/dump.tar" ] && [ ! -e "$wd/backups" ]
  ! ls "$wd"/*.jsonl 2>/dev/null
  [ "$(ls -1 "$RUINA_OFFSITE_DIR" | wc -l)" -eq 1 ]
  off="$RUINA_OFFSITE_DIR/$(ls -1 "$RUINA_OFFSITE_DIR")"
  [ -s "$off/db.dump" ]
  [ "$(stat -c %a "$off")" = 700 ]
  # The worktree is gone from git's bookkeeping too.
  ! git -C "$RUINA_REPO_DIR" worktree list | grep -q "$wd/repo"
}

@test "clone run fails when the head version deletes data" {
  clone_env
  export RUINA_KEEP_OFFSITE=1
  wd="$BATS_FILE_TMPDIR/wd2"
  # BROKEN_SHA (helpers.bash) is an empty commit on top of $SHA that exists only in the test repo:
  # a different head sha whose images are $SHA's (helpers' teardown removes these tags).
  for i in api client smoke; do docker tag "ruinarpg-$i:$SHA" "ruinarpg-$i:$BROKEN_SHA"; done
  before="$(ls -1 "$RUINA_OFFSITE_DIR")"
  RUINA_HEAD_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/delete-users-smoke.sh" \
    run "$RUINA_REPO_DIR/deploy/clone.sh" run "$SHA" "$BROKEN_SHA" "$wd"
  echo "$output"; cat "$wd/report.md" "$wd"/logs/*.log || true
  [ "$status" -eq 1 ]
  grep -q "lost" "$wd/report.md"
  grep -q "AspNetUsers" "$wd/report.md"
  grep -q "Resultado: FALHOU" "$wd/report.md"
  # The head deploy itself ran the head commit.
  grep -q "sha=$BROKEN_SHA" "$wd/logs/head.log"
  [ -z "$(leftovers)" ]
  [ ! -e "$wd/repo" ]
  # Pruned to the newest one, which is not the previous run's.
  [ "$(ls -1 "$RUINA_OFFSITE_DIR" | wc -l)" -eq 1 ]
  [ "$(ls -1 "$RUINA_OFFSITE_DIR")" != "$before" ]
}

@test "bootstrap: a base without deploy/deploy.sh is fingerprinted from the restored dump and forces a window" {
  clone_env
  wd="$BATS_FILE_TMPDIR/wd3"
  old="$(cat "$BATS_FILE_TMPDIR/oldbase")"
  run "$RUINA_REPO_DIR/deploy/clone.sh" run "$old" "$SHA" "$wd"
  echo "$output"; cat "$wd/report.md" "$wd"/logs/*.log || true
  [ "$status" -eq 0 ]
  grep -q "bootstrap: a versão base não tem deploy/deploy.sh" "$wd/report.md"
  grep -qx '\*\*Resultado: OK\*\*' "$wd/report.md"
  [ "$(cat "$wd/compat")" = janela ]
  [ ! -e "$wd/logs/base.log" ]
  grep -q "sha=$SHA" "$wd/logs/head.log"
  [ -s "$wd/logs/fingerprint-a.log" ] && [ -s "$wd/logs/compare.log" ]
  [ -z "$(leftovers)" ]
  [ ! -e "$wd/repo" ]
}
