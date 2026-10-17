# GitHub

Tudo em Settings do repositorio.

1. **Settings > Actions > General > Fork pull request workflows**: marque
   "Require approval for all outside collaborators" (nao o padrao "first-time contributors").
   **Verificacao:** a opcao aparece salva.

2. **Settings > Environments > New environment** `production`. Em *Deployment branches and tags* escolha
   "Selected branches and tags" e adicione apenas `main`. Esta e a barreira real: PRs rodam com os arquivos
   de workflow do proprio PR, mas o Environment so libera os segredos para a `main`.
   **Verificacao:** a regra lista somente `main`.

3. Gere a **chave de deploy na sua maquina** (ou num diretorio temporario), nunca no runner:
   ```bash
   d="$(mktemp -d)" && ssh-keygen -t ed25519 -N "" -C "ruinarpg-deploy" -f "$d/ruinarpg_deploy"
   cat "$d/ruinarpg_deploy.pub"   # va para producao.md, passo 5
   ```
   Cole o conteudo de `$d/ruinarpg_deploy` (privada) no segredo do passo 4 e **apague**: `shred -u "$d/ruinarpg_deploy"; rm -rf "$d"`.
   **Verificacao:** `ls "$d"` nao existe mais.

4. No Environment `production`, em *Environment secrets*:
   - `DEPLOY_SSH_KEY`: a chave privada do passo 3.
   - `PROD_SSH_KNOWN_HOSTS`: a saida de `ssh-keyscan -t ed25519 192.168.80.10` (a chave e o IP, pois o `deploy.yml` conecta por `HostName 192.168.80.10`). Confira a impressao digital com `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` em producao.
   **Verificacao:** os dois nomes aparecem em *Environment secrets*.

5. Em **Settings > Secrets and variables > Actions > Repository secrets**: `RELEASE_BOT_TOKEN`, um PAT
   fine-grained restrito a este repositorio com *Contents: Read and write* e *Pull requests: Read and write*.
   So e usado em runners hospedados pelo GitHub (`ubuntu-latest`), nunca no runner proprio.
   **Verificacao:** `RELEASE_BOT_TOKEN` aparece em *Repository secrets*.

6. **Settings > General > Pull Requests**: marque "Allow auto-merge". **Settings > Branches > Add rule** para `main`:
   "Require status checks to pass" com os checks `ci` e `clone` (nomes dos jobs de `release.yml`).
   **Verificacao:** um PR mostra `ci` e `clone` como obrigatorios e o botao de auto-merge.

7. Crie o label `deploy:janela` (Issues > Labels > New label). Um PR com esse label faz o deploy usar `--window`.
   **Verificacao:** o label existe na lista.
