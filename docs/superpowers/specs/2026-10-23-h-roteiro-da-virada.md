# Roteiro da virada da produção: 1.5.0 → 2.0.0 (sub-projeto H)

Este é o roteiro executável da virada. Ele transforma em passos numerados o H.1–H.6 do spec
[`2026-10-23-versao-2-0-0-h-virada-design.md`](2026-10-23-versao-2-0-0-h-virada-design.md) e os 12 passos e
as notas de B e C de [`2026-10-17-versao-2-0-0-h-notas-do-cutover.md`](2026-10-17-versao-2-0-0-h-notas-do-cutover.md).
Cada passo traz o **comando exato**, o que na saída quer dizer que **passou**, o que fazer **se falhar** e se
precisa do **aval do usuário** ("pode seguir") no momento. Diferenças entre a produção e o ensaio nesta VM
estão marcadas em cada passo com **Ensaio:**.

Cada passo executado vai para o registro [`2026-10-23-h-registro-da-virada.md`](2026-10-23-h-registro-da-virada.md)
(comando, saída relevante, quem aprovou, horário). Nada de dado pessoal no registro: só caminhos, contagens,
shas e as linhas `OK`/`RESULT`.

## Regras que valem em todos os passos

- **Todo comando de produção é mostrado ao usuário antes de rodar.** Os passos marcados "Aval: **sim**"
  só rodam depois do "pode seguir" do usuário naquele momento (parar, checkout, deploy, restaurar,
  qualquer escrita na produção).
- **Nunca** rodar o `--migrate` da 1.5.0 (`make migrate`, `make deploy` da 1.5.0, `deploy.sh migrate` com a
  1.5.0 ativa) contra um banco cujo histórico já tem a baseline da 2.0.0 (notas de B).
- **Nada da 1.5.0 é apagado** (contêineres, imagens, volumes, pacote, dump final) antes do critério de
  aceite: 2.0.0 sem perda de dados **e** 2.1.0 lançada.
- **Voltar para a 1.5.0 só se houver perda de dados** (seção 7). Perda descoberta depois do uso real é
  reparada trazendo as linhas de volta para a 2.0.0 (seção 8).
- Pacotes, dumps e fingerprints têm dados pessoais: só na produção e nesta VM, diretórios 700 / arquivos
  600, nunca no git nem no GitHub.
- Um passo que falha **para o roteiro**: não improvise o passo seguinte. Registre a saída, avise o usuário
  e decida com ele.

## 0. Convenção de comandos

### 0.1 Acesso à produção (uma vez, em O1)

1. Chave dedicada nesta VM (sem relação com as chaves de dump/deploy de `deploy/provision/`):
   ```bash
   ssh-keygen -t ed25519 -N "" -C "ruinarpg-virada@192.168.80.16" -f ~/.ssh/ruinarpg_virada
   cat ~/.ssh/ruinarpg_virada.pub
   ```
   O usuário acrescenta a chave pública ao `~/.ssh/authorized_keys` da conta que ele escolher na produção
   (uma conta que pode rodar `sudo docker …`).
2. A senha do sudo dessa conta vai para um arquivo 600 nesta VM. O usuário a digita (nada aparece na tela,
   nada vai para o histórico do shell):
   ```bash
   install -d -m 700 /home/pro/.ruinarpg-virada
   (umask 077 && IFS= read -rs -p 'senha do sudo na produção: ' s && printf '%s\n' "$s" > /home/pro/.ruinarpg-virada/sudo; unset s; echo)
   stat -c '%a %U' /home/pro/.ruinarpg-virada /home/pro/.ruinarpg-virada/sudo
   ```
   **Passou se:** `700 pro` e `600 pro`. A senha **nunca** é impressa, copiada para o registro, para um
   relatório ou para o git; o arquivo é apagado no fim do H (passo D6).

### 0.2 Variáveis e funções (defina em cada terminal novo, antes de qualquer passo)

```bash
cd /home/pro/RuinaRPG
CONTA=<conta escolhida pelo usuário>                  # conta da produção com sudo
PROD="$CONTA@192.168.80.10"
SENHA=/home/pro/.ruinarpg-virada/sudo                 # 600; nunca impresso
REPO=/home/ruina/RuinaRPG                             # checkout da produção
DONO="sudo -u ruina"                                  # git no checkout sempre como o dono
BKP=/var/backups/ruinarpg-virada                      # pacotes na produção (700)
FERR=/root/virada                                     # pacote.sh + contagens.sql na produção
LOCAL=/home/pro/backups/ruinarpg-virada               # cópias nesta VM (700)
URL=https://<domínio público>                         # o site pelo túnel Cloudflare
V150=729dac9a8393383df01caa2720726454273d784a          # 1.5.0
DB=ruinarpg-db-1 API150=ruinarpg-api-1 NGINX150=ruinarpg-nginx-1   # confirme no P3

# Um comando como root na produção: a senha entra pelo stdin do sudo, o comando é uma única string.
prod() { ssh -i ~/.ssh/ruinarpg_virada -o IdentitiesOnly=yes -o BatchMode=yes -o ServerAliveInterval=30 "$PROD" "sudo -S -p '' bash -c $(printf %q "$1")" < "$SENHA"; }
# Uma consulta SQL no banco da produção (o SQL vai em base64, sem problemas de aspas; o stdin do ssh é da senha).
sqlprod() { local b; b="$(printf '%s\n' "$1" | base64 -w0)"; prod "echo $b | base64 -d | docker exec -i $DB sh -c 'exec psql -X -q -At -v ON_ERROR_STOP=1 -U \"\$POSTGRES_USER\" -d \"\${POSTGRES_DB:-\$POSTGRES_USER}\"'"; }
# Copia um diretório de $BKP (produção) para $LOCAL (esta VM), preservando 700/600.
trazer() { prod "tar -C $BKP -cf - $1" | (umask 077 && tar -C "$LOCAL" -xpf -); }
```

