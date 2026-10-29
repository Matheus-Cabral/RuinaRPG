# Static checks on the provisioning files. None of these scripts is run here.
P="$BATS_TEST_DIRNAME/../provision"
W="$BATS_TEST_DIRNAME/../../.github/workflows"
GATE_PATH=/opt/ruinarpg/gate.sh
PFX_DEPLOY='command="sudo /opt/ruinarpg/gate.sh",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding '
PFX_DUMP='command="sudo /opt/ruinarpg/gate.sh dump-only",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding '

@test "scripts parse (bash -n)" {
  for f in install-gate.sh setup-runner.sh runner-cleanup.sh; do
    [ -f "$P/$f" ]
    bash -n "$P/$f"
  done
}

@test "all runbooks exist" {
  for f in README.md producao.md runner.md github.md sudoers-ruinarpg; do [ -f "$P/$f" ]; done
}

@test "sudoers passes visudo -cf" {
  command -v visudo >/dev/null || skip "visudo ausente"
  visudo -cf "$P/sudoers-ruinarpg"
}

@test "sudoers allows exactly the two gate forms, keeps SSH_ORIGINAL_COMMAND, no SETENV" {
  grep -qF 'Defaults!/opt/ruinarpg/gate.sh env_keep += "SSH_ORIGINAL_COMMAND"' "$P/sudoers-ruinarpg"
  grep -qF 'deploy ALL=(root) NOPASSWD: /opt/ruinarpg/gate.sh "", /opt/ruinarpg/gate.sh dump-only' "$P/sudoers-ruinarpg"
  ! grep -q SETENV "$P/sudoers-ruinarpg"
}

@test "gate path is the same everywhere" {
  grep -qF "$GATE_PATH" "$P/sudoers-ruinarpg"
  grep -qF "$GATE_PATH" "$P/producao.md"
  grep -qF "$GATE_PATH" "$P/install-gate.sh"
}

@test "both authorized_keys prefixes are exact in producao.md" {
  grep -qF "$PFX_DEPLOY" "$P/producao.md"
  grep -qF "$PFX_DUMP" "$P/producao.md"
}

@test "secret names in github.md match deploy.yml and release.yml" {
  for s in DEPLOY_SSH_KEY PROD_SSH_KNOWN_HOSTS; do
    grep -qF "secrets.$s" "$W/deploy.yml"; grep -qF "$s" "$P/github.md"
  done
  grep -qF 'secrets.RELEASE_BOT_TOKEN' "$W/release.yml"; grep -qF RELEASE_BOT_TOKEN "$P/github.md"
}

@test "runner labels in runner.md match the workflows" {
  grep -qF 'runs-on: [self-hosted, ruinarpg]' "$W/deploy.yml"
  grep -qF 'runs-on: [self-hosted, ruinarpg]' "$W/release.yml"
  grep -qF 'self-hosted,ruinarpg' "$P/runner.md"
  grep -qF 'self-hosted,ruinarpg' "$P/setup-runner.sh"
}

@test "github.md names the environment, required checks and label" {
  grep -qF 'environment: production' "$W/deploy.yml"
  for t in '`production`' '`ci`' '`clone`' '`deploy:janela`'; do grep -qF "$t" "$P/github.md"; done
  grep -qE '^  ci:' "$W/release.yml"; grep -qE '^  clone:' "$W/release.yml"
}

@test "runner version is pinned with a sha256" {
  grep -qE '^RUNNER_VERSION=[0-9]+\.[0-9]+\.[0-9]+$' "$P/setup-runner.sh"
  grep -qE '^RUNNER_SHA256=[0-9a-f]{64}$' "$P/setup-runner.sh"
}

@test "offsite dir and prod host match the code" {
  grep -qF /var/backups/ruinarpg-offsite "$P/setup-runner.sh"
  grep -qF /var/backups/ruinarpg-offsite "$BATS_TEST_DIRNAME/../clone.sh"
  grep -qF 192.168.80.10 "$P/setup-runner.sh"
  grep -qF 192.168.80.10 "$BATS_TEST_DIRNAME/../clone.sh"
}

@test "dump key is pasted in producao.md step 4 (dump-only prefix), never step 5" {
  ! grep -q 'passo 5' "$P/setup-runner.sh"
  grep -q 'passo 4 de producao.md' "$P/setup-runner.sh"
  grep -q 'gate.sh dump-only' "$P/setup-runner.sh"
}

