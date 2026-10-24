# Producao (192.168.80.10)

Rode como um usuario com sudo, dentro do checkout do repositorio com `deploy/` (ex.: `/home/ruina/RuinaRPG`; faca a Parte 0 primeiro).

## Parte 0: trazer `deploy/` ao checkout (producao ainda na 1.5.0)

O checkout de producao e a `main` (1.5.0) e nao tem `deploy/`, que a Parte A usa. Traga so a pasta `deploy`
como arquivos nao rastreados, como o dono do checkout (`ruina`):

0. ```bash
   sudo -u ruina git -C /home/ruina/RuinaRPG fetch origin
   sudo -u ruina git -C /home/ruina/RuinaRPG archive origin/release/2.0.0 deploy | sudo -u ruina tar -x -C /home/ruina/RuinaRPG
   ```
   Isto reaproveita os volumes `ruinarpg_pgdata` e `ruinarpg_images` pelo servico `db` do compose antigo.
   Para o passo H: rode `sudo -u ruina rm -rf /home/ruina/RuinaRPG/deploy` (arquivos nao rastreados) antes de trocar para o checkout real.
   **Verificacao:** `ls /home/ruina/RuinaRPG/deploy/deploy.sh` existe.

## Parte A: gate e sudoers

1. Crie o usuario de deploy, sem senha:
   ```bash
   sudo adduser --disabled-password --gecos "" deploy
   sudo install -d -m 755 /opt/ruinarpg
   ```
   **Verificacao:** `id deploy` mostra o usuario; `ls -ld /opt/ruinarpg` mostra `drwxr-xr-x root root`.

2. Instale o gate, o sudoers e o diretorio de backups:
   ```bash
   sudo deploy/provision/install-gate.sh
   ```
   O script copia `deploy/gate.sh` para `/opt/ruinarpg/gate.sh` (root:root 0755), instala `sudoers-ruinarpg`
   em `/etc/sudoers.d/ruinarpg` (0440, depois de `visudo -cf`) e cria `/var/backups/ruinarpg` (root, 0700).
   **Verificacao:** a ultima linha e `gate instalado em /opt/ruinarpg/gate.sh`; `sudo visudo -c` termina sem erro;
   `sudo ls -ld /var/backups/ruinarpg` mostra `drwx------ root root`.

   Os backups contem hashes de senha: `/var/backups/ruinarpg` e `root:root 0700`; backups manuais do dono exigem `sudo`.

3. Prepare o `authorized_keys` do `deploy`:
   ```bash
   sudo install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
   [ -f /home/deploy/.ssh/authorized_keys ] || sudo install -m 600 -o deploy -g deploy /dev/null /home/deploy/.ssh/authorized_keys
   ```
   **Verificacao:** `sudo ls -l /home/deploy/.ssh/authorized_keys` mostra `-rw------- deploy deploy`.

## Parte B: autorizar as duas chaves

4. A chave de **dump** vem do runner (`runner.md`, passo 5, imprime a publica). Acrescente uma linha com este prefixo exato
   seguido da chave publica inteira (`ssh-ed25519 AAAA... ghrunner-dump@ruinarpg`):
   ```
   command="sudo /opt/ruinarpg/gate.sh dump-only",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding ssh-ed25519 AAAA...
   ```
   ```bash
   echo 'command="sudo /opt/ruinarpg/gate.sh dump-only",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding <CHAVE_PUBLICA_DE_DUMP>' | sudo tee -a /home/deploy/.ssh/authorized_keys
   ```
   **Verificacao:** `sudo grep -c 'gate.sh dump-only' /home/deploy/.ssh/authorized_keys` imprime `1`.

5. A chave de **deploy** e gerada na sua maquina (`github.md`, passo 3). Acrescente a publica com este prefixo exato:
   ```
   command="sudo /opt/ruinarpg/gate.sh",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding ssh-ed25519 AAAA...
   ```
   ```bash
   echo 'command="sudo /opt/ruinarpg/gate.sh",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding <CHAVE_PUBLICA_DE_DEPLOY>' | sudo tee -a /home/deploy/.ssh/authorized_keys
   ```
   **Verificacao:** `sudo wc -l /home/deploy/.ssh/authorized_keys` imprime `2`.

## Parte C: testar o caminho do gate (antes do primeiro ensaio)

6. Teste o caminho do gate como root (sem SSH):
   ```bash
   sudo env SSH_ORIGINAL_COMMAND=status /opt/ruinarpg/gate.sh
   sudo env SSH_ORIGINAL_COMMAND=dump /opt/ruinarpg/gate.sh dump-only | tar -t
   ```
   **Verificacao:** o primeiro imprime o estado das cores; a listagem do segundo contem `db.dump` e `images.tar`.

## Verificacao final (da VM do runner, depois de `runner.md`)

Como `ghrunner` em 192.168.80.16:
```bash
ssh deploy@192.168.80.10 status        # saida do status do gate
ssh deploy@192.168.80.10 "deploy 0123456789abcdef0123456789abcdef01234567"; echo $?   # comando recusado, codigo 2
ssh deploy@192.168.80.10 bash          # comando recusado
ssh deploy@192.168.80.10 dump | tar -t # db.dump e images.tar
```
**Verificacao:** os tres primeiros comportam-se como indicado; `deploy` com a chave de dump e sempre recusado.

## Caminho manual (emergencia)

`ruina` nao esta no grupo `docker`: na producao o caminho manual e sempre com `sudo`, no checkout:
```bash
cd /home/ruina/RuinaRPG
sudo make status
sudo make rollback
sudo make deploy            # implanta o HEAD como esta; recusa alteracoes locais em arquivos versionados
sudo deploy/deploy.sh restore /var/backups/ruinarpg/<backup>
```
O log de cada `deploy`, `rollback` e `restore` (incluindo os do pipeline) fica em `deploy/state/deploy.log`.
