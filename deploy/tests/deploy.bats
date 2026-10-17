# One throwaway stack for the whole file; the tests build on each other and run in order.
load helpers

@test "first deploy serves the app on the blue color" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == "RESULT mode=zero-downtime sha=$SHA active=blue backup="*" smoke=ok rollback=none" ]]
  [ "$(cat "$BATS_FILE_TMPDIR/smoke.log")" = "blue" ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
  run curl -fsS "http://127.0.0.1:18089/api/version"
  [[ "$output" == *"\"sha\":\"$SHA\""* ]]
  run curl -fsSI "http://127.0.0.1:18089/"
  [[ "$output" == *"Cache-Control: no-cache"* ]]
}

@test "second deploy switches to green with no failed request during the switch" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  ( end=$((SECONDS+240)); fails=0; while (( SECONDS < end )); do curl -fsS -o /dev/null "http://127.0.0.1:18089/api/health" || fails=$((fails+1)); [[ -f "$BATS_FILE_TMPDIR/stop" ]] && break; sleep 0.2; done; echo "$fails" > "$BATS_FILE_TMPDIR/fails" ) &
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  touch "$BATS_FILE_TMPDIR/stop"; wait
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == *"active=green"* ]]
  [ "$(cat "$BATS_FILE_TMPDIR/fails")" -eq 0 ]
  [ "$(tail -1 "$BATS_FILE_TMPDIR/smoke.log")" = "green" ]
  grep -qx 'ACTIVE=green' "$RUINA_STATE_DIR/colors.env"
  grep -qx 'PREVIOUS_COLOR=blue' "$RUINA_STATE_DIR/colors.env"
  run docker compose -p "$RUINA_PROJECT" ps --status running --services
  [[ "$output" != *"api-blue"* ]]
  [[ "$output" == *"api-green"* ]]
}

@test "backup writes db.dump and images.tar and keeps the newest five" {
  for i in 1 2 3 4 5 6; do "$RUINA_REPO_DIR/deploy/deploy.sh" backup >/dev/null; sleep 1; done
  [ "$(ls -1 "$RUINA_BACKUP_DIR" | wc -l)" -eq 5 ]
  newest="$RUINA_BACKUP_DIR/$(ls -1t "$RUINA_BACKUP_DIR" | head -1)"
  [ -s "$newest/db.dump" ] && [ -f "$newest/images.tar" ]
  [ "$(cat "$newest/sha")" = "$SHA" ]
}

@test "backups are private to their owner" {
  [ "$(stat -c %a "$RUINA_BACKUP_DIR")" = 700 ]
  newest="$RUINA_BACKUP_DIR/$(ls -1 "$RUINA_BACKUP_DIR" | sort -r | head -1)"
  [ "$(stat -c %a "$newest")" = 700 ]
  [ "$(stat -c %a "$newest/db.dump")" = 600 ]
}

@test "invalid numeric settings are refused" {
  RUINA_KEEP_BACKUPS=0 run "$RUINA_REPO_DIR/deploy/deploy.sh" backup
  [ "$status" -ne 0 ]
  [[ "$output" == *"RUINA_KEEP_BACKUPS"* ]]
  RUINA_HEALTH_TIMEOUT=abc run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -ne 0 ]
  [[ "$output" == *"RUINA_HEALTH_TIMEOUT"* ]]
  # Leading zeros: bash arithmetic would read 010 as octal 8.
  RUINA_HEALTH_TIMEOUT=010 run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -ne 0 ]
  [[ "$output" == *"RUINA_HEALTH_TIMEOUT"* ]]
  RUINA_KEEP_BACKUPS=05 run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -ne 0 ]
  RUINA_DRAIN_SECONDS=0 run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -eq 0 ]
}

@test "select_prunable keeps the newest N plus the current and previous" {
  run bash -c 'source "$1" && printf "a\nb\nc\nd\n<none>\ne\n" | select_prunable 2 c ""' _ "$RUINA_REPO_DIR/deploy/deploy.sh"
  [ "$status" -eq 0 ]
  [ "$output" = "$(printf 'd\ne')" ]
  run bash -c 'source "$1" && printf "a\nb\nc\n" | select_prunable 0 a b' _ "$RUINA_REPO_DIR/deploy/deploy.sh"
  [ "$output" = "c" ]
}

