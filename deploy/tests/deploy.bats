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

@test "backup writes db.dump and images.tar and keeps the newest five plus the last deploy's" {
  last="$(sed -n 's/^LAST_BACKUP=//p' "$RUINA_STATE_DIR/colors.env")"
  for i in 1 2 3 4 5 6; do "$RUINA_REPO_DIR/deploy/deploy.sh" backup >/dev/null; sleep 1; done
  [ "$(ls -1 "$RUINA_BACKUP_DIR" | wc -l)" -eq 6 ]
  [ -d "$last" ]
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
  RUINA_SMOKE_TIMEOUT=0 run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -ne 0 ]
  [[ "$output" == *"RUINA_SMOKE_TIMEOUT"* ]]
  RUINA_SMOKE_TIMEOUT=5m run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -ne 0 ]
  RUINA_DRAIN_SECONDS=0 run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -eq 0 ]
}

@test "select_prunable keeps the newest N plus the current and previous, and only ever names sha tags" {
  a="$(printf 'a%.0s' {1..40})" b="$(printf 'b%.0s' {1..40})" c="$(printf 'c%.0s' {1..40})"
  d="$(printf 'd%.0s' {1..40})" e="$(printf 'e%.0s' {1..40})"
  run bash -c 'source "$1" && printf "%s\n" "$2" "$3" "$4" "$5" "<none>" "$6" | select_prunable 2 "$4" ""' _ "$RUINA_REPO_DIR/deploy/deploy.sh" "$a" "$b" "$c" "$d" "$e"
  [ "$status" -eq 0 ]
  [ "$output" = "$(printf '%s\n%s' "$d" "$e")" ]
  run bash -c 'source "$1" && printf "%s\n" "$2" "$3" "$4" | select_prunable 0 "$2" "$3"' _ "$RUINA_REPO_DIR/deploy/deploy.sh" "$a" "$b" "$c"
  [ "$output" = "$c" ]
  # Non-sha tags (1.5.0's ruinarpg-api:latest, a hand-made tag) are neither counted nor removed.
  run bash -c 'source "$1" && printf "%s\n" latest "$2" "$3" "$4" 1.5.0 "$(echo "$4" | tr a-f A-F)" "${4:0:39}" | select_prunable 2 "" ""' _ "$RUINA_REPO_DIR/deploy/deploy.sh" "$a" "$b" "$c"
  [ "$status" -eq 0 ]
  [ "$output" = "$c" ]
}

@test "build_images builds the three targets of deploy/Dockerfile with one GIT_SHA, skipping tags that exist" {
  run bash -c '
    source "$1"
    log() { :; }
    docker() {
      if [[ "$1 $2" == "image inspect" ]]; then [[ "$3" == ruinarpg-client:* ]]; return; fi
      echo "docker $*"
    }
    RUINA_REPO_DIR=/repo build_images abc123' _ "$RUINA_REPO_DIR/deploy/deploy.sh"
  [ "$status" -eq 0 ]
  [ "${#lines[@]}" -eq 2 ]
  [ "${lines[0]}" = "docker build -q -f /repo/deploy/Dockerfile --target api --build-arg GIT_SHA=abc123 -t ruinarpg-api:abc123 /repo" ]
  [ "${lines[1]}" = "docker build -q -f /repo/deploy/Dockerfile --target smoke --build-arg GIT_SHA=abc123 -t ruinarpg-smoke:abc123 /repo" ]
}

@test "prune_backups never removes the window backup or the last deploy's backup" {
  d="$BATS_TEST_TMPDIR/prune"; mkdir -p "$d"
  for n in 1 2 3 4 5; do mkdir "$d/2026010${n}T000000Z-abc"; done
  # A backup that died halfway leaves a hidden .partial: removed once it is over a day old.
  mkdir "$d/.20260101T000000Z-abc.partial" "$d/.20260106T000000Z-abc.partial"
  touch -d '2 days ago' "$d/.20260101T000000Z-abc.partial"
  run bash -c 'source "$1" && RUINA_BACKUP_DIR="$2" RUINA_KEEP_BACKUPS=2 WINDOW_BACKUP="$2/20260101T000000Z-abc" LAST_BACKUP="$2/20260102T000000Z-abc" prune_backups' _ "$RUINA_REPO_DIR/deploy/deploy.sh" "$d"
  [ "$status" -eq 0 ]
  [ "$(ls -1 "$d" | tr '\n' ' ')" = "20260101T000000Z-abc 20260102T000000Z-abc 20260104T000000Z-abc 20260105T000000Z-abc " ]
  [ ! -e "$d/.20260101T000000Z-abc.partial" ]
  [ -d "$d/.20260106T000000Z-abc.partial" ]
}

