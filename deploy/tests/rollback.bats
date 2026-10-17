# Window mode and rollback (Técnico R0014–R0015). One throwaway stack, separate from deploy.bats; the
# tests build on each other and run in order.
load helpers

psql_count() {
  docker compose -p "$RUINA_PROJECT" exec -T db sh -c 'psql -U "$POSTGRES_USER" -d ruinarpg -Atc "SELECT count(*) FROM smoke_marker"'
}

@test "setup: first deploy" {
  RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh" run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  echo "$output"
  [ "$status" -eq 0 ]
}

@test "zero-downtime rollback keeps rows written after the switch" {
  # A smoke that writes a row while the new color is live, then fails — the row must survive the rollback.
  export RUINA_SMOKE_CMD="docker compose -p $RUINA_PROJECT exec -T db sh -c 'psql -U \"\$POSTGRES_USER\" -d ruinarpg -c \"CREATE TABLE IF NOT EXISTS smoke_marker(x int); INSERT INTO smoke_marker VALUES (1)\"' && exit 1"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "${lines[-1]}" == *"rollback=ok"* ]]
  [[ "${lines[-1]}" == *"active=blue"* ]]
  run psql_count
  [ "$output" = "1" ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "window deploy answers 503 with the maintenance header while switching, then serves the new color" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  ( for _ in $(seq 1 600); do curl -s -o /dev/null -D - "http://127.0.0.1:18089/api/health" >> "$BATS_FILE_TMPDIR/window-headers"; sleep 0.2; [[ -f "$BATS_FILE_TMPDIR/stop2" ]] && break; done ) &
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA" --window
  touch "$BATS_FILE_TMPDIR/stop2"; wait
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == *"mode=window"*"active=green"* ]]
  grep -qi "X-Ruina-Manutencao: 1" "$BATS_FILE_TMPDIR/window-headers"
  grep -qx 'MODE=window' "$RUINA_STATE_DIR/colors.env"
}

@test "window rollback restores the backup taken inside the window" {
  export RUINA_SMOKE_CMD="docker compose -p $RUINA_PROJECT exec -T db sh -c 'psql -U \"\$POSTGRES_USER\" -d ruinarpg -c \"DROP TABLE smoke_marker\"' && exit 1"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA" --window
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "${lines[-1]}" == *"rollback=ok"* ]]
  [[ "${lines[-1]}" == *"active=green"* ]]
  # The restore point is the backup taken inside the window, the one RESULT reports.
  bk="${lines[-1]#*backup=}"; bk="${bk%% *}"
  [[ "$output" == *"restaurando o banco de $bk"* ]]
  run psql_count
  [ "$output" = "1" ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
  # The failed deploy must not have replaced the restore point of the deploy that is still live.
  grep -qx 'MODE=window' "$RUINA_STATE_DIR/colors.env"
  grep -qx "BLUE_TAG=$SHA" "$RUINA_STATE_DIR/colors.env"
}

@test "rollback refuses, changing nothing, when the window backup is gone" {
  wb="$(sed -n 's/^WINDOW_BACKUP=//p' "$RUINA_STATE_DIR/colors.env")"
  [ -d "$wb" ]
  mv "$wb" "$wb.moved"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" rollback
  mv "$wb.moved" "$wb"
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"nada foi alterado"* ]]
  grep -qx 'ACTIVE=green' "$RUINA_STATE_DIR/colors.env"
  grep -qx "BLUE_TAG=$SHA" "$RUINA_STATE_DIR/colors.env"
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "rollback after a window deploy restores that window's backup and retags the previous color" {
  docker compose -p "$RUINA_PROJECT" exec -T db sh -c 'psql -U "$POSTGRES_USER" -d ruinarpg -c "INSERT INTO smoke_marker VALUES (2)"'
  # The previous color's tag must come from PREVIOUS_SHA, not from whatever colors.env holds.
  sed -i 's/^BLUE_TAG=.*/BLUE_TAG=bogus/' "$RUINA_STATE_DIR/colors.env"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" rollback
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == "RESULT mode=window sha=$SHA active=blue "*" rollback=ok" ]]
  # What the restore discards is kept in a fresh backup.
  [[ "$output" == *"dados descartados pelo rollback salvos em "* ]]
  saved="$(sed -n 's/.*dados descartados pelo rollback salvos em //p' <<<"$output" | tail -1)"
  [ -s "$saved/db.dump" ]
  grep -qx "BLUE_TAG=$SHA" "$RUINA_STATE_DIR/colors.env"
  grep -qx 'ACTIVE=blue' "$RUINA_STATE_DIR/colors.env"
  run psql_count
  [ "$output" = "1" ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
  run docker compose -p "$RUINA_PROJECT" ps --status running --services
  [[ "$output" != *"api-green"* ]]
}

@test "rollback after a zero-downtime deploy keeps rows written since" {
  RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh" run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == *"active=green"* ]]
  docker compose -p "$RUINA_PROJECT" exec -T db sh -c 'psql -U "$POSTGRES_USER" -d ruinarpg -c "INSERT INTO smoke_marker VALUES (3)"'
  sed -i 's/^BLUE_TAG=.*/BLUE_TAG=bogus/' "$RUINA_STATE_DIR/colors.env"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" rollback
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == "RESULT mode=zero-downtime sha=$SHA active=blue "*" rollback=ok" ]]
  run psql_count
  [ "$output" = "2" ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
  run docker compose -p "$RUINA_PROJECT" ps --status running --services
  [[ "$output" != *"api-green"* ]]
}

@test "a corrupt dump fails the restore and leaves the data intact" {
  good="$("$RUINA_REPO_DIR/deploy/deploy.sh" backup)"
  bad="$BATS_FILE_TMPDIR/corrupt"; mkdir -p "$bad"
  head -c "$(( $(stat -c %s "$good/db.dump") / 2 ))" "$good/db.dump" > "$bad/db.dump"
  cp "$good/images.tar" "$bad/images.tar"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" restore "$bad"
  echo "$output"
  [ "$status" -ne 0 ]
  run psql_count
  [ "$output" = "2" ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "rollback with nothing to go back to is refused" {
  run "$RUINA_REPO_DIR/deploy/deploy.sh" rollback
  [ "$status" -ne 0 ]
  [[ "$output" == *"não há deploy anterior"* ]]
}