- As strings passadas a `prod` usam aspas duplas: `$REPO`, `$BKP` etc. são expandidas **nesta VM** antes de
  irem para a produção. `\$` fica para o lado remoto.
- `prod` lê a senha a cada chamada; o comando remoto não pode ler do stdin (use arquivos, nunca `-i` sem
  pipe). Uma desconexão no meio de um `deploy.sh deploy` vira falha e o `deploy.sh` desfaz a troca; confira
  sempre `deploy/state/deploy.log` e `deploy.sh status` antes de repetir qualquer coisa.
- Teste do acesso (O1): `ssh -i ~/.ssh/ruinarpg_virada -o IdentitiesOnly=yes -o BatchMode=yes "$PROD" true && echo ssh-ok`
  e `prod true && echo sudo-ok`. **Passou se:** `ssh-ok` e `sudo-ok`.

### 0.3 As mesmas funções no ensaio (nesta VM)

No ensaio (seção 3) os passos das seções 5–7 rodam **com o mesmo texto**, trocando só as variáveis e a
função `prod` (sem ssh; `RUINA_PACOTE_SEM_ROOT=1` porque o `pro` roda o docker sem root):

```bash
cd /home/pro/RuinaRPG
REPO=/home/pro/ensaio/RuinaRPG DONO=""
BKP=/home/pro/ensaio/producao-backups FERR=/home/pro/ensaio/virada LOCAL=/home/pro/ensaio/vm-backups
URL=http://127.0.0.1 V150=729dac9a8393383df01caa2720726454273d784a
DB=ruinarpg-db-1 API150=ruinarpg-api-1 NGINX150=ruinarpg-nginx-1
install -d -m 700 /home/pro/ensaio "$BKP" "$FERR" "$LOCAL" /home/pro/ensaio/deploy-backups
prod() { RUINA_PACOTE_SEM_ROOT=1 RUINA_BACKUP_DIR=/home/pro/ensaio/deploy-backups bash -c "$1"; }
# sqlprod e trazer: idênticas às de 0.2
```

## 1. Preflight (spec H.1) — só leitura

Todos os passos desta seção são somente leitura: aval **não** (mas cada comando é mostrado antes).
Crie o diretório local antes: `install -d -m 700 "$LOCAL"`.

**P1. Docker ≥ 23 (notas de C), compose, git.**
```bash
prod "docker version --format '{{.Server.Version}}'; docker compose version; docker-compose version 2>/dev/null || true; git --version"
```
Passou se: versão do servidor ≥ 23; `docker compose version` responde (v2). Se falhar: Docker < 23 → atualizar
o Docker **antes** da janela (combinar com o usuário; fora do roteiro); sem `docker compose` v2 → parar e
avaliar (o `deploy.sh` usa `docker compose`).

**P2. Volumes.**
```bash
prod "docker volume ls --format '{{.Name}}' | grep '^ruinarpg_'"
```
Passou se: aparecem `ruinarpg_pgdata` e `ruinarpg_images`. Se falhar: parar — os dados não estão onde o
roteiro supõe.

**P3. Contêineres, nomes, imagens (compose v1/v2) e rótulo do projeto.**
```bash
prod "docker ps -a --filter label=com.docker.compose.project=ruinarpg --format '{{.Names}} {{.State}} {{.Label \"com.docker.compose.service\"}}'"
prod "for c in \$(docker ps -q --filter label=com.docker.compose.project=ruinarpg); do docker inspect -f '{{.Name}} {{.Config.Image}} {{.Image}}' \$c; done"
prod "for c in $API150 $NGINX150; do i=\$(docker inspect -f '{{.Config.Image}}' \$c); echo \"\$c \$i -> \$(docker image inspect -f '{{.Id}}' \$i)\"; done"
```
Passou se: exatamente três contêineres `running` (serviços `db`, `api`, `nginx`); os nomes conferem com
`$DB`, `$API150`, `$NGINX150` (compose v2: `ruinarpg-api-1`; v1: `ruinarpg_api_1` — **ajuste as variáveis**);
o nome de imagem de `api` e `nginx` aponta para o mesmo id que o contêiner roda (senão o `pacote.sh` recusa).
Se falhar: imagem re-buildada depois de o contêiner subir → parar e investigar com o usuário.

**P4. A api monta `ruinarpg_images` em `/images`; ambiente do `db`.**
```bash
prod "docker inspect -f '{{range .Mounts}}{{.Name}} {{.Destination}}{{println}}{{end}}' $API150; docker exec $DB sh -c 'echo POSTGRES_USER=\${POSTGRES_USER:+definido} POSTGRES_DB=\${POSTGRES_DB:-vazio}'"
```
Passou se: `ruinarpg_images /images`; `POSTGRES_USER=definido POSTGRES_DB=ruinarpg`.

