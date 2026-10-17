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