@test "producao.md: Parte 0 comes before Parte A; git runs as ruina; authorized_keys not truncated" {
  a="$(grep -n '^## Parte 0' "$P/producao.md" | cut -d: -f1)"
  b="$(grep -n '^## Parte A' "$P/producao.md" | cut -d: -f1)"
  [ -n "$a" ] && [ -n "$b" ] && [ "$a" -lt "$b" ]
  grep -qF 'sudo -u ruina git -C /home/ruina/RuinaRPG fetch origin' "$P/producao.md"
  ! grep -q '^   git -C /home/ruina' "$P/producao.md"
  grep -qF '[ -f /home/deploy/.ssh/authorized_keys ] || sudo install' "$P/producao.md"
}

@test "runner.md checks the host key fingerprint and fixes home permissions" {
  grep -qF 'ssh-keygen -lF 192.168.80.10' "$P/runner.md"
  grep -qF 'chmod 755 /home/pro' "$P/runner.md"
  grep -qF 'chmod go-w ~/.bashrc ~/.profile' "$P/runner.md"
}

@test "runner-cleanup.sh skips on inspect failure and header says cron" {
  grep -q 'cron' "$P/runner-cleanup.sh"; ! grep -q 'systemd timer' "$P/runner-cleanup.sh"
  grep -A1 'created=' "$P/runner-cleanup.sh" | grep -q '|| continue'
}

@test "setup-runner.sh final message evaluates cleanly and mentions dump-only" {
  line="$(grep -m1 '^echo .Chave publica de DUMP' "$P/setup-runner.sh")"
  run --separate-stderr bash -c "$line"
  [ "$status" -eq 0 ]
  [ -z "$stderr" ]
  [[ "$output" == *"gate.sh dump-only"* ]]
}

@test "deploy.yml: agent lifetime covers a long deploy; offsite copies counted by suffix; stale partials pruned" {
  grep -qF 'ssh-agent -s -a "$d/agent.sock" -t 7200' "$W/deploy.yml"
  grep -qF -- "-pos-deploy" "$W/deploy.yml"
  grep -qE 'regex .*pos-deploy' "$W/deploy.yml"
  grep -qF -- "-name '.*.partial' -mtime +0" "$W/deploy.yml"
}

@test "release.yml strips CONTEXT lines as well as DETAIL before uploading" {
  grep -qF "/DETAIL:/d" "$W/release.yml"
  grep -qF "/CONTEXT:/d" "$W/release.yml"
}

@test "runner-cleanup.sh never removes the images a state file names as current or previous" {
  bin="$BATS_TEST_TMPDIR/bin"; mkdir -p "$bin"
  calls="$BATS_TEST_TMPDIR/calls"; : > "$calls"
  cat > "$bin/docker" <<FAKE
#!/usr/bin/env bash
case "\$1 \$2" in
  "builder prune") ;;
  "ps -a") ;;
  "image ls") printf '%s\n' "ruinarpg-api:aaa id-a" "ruinarpg-client:bbb id-b" "ruinarpg-smoke:ccc id-c" "ruinarpg-api:ddd id-d" ;;
  "image inspect") echo 2020-01-01T00:00:00Z ;;
  "image rm") echo "rm \$3" >> "$calls" ;;
esac
FAKE
  chmod +x "$bin/docker"
  s1="$BATS_TEST_TMPDIR/one.env" s2="$BATS_TEST_TMPDIR/two.env"
  printf 'ACTIVE=blue\nCURRENT_SHA=aaa\nPREVIOUS_SHA=\n' > "$s1"
  printf 'ACTIVE=green\nCURRENT_SHA=ddd\nPREVIOUS_SHA=bbb\n' > "$s2"
  PATH="$bin:$PATH" run bash "$P/runner-cleanup.sh" "$s1" "$s2"
  echo "$output"
  [ "$status" -eq 0 ]
  [ "$(cat "$calls")" = "rm ruinarpg-smoke:ccc" ]
  # A state file that exists but cannot be read: nothing is removed.
  : > "$calls"
  locked="$BATS_TEST_TMPDIR/locked.env"; printf 'CURRENT_SHA=ccc\n' > "$locked"; chmod 000 "$locked"
  if [[ ! -r "$locked" ]]; then
    PATH="$bin:$PATH" run bash "$P/runner-cleanup.sh" "$s1" "$locked"
    [ "$status" -ne 0 ]
    [ ! -s "$calls" ]
  fi
  # A checkout that never deployed has no colors.env and nothing to protect: the cleanup goes on.
  : > "$calls"
  PATH="$bin:$PATH" run bash "$P/runner-cleanup.sh" "$s2" "$BATS_TEST_TMPDIR/missing.env"
  echo "$output"
  [ "$status" -eq 0 ]
  [ "$(cat "$calls")" = "rm ruinarpg-api:aaa
rm ruinarpg-smoke:ccc" ]
  grep -qF /home/pro/RuinaRPG/deploy/state/colors.env "$P/runner.md"
}