**P5. `.env`: Production e todas as variáveis da 2.0.0 (só os nomes, nunca os valores).**
```bash
prod "grep -c '^ASPNETCORE_ENVIRONMENT=Production\$' $REPO/.env"
diff <(prod "grep -o '^[A-Za-z_][A-Za-z0-9_]*=' $REPO/.env | sort") <(git show origin/release/2.0.0:.env.example | grep -o '^[A-Za-z_][A-Za-z0-9_]*=' | sort) && echo env-ok
```
Passou se: `1` e `env-ok`. Se falhar: faltando variável → o usuário completa o `.env` na H.3 (escrita
aprovada), nunca dentro da janela.

**P6. sha implantado e checkout limpo.**
```bash
prod "$DONO git -C $REPO rev-parse HEAD; $DONO git -C $REPO status --porcelain"
```
Passou se: `729dac9a8393383df01caa2720726454273d784a`; `status` vazio (ou só `?? deploy/` depois da Parte 0
de `deploy/provision/producao.md`). Se falhar: arquivo versionado alterado → parar; o `deploy.sh` recusa
checkout sujo.

**P7. Histórico de migrations (notas de B).**
```bash
sqlprod 'SELECT count(*), max("MigrationId") FROM "__EFMigrationsHistory"'
```
Passou se: `84|20261014100935_AddAutomaticoToSheetLogEntries`. Se falhar: parar — o `--migrate` da 2.0.0
recusaria dentro da janela (`HistoricoDeMigracoes150`).

**P8. Contagens de referência.**
```bash
sqlprod "$(cat tools/virada/contagens.sql)" > "$LOCAL/contagens-preflight.tsv"; wc -l "$LOCAL/contagens-preflight.tsv"
```
Passou se: uma linha `tabela<TAB>linhas` por tabela. Só referência (a aplicação está no ar; o pacote tem
as suas próprias contagens, tiradas no mesmo snapshot do dump).

**P9. Espaço em disco.**
```bash
prod "df -h /var/backups /var/lib/docker; docker system df; docker run --rm --network none -v ruinarpg_images:/i:ro --entrypoint du postgres:16-alpine -sh /i"
sqlprod 'SELECT pg_size_pretty(pg_database_size(current_database()))'
```
Passou se: livre em `/var/backups` ≥ 2 × (banco + volume de imagens) + ~300 MB das duas imagens salvas, e
espaço em `/var/lib/docker` para o build da 2.0.0 (SDK do .NET, alguns GB). Mesma conta em `$LOCAL`
(`df -h /home/pro`). Se falhar: liberar espaço com o usuário antes de seguir.

## 2. Pacote da 1.5.0 (spec H.1) — com a aplicação no ar

**K1. Levar `pacote.sh` e `contagens.sql` para fora do checkout da produção.** Aval: **sim** (escreve em `/root`).
```bash
scp -i ~/.ssh/ruinarpg_virada -o IdentitiesOnly=yes tools/virada/pacote.sh tools/virada/contagens.sql "$PROD:"
prod "install -d -m 700 $FERR && install -m 700 ~$CONTA/pacote.sh $FERR/pacote.sh && install -m 600 ~$CONTA/contagens.sql $FERR/contagens.sql && rm -f ~$CONTA/pacote.sh ~$CONTA/contagens.sql && ls -l $FERR"
```
Passou se: `pacote.sh` (700) e `contagens.sql` (600) em `$FERR`.
**Ensaio:** `install -m 700 tools/virada/pacote.sh "$FERR/" && install -m 600 tools/virada/contagens.sql "$FERR/"`.

**K2. Gerar o pacote completo.** Aval: **sim** (escreve em `$BKP`; não para nada nem escreve no banco).
```bash
PACOTE="$(basename "$(prod "$FERR/pacote.sh $BKP")")"; echo "$PACOTE"
```
Passou se: imprime `<AAAAMMDDTHHMMSSZ>-pacote-150` e o código de saída é 0. Se falhar: o script apaga o
parcial; leia a mensagem (`investigue` = imagem trocada, ver P3) e pare.

**K3. Conferir na produção, copiar para esta VM, conferir aqui.**
```bash
prod "cd $BKP/$PACOTE && ls -l && sha256sum -c --strict SHA256SUMS"
trazer "$PACOTE"
(cd "$LOCAL/$PACOTE" && ls -l && sha256sum -c --strict SHA256SUMS)
```
Passou se: cinco arquivos (`SHA256SUMS contagens.tsv dump.tar imagens-docker.tar imagens.tsv`), 600, e
todas as linhas `: OK` nos **dois** lados. Se falhar: apagar só a cópia local com problema e repetir `trazer`.

**K4. Restauração de teste nesta VM.**
```bash
tools/virada/restaurar-pacote.sh "$LOCAL/$PACOTE"
docker exec ruinarpg-pacote-db-1 psql -U ruinarpg -d ruinarpg -At -c 'SELECT count(*), count(*) FILTER (WHERE "IsRulesAuditor") FROM "AspNetUsers"' -c 'SELECT r."Name", count(*) FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" GROUP BY 1 ORDER BY 1'
grep -i -E 'auditoria|tabela|nive|coluna|valor' "$LOCAL/$PACOTE/contagens.tsv"
```
Passou se: a última linha do script é `OK pacote <dir>`; usuários, Auditores de Regras e papéis com
contagens > 0 e coerentes com o preflight; as tabelas da Auditoria aparecem com linhas. Na primeira
restauração real confira também que a 1.5.0 respondeu `"OK"` em `/api/health` (o script espera exatamente
isso). `fingerprint.jsonl` fica no pacote (referência "A"). Se falhar: o pacote **não** vale; descubra a
causa antes de qualquer outra coisa.