@test "status prints the color state and the recent backups" {
  run "$RUINA_REPO_DIR/deploy/deploy.sh" status
  [ "$status" -eq 0 ]
  [[ "$output" == *"ACTIVE=green"* ]]
  [[ "$output" == *"$(ls -1 "$RUINA_BACKUP_DIR" | sort -r | head -1)"* ]]
}

@test "restore runs under maintenance and brings the active color back" {
  newest="$RUINA_BACKUP_DIR/$(ls -1 "$RUINA_BACKUP_DIR" | sort -r | head -1)"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" restore "$newest"
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "$output" == *"nginx -> manutenção"* ]]
  [[ "$output" == *"nginx -> api-green"* ]]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "a restore whose color does not come back exits 2 and points upstream.conf back at the active color" {
  newest="$RUINA_BACKUP_DIR/$(ls -1 "$RUINA_BACKUP_DIR" | sort -r | head -1)"
  # api-green cannot start: its image tag does not exist.
  sed -i 's/^GREEN_TAG=.*/GREEN_TAG=bats-missing-tag/' "$RUINA_STATE_DIR/colors.env"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" restore "$newest"
  sed -i "s/^GREEN_TAG=.*/GREEN_TAG=$SHA/" "$RUINA_STATE_DIR/colors.env"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"o nginx pode estar em manutenção. Rode deploy.sh status"* ]]
  [ "$(cat "$RUINA_STATE_DIR/upstream.conf")" = "upstream app { server api-green:8080; }" ]
  # With the tag back, a new restore brings the site back.
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
  # Untracked, so the dirty-tree check lets it through and the checkout itself fails.
  echo local > "$RUINA_REPO_DIR/conflict.txt"
  head_before="$(git -C "$RUINA_REPO_DIR" rev-parse HEAD)"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$CONFLICT_SHA"
  rm "$RUINA_REPO_DIR/conflict.txt"
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"checkout falhou"* ]]
  [ "$(git -C "$RUINA_REPO_DIR" rev-parse HEAD)" = "$head_before" ]
  ! docker image inspect "ruinarpg-api:$CONFLICT_SHA" >/dev/null 2>&1
}

@test "a dirty tracked tree is refused before anything changes" {
  echo local-edit >> "$RUINA_REPO_DIR/README.md"
  head_before="$(git -C "$RUINA_REPO_DIR" rev-parse HEAD)"
  backups_before="$(ls -1 "$RUINA_BACKUP_DIR" | wc -l)"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  git -C "$RUINA_REPO_DIR" checkout --quiet -- README.md
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"alterações locais"* ]]
  [ "$(git -C "$RUINA_REPO_DIR" rev-parse HEAD)" = "$head_before" ]
  [ "$(ls -1 "$RUINA_BACKUP_DIR" | wc -l)" -eq "$backups_before" ]
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
  # Release directories are named by sha; only sha names are ever pruned.
  fakeold="$(printf '0%.0s' {1..39})1" fakeprev="$(printf '0%.0s' {1..39})2"
  docker run --rm -v "${RUINA_PROJECT}_webroot:/webroot" busybox:1.36 sh -c \
    "mkdir -p /webroot/releases/$fakeold /webroot/releases/$fakeprev && touch -d '2020-01-01 00:00' /webroot/releases/$fakeold /webroot/releases/$fakeprev"
  # Pretend the serving release is "fakeprev", so it becomes PREVIOUS_SHA after this deploy.
  sed -i "s/^CURRENT_SHA=.*/CURRENT_SHA=$fakeprev/" "$RUINA_STATE_DIR/colors.env"
  docker tag "ruinarpg-client:$SHA" "ruinarpg-client:$fakeprev"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  docker rmi "ruinarpg-client:$fakeprev" >/dev/null
  echo "$output"
  [ "$status" -eq 0 ]
  run docker run --rm -v "${RUINA_PROJECT}_webroot:/webroot:ro" busybox:1.36 ls -1 /webroot/releases
  [[ "$output" != *"$fakeold"* ]]
  [[ "$output" == *"$fakeprev"* ]]
  [[ "$output" == *"$SHA"* ]]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

