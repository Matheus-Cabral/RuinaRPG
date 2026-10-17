#!/usr/bin/env bash
# Roda NO RUNNER (192.168.80.16) como ghrunner. Uso: setup-runner.sh <TOKEN_DE_REGISTRO> <URL_DO_REPO>
# O servico systemd (svc.sh) precisa de sudo e ghrunner nao tem: runner.md o instala depois, com o seu usuario.
# Token: GitHub > repo > Settings > Actions > Runners > New self-hosted runner (vale ~1 hora).
set -euo pipefail

RUNNER_VERSION=2.338.0
# sha256 do linux-x64, da pagina da release v2.338.0
RUNNER_SHA256=af4b794c1bc41d73d40535e3fe092a39f9679cd8d965954c2aca25a05ca41d32
LABELS=self-hosted,ruinarpg
PROD_HOST=192.168.80.10
OFFSITE=/var/backups/ruinarpg-offsite
KEY="$HOME/.ssh/ruinarpg_dump"
DIR="$HOME/actions-runner"

token="${1:?uso: setup-runner.sh <token> <https://github.com/dono/repo>}"
url="${2:?uso: setup-runner.sh <token> <https://github.com/dono/repo>}"
[[ "$(id -un)" == ghrunner ]] || { echo "rode como ghrunner" >&2; exit 1; }

# 1. Runner da Actions, versao fixa, sha256 conferido.
mkdir -p "$DIR" && cd "$DIR"
tgz="actions-runner-linux-x64-$RUNNER_VERSION.tar.gz"
curl -fsSLO "https://github.com/actions/runner/releases/download/v$RUNNER_VERSION/$tgz"
echo "$RUNNER_SHA256  $tgz" | sha256sum -c -
tar -xzf "$tgz" && rm "$tgz"
./config.sh --unattended --url "$url" --token "$token" --labels "$LABELS" --name ruinarpg-runner --replace

# 2. Chave SOMENTE de dump (no disco do runner). A chave de deploy nunca fica aqui.
mkdir -p "$HOME/.ssh" && chmod 700 "$HOME/.ssh"
[[ -f "$KEY" ]] || ssh-keygen -t ed25519 -N "" -C "ghrunner-dump@ruinarpg" -f "$KEY"
touch "$HOME/.ssh/config" && chmod 600 "$HOME/.ssh/config"
grep -q "^Host $PROD_HOST\$" "$HOME/.ssh/config" || cat >> "$HOME/.ssh/config" <<CONFIG
Host $PROD_HOST
  User deploy
  IdentityFile $KEY
  IdentitiesOnly yes
CONFIG
ssh-keyscan -t ed25519 "$PROD_HOST" >> "$HOME/.ssh/known_hosts" 2>/dev/null
chmod 600 "$HOME/.ssh/known_hosts"

# 3. O diretorio das copias fora da maquina ($OFFSITE) precisa de root: runner.md cria antes deste script.
[[ -O "$OFFSITE" && -w "$OFFSITE" ]] || { echo "crie $OFFSITE como ghrunner 0700 (runner.md, passo 4)" >&2; exit 1; }

# 4. .NET 8 SDK (o job ci compila e testa).
command -v dotnet >/dev/null && dotnet --version | grep -q '^8\.' || {
  echo "ATENCAO: .NET 8 SDK ausente. Instale: sudo apt-get install -y dotnet-sdk-8.0 (veja runner.md)" >&2
}

echo
echo "Chave publica de DUMP (cole no passo 5 de producao.md):"
cat "$KEY.pub"