**K5. Desmontar a restauração.**
```bash
tools/virada/restaurar-pacote.sh --remover
```
Passou se: nada sobra de `ruinarpg-pacote` (`docker ps -a --filter label=com.docker.compose.project=ruinarpg-pacote` vazio).

Relatório ao usuário (fim de O1): caminho do pacote nos dois lugares, checksums, `OK pacote`, contagens.

## 3. Ensaio nesta VM (spec H.2) — nunca na produção

### 3.1 Ensaios do pipeline (3 limpos seguidos)

**E1. Uma rodada do clone com o pacote, allowlist vazia.**
```bash
git fetch origin
HEAD200="$(git rev-parse origin/release/2.0.0)"; test "$(git rev-parse HEAD)" = "$HEAD200" && git diff --quiet && echo checkout-ok
git show "$HEAD200:deploy/data-changes.yml" | grep -v '^#'
WD=/home/pro/ensaio/clone-$(date -u +%Y%m%dT%H%M%SZ)
RUINA_DUMP_CMD="cat $LOCAL/$PACOTE/dump.tar" RUINA_OFFSITE_DIR=/home/pro/ensaio/offsite deploy/clone.sh run "$V150" "$HEAD200" "$WD"; echo "código $?"
cat "$WD/report.md"; cat "$WD/compat"
```
(`$LOCAL` aqui é o de 0.2: o pacote real copiado para esta VM.)
Passou se: `checkout-ok`; a allowlist é `changes: []`; `código 0`; o relatório diz `bootstrap`,
`Resultado: OK` e todos os passos `ok`; `compat` = `janela` (forçado no bootstrap). Se falhar: logs em
`$WD/logs/`; `deploy/clone.sh teardown "$WD"`; a correção vira uma tarefa de código e a contagem recomeça.

**E2. A baseline equivale à 1.5.0.**
```bash
tools/migrations/verificar-baseline.sh "$V150"
```
Passou se: quatro linhas começando com `OK` e nenhuma `FALHA`.

Repita E1+E2 até **3 rodadas limpas seguidas**. Registre cada uma.

### 3.2 Ensaio completo do roteiro

**E3. Stack da 1.5.0 igual à da produção.** Aval: **sim** (remove a stack de desenvolvimento 2.0.0 desta VM
e os volumes dela; o usuário já aprovou para os ensaios).
```bash
mkdir -p /home/pro/ensaio
tools/virada/ensaio-150.sh preparar "/home/pro/backups/ruinarpg-virada/$PACOTE" /home/pro/ensaio/RuinaRPG --remover-stack-atual
```
Passou se: `OK ensaio ruinarpg em /home/pro/ensaio/RuinaRPG (porta 80)`. O nome do diretório tem de ser o
do projeto (`RuinaRPG` → `ruinarpg`), como `/home/ruina/RuinaRPG` na produção.

**E4. Rodar o roteiro ao pé da letra.** Defina as variáveis de 0.3 e execute, cronometrando a janela:
seção 4 (só R5 e R6 — retag e pré-build) → seção 5 (Janela) → seção 6 (Verificação) → seção 7 (Volta) →
seção 5 de novo → seção 6 de novo. No ensaio:
- `MERGE` é o sha de `origin/release/2.0.0` (`MERGE="$(git rev-parse origin/release/2.0.0)"`). No worktree
  o checkout é sempre por sha: `release/2.0.0` já está em `/home/pro/RuinaRPG` e `git checkout release/2.0.0`
  falha ali.
- Passos só da produção (pulados no ensaio): K1 por `scp`, J6 (`install-gate.sh`), J8 (aprovação do
  `deploy.yml`), as consultas pelo Cloudflare de V3 (use `URL=http://127.0.0.1`).
- Se a 2.0.0 deixar arquivos de root no worktree, o `desfazer` avisa; remova-os à mão.

**E5. Desmontar e devolver a stack de desenvolvimento.**
```bash
tools/virada/ensaio-150.sh desfazer /home/pro/ensaio/RuinaRPG
RUINA_BACKUP_DIR=/home/pro/backups/ruinarpg make deploy
curl -fsS http://127.0.0.1/api/health; echo
```
Passou se: `OK desfeito ruinarpg`; o `make deploy` termina com `RESULT … smoke=ok` (o `deploy.sh` migra
sozinho); `OK`.

Qualquer divergência entre o roteiro e o que aconteceu → tarefa de código (roteiro ou scripts) e o ensaio
completo se repete. Relatório ao usuário (fim de O2): tempo fora do ar, resultados; combinar a janela.

## 4. Preparação da produção (spec H.3) — com o usuário, aplicação no ar