active_color() { sed -n 's/^ACTIVE=//p' "$RUINA_STATE_DIR/colors.env"; }
other_of() { if [ "$1" = blue ]; then echo green; else echo blue; fi; }

# The undo left the old color in front: state, upstream.conf, running containers and nginx agree.
assert_old_color_serving() {
  local old="$1" new; new="$(other_of "$1")"
  grep -qx "ACTIVE=$old" "$RUINA_STATE_DIR/colors.env"
  [ "$(cat "$RUINA_STATE_DIR/upstream.conf")" = "upstream app { server api-$old:8080; }" ]
  run docker compose -p "$RUINA_PROJECT" ps --status running --services
  [[ "$output" == *"api-$old"* ]]
  [[ "$output" != *"api-$new"* ]]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "a deploy whose stderr reader goes away after the switch still undoes it, and deploy.log keeps the whole run" {
  old="$(active_color)"
  log="$RUINA_STATE_DIR/deploy.log"
  start=$(( $(wc -l < "$log" 2>/dev/null || echo 0) + 1 ))
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh" FAKE_SMOKE_EXIT=1
  # The reader (the SSH channel, in production) exits as soon as nginx switches.
  run bash -c '"$1" deploy "$2" 2> >(sed -n "/nginx -> api-/q")' _ "$RUINA_REPO_DIR/deploy/deploy.sh" "$SHA"
  echo "$output"; tail -n +"$start" "$log"
  [ "$status" -eq 1 ]
  [[ "${lines[-1]}" == "RESULT mode=zero-downtime sha=$SHA active=$old "*" smoke=failed rollback=ok" ]]
  assert_old_color_serving "$old"
  tail -n +"$start" "$log" | grep -q "nginx -> api-$(other_of "$old")"
  tail -n +"$start" "$log" | grep -q "smoke falhou"
  tail -n +"$start" "$log" | grep -q "rollback concluído: api-$old"
  tail -n +"$start" "$log" | grep -q "^RESULT .*rollback=ok"
}

@test "a TERM mid-deploy undoes the switch; a checkout already at the sha keeps its branch" {
  old="$(active_color)"
  git -C "$RUINA_REPO_DIR" checkout --quiet -B bats-dev "$SHA"
  # Untracked files never block a deploy.
  touch "$RUINA_REPO_DIR/untracked-is-fine"
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/term-deploy-smoke.sh"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  rm "$RUINA_REPO_DIR/untracked-is-fine"
  echo "$output"
  # The signal turns into a failure (143) that the undo handles; the run then ends as any rolled-back deploy.
  [ "$status" -eq 1 ]
  [[ "$output" == *"deploy falhou (código 143): desfazendo a troca"* ]]
  [[ "${lines[-1]}" == *"rollback=ok" ]]
  assert_old_color_serving "$old"
  [ "$(git -C "$RUINA_REPO_DIR" symbolic-ref --short HEAD)" = bats-dev ]
}

@test "a smoke that runs past RUINA_SMOKE_TIMEOUT is a failed smoke and rolls back" {
  old="$(active_color)"
  export RUINA_SMOKE_TIMEOUT=3 RUINA_SMOKE_CMD='sleep 120'
  start=$SECONDS
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  echo "$output"
  [ "$status" -eq 1 ]
  (( SECONDS - start < 120 ))
  [[ "$output" == *"smoke excedeu 3s"* ]]
  [[ "${lines[-1]}" == *"smoke=failed rollback=ok" ]]
  assert_old_color_serving "$old"
}
