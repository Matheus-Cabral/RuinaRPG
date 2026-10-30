# Static checks on signal handling in deploy.sh. Nothing is deployed here.
D="$BATS_TEST_DIRNAME/../deploy.sh"

# Prints the body of a shell function, from its "name() {" line to the closing "}" at column 0.
corpo() { awk -v f="$1" '$0 ~ "^"f"\\(\\) \\{" {on=1} on {print} on && /^}/ {exit}' "$D"; }

# Line number (inside the body) of the first line matching a fixed string.
linha() { grep -nF -- "$2" <<< "$1" | head -1 | cut -d: -f1; }

# Signals are ignored BEFORE the undo is disarmed: a signal landing between the two would otherwise exit
# with nothing undone and the state never saved.
assert_ignora_antes_de_desarmar() {
  local body ignora desarma
  body="$(corpo "$1")"
  [ -n "$body" ]
  ignora="$(linha "$body" "trap '' HUP INT TERM")"
  desarma="$(linha "$body" 'UNDO_ARMED=""')"
  echo "$1: trap '' na linha $ignora, UNDO_ARMED=\"\" na linha $desarma"
  [ -n "$ignora" ]
  [ -n "$desarma" ]
  [ "$ignora" -lt "$desarma" ]
}

@test "undo_on_failure ignores signals before disarming the undo" {
  assert_ignora_antes_de_desarmar undo_on_failure
}

@test "smoke_failed ignores signals before disarming the undo" {
  assert_ignora_antes_de_desarmar smoke_failed
}

@test "the commit of a deploy ignores signals before disarming the undo" {
  assert_ignora_antes_de_desarmar cmd_deploy
}