**R1. Provisionamento**, na ordem de `deploy/provision/README.md`, cada linha **Verificação** conferida
antes do passo seguinte, cada comando aprovado pelo usuário (aval: **sim**, um a um):
1. `deploy/provision/producao.md`, Parte 0 e Parte A (pela função `prod`, com `cd $REPO &&` na frente dos
   comandos relativos, ex.: `prod "cd $REPO && deploy/provision/install-gate.sh"`);
2. `deploy/provision/runner.md` (nesta VM, com o sudo local do usuário);
3. `deploy/provision/producao.md`, Partes B e C, e a "Verificação final";
4. `deploy/provision/github.md` (no navegador, pelo usuário).

Depois da Parte 0, P6 passa a mostrar `?? deploy/`: é o esperado.

**R2. Required reviewers no Environment `production`** (só para este deploy; removido em D2). O usuário,
em GitHub > Settings > Environments > `production` > *Deployment protection rules*: marca *Required
reviewers*, adiciona a si mesmo, salva. Aval: **sim**.
Passou se: a página do Environment mostra a regra com 1 revisor. **Tem de estar ativa antes de abrir o PR**
(o PR faz auto-merge sozinho e o merge dispara o `deploy.yml`).

**R3. Allowlist vazia na cabeça do PR e passo 12 das notas.**
```bash
git show origin/release/2.0.0:deploy/data-changes.yml | grep -v '^#'
git merge-base --is-ancestor 7fbbd16 origin/release/2.0.0 && echo desfazer-ssh-ok
```
Passou se: `changes: []` e `desfazer-ssh-ok` (correção do "Critical #1" da revisão final do F).

**R4. Texto do `ChangelogDialog` aprovado.** O usuário aprova a redação proposta em
`src/RuinaRPG.Client/Shared/ChangelogDialog.razor` (versão 2.0.0); o comentário "RASCUNHO" sai num commit
antes do PR. Aval: **sim**.

**R5. Re-marcar as imagens da 1.5.0 para o retorno.** Aval: **sim**.
```bash
prod "docker tag \$(docker inspect -f '{{.Image}}' $API150) ruinarpg-legacy-api:1.5.0 && docker tag \$(docker inspect -f '{{.Image}}' $NGINX150) ruinarpg-legacy-nginx:1.5.0 && docker image ls --format '{{.Repository}}:{{.Tag}} {{.ID}}' | grep legacy"
```
Passou se: `ruinarpg-legacy-api:1.5.0` e `ruinarpg-legacy-nginx:1.5.0` com os ids que os contêineres rodam
(P3). Pela id e não pelo nome (`ruinarpg-api:latest` ou `ruinarpg_api:latest`, conforme o compose).

**R6. Pré-build das imagens da 2.0.0 e pré-pull** (aquece o cache do build; o build a frio de ~4 min fica
fora da janela). Aval: **sim** (escreve imagens e cache do docker; nada de dados).
```bash
HEAD200="$(git rev-parse origin/release/2.0.0)"
prod "docker pull nginx:1.27-alpine && docker pull busybox:1.36 && docker pull alpine:3.20"
prod "rm -rf $FERR/prebuild && install -d -m 700 $FERR/prebuild && $DONO git -C $REPO fetch --quiet origin && $DONO git -C $REPO archive $HEAD200 | tar -x -C $FERR/prebuild && for t in api client smoke; do docker build -q -f $FERR/prebuild/deploy/Dockerfile --target \$t --build-arg GIT_SHA=$HEAD200 -t ruinarpg-prebuild-\$t:aquecimento $FERR/prebuild || exit 1; done && rm -rf $FERR/prebuild && echo prebuild-ok"
```
Passou se: `prebuild-ok`. O sha do merge só existe depois do merge, então o `deploy.sh` ainda compila as
imagens do merge na janela — mas com as imagens base, o restore do NuGet e o cache já prontos (build
quente). As imagens `ruinarpg-prebuild-*:aquecimento` são removidas em D4.
**Ensaio:** igual, cronometrado.

**R7. PR da virada.** Com R2–R6 feitos:
```bash
gh pr create --base main --head release/2.0.0 --title "Versão 2.0.0" --body-file <corpo aprovado pelo usuário>
gh pr checks --watch
```
Passou se: `ci` e `clone` verdes (o `clone` roda contra o clone da produção em bootstrap, allowlist vazia,
`compat` = `janela`, o label `deploy:janela` entra); o PR faz auto-merge; o run do `deploy.yml` fica
**esperando aprovação** (Required reviewers). **Não aprovar.** Se falhar: o PR não faz merge; corrigir por
tarefa de código.

Anote o sha do merge: `git fetch origin && MERGE="$(git rev-parse origin/main)"; git log -1 --format='%H %s' "$MERGE"`.

## 5. Janela (spec H.4) — no horário combinado

Marque o horário de início no registro: a janela (tempo fora do ar) começa em J1 e termina em V6.

**J1. Abrir a janela: parar `nginx` e `api` da 1.5.0, manter o `db`.** Aval: **sim**.
```bash
prod "docker stop $NGINX150 $API150 && docker ps -a --filter label=com.docker.compose.project=ruinarpg --format '{{.Names}} {{.State}}'"
```
Passou se: `$DB running`, `$API150 exited`, `$NGINX150 exited`. A porta 80 fica livre e ninguém mais escreve.
Se falhar: `docker start $API150 $NGINX150` devolve a 1.5.0; parar e investigar.

