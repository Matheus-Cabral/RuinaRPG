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
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
  grep -qx 'ACTIVE=green' "$RUINA_STATE_DIR/colors.env"
  run docker compose -p "$RUINA_PROJECT" ps --status running --services
  [[ "$output" != *"api-blue"* ]]
}

@test "a concurrent deploy is refused" {
  exec 9>"$RUINA_STATE_DIR/deploy.lock"; flock 9
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  flock -u 9
  [ "$status" -ne 0 ]
  [[ "$output" == *"outro deploy em andamento"* ]]
}
