# Runner (esta VM, 192.168.80.16)

1. Corrija as permissoes do `~/.ssh` do usuario `pro` (estavam 777):
   ```bash
   chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys
   ```
   Tambem feche o home e os arquivos de shell, que estavam 777:
   ```bash
   chmod 755 /home/pro
   chmod go-w ~/.bashrc ~/.profile
   ```
   **Verificacao:** `stat -c '%a' ~/.ssh ~/.ssh/authorized_keys /home/pro ~/.bashrc ~/.profile` imprime `700`, `600`, `755` e dois valores sem escrita para grupo/outros (ex.: `644`).

2. Crie o usuario do runner e de acesso ao docker:
   ```bash
   sudo adduser --disabled-password --gecos "" ghrunner
   sudo usermod -aG docker ghrunner
   ```
   **Atencao:** o grupo `docker` equivale a root **nesta VM de desenvolvimento**. Aceito, porque o runner precisa dirigir o clone.
   **Verificacao:** `id ghrunner` lista `docker`.

3. Instale o .NET 8 SDK se faltar:
   ```bash
   dotnet --version || sudo apt-get install -y dotnet-sdk-8.0
   ```
   **Verificacao:** `dotnet --version` imprime `8.x`.

4. Crie o diretorio das copias de backup fora da maquina de producao:
   ```bash
   sudo install -d -m 700 -o ghrunner -g ghrunner /var/backups/ruinarpg-offsite
   ```
   **Verificacao:** `ls -ld /var/backups/ruinarpg-offsite` mostra `drwx------ ghrunner ghrunner`.

5. Registre o runner. O token vem de GitHub > repo > Settings > Actions > Runners > New self-hosted runner
   (valido por ~1 hora). Os labels registrados sao `self-hosted,ruinarpg`, os mesmos de `runs-on` em
   `release.yml` e `deploy.yml`. O script baixa o runner **v2.338.0**, confere o sha256 (da pagina da release),
   registra, gera a chave de dump `~ghrunner/.ssh/ruinarpg_dump`, configura o `~/.ssh/config` para `192.168.80.10`
   e imprime a chave publica:
   ```bash
   sudo -iu ghrunner bash /home/pro/RuinaRPG/deploy/provision/setup-runner.sh <TOKEN> https://github.com/<dono>/RuinaRPG
   ```
   (Se `ghrunner` nao ler `/home/pro`, copie antes: `sudo install -m 755 /home/pro/RuinaRPG/deploy/provision/setup-runner.sh /tmp/` e rode de la.)
   **Verificacao:** o script termina imprimindo `ssh-ed25519 ... ghrunner-dump@ruinarpg`; guarde-a para `producao.md`, passo 4.
   Para atualizar o runner no futuro, troque `RUNNER_VERSION` e `RUNNER_SHA256` no script.

6. Instale o runner como servico (precisa de sudo, por isso fora do script):
   ```bash
   cd /home/ghrunner/actions-runner && sudo ./svc.sh install ghrunner && sudo ./svc.sh start
   ```
   **Verificacao:** `sudo ./svc.sh status` mostra `active (running)`; em GitHub > Settings > Actions > Runners o runner aparece `Idle` com os labels `self-hosted`, `ruinarpg`.

7. Limpeza semanal (o `clone.sh` nao faz prune, que seria global no host):
   ```bash
   sudo install -m 755 -o ghrunner -g ghrunner /home/pro/RuinaRPG/deploy/provision/runner-cleanup.sh /home/ghrunner/runner-cleanup.sh
   echo '0 4 * * 0 /home/ghrunner/runner-cleanup.sh /home/pro/RuinaRPG/deploy/state/colors.env >> /home/ghrunner/runner-cleanup.log 2>&1' | sudo -u ghrunner crontab -
   ```
   Os argumentos sao os `deploy/state/colors.env` de cada checkout que faz deploy neste host (aqui, a stack de dev em `/home/pro/RuinaRPG`). As imagens de `CURRENT_SHA` e `PREVIOUS_SHA` desses arquivos nunca sao removidas, mesmo paradas ha mais de 7 dias: sao a release ativa e o alvo do rollback. Se um checkout novo passar a fazer deploy aqui, acrescente o `colors.env` dele na linha do cron. Um arquivo listado que existe mas o `ghrunner` nao consegue ler aborta a limpeza antes de remover qualquer imagem; um que ainda nao existe (checkout sem deploy) e pulado.
   **Verificacao:** `sudo -u ghrunner crontab -l` mostra a linha; `sudo -u ghrunner /home/ghrunner/runner-cleanup.sh /home/pro/RuinaRPG/deploy/state/colors.env` termina sem erro.

8. Confira a chave do host de producao (o `setup-runner.sh` a registrou sem verificar, trust-on-first-use):
   ```bash
   sudo -u ghrunner ssh-keygen -lF 192.168.80.10
   ```
   e, em producao, `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`.
   **Verificacao:** as duas impressoes digitais (`SHA256:...`) sao identicas. Se diferirem, apague a linha com `sudo -u ghrunner ssh-keygen -R 192.168.80.10` e investigue.

9. Depois de autorizar a chave de dump em producao (`producao.md`, parte B), teste:
   `sudo -iu ghrunner ssh deploy@192.168.80.10 status`.
   **Verificacao:** imprime o status do gate (e nao pede senha).