**J2. Dump final (só banco + volume de imagens).** Aval: **sim**.
```bash
BANCO="$(basename "$(prod "$FERR/pacote.sh --so-banco $BKP")")"; echo "$BANCO"
```
Passou se: `<AAAAMMDDTHHMMSSZ>-banco-150`, código 0.

**J3. Conferir, copiar para esta VM, conferir.**
```bash
prod "cd $BKP/$BANCO && ls -l && sha256sum -c --strict SHA256SUMS"
trazer "$BANCO"
(cd "$LOCAL/$BANCO" && sha256sum -c --strict SHA256SUMS)
```
Passou se: três arquivos (`SHA256SUMS contagens.tsv dump.tar`), todas as linhas `: OK` nos dois lados.

**J4. Restaurar o dump final nesta VM e tirar o fingerprint de referência.**
```bash
tools/virada/restaurar-pacote.sh "$LOCAL/$BANCO" --projeto ruinarpg-pacote-final
tools/virada/restaurar-pacote.sh --remover --projeto ruinarpg-pacote-final
```
Passou se: `OK pacote <dir> (só banco: api e nginx não verificados; N arquivos no volume de imagens)` e
`$LOCAL/$BANCO/fingerprint.jsonl` existe (600). Se falhar: **não seguir** — sem dump final testado não há
virada; `docker start $API150 $NGINX150` devolve a 1.5.0.

**J5. Trocar o checkout para o merge.** Aval: **sim**.
```bash
prod "$DONO rm -rf $REPO/deploy && $DONO git -C $REPO fetch origin && $DONO git -C $REPO checkout --detach $MERGE && $DONO git -C $REPO rev-parse HEAD && $DONO git -C $REPO status --porcelain"
```
Passou se: imprime `$MERGE` e o `status` vem vazio. **Ensaio:** o `status` mostra `?? .ensaio-150` (a marca do
`ensaio-150.sh`), e só isso. (`deploy/` da Parte 0 são arquivos não rastreados: sem
removê-los o checkout aborta. O `deploy/state` que ela criou some junto; o `deploy.sh` recria.)
Se falhar: `prod "$DONO git -C $REPO checkout --detach $V150"` e `docker start $API150 $NGINX150` devolvem a 1.5.0.

**J6. Reinstalar o gate** (o `/opt/ruinarpg/gate.sh` é a cópia da Parte 0). Aval: **sim**.
```bash
prod "$REPO/deploy/provision/install-gate.sh"
```
Passou se: a última linha é `gate instalado em /opt/ruinarpg/gate.sh`.
**Ensaio:** pulado (não há usuário `deploy` nesta VM).

**J7. Deploy à mão, olhando o terminal.** Aval: **sim**.
```bash
prod "cd $REPO && deploy/deploy.sh deploy $MERGE"
```
Passou se: no log aparecem `modo zero-downtime; cor ativa: none; nova cor: blue`, as migrations com a
consolidação do histórico (`Histórico de migrations consolidado: as 84 migrations da 1.5.0 viraram a
baseline 20261017120000_Baseline_2_0_0`), o health check e o smoke; a última linha é
`RESULT mode=zero-downtime sha=<MERGE> active=blue backup=<…> smoke=ok rollback=none`. (Sem cor ativa não há
janela do `deploy.sh`: o aviso `sem cor ativa atrás do nginx: deploy sem janela` das notas só aparece com
`--window`, que aqui não é usado.) O build é quente se R6 foi feito.
Se falhar:
- falhou antes da troca (build, migrate, health): o `deploy.sh` desfaz o que fez; o histórico pode já estar
  consolidado (notas de B). **Não** rode `make migrate` nem `deploy.sh migrate`. Corrigir e repetir J7
  (a 2.0.0 aceita a baseline) ou, por decisão do usuário, seção 7 (Volta);
- `smoke=failed` num primeiro deploy: a cor nova fica ativa; avaliar com o usuário (V1–V5) antes de decidir;
- conexão caiu: `prod "tail -n 50 $REPO/deploy/state/deploy.log; cd $REPO && deploy/deploy.sh status"`.

**J8. Rejeitar a aprovação pendente do `deploy.yml`** (aprovar só reimplantaria o mesmo sha no green, em
janela: inútil). O usuário, em GitHub > Actions > o run `deploy` do merge > *Review deployments* >
**Reject**. Ou:
```bash
RUN="$(gh run list --workflow deploy.yml --commit "$MERGE" --json databaseId --jq '.[0].databaseId')"
ENV_ID="$(gh api "repos/{owner}/{repo}/actions/runs/$RUN/pending_deployments" --jq '.[0].environment.id')"
gh api -X POST "repos/{owner}/{repo}/actions/runs/$RUN/pending_deployments" -F "environment_ids[]=$ENV_ID" -f state=rejected -f comment="virada manual (H)"
```
Aval: **sim**. Passou se: o run termina como rejeitado, sem ter rodado nenhum passo.
**Ensaio:** pulado.

## 6. Verificação (notas, passo 9; spec H.4)

**V1. Estado das cores.**
```bash
prod "cd $REPO && deploy/deploy.sh status"
```
Passou se: `ACTIVE=blue`, `CURRENT_SHA=$MERGE`, `MODE=zero-downtime`.

