# The gate is the SSH forced command: a strict grammar in front of deploy.sh. No docker needed.
GATE="$BATS_TEST_DIRNAME/../gate.sh"

setup() {
  T="$BATS_TEST_TMPDIR"
  git init -q --bare "$T/origin.git"
  git init -q -b main "$T/repo"
  git -C "$T/repo" config user.email t@t; git -C "$T/repo" config user.name t
  git -C "$T/repo" remote add origin "$T/origin.git"
  git -C "$T/repo" commit -q --allow-empty -m main1
  M="$(git -C "$T/repo" rev-parse HEAD)"
  git -C "$T/repo" push -q origin main
  git -C "$T/repo" checkout -q -b side
  git -C "$T/repo" commit -q --allow-empty -m side
  S="$(git -C "$T/repo" rev-parse HEAD)"
  git -C "$T/repo" push -q origin side
  git -C "$T/repo" checkout -q main
  mkdir "$T/backup"; echo data > "$T/backup/db.dump"
  CALLS="$T/calls"; : > "$CALLS"
  cat > "$T/fake-deploy.sh" <<FAKE
#!/usr/bin/env bash
echo "\$*" >> "$CALLS"
[[ "\$1" == backup ]] && echo "$T/backup"
exit 0
FAKE
  chmod +x "$T/fake-deploy.sh"
  export RUINA_REPO_DIR="$T/repo" RUINA_DEPLOY="$T/fake-deploy.sh"
  unset RUINA_SKIP_FETCH
}

@test "rejects unknown verbs"            { SSH_ORIGINAL_COMMAND="bash" run "$GATE"; [ "$status" -eq 2 ]; [[ "$output" == *"comando recusado"* ]]; }
@test "rejects an empty command"         { SSH_ORIGINAL_COMMAND="" run "$GATE"; [ "$status" -eq 2 ]; }
@test "rejects an unset command"         { unset SSH_ORIGINAL_COMMAND; run "$GATE"; [ "$status" -eq 2 ]; }
@test "rejects command chaining"         { SSH_ORIGINAL_COMMAND="deploy $M; rm -rf /" run "$GATE"; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
@test "rejects newline injection"        { SSH_ORIGINAL_COMMAND=$'status\nrm -rf /' run "$GATE"; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
@test "rejects newline after a valid deploy" { SSH_ORIGINAL_COMMAND=$"deploy $M"$'\nstatus' run "$GATE"; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
@test "rejects extra tokens"             { SSH_ORIGINAL_COMMAND="status now" run "$GATE"; [ "$status" -eq 2 ]; }
@test "rejects a malformed sha"          { SSH_ORIGINAL_COMMAND="deploy abc" run "$GATE"; [ "$status" -eq 2 ]; }
@test "rejects an uppercase sha"         { SSH_ORIGINAL_COMMAND="deploy ${M^^}" run "$GATE"; [ "$status" -eq 2 ]; }
@test "rejects sha not on main"          { SSH_ORIGINAL_COMMAND="deploy $S" run "$GATE"; [ "$status" -eq 3 ]; [[ "$output" == *"sha não está na main"* ]]; [ ! -s "$CALLS" ]; }
@test "deploys a sha on main"            { SSH_ORIGINAL_COMMAND="deploy $M" run "$GATE"; [ "$status" -eq 0 ]; grep -qx "deploy $M" "$CALLS"; }
@test "passes --window through"          { SSH_ORIGINAL_COMMAND="deploy $M --window" run "$GATE"; [ "$status" -eq 0 ]; grep -qx "deploy $M --window" "$CALLS"; }
@test "dump streams a tar of the backup" { SSH_ORIGINAL_COMMAND="dump" run bash -c "$GATE | tar -tf -"; [[ "$output" == *"db.dump"* ]]; grep -qx backup "$CALLS"; }
@test "dump stdout carries only the tar"  { SSH_ORIGINAL_COMMAND="dump" bash -c "$GATE 2>/dev/null" > "$T/out.tar"; tar -xOf "$T/out.tar" ./db.dump | grep -qx data; }
@test "status is passed through"         { SSH_ORIGINAL_COMMAND="status" run "$GATE"; [ "$status" -eq 0 ]; grep -qx "status" "$CALLS"; }

@test "rejects non-ASCII 'hex' under a UTF-8 locale" {
  locale -a | grep -qi '^en_US.utf8$' || skip "locale en_US.UTF-8 ausente"
  w=$(printf 'é%.0s' {1..40}); LC_ALL=en_US.UTF-8 SSH_ORIGINAL_COMMAND="deploy $w" run "$GATE"
  [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]
}

# Second key (R8): the runner's on-disk key is forced to `gate.sh dump-only` — it can read, never deploy.
@test "dump-only refuses deploy"         { SSH_ORIGINAL_COMMAND="deploy $M" run "$GATE" dump-only; [ "$status" -eq 2 ]; [[ "$output" == *"comando recusado"* ]]; [ ! -s "$CALLS" ]; }
@test "dump-only refuses deploy --window" { SSH_ORIGINAL_COMMAND="deploy $M --window" run "$GATE" dump-only; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
@test "dump-only still dumps"            { SSH_ORIGINAL_COMMAND="dump" run bash -c "$GATE dump-only | tar -tf -"; [[ "$output" == *"db.dump"* ]]; grep -qx backup "$CALLS"; }
@test "dump-only still answers status"   { SSH_ORIGINAL_COMMAND="status" run "$GATE" dump-only; [ "$status" -eq 0 ]; grep -qx "status" "$CALLS"; }
@test "dump-only keeps the grammar"      { SSH_ORIGINAL_COMMAND="bash" run "$GATE" dump-only; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
@test "rejects an unknown gate mode"     { SSH_ORIGINAL_COMMAND="status" run "$GATE" everything; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
@test "rejects extra gate arguments"     { SSH_ORIGINAL_COMMAND="status" run "$GATE" dump-only extra; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
