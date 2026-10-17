setup_file() {
  export PROJ="ruinarpg-bats-fp-$$"
  docker compose -p "$PROJ" -f "$BATS_TEST_DIRNAME/fixtures/pg-only.yml" up -d --wait
  docker compose -p "$PROJ" -f "$BATS_TEST_DIRNAME/fixtures/pg-only.yml" exec -T db psql -U t -d ruinarpg -v ON_ERROR_STOP=1 -c '
    CREATE TABLE "Users"("Id" int PRIMARY KEY, "Name" text);
    CREATE TABLE "Log"("Msg" text);
    CREATE TABLE "__EFMigrationsHistory"("MigrationId" text PRIMARY KEY);
    INSERT INTO "Users" VALUES (1, $$a$$), (2, NULL);
    INSERT INTO "Log" VALUES ($$x$$);
    INSERT INTO "__EFMigrationsHistory" VALUES ($$m1$$);'
}
teardown_file() { docker compose -p "$PROJ" -f "$BATS_TEST_DIRNAME/fixtures/pg-only.yml" down --volumes; }

@test "fingerprint dumps every row as JSON keyed by primary key, skipping the migrations table" {
  RUINA_COMPOSE_FILE="$BATS_TEST_DIRNAME/fixtures/pg-only.yml" RUINA_DB_USER=t \
    run "$BATS_TEST_DIRNAME/../fingerprint.sh" "$PROJ" "$BATS_TEST_TMPDIR/a.jsonl"
  [ "$status" -eq 0 ]
  run python3 -c 'import json,sys; rows=[json.loads(l) for l in open(sys.argv[1])]; print(sorted((r["table"], r["pk"]) for r in rows))' "$BATS_TEST_TMPDIR/a.jsonl"
  [ "$output" = "[('Log', None), ('Users', '1'), ('Users', '2')]" ]
  grep -q '"Name": null' "$BATS_TEST_TMPDIR/a.jsonl"
}