**V2. Versão, saúde e nginx da 2.0.0.**
```bash
curl -fsS "$URL/api/version"; echo; curl -fsS "$URL/api/health"; echo
prod "docker exec ruinarpg-web-1 grep -c gzip_static /etc/nginx/conf.d/default.conf"
```
Passou se: `{"versao":"2.0.0","sha":"<MERGE>"}`; `OK`; contagem > 0.

**V3. Cabeçalhos pelo Cloudflare (notas de C).**
```bash
for p in / /_framework/blazor.boot.json /css/app.css; do echo "== $p"; curl -sI -H 'Accept-Encoding: gzip' "$URL$p" | grep -i -E '^(HTTP|cache-control|content-encoding)'; done
WASM="$(curl -fsS "$URL/_framework/blazor.boot.json" | python3 -c 'import json,sys; print(next(iter(json.load(sys.stdin)["resources"]["assembly"])))')"
echo "== $WASM"; curl -sI -H 'Accept-Encoding: gzip' "$URL/_framework/$WASM" | grep -i -E '^(HTTP|cache-control|content-encoding)'
IMG="$(prod "docker run --rm --network none -v ruinarpg_images:/i:ro busybox:1.36 sh -c 'cd /i && find . -type f | head -1'")"; IMG="${IMG#./}"
echo "== /images/$IMG"; curl -sI "$URL/images/$IMG" | grep -i -E '^(HTTP|cache-control)'
```
Passou se: todas `200`; `/`, `blazor.boot.json`, `app.css` e o `.wasm` com `cache-control: no-cache`; o
`.wasm` e o `.css` com `content-encoding: gzip` (ou `br`, se o Cloudflare recodificar); `/images/…` com
`cache-control: private, max-age=31536000, immutable`. `cache-control` ausente → uma regra do Cloudflare o
sobrescreve: conferir o painel (não é perda de dados; não justifica voltar).
**Ensaio:** com `URL=http://127.0.0.1` (sem Cloudflare).

**V4. Fingerprint da produção = fingerprint do dump final, diferença zero.** Aval: **sim** (para a api da
2.0.0 por um ou dois minutos, para que ninguém escreva enquanto o fingerprint é tirado).
```bash
FP="fingerprint-producao-200-$(date -u +%Y%m%dT%H%M%SZ).jsonl"
prod "docker stop ruinarpg-api-blue-1 && cd $REPO && (umask 077 && deploy/fingerprint.sh ruinarpg $BKP/$FP); rc=\$?; docker start ruinarpg-api-blue-1; exit \$rc"
prod "cat $BKP/$FP" | (umask 077 && cat > "$LOCAL/$FP")
(umask 077 && printf 'changes: []\n' > "$LOCAL/allowlist-vazia.yml")
python3 deploy/compare.py "$LOCAL/$BANCO/fingerprint.jsonl" "$LOCAL/$FP" "$LOCAL/allowlist-vazia.yml" --full "$LOCAL/comparacao-$BANCO.md"; echo "código $?"
curl -fsS "$URL/api/health"; echo
```
Passou se: `Resultado: OK`, `código 0`, e a api voltou (`OK`; o cliente repete as requisições durante a
parada). Linhas novas criadas pelos seeders da 2.0.0 não contam; qualquer linha **perdida** ou **alterada**
conta. Se falhar (`Resultado: FALHOU`): o detalhe (com pks de amostra) está em
`$LOCAL/comparacao-$BANCO.md` — **não** publicar. Avise o usuário e recomende a seção 7 (Volta); a decisão é
dele.

**V5. Conferência manual do usuário**, pelo site público:
- entrar como Mestre e como jogador (contas reais que ele conheça);
- o diálogo "Novidades da Versão 2.0.0" aparece uma vez e some ao fechar;
- abrir fichas de Personagem, NPC e Criatura: valores, Combate, Magias, Posses, Diário, imagens;
- abrir uma campanha, um encontro, o Catálogo e o Banco de Magias;
- como Auditor de Regras, abrir as páginas de Auditoria (tabelas presentes, valores de antes);
- o aviso "Nova versão disponível" não fica reaparecendo em loop.

Passou se: o usuário confirma. Problema de dados → seção 7 por decisão dele; problema só de tela →
correção pelo pipeline (2.0.1), sem voltar.

**V6. Fechar a janela.** Anote o horário de fim no registro (tempo fora do ar = J1 → V6). Os contêineres
`$API150` e `$NGINX150` ficam **parados, não removidos** (notas, passo 10).

## 7. Volta para a 1.5.0 (spec H.5; notas, passo 11) — só se houver perda de dados

Usa o **dump final** (`$BANCO`), nunca o `--migrate` da 1.5.0. Cada passo: aval **sim**.

**X1. Parar a 2.0.0.**
```bash
prod "cd $REPO && docker compose -p ruinarpg stop web api-blue api-green && docker ps --filter label=com.docker.compose.project=ruinarpg --format '{{.Names}} {{.State}}'"
```
Passou se: só `$DB` em execução.