@test "status prints the color state and the recent backups" {
  run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -eq 0 ]
  [[ "$output" == *"ACTIVE=green"* ]]
  [[ "$output" == *"$(ls -1 "$RUINA_BACKUP_DIR" | sort -r | head -1)"* ]]
}

@test "restore loads a backup back while the active color keeps serving" {
  newest="$RUINA_BACKUP_DIR/$(ls -1 "$RUINA_BACKUP_DIR" | sort -r | head -1)"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" restore "$newest"
  echo "$output"
  [ "$status" -eq 0 ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "failed health check keeps the old color serving" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  export RUINA_HEALTH_TIMEOUT=5
  # A fake "API" that accepts --migrate but never answers /health; client/smoke tags reuse $SHA's
  # images so nothing is rebuilt for this throwaway commit.
  docker build -q -t "ruinarpg-api:$BROKEN_SHA" - >/dev/null <<'DOCKERFILE'
FROM busybox:1.36
RUN printf '#!/bin/sh\ncase " $* " in *" --migrate "*) exit 0;; esac\ntrap "exit 0" TERM\nsleep 3600 & wait\n' > /fake-api && chmod +x /fake-api
ENTRYPOINT ["/fake-api"]
DOCKERFILE
  docker tag "ruinarpg-client:$SHA" "ruinarpg-client:$BROKEN_SHA"
  docker tag "ruinarpg-smoke:$SHA" "ruinarpg-smoke:$BROKEN_SHA"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$BROKEN_SHA"
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"health check falhou"* ]]
  [[ "$output" == *"desfazendo a troca"* ]]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
  grep -qx 'ACTIVE=green' "$RUINA_STATE_DIR/colors.env"
  # The stopped color is the rollback target: its tag must still be the release it last ran.
  grep -qx "BLUE_TAG=$SHA" "$RUINA_STATE_DIR/colors.env"
  run docker compose -p "$RUINA_PROJECT" ps --status running --services
  [[ "$output" != *"api-blue"* ]]
}

@test "a failed checkout aborts the deploy before anything is built" {
  echo local-edit >> "$RUINA_REPO_DIR/README.md"
  head_before="$(git -C "$RUINA_REPO_DIR" rev-parse HEAD)"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$CONFLICT_SHA"
  git -C "$RUINA_REPO_DIR" checkout --quiet -- README.md
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"checkout falhou"* ]]
  [ "$(git -C "$RUINA_REPO_DIR" rev-parse HEAD)" = "$head_before" ]
  ! docker image inspect "ruinarpg-api:$CONFLICT_SHA" >/dev/null 2>&1
}

@test "an unknown sha is refused" {
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy 0123456789abcdef0123456789abcdef01234567
  [ "$status" -ne 0 ]
  [[ "$output" == *"commit desconhecido"* ]]
}

@test "a concurrent deploy is refused" {
  exec 9>"$RUINA_STATE_DIR/deploy.lock"; flock 9
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  flock -u 9
  [ "$status" -ne 0 ]
  [[ "$output" == *"outro deploy em andamento"* ]]
}

@test "old client releases are pruned, keeping the current and the previous" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  export RUINA_KEEP_RELEASES=1
  docker run --rm -v "${RUINA_PROJECT}_webroot:/webroot" busybox:1.36 sh -c \
    'mkdir -p /webroot/releases/fakeold /webroot/releases/fakeprev && touch -d "2020-01-01 00:00" /webroot/releases/fakeold /webroot/releases/fakeprev'
  # Pretend the serving release is "fakeprev", so it becomes PREVIOUS_SHA after this deploy.
  sed -i 's/^CURRENT_SHA=.*/CURRENT_SHA=fakeprev/' "$RUINA_STATE_DIR/colors.env"
  docker tag "ruinarpg-client:$SHA" ruinarpg-client:fakeprev
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  docker rmi ruinarpg-client:fakeprev >/dev/null
  echo "$output"
  [ "$status" -eq 0 ]
  run docker run --rm -v "${RUINA_PROJECT}_webroot:/webroot:ro" busybox:1.36 ls -1 /webroot/releases
  [[ "$output" != *fakeold* ]]
  [[ "$output" == *fakeprev* ]]
  [[ "$output" == *"$SHA"* ]]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}
