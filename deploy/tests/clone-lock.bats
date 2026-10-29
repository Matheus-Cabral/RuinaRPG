# The clone lock is shared by every OS user on the host: the runner (ghrunner) and a developer run the
# same compose project on the same port. A lock file another user created must not stop a clone.
setup() {
  export TMPDIR="$BATS_TEST_TMPDIR" RUINA_CLONE_PROJECT="ruinarpg-bats-lock-$$"
  LOCK="$TMPDIR/$RUINA_CLONE_PROJECT.lock"
}

@test "teardown takes the lock even when the lock file is not writable by this user" {
  : > "$LOCK"; chmod 444 "$LOCK"
  run "$BATS_TEST_DIRNAME/../clone.sh" teardown "$BATS_TEST_TMPDIR/nao-existe"
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "$output" != *"Permission denied"* ]]
  [[ "$output" != *"ocupado"* ]]
}

@test "a lock file created by the clone can be opened by the other users of the host" {
  run "$BATS_TEST_DIRNAME/../clone.sh" teardown "$BATS_TEST_TMPDIR/nao-existe"
  [ "$status" -eq 0 ]
  [ "$(stat -c %a "$LOCK")" = 644 ]
}