**X2. Restaurar o dump final** (banco e volume de imagens) com `ACTIVE=none`, para o `deploy.sh restore` não
religar a cor.
```bash
prod "install -d -m 700 $BKP/volta-$BANCO && tar -C $BKP/volta-$BANCO -xf $BKP/$BANCO/dump.tar && sed -i 's/^ACTIVE=.*/ACTIVE=none/' $REPO/deploy/state/colors.env && cd $REPO && deploy/deploy.sh restore $BKP/volta-$BANCO"
sqlprod "$(cat tools/virada/contagens.sql)" | diff - "$LOCAL/$BANCO/contagens.tsv" && echo contagens-iguais
sqlprod 'SELECT count(*), max("MigrationId") FROM "__EFMigrationsHistory"'
```
Passou se: o log termina em `restauração concluída`; `contagens-iguais`;
`84|20261014100935_AddAutomaticoToSheetLogEntries` (o histórico voltou ao da 1.5.0).

**X3. Checkout da 1.5.0** (`deploy/state` é ignorado pelo git e sobrevive).
```bash
prod "$DONO git -C $REPO checkout --detach $V150 && $DONO git -C $REPO rev-parse HEAD"
```
Passou se: `729dac9a8393383df01caa2720726454273d784a`.

**X4. Religar os contêineres parados da 1.5.0.**
```bash
prod "docker start $API150 $NGINX150 && sleep 10 && curl -fsS http://127.0.0.1/api/health; echo"
sqlprod "$(cat tools/virada/contagens.sql)" | diff - "$LOCAL/$BANCO/contagens.tsv" && echo contagens-iguais
curl -fsS "$URL/api/health"; echo
```
Passou se: `OK` na produção e pelo `$URL`; `contagens-iguais` (a 1.5.0 em Production não migra ao subir).
Depois, o usuário repete V5 na 1.5.0. **Nunca** `make migrate`/`make deploy` da 1.5.0 aqui.

Depois de uma volta: a `main` já tem a 2.0.0 e o `deploy.yml` de qualquer merge novo vai pedir aprovação —
**não aprovar** até uma nova virada ser combinada. Uma nova tentativa recomeça na seção 5 (J5 remove o
`deploy/state` que sobrou).

## 8. Depois (spec H.6)

**D1. Período de observação.** `$API150` e `$NGINX150` ficam parados e não removidos; as imagens
`ruinarpg-legacy-*:1.5.0`, o pacote (`$PACOTE`) e o dump final (`$BANCO`), nos dois lugares, ficam até o
critério de aceite (2.0.0 sem perda **e** 2.1.0 lançada). Só então: `docker rm $API150 $NGINX150` (as tags
legacy guardam as imagens) e, com o aval do usuário, a remoção do resto.

**D2. Remover os Required reviewers** do Environment `production` (o usuário, no GitHub). O próximo deploy
(2.0.1/2.1.0) segue o caminho automático; antes dele, confira de novo
`git merge-base --is-ancestor 7fbbd16 origin/main && echo desfazer-ssh-ok` (notas, passo 12).

**D3. Backlog:** registrar o item "sistema de backup da produção" na 2.2.0 (memória e fila de planos).

**D4. Limpar o aquecimento:** `prod "docker rmi ruinarpg-prebuild-api:aquecimento ruinarpg-prebuild-client:aquecimento ruinarpg-prebuild-smoke:aquecimento"` (aval **sim**).

**D5. Linhas perdidas notadas só depois do uso real.** Uma volta completa apagaria o que foi escrito na
2.0.0 desde a virada, então a recuperação é trazer as linhas que faltam do dump final para a 2.0.0
(descrito, não automatizado; cada escrita na produção com o aval do usuário):
1. Restaurar o dump final numa stack descartável nesta VM:
   `tools/virada/restaurar-pacote.sh "$LOCAL/$BANCO" --projeto ruinarpg-pacote-recuperacao` (o
   `fingerprint.jsonl` do pacote é o "antes").
2. Tirar um fingerprint novo da produção (como em V4, sem precisar parar a api se a comparação for só de
   linhas perdidas) e comparar: `python3 deploy/compare.py "$LOCAL/$BANCO/fingerprint.jsonl" <fingerprint novo> "$LOCAL/allowlist-vazia.yml" --full <relatório>`.
   As linhas `lost` são as candidatas; as `altered` incluem as mudanças legítimas feitas pelos usuários
   desde a virada e são analisadas uma a uma com o usuário.
3. Para cada tabela com linhas perdidas, gerar na stack descartável os `INSERT` dessas linhas (pelas pks do
   relatório completo, respeitando a ordem das chaves estrangeiras: pais antes de filhos) e conferir que
   não colidem com linhas criadas depois na 2.0.0.
4. Antes de inserir: `prod "cd $REPO && deploy/deploy.sh backup"`. Inserir numa transação na produção, com
   o aval do usuário; repetir o passo 2 e confirmar que as linhas deixaram de aparecer como `lost`.
5. Arquivos de imagem perdidos: copiar do `images.tar` do dump final para o volume `ruinarpg_images`.
6. `tools/virada/restaurar-pacote.sh --remover --projeto ruinarpg-pacote-recuperacao`.

**D6. Fim do H.** Apagar a senha do sudo (`shred -u /home/pro/.ruinarpg-virada/sudo && rmdir /home/pro/.ruinarpg-virada`),
o usuário remove a chave `ruinarpg-virada@192.168.80.16` do `authorized_keys` da conta na produção, e
`rm -f ~/.ssh/ruinarpg_virada ~/.ssh/ruinarpg_virada.pub`. Atualizar a memória e a fila de planos.
