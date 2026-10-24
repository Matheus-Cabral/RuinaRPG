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
  qualquer escrita na produção, fechar ou reabrir a rota pública).
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
- Cada chamada do Bash começa carregando o ambiente do alvo; os valores de um passo passam para os
  seguintes só pelo arquivo de estado; passos longos rodam com timeout de 10 min ou em segundo plano
  (seção 0.4).

## 0. Convenção de comandos

### 0.1 Acesso à produção (uma vez, em O1)

1. Chave dedicada nesta VM (sem relação com as chaves de dump/deploy de `deploy/provision/`):
   ```bash
   ssh-keygen -t ed25519 -N "" -C "ruinarpg-virada@192.168.80.16" -f ~/.ssh/ruinarpg_virada
   cat ~/.ssh/ruinarpg_virada.pub
   ```
   O usuário acrescenta a chave pública ao `~/.ssh/authorized_keys` da conta que ele escolher na produção
   (uma conta que pode rodar `sudo docker …`).
2. A senha do sudo dessa conta vai para um arquivo 600 nesta VM. **O usuário roda este passo no próprio
   terminal** (ou no Claude Code com o prefixo `!`), porque o `read -rs` precisa de um TTY que o agente não
   tem. Ele digita a senha (nada aparece na tela, nada vai para o histórico do shell):
   ```bash
   install -d -m 700 /home/pro/.ruinarpg-virada
   (umask 077 && IFS= read -rs -p 'senha do sudo na produção: ' s && printf '%s\n' "$s" > /home/pro/.ruinarpg-virada/sudo; unset s; echo)
   stat -c '%a %U' /home/pro/.ruinarpg-virada /home/pro/.ruinarpg-virada/sudo
   ```
   **Passou se:** `700 pro` e `600 pro`. A senha **nunca** é impressa, copiada para o registro, para um
   relatório ou para o git; o arquivo é apagado no fim do H (passo D6).
3. A conta e o domínio público vão para o arquivo de estado (seção 0.4):
   ```bash
   . /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
   guardar CONTA <conta escolhida pelo usuário>
   guardar URL https://<domínio público>
   ```
4. Teste do acesso:
   ```bash
   . /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
   _ssh_prod true && echo ssh-ok
   prod true && echo sudo-ok
   ```
   **Passou se:** `ssh-ok` e `sudo-ok`.

### 0.2 Ambiente da produção: `tools/virada/ambiente-producao.sh`

Toda chamada que age na produção começa com
`. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1`. O arquivo:

- faz `cd /home/pro/RuinaRPG`, define `ALVO=producao` (somente leitura) e imprime na saída de erro
  `== ALVO=producao: <conta>@192.168.80.10, checkout /home/ruina/RuinaRPG; estado …` — confira essa linha
  em toda saída;
- define `SENHA=/home/pro/.ruinarpg-virada/sudo`, `REPO=/home/ruina/RuinaRPG`, `DONO="sudo -u ruina"` (git no
  checkout sempre como o dono), `BKP=/var/backups/ruinarpg-virada` (pacotes na produção, 700),
  `FERR=/root/virada` (pacote.sh + contagens.sql na produção), `LOCAL=/home/pro/backups/ruinarpg-virada`
  (cópias nesta VM, 700), `LOGS=$LOCAL/logs`, `V150=729dac9a8393383df01caa2720726454273d784a` (1.5.0) e os
  padrões `DB=ruinarpg-db-1 API150=ruinarpg-api-1 NGINX150=ruinarpg-nginx-1` (confirmados no P3);
- carrega `~/.virada/estado-producao.env` (`CONTA`, `URL`, `PACOTE`, `BANCO`, `MERGE`, …);
- define as funções (em `tools/virada/ambiente-comum.sh`):
  - `prod '<comando>'` — mostra o comando (`[producao] $ …`) e o roda como root na produção: um único
    argumento, que vai quotado (`printf %q`) para `sudo -S -p '' bash -c` pelo ssh com a chave de 0.1; a
    senha entra pelo stdin do sudo (com `SUDO_SEM_SENHA=1`, P1, o stdin é `/dev/null` e o sudo é `-n`);
  - `sqlprod '<SQL>'` — uma consulta no banco `$DB` (o SQL vai em base64, sem problemas de aspas);
  - `trazer <dir>` — copia `$BKP/<dir>` da produção para `$LOCAL`, preservando 700/600;
  - `guardar VAR valor` e `passo_longo <passo> <comando…>` (seção 0.4).
- Recusa (`recusado: este shell já carregou o ambiente 'ensaio'`) se o mesmo shell já carregou o
  ambiente do ensaio. Não escreve nada ao ser carregado.

Notas:
- As strings passadas a `prod` usam aspas duplas: `$REPO`, `$BKP` etc. são expandidas **nesta VM** antes de
  irem para a produção. `\$` fica para o lado remoto.
- `prod` supõe que o shell de login da conta na produção é o bash; confira no P1.
- `prod` lê a senha a cada chamada; o comando remoto não pode ler do stdin (use arquivos, nunca `-i` sem
  pipe). Uma desconexão no meio de um `deploy.sh deploy` vira falha e o `deploy.sh` desfaz a troca; confira
  sempre `deploy/state/deploy.log` e `deploy.sh status` antes de repetir qualquer coisa.

### 0.3 Ambiente do ensaio: `tools/virada/ambiente-ensaio.sh`

No ensaio (seção 3) os passos das seções 4–7 rodam **com o mesmo texto**; só a primeira linha de cada
bloco muda para `. /home/pro/RuinaRPG/tools/virada/ambiente-ensaio.sh || exit 1`. Esse arquivo define
`ALVO=ensaio`, `REPO=/home/pro/ensaio/RuinaRPG DONO=""`, `BKP=/home/pro/ensaio/producao-backups`,
`FERR=/home/pro/ensaio/virada`, `LOCAL=/home/pro/ensaio/vm-backups`, `URL=http://127.0.0.1`, os mesmos
`V150`, `DB`, `API150`, `NGINX150`, e um `prod` sem ssh (`RUINA_PACOTE_SEM_ROOT=1` porque o `pro` roda o
docker sem root, `RUINA_BACKUP_DIR=/home/pro/ensaio/deploy-backups`); `sqlprod`, `trazer`, `guardar` e
`passo_longo` são os mesmos. O estado fica em `~/.virada/estado-ensaio.env`, separado do da produção.
Recusa se o mesmo shell já carregou o ambiente da produção, e imprime `== ALVO=ensaio: …` em toda chamada.

### 0.4 Como executar: uma chamada por bloco, estado em arquivo, passos longos

O roteiro é executado por um agente cujo Bash **não guarda variáveis nem funções entre chamadas** e mata
um comando depois de 2 minutos (padrão). Por isso:

- **Cada bloco de comandos é uma chamada** e começa com a linha do ambiente (0.2 ou 0.3). Nunca carregue
  os dois ambientes na mesma chamada (o segundo recusa). Confira a linha `== ALVO=…` na saída.
- **Estado entre passos.** Um passo que produz um valor usado depois (`PACOTE`, `BANCO`, `MERGE`,
  `HEAD200`, `FP`, `WD`, `VOLTA_BKP`, e `CONTA`, `URL`, `DB`, `API150`, `NGINX150`, `IMGAPI150`,
  `IMGNGINX150`, `SUDO_SEM_SENHA` de O1/P3) o grava com `guardar VAR valor`, que acrescenta `VAR=valor` a
  `~/.virada/estado-<alvo>.env` (diretório 700, arquivo 600; a última linha de cada variável vale). O
  ambiente carrega esse arquivo a cada chamada sem executá-lo (só aceita essas variáveis e valores com
  `[A-Za-z0-9._:/@+-]`); `guardar` recusa valor vazio. Para ver o estado: `cat ~/.virada/estado-<alvo>.env`
  (não há segredo nele). A senha **não** vai para o estado: continua só no arquivo de 0.1.
- **Guardas.** Todo bloco que precisa de um valor de um passo anterior começa com `: "${VAR:?…}"`: sem o
  valor, a chamada para ali, sem rodar nada.
- **Passos longos** (marcados **Longo**): rodar com timeout de 600000 ms ou em segundo plano; depois ler
  `$LOGS/<passo>.log` (e, nos deploys, `deploy/state/deploy.log`). Eles rodam por
  `passo_longo <passo> <comando…>`, que grava stdout e stderr em `$LOGS/<passo>.log` nesta VM (600; um log
  anterior do mesmo passo é renomeado com o horário) e termina com a linha `== <passo> código N`. Os que
  agem na produção (**Longo, remoto**) rodam **em segundo plano** sempre que puderem passar de 10 min (J7
  sem o R8, K2, R6): se o Bash matar a chamada, o ssh cai, o lado remoto recebe HUP e o `deploy.sh` desfaz o
  deploy (o `pacote.sh` apaga o parcial). Para ler o resultado:
  `. <ambiente> && tail -n 40 "$LOGS/<passo>.log"`.

## 1. Preflight (spec H.1) — só leitura

Todos os passos desta seção são somente leitura: aval **não** (mas cada comando é mostrado antes). Todos
começam com `. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1`.

**P1. Shell e sudo da conta; Docker ≥ 23 (notas de C), compose, git.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
_ssh_prod 'echo "shell=$SHELL"; sudo -n true 2>/dev/null && echo sudo-NOPASSWD || echo sudo-pede-senha'
prod "docker version --format '{{.Server.Version}}'; docker compose version; docker-compose version 2>/dev/null || true; git --version"
```
Passou se: `shell=/bin/bash`; `sudo-pede-senha` (o normal: a senha entra pelo stdin do `sudo -S`); versão do
servidor ≥ 23; `docker compose version` responde (v2). Se aparecer `sudo-NOPASSWD`: o sudo não lê o stdin e
a senha chegaria ao comando remoto (ex.: um `compose run` a leria) — rode `guardar SUDO_SEM_SENHA 1` antes de
qualquer outro passo (o `prod` passa a usar `sudo -n` com o stdin em `/dev/null`) e repita o P1. Se
falhar: Docker < 23 → atualizar o Docker **antes** da janela (combinar com o usuário; fora do roteiro); sem
`docker compose` v2 → parar e avaliar (o `deploy.sh` usa `docker compose`); shell que não é o bash → parar
(o `prod` depende do quoting do bash).

**P2. Volumes.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "docker volume ls --format '{{.Name}}' | grep '^ruinarpg_'"
```
Passou se: aparecem `ruinarpg_pgdata` e `ruinarpg_images`. Se falhar: parar — os dados não estão onde o
roteiro supõe.

**P3. Contêineres, nomes, imagens (compose v1/v2) e rótulo do projeto.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "docker ps -a --filter label=com.docker.compose.project=ruinarpg --format '{{.Names}} {{.State}} {{.Label \"com.docker.compose.service\"}}'"
prod "for c in \$(docker ps -q --filter label=com.docker.compose.project=ruinarpg); do docker inspect -f '{{.Name}} {{.Config.Image}} {{.Image}}' \$c; done"
prod "for c in $API150 $NGINX150; do i=\$(docker inspect -f '{{.Config.Image}}' \$c); echo \"\$c \$i -> \$(docker image inspect -f '{{.Id}}' \$i)\"; done"
```
Passou se: exatamente três contêineres `running` (serviços `db`, `api`, `nginx`); os nomes conferem com
`$DB`, `$API150`, `$NGINX150` (compose v2: `ruinarpg-api-1`); o nome de imagem de `api` e `nginx` aponta para
o mesmo id que o contêiner roda (senão o `pacote.sh` recusa). Depois, grave os nomes das imagens (usados
no último recurso do X4):
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
guardar IMGAPI150 "$(prod "docker inspect -f '{{.Config.Image}}' $API150")"
guardar IMGNGINX150 "$(prod "docker inspect -f '{{.Config.Image}}' $NGINX150")"
```
Se falhar: imagem re-buildada depois de o contêiner subir → parar e investigar com o usuário. **Nomes de
compose v1** (`ruinarpg_api_1`, `ruinarpg_db_1`) → **parar e reavaliar com o usuário antes do R1**: o
`gate dump` do R1 (Parte C) e do `clone` do R7 roda `deploy.sh backup`, que faz `compose up -d --wait db`
com o compose v2 e o `docker-compose.yml` da 1.5.0 na stack no ar, e o compose v2 pode recriar o contêiner
do `db` criado pelo v1. Só depois de decidir, ajuste com `guardar DB <nome>`, `guardar API150 <nome>`,
`guardar NGINX150 <nome>`.

**P4. A api monta `ruinarpg_images` em `/images`; ambiente do `db`.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "docker inspect -f '{{range .Mounts}}{{.Name}} {{.Destination}}{{println}}{{end}}' $API150; docker exec $DB sh -c 'echo POSTGRES_USER=\${POSTGRES_USER:+definido} POSTGRES_DB=\${POSTGRES_DB:-vazio}'"
```
Passou se: `ruinarpg_images /images`; `POSTGRES_USER=definido POSTGRES_DB=ruinarpg`.

**P5. `.env`: Production e todas as variáveis da 2.0.0 (só os nomes, nunca os valores).**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "grep -c '^ASPNETCORE_ENVIRONMENT=Production\$' $REPO/.env"
git cat-file -e origin/release/2.0.0:.env.example || { echo "sem .env.example: git fetch origin"; exit 1; }
faltam="$(LC_ALL=C comm -13 <(prod "grep -o '^[A-Za-z_][A-Za-z0-9_]*=' $REPO/.env" | LC_ALL=C sort -u) <(git show origin/release/2.0.0:.env.example | grep -o '^[A-Za-z_][A-Za-z0-9_]*=' | LC_ALL=C sort -u))"
if [ -z "$faltam" ]; then echo env-ok; else echo "faltam no .env:" $faltam; fi
```
Passou se: `1` e `env-ok`. Só as chaves que **faltam** no `.env` da produção falham; chaves a mais (da 1.5.0)
não importam. Se falhar: faltando variável → o usuário completa o `.env` na H.3 (escrita aprovada), nunca
dentro da janela.

**P6. sha implantado e checkout limpo.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "$DONO git -C $REPO rev-parse HEAD; $DONO git -C $REPO status --porcelain"
```
Passou se: `729dac9a8393383df01caa2720726454273d784a`; `status` vazio (ou só `?? deploy/` depois da Parte 0
de `deploy/provision/producao.md`). Se falhar: arquivo versionado alterado → parar; o `deploy.sh` recusa
checkout sujo.

**P7. Histórico de migrations (notas de B) e fuso do banco.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
sqlprod 'SELECT count(*), max("MigrationId") FROM "__EFMigrationsHistory"'
sqlprod 'SHOW timezone'
```
Passou se: `84|20261014100935_AddAutomaticoToSheetLogEntries` e `UTC`. Se falhar: histórico diferente →
parar — o `--migrate` da 2.0.0 recusaria dentro da janela (`HistoricoDeMigracoes150`). Fuso diferente de
`UTC` → anote: o banco restaurado no K4/J4 tem de mostrar o **mesmo** fuso, senão os fingerprints (datas
como texto) não são comparáveis; se não baterem, parar e decidir com o usuário.

**P8. Contagens de referência.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
install -d -m 700 "$LOCAL"
sqlprod "$(cat tools/virada/contagens.sql)" > "$LOCAL/contagens-preflight.tsv"; wc -l "$LOCAL/contagens-preflight.tsv"
```
Passou se: uma linha `tabela<TAB>linhas` por tabela. Só referência (a aplicação está no ar; o pacote tem
as suas próprias contagens, tiradas no mesmo snapshot do dump).

**P9. Espaço em disco.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "df -h /var/backups /var/lib/docker; docker system df; docker run --rm --network none -v ruinarpg_images:/i:ro --entrypoint du postgres:16-alpine -sh /i"
sqlprod 'SELECT pg_size_pretty(pg_database_size(current_database()))'
df -h /home/pro
```
Passou se: livre em `/var/backups` ≥ 2 × (banco + volume de imagens) + ~300 MB das duas imagens salvas, e
espaço em `/var/lib/docker` para o build da 2.0.0 (SDK do .NET, alguns GB). Mesma conta em `$LOCAL`
(`/home/pro`). Se falhar: liberar espaço com o usuário antes de seguir.

## 2. Pacote da 1.5.0 (spec H.1) — com a aplicação no ar

**K1. Levar `pacote.sh` e `contagens.sql` para fora do checkout da produção.** Aval: **sim** (escreve em `/root`).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${CONTA:?guardar CONTA (0.1)}"
scp -i ~/.ssh/ruinarpg_virada -o IdentitiesOnly=yes tools/virada/pacote.sh tools/virada/contagens.sql "$CONTA@192.168.80.10:"
prod "install -d -m 700 $FERR && install -m 700 ~$CONTA/pacote.sh $FERR/pacote.sh && install -m 600 ~$CONTA/contagens.sql $FERR/contagens.sql && rm -f ~$CONTA/pacote.sh ~$CONTA/contagens.sql && ls -l $FERR"
```
Passou se: `pacote.sh` (700) e `contagens.sql` (600) em `$FERR`.
**Ensaio** (no E4, antes da seção 5):
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-ensaio.sh || exit 1
install -d -m 700 "$FERR" && install -m 700 tools/virada/pacote.sh "$FERR/" && install -m 600 tools/virada/contagens.sql "$FERR/" && ls -l "$FERR"
```

**K2. Gerar o pacote completo.** Aval: **sim** (escreve em `$BKP`; não para nada nem escreve no banco).
**Longo, remoto** (em segundo plano).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
passo_longo K2 prod "$FERR/pacote.sh $BKP" && guardar PACOTE "$(basename "$(grep -E '^/.+-pacote-150$' "$LOGS/K2.log" | tail -1)")"
```
Passou se: o log termina com `== K2 código 0` e `guardado: PACOTE=<AAAAMMDDTHHMMSSZ>-pacote-150`. Se falhar:
o script apaga o parcial; leia a mensagem no log (`investigue` = imagem trocada, ver P3) e pare.

**K3. Conferir na produção, copiar para esta VM, conferir aqui.** **Longo** (a cópia).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${PACOTE:?rode o K2}"
k3() { prod "cd $BKP/$PACOTE && ls -l && sha256sum -c --strict SHA256SUMS" && trazer "$PACOTE" && (cd "$LOCAL/$PACOTE" && ls -l && sha256sum -c --strict SHA256SUMS); }
passo_longo K3 k3
```
Passou se: cinco arquivos (`SHA256SUMS contagens.tsv dump.tar imagens-docker.tar imagens.tsv`), 600, e
todas as linhas `: OK` nos **dois** lados; `== K3 código 0`. Se falhar: apagar só a cópia local com problema
e repetir.

**K4. Restauração de teste nesta VM.** **Longo**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${PACOTE:?rode o K2}"
passo_longo K4 tools/virada/restaurar-pacote.sh "$LOCAL/$PACOTE"
```
Depois, numa chamada nova:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${PACOTE:?rode o K2}"
docker exec ruinarpg-pacote-db-1 psql -U ruinarpg -d ruinarpg -At -c 'SHOW timezone' -c 'SELECT count(*), count(*) FILTER (WHERE "IsRulesAuditor") FROM "AspNetUsers"' -c 'SELECT r."Name", count(*) FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" GROUP BY 1 ORDER BY 1'
grep -i -E 'auditoria|tabela|nive|coluna|valor' "$LOCAL/$PACOTE/contagens.tsv"
```
Passou se: a última linha do script (antes de `== K4 código 0`) é `OK pacote <dir>`; o fuso é o mesmo do P7
(`UTC`); usuários, Auditores de Regras e papéis com contagens > 0 e coerentes com o preflight; as tabelas da
Auditoria aparecem com linhas. Na primeira restauração real confira também que a 1.5.0 respondeu `"OK"` em
`/api/health` (o script espera exatamente isso). `fingerprint.jsonl` fica no pacote (referência "A"). Se
falhar: o pacote **não** vale; descubra a causa antes de qualquer outra coisa. Fuso diferente do P7 → parar.

**K5. Desmontar a restauração.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
tools/virada/restaurar-pacote.sh --remover
docker ps -a --filter label=com.docker.compose.project=ruinarpg-pacote
```
Passou se: a listagem vem vazia.

Relatório ao usuário (fim de O1): caminho do pacote nos dois lugares, checksums, `OK pacote`, contagens.

## 3. Ensaio nesta VM (spec H.2) — nunca na produção

E1–E3 e E5 rodam só nesta VM, com o ambiente da produção apenas para `$LOCAL`, `$PACOTE`, `$V150` e
`passo_longo` (nenhum deles chama `prod`); o E4 usa o ambiente do ensaio.

### 3.1 Ensaios do pipeline (3 limpos seguidos)

**E1. Uma rodada do clone com o pacote, allowlist vazia.** **Longo**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${PACOTE:?rode o K2}"
git fetch origin
h="$(git rev-parse origin/release/2.0.0)"; test "$(git rev-parse HEAD)" = "$h" && git diff --quiet && echo checkout-ok && guardar HEAD200 "$h" || exit 1
git show "$HEAD200:deploy/data-changes.yml" | grep -v '^#'
install -d -m 700 /home/pro/ensaio
guardar WD "/home/pro/ensaio/clone-$(date -u +%Y%m%dT%H%M%SZ)"
passo_longo E1 env RUINA_DUMP_CMD="cat $LOCAL/$PACOTE/dump.tar" RUINA_OFFSITE_DIR=/home/pro/ensaio/offsite deploy/clone.sh run "$V150" "$HEAD200" "$WD"
```
Depois, numa chamada nova:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${WD:?rode o E1}"
tail -n 3 "$LOGS/E1.log"; cat "$WD/report.md"; cat "$WD/compat"
```
(`$LOCAL` aqui é o de 0.2: o pacote real copiado para esta VM.)
Passou se: `checkout-ok`; a allowlist é `changes: []`; `== E1 código 0`; o relatório diz `bootstrap`,
`Resultado: OK` e todos os passos `ok`; `compat` = `janela` (forçado no bootstrap). Se falhar: logs em
`$WD/logs/`; `deploy/clone.sh teardown "$WD"`; a correção vira uma tarefa de código e a contagem recomeça.

**E2. A baseline equivale à 1.5.0.** **Longo**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
passo_longo E2 tools/migrations/verificar-baseline.sh "$V150"
```
Passou se: quatro linhas começando com `OK`, nenhuma `FALHA`, `== E2 código 0`.

Repita E1+E2 até **3 rodadas limpas seguidas**. Registre cada uma.

### 3.2 Ensaio completo do roteiro

**E3. Stack da 1.5.0 igual à da produção.** Aval: **sim** (remove a stack de desenvolvimento 2.0.0 desta VM
e os volumes dela; o usuário já aprovou para os ensaios). **Longo**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${PACOTE:?rode o K2}"
mkdir -p /home/pro/ensaio
passo_longo E3 tools/virada/ensaio-150.sh preparar "$LOCAL/$PACOTE" /home/pro/ensaio/RuinaRPG --remover-stack-atual
```
Passou se: `OK ensaio ruinarpg em /home/pro/ensaio/RuinaRPG (porta 80)`. O nome do diretório tem de ser o
do projeto (`RuinaRPG` → `ruinarpg`), como `/home/ruina/RuinaRPG` na produção.

**E4. Rodar o roteiro ao pé da letra.** Com o ambiente do ensaio (0.3) em cada bloco, cronometrando a
janela. Primeiro, estado novo e o sha do merge:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-ensaio.sh || exit 1
if [ -e "$ESTADO" ]; then mv "$ESTADO" "$ESTADO.$(date -u +%Y%m%dT%H%M%SZ)"; fi
guardar MERGE "$(git rev-parse origin/release/2.0.0)"
```
Depois: K1 (variante do ensaio: instala `pacote.sh` e `contagens.sql` em `$FERR`) → seção 4 (só R5, R6 e
R8 — retag e pré-builds) → seção 5 (Janela) → seção 6 (Verificação) → seção 7 (Volta) → seção 5 de novo →
seção 6 de novo. No ensaio:
- `MERGE` é o sha de `origin/release/2.0.0` (guardado acima). No worktree o checkout é sempre por sha:
  `release/2.0.0` já está em `/home/pro/RuinaRPG` e `git checkout release/2.0.0` falha ali.
- Passos só da produção (pulados no ensaio): K1 por `scp`, a rota pública de J1/J8/seção 7 (não há
  Cloudflare), J6 (`install-gate.sh`), J9 (aprovação do `deploy.yml`), as consultas pelo Cloudflare de V3
  (o ambiente usa `URL=http://127.0.0.1`).
- Se a 2.0.0 deixar arquivos de root no worktree, o `desfazer` avisa; remova-os à mão.
- O ensaio **não** exercita o caminho de comandos da produção (sem ssh, sem `sudo -S`, e com
  `RUINA_PACOTE_SEM_ROOT=1`): as primeiras execuções reais desse caminho são as de O1 (0.1, preflight, K1–K3),
  que por isso são só leitura ou escrevem fora dos dados.

**E5. Desmontar e devolver a stack de desenvolvimento.** **Longo** (o `make deploy`).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
tools/virada/ensaio-150.sh desfazer /home/pro/ensaio/RuinaRPG
deploy/deploy.sh status; docker ps -a --filter label=com.docker.compose.project=ruinarpg --format '{{.Names}} {{.State}}'
```
O E3 apagou os contêineres da stack de desenvolvimento, mas `deploy/state/colors.env` desta VM ainda pode
nomear como ativa uma cor que não existe mais. Se o `status` mostra `ACTIVE=blue` ou `green` e a listagem
de contêineres vem vazia, ponha o estado de lado (o `deploy.sh` recria um com `ACTIVE=none`):
`mv deploy/state/colors.env deploy/state/colors.env.antes-do-ensaio`. Então:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
passo_longo E5 env RUINA_BACKUP_DIR=/home/pro/backups/ruinarpg make deploy
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

Depois da Parte 0, P6 passa a mostrar `?? deploy/`: é o esperado. **Atenção (Parte C e R7):** o `gate dump`
roda `deploy.sh backup`, que faz `compose up -d --wait db` com o `docker-compose.yml` da 1.5.0 na stack no
ar. Com os nomes de compose v2 do P3 isso não muda o `db`; se o P3 mostrou nomes de compose v1, **parar e
reavaliar** antes da Parte C (o compose v2 pode recriar o contêiner do `db`).

**R2. Required reviewers no Environment `production`** (só para este deploy; removido em D2). O usuário,
em GitHub > Settings > Environments > `production` > *Deployment protection rules*: marca *Required
reviewers*, adiciona a si mesmo, salva. Aval: **sim**.
Passou se: a página do Environment mostra a regra com 1 revisor. **Tem de estar ativa antes de abrir o PR**
(o PR faz auto-merge sozinho e o merge dispara o `deploy.yml`).

**R3. Allowlist vazia na cabeça do PR e passo 12 das notas.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
git fetch origin
git show origin/release/2.0.0:deploy/data-changes.yml | grep -v '^#'
git merge-base --is-ancestor 7fbbd16 origin/release/2.0.0 && echo desfazer-ssh-ok
```
Passou se: `changes: []` e `desfazer-ssh-ok` (correção do "Critical #1" da revisão final do F).

**R4. Texto do `ChangelogDialog` aprovado.** O usuário aprova a redação proposta em
`src/RuinaRPG.Client/Shared/ChangelogDialog.razor` (versão 2.0.0); o comentário "RASCUNHO" sai num commit
antes do PR. O item "Aviso de nova versão" (o aviso "Nova versão disponível" do F) foi acrescentado
pelo implementador além da lista combinada (carregamento, unificações do E): o usuário decide se fica ou
sai. Aval: **sim**.

**R5. Re-marcar as imagens da 1.5.0 para o retorno.** Aval: **sim**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "docker tag \$(docker inspect -f '{{.Image}}' $API150) ruinarpg-legacy-api:1.5.0 && docker tag \$(docker inspect -f '{{.Image}}' $NGINX150) ruinarpg-legacy-nginx:1.5.0 && docker image ls --format '{{.Repository}}:{{.Tag}} {{.ID}}' | grep legacy"
```
Passou se: `ruinarpg-legacy-api:1.5.0` e `ruinarpg-legacy-nginx:1.5.0` com os ids que os contêineres rodam
(P3). Pela id e não pelo nome (`ruinarpg-api:latest` ou `ruinarpg_api:latest`, conforme o compose).

**R6. Pré-build das imagens da 2.0.0 e pré-pull** (aquece o cache do build; o build a frio de ~4 min fica
fora da janela). Aval: **sim** (escreve imagens e cache do docker; nada de dados). **Longo, remoto** (em
segundo plano).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
git fetch origin && guardar HEAD200 "$(git rev-parse origin/release/2.0.0)" || exit 1
r6() { prod "docker pull nginx:1.27-alpine && docker pull busybox:1.36 && docker pull alpine:3.20" && prod "rm -rf $FERR/prebuild && install -d -m 700 $FERR/prebuild && $DONO git -C $REPO fetch --quiet origin && $DONO git -C $REPO archive $HEAD200 | tar -x -C $FERR/prebuild && for t in api client smoke; do docker build -q -f $FERR/prebuild/deploy/Dockerfile --target \$t --build-arg GIT_SHA=$HEAD200 -t ruinarpg-prebuild-\$t:aquecimento $FERR/prebuild || exit 1; done && rm -rf $FERR/prebuild && echo prebuild-ok"; }
passo_longo R6 r6
```
Passou se: `prebuild-ok` e `== R6 código 0`. Com o build quente, o R8 (depois do merge) só compila o que
mudou. As imagens `ruinarpg-prebuild-*:aquecimento` são removidas em D4.
**Ensaio:** igual, cronometrado.

**R7. PR da virada.** Com R2–R6 feitos:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
gh pr create --base main --head release/2.0.0 --title "Versão 2.0.0" --body-file <corpo aprovado pelo usuário>
```
**Longo** (o `clone` do PR leva vários minutos; em segundo plano):
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
passo_longo R7 gh pr checks release/2.0.0 --watch
```
Passou se: `ci` e `clone` verdes (o `clone` roda contra o clone da produção em bootstrap, allowlist vazia,
`compat` = `janela`, o label `deploy:janela` entra); o PR faz auto-merge; o run do `deploy.yml` fica
**esperando aprovação** (Required reviewers). **Não aprovar.** Se falhar: o PR não faz merge; corrigir por
tarefa de código. (O `clone` passa pelo `gate dump`: ver a atenção do R1 sobre compose v1.)

Anote o sha do merge:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
git fetch origin && guardar MERGE "$(git rev-parse origin/main)" && git log -1 --format='%H %s' "$MERGE"
```

**R8. Pré-build das imagens do merge** (opcional, recomendado: o `deploy.sh` reaproveita uma tag
`ruinarpg-<alvo>:<sha>` que já existe e pula o build, então o J7 não compila nada dentro da janela). Aval:
**sim** (escreve imagens; nada de dados). **Longo, remoto** (em segundo plano).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${MERGE:?rode o R7}"
passo_longo R8 prod "rm -rf $FERR/prebuild && install -d -m 700 $FERR/prebuild && $DONO git -C $REPO fetch --quiet origin && $DONO git -C $REPO archive $MERGE | tar -x -C $FERR/prebuild && for t in api client smoke; do docker image inspect ruinarpg-\$t:$MERGE >/dev/null 2>&1 && continue; docker build -q -f $FERR/prebuild/deploy/Dockerfile --target \$t --build-arg GIT_SHA=$MERGE -t ruinarpg-\$t:$MERGE $FERR/prebuild || exit 1; done && rm -rf $FERR/prebuild && docker image ls --format '{{.Repository}}:{{.Tag}}' | grep -F ':$MERGE'"
```
Passou se: `== R8 código 0` e as três imagens `ruinarpg-api:<MERGE>`, `ruinarpg-client:<MERGE>`,
`ruinarpg-smoke:<MERGE>`. São as mesmas que o `deploy.sh` faria (mesmo Dockerfile, mesmos alvos, mesmo
`GIT_SHA`, a árvore do mesmo commit). No J7 o log mostra `imagem ruinarpg-…:<MERGE> já existe, build pulado`.
**Ensaio:** igual (no worktree do ensaio).

## 5. Janela (spec H.4) — no horário combinado

Marque o horário de início no registro: a janela (tempo fora do ar) começa em J1 e termina em V5. A rota
pública fica **fechada** de J1 até o J8 dar `Resultado: OK`: assim nenhum usuário real escreve no banco
entre a troca do nginx (J7) e o fingerprint (J8).

**J1. Abrir a janela: fechar a rota pública, parar `nginx` e `api` da 1.5.0, manter o `db`.**

1. **O usuário fecha a rota pública.** Aval: **sim**. O `cloudflared` roda num LXC separado (Técnico R0010):
   o usuário para o serviço lá (ex.: `systemctl stop cloudflared` no LXC) ou desativa o hostname público
   no painel do Cloudflare (Zero Trust > Tunnels > Public Hostname). Depois:
   ```bash
   . /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
   : "${URL:?guardar URL (0.1)}"
   curl -sS -o /dev/null -m 15 -w '%{http_code}\n' "$URL/api/health"
   ```
   Passou se: o código **não** é `200` (o Cloudflare responde 530/502/404 com o túnel parado). Se a rota não
   puder ser fechada: registre a decisão do usuário e siga — o J8 trata esse caso.
   **Ensaio:** pulado.
2. **Parar a 1.5.0.** Aval: **sim**.
   ```bash
   . /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
   prod "docker stop $NGINX150 $API150 && docker ps -a --filter label=com.docker.compose.project=ruinarpg --format '{{.Names}} {{.State}}'"
   ```
   Passou se: `$DB running`, `$API150 exited`, `$NGINX150 exited`. A porta 80 fica livre e ninguém mais
   escreve.

Se falhar: `prod "docker start $API150 $NGINX150"` devolve a 1.5.0; o usuário reabre a rota depois de
`curl -fsS "$URL/api/health"` responder pela produção; parar e investigar.

**J2. Dump final (só banco + volume de imagens).** Aval: **sim**. **Longo, remoto**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
passo_longo J2 prod "$FERR/pacote.sh --so-banco $BKP" && guardar BANCO "$(basename "$(grep -E '^/.+-banco-150$' "$LOGS/J2.log" | tail -1)")"
```
Passou se: `== J2 código 0` e `guardado: BANCO=<AAAAMMDDTHHMMSSZ>-banco-150`. Se falhar: o retorno de J1.

**J3. Conferir, copiar para esta VM, conferir.** **Longo**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}"
j3() { prod "cd $BKP/$BANCO && ls -l && sha256sum -c --strict SHA256SUMS" && trazer "$BANCO" && (cd "$LOCAL/$BANCO" && sha256sum -c --strict SHA256SUMS); }
passo_longo J3 j3
```
Passou se: três arquivos (`SHA256SUMS contagens.tsv dump.tar`), todas as linhas `: OK` nos dois lados,
`== J3 código 0`.

**J4. Restaurar o dump final nesta VM e tirar o fingerprint de referência.** **Longo**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}"
passo_longo J4 tools/virada/restaurar-pacote.sh "$LOCAL/$BANCO" --projeto ruinarpg-pacote-final
```
Depois, numa chamada nova (fuso do banco restaurado, e desmontar):
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}"
docker exec ruinarpg-pacote-final-db-1 psql -U ruinarpg -d ruinarpg -At -c 'SHOW timezone'
ls -l "$LOCAL/$BANCO/fingerprint.jsonl"
tools/virada/restaurar-pacote.sh --remover --projeto ruinarpg-pacote-final
```
Passou se: `OK pacote <dir> (só banco: api e nginx não verificados; N arquivos no volume de imagens)` no log;
o fuso é o mesmo do P7 (`UTC`); `$LOCAL/$BANCO/fingerprint.jsonl` existe (600). Se falhar (ou o fuso diferir):
**não seguir** — sem dump final testado não há virada; o retorno de J1.

**J5. Trocar o checkout para o merge.** Aval: **sim**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${MERGE:?rode o R7}"
prod "$DONO rm -rf $REPO/deploy && $DONO git -C $REPO fetch origin && $DONO git -C $REPO checkout --detach $MERGE && $DONO git -C $REPO rev-parse HEAD && $DONO git -C $REPO status --porcelain"
```
Passou se: imprime `$MERGE` e o `status` vem vazio. **Ensaio:** o `status` mostra `?? .ensaio-150` (a marca do
`ensaio-150.sh`), e só isso. (`deploy/` da Parte 0 são arquivos não rastreados: sem
removê-los o checkout aborta. O `deploy/state` que ela criou some junto; o `deploy.sh` recria.)
Se falhar — **o retorno do J5**: `prod "$DONO git -C $REPO checkout --detach $V150 && docker start $API150 $NGINX150"`
devolve a 1.5.0; depois de `prod "sleep 10; curl -fsS http://127.0.0.1/api/health"` responder `OK`, o usuário
reabre a rota (aval **sim**).

**J6. Reinstalar o gate** (o `/opt/ruinarpg/gate.sh` é a cópia da Parte 0). Aval: **sim**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "$REPO/deploy/provision/install-gate.sh"
```
Passou se: a última linha é `gate instalado em /opt/ruinarpg/gate.sh`.
**Ensaio:** pulado (não há usuário `deploy` nesta VM).

**J7. Deploy à mão.** Aval: **sim**. **Longo, remoto: em segundo plano** (se o Bash matar a chamada, o
`deploy.sh` recebe HUP e desfaz o deploy).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${MERGE:?rode o R7}"
passo_longo J7 prod "cd $REPO && deploy/deploy.sh deploy $MERGE"
```
Ao terminar: `tail -n 40 "$LOGS/J7.log"` (o mesmo texto está em `deploy/state/deploy.log` na produção).
Passou se: no log aparecem `modo zero-downtime; cor ativa: none; nova cor: blue`, as migrations com a
consolidação do histórico (`Histórico de migrations consolidado: as 84 migrations da 1.5.0 viraram a
baseline 20261017120000_Baseline_2_0_0`), o health check e o smoke; a última linha antes de `== J7 código 0`
é `RESULT mode=zero-downtime sha=<MERGE> active=blue backup=<…> smoke=ok rollback=none`. (Sem cor ativa não
há janela do `deploy.sh`: o aviso `sem cor ativa atrás do nginx: deploy sem janela` das notas só aparece com
`--window`, que aqui não é usado.) Com o R8 feito, o log mostra `build pulado` nas três imagens.
Se falhar — olhe no log se a linha `migrations e seeders com api-blue` apareceu:
- **falhou antes dela** (checkout, backup, build, publicação do cliente): a 2.0.0 não escreveu no banco (o
  backup só leu; o `db` pode ter sido recriado pelo compose da 2.0.0, com os mesmos volumes). Corrigir e
  repetir o J7, ou o **retorno do J5** (checkout da 1.5.0 + `docker start`; se a api não achar o `db`, o
  "Se falhar" do X4), por decisão do usuário;
- **falhou depois dela** (migrate, health, nginx): o `deploy.sh` desfaz o que fez, mas o histórico pode já
  estar consolidado (notas de B) e a 1.5.0 não pode mais subir nesse banco. **Não** rode `make migrate` nem
  `deploy.sh migrate`. Corrigir e repetir J7 (a 2.0.0 aceita a baseline) ou, por decisão do usuário,
  seção 7 (Volta), que restaura o dump final;
- `smoke=failed` num primeiro deploy: a cor nova fica ativa; fazer o J8 mesmo assim e avaliar com o usuário
  (V1–V4) antes de decidir;
- conexão caiu: `prod "tail -n 50 $REPO/deploy/state/deploy.log; cd $REPO && deploy/deploy.sh status"`.

**J8. Comparar os dados com o site fechado: diferença zero; só então reabrir.** Logo depois da linha `RESULT`
do J7, antes de qualquer verificação pelo site. Aval: **sim**. **Longo, remoto** (o fingerprint).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}"
guardar FP "fingerprint-producao-200-$(date -u +%Y%m%dT%H%M%SZ).jsonl"
passo_longo J8 prod "docker stop ruinarpg-web-1 ruinarpg-api-blue-1 && cd $REPO && (umask 077 && deploy/fingerprint.sh ruinarpg $BKP/$FP); rc=\$?; docker start ruinarpg-api-blue-1; exit \$rc"
```
Passou se: `== J8 código 0` (o `api-blue` volta sempre, mesmo se o fingerprint falhar; o `web` fica parado de
propósito). Depois, numa chamada nova, a comparação:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}" "${FP:?rode o J8}"
set -o pipefail
prod "cat $BKP/$FP" | (umask 077 && cat > "$LOCAL/$FP") || { echo "cópia do fingerprint falhou"; exit 1; }
(umask 077 && printf 'changes: []\n' > "$LOCAL/allowlist-vazia.yml")
python3 deploy/compare.py "$LOCAL/$BANCO/fingerprint.jsonl" "$LOCAL/$FP" "$LOCAL/allowlist-vazia.yml" --full "$LOCAL/comparacao-$BANCO.md"; echo "código $?"
```
Passou se: `Resultado: OK` e `código 0`. Linhas novas criadas pelos seeders da 2.0.0 não contam. **Só um
`Resultado: OK` reabre o site**:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "docker start ruinarpg-web-1 && sleep 3 && curl -fsS http://127.0.0.1/api/health"; echo
```
Passou se: `OK`. Então **o usuário reabre a rota pública** (aval **sim**: `systemctl start cloudflared` no
LXC, ou reativa o hostname) e:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${URL:?guardar URL (0.1)}"
curl -fsS "$URL/api/health"; echo
```
Passou se: `OK` pela URL pública. **Ensaio:** igual, sem a rota pública.

Como ler um `Resultado: FALHOU` (o detalhe, com pks de amostra, está em `$LOCAL/comparacao-$BANCO.md` —
**não** publicar): com a rota fechada desde o J1 nenhum usuário real escreveu, então **qualquer** linha
`lost` ou `altered` é um achado. O site continua fechado e o usuário decide:
- linhas **perdidas** (`lost`) → perda de dados: recomende a seção 7 (Volta);
- linhas **alteradas** → investigue com o usuário (quais tabelas e colunas, por quê). Uma alteração que ele
  aceitar, por decisão registrada, entra numa allowlist `$LOCAL/allowlist-j8.yml` (formato do
  `deploy/data-changes.yml`: `table`, `columns` exatas, `change: altered`, `reason`; **nunca** `lost`), e a
  comparação é repetida com ela no lugar da vazia. Só o `Resultado: OK` dessa comparação reabre o site.

Só se a rota **não** pôde ser fechada no J1 (decisão registrada): acessos reais entre a troca do nginx e o
J8 escrevem `RefreshTokens.RevokedAt` (renovação do token) e `AspNetUsers.LastSeenAppVersion` (diálogo de
novidades fechado). Alterações **só** nessas colunas são explicadas por esse uso e vão para a
`allowlist-j8.yml` (`altered`, essas colunas exatamente); qualquer outra coisa segue a regra acima.

Depois que o site reabre, qualquer fingerprint novo (ex.: D5) mostra escritas legítimas dos usuários:
nunca são motivo para a Volta.

Se falhar:
- o fingerprint falhou (código ≠ 0 no primeiro comando): o `api-blue` já voltou; o site continua fechado.
  Leia o erro no log e repita o primeiro comando;
- a comparação deu perda de dados e o usuário escolhe a Volta → seção 7 (X1 para tudo; a rota continua
  fechada).

**J9. Rejeitar a aprovação pendente do `deploy.yml`** (aprovar só reimplantaria o mesmo sha no green, em
janela: inútil). O usuário, em GitHub > Actions > o run `deploy` do merge > *Review deployments* >
**Reject**. Ou:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${MERGE:?rode o R7}"
RUN="$(gh run list --workflow deploy.yml --commit "$MERGE" --json databaseId --jq '.[0].databaseId')"
ENV_ID="$(gh api "repos/{owner}/{repo}/actions/runs/$RUN/pending_deployments" --jq '.[0].environment.id')"
gh api -X POST "repos/{owner}/{repo}/actions/runs/$RUN/pending_deployments" -F "environment_ids[]=$ENV_ID" -f state=rejected -f comment="virada manual (H)"
```
Aval: **sim**. Passou se: o run termina como rejeitado, sem ter rodado nenhum passo.
**Ensaio:** pulado.

## 6. Verificação (notas, passo 9; spec H.4)

O site e a rota pública reabriram no J8, depois da comparação de dados. Daqui em diante, escritas dos
usuários são esperadas.

**V1. Estado das cores.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${MERGE:?rode o R7}"
prod "cd $REPO && deploy/deploy.sh status"; echo "esperado: $MERGE"
```
Passou se: `ACTIVE=blue`, `CURRENT_SHA=$MERGE`, `MODE=zero-downtime`.

**V2. Versão, saúde e nginx da 2.0.0, pela URL pública.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${URL:?guardar URL (0.1)}" "${MERGE:?rode o R7}"
curl -fsS "$URL/api/version"; echo; curl -fsS "$URL/api/health"; echo
prod "docker exec ruinarpg-web-1 grep -c gzip_static /etc/nginx/conf.d/default.conf"
```
Passou se: `{"versao":"2.0.0","sha":"<MERGE>"}`; `OK`; contagem > 0.

**V3. Cabeçalhos pelo Cloudflare (notas de C).**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${URL:?guardar URL (0.1)}"
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

**V4. Conferência manual do usuário**, pelo site público:
- entrar como Mestre e como jogador (contas reais que ele conheça);
- o diálogo "Novidades da Versão 2.0.0" aparece uma vez e some ao fechar;
- abrir fichas de Personagem, NPC e Criatura: valores, Combate, Magias, Posses, Diário, imagens;
- abrir uma campanha, um encontro, o Catálogo e o Banco de Magias;
- como Auditor de Regras, abrir as páginas de Auditoria (tabelas presentes, valores de antes);
- o aviso "Nova versão disponível" não fica reaparecendo em loop.

Passou se: o usuário confirma. Problema de dados → seção 7 por decisão dele; problema só de tela →
correção pelo pipeline (2.0.1), sem voltar.

**V5. Fechar a janela.** Anote o horário de fim no registro (tempo fora do ar = J1 → V5). Os contêineres
`$API150` e `$NGINX150` ficam **parados, não removidos** (notas, passo 10).

## 7. Volta para a 1.5.0 (spec H.5; notas, passo 11) — só se houver perda de dados

Usa o **dump final** (`$BANCO`), nunca o `--migrate` da 1.5.0. Cada passo: aval **sim**. Todos começam com
`. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1`.

**X0. Rota fechada, `deploy.yml` rejeitado, nome do contêiner do banco.**
1. A rota pública fica **fechada** durante toda a Volta. Vindo do J8 ela ainda está fechada; se já reabriu
   (perda notada no V4), o usuário a fecha de novo como no J1 (aval **sim**) e o `curl` do J1 confirma que
   não responde `200`. **Ensaio:** pulado.
2. Se o J9 ainda não foi feito, faça-o agora: a aprovação pendente do `deploy.yml` é **rejeitada**, nunca
   aprovada (aprovar reimplantaria a 2.0.0 por cima da Volta). **Ensaio:** pulado.
3. O J7 recria o `db` pelo compose da 2.0.0: num compose v1 o nome muda (`ruinarpg_db_1` → `ruinarpg-db-1`),
   e `sqlprod` usa `$DB`.
   ```bash
   . /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
   prod "docker ps -a --filter label=com.docker.compose.project=ruinarpg --filter label=com.docker.compose.service=db --format '{{.Names}} {{.State}}'"
   ```
   Passou se: um único contêiner `running`; se o nome diferir de `$DB`, `guardar DB <nome>`.

**X1. Parar a 2.0.0.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "cd $REPO && docker compose -p ruinarpg stop web api-blue api-green && docker ps --filter label=com.docker.compose.project=ruinarpg --format '{{.Names}} {{.State}}'"
```
Passou se: só `$DB` em execução.

**X1b. Backup do banco da 2.0.0 antes de restaurar.** O que foi escrito na 2.0.0 desde o J8 só existe nesse
banco; a restauração do X2 o substitui. **Longo, remoto**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
passo_longo X1b prod "cd $REPO && d=\$(deploy/deploy.sh backup) && n=volta-200-\$(basename \"\$d\") && mv \"\$d\" $BKP/\$n && echo $BKP/\$n" && guardar VOLTA_BKP "$(grep -E "^$BKP/volta-200-" "$LOGS/X1b.log" | tail -1)"
```
Passou se: `== X1b código 0` e `guardado: VOLTA_BKP=$BKP/volta-200-<AAAAMMDDTHHMMSSZ>-<sha7>` (funciona com
`ACTIVE=blue`: o `deploy.sh backup` só lê o banco e o volume de imagens). O backup sai do diretório do
`deploy.sh` e ganha o prefixo `volta-200-` para nenhuma poda de backups (que guarda os 5 mais novos com nome
começando pelo horário) apagá-lo. Se falhar: **não** restaurar; parar e decidir com o usuário.

**X2. Restaurar o dump final** (banco e volume de imagens) com `ACTIVE=none`, para o `deploy.sh restore` não
religar a cor. **Longo, remoto**.
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}" "${VOLTA_BKP:?rode o X1b}"
passo_longo X2 prod "cd $BKP/$BANCO && sha256sum -c --strict SHA256SUMS && install -d -m 700 $BKP/volta-$BANCO && tar -C $BKP/volta-$BANCO -xf $BKP/$BANCO/dump.tar && sed -i 's/^ACTIVE=.*/ACTIVE=none/' $REPO/deploy/state/colors.env && cd $REPO && deploy/deploy.sh restore $BKP/volta-$BANCO"
```
Depois, numa chamada nova:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}"
set -o pipefail
sqlprod "$(cat tools/virada/contagens.sql)" | diff - "$LOCAL/$BANCO/contagens.tsv" && echo contagens-iguais
sqlprod 'SELECT count(*), max("MigrationId") FROM "__EFMigrationsHistory"'
```
Passou se: o `SHA256SUMS` do dump final confere (`: OK`) antes de qualquer extração; o log termina em
`restauração concluída` e `== X2 código 0`; `contagens-iguais`;
`84|20261014100935_AddAutomaticoToSheetLogEntries` (o histórico voltou ao da 1.5.0).

**X3. Checkout da 1.5.0** (`deploy/state` é ignorado pelo git e sobrevive).
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "$DONO git -C $REPO checkout --detach $V150 && $DONO git -C $REPO rev-parse HEAD"
```
Passou se: `729dac9a8393383df01caa2720726454273d784a`.

**X4. Religar os contêineres parados da 1.5.0.**
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${BANCO:?rode o J2}"
set -o pipefail
prod "docker start $API150 $NGINX150 && sleep 10 && curl -fsS http://127.0.0.1/api/health; echo"
sqlprod "$(cat tools/virada/contagens.sql)" | diff - "$LOCAL/$BANCO/contagens.tsv" && echo contagens-iguais
```
Passou se: `OK` na produção; `contagens-iguais` (a 1.5.0 em Production não migra ao subir).
Se falhar (ex.: `network … not found`, ou a api não acha o `db`): o J7 recriou o `db` (o healthcheck mudou na
2.0.0) e a rede pode ter sido recriada, ou a produção usa compose v1. Reconecte os contêineres antigos à rede
do projeto com os aliases dos serviços e religue:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "docker network inspect -f '{{.Name}} {{.Id}}' ruinarpg_default; docker inspect -f '{{json .NetworkSettings.Networks}}' $API150 $NGINX150"
prod "docker network disconnect -f ruinarpg_default $API150; docker network disconnect -f ruinarpg_default $NGINX150; docker network connect --alias api ruinarpg_default $API150 && docker network connect --alias nginx ruinarpg_default $NGINX150 && docker start $API150 $NGINX150 && sleep 10 && curl -fsS http://127.0.0.1/api/health; echo"
```
(O `disconnect -f` pode dizer que o contêiner não está na rede: é esperado. Se `ruinarpg_default` não existir,
crie-a antes com `docker network create ruinarpg_default` e conecte também o `$DB` com `--alias db`.) Depois
repita as contagens acima.
**Último recurso** (os contêineres antigos não sobem nem reconectados): removê-los e recriá-los das imagens
guardadas no R5 pelo compose da 1.5.0 (o checkout já está na 1.5.0 desde o X3). As tags `legacy` guardam as
imagens, então o `docker rm` não perde nada:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${IMGAPI150:?rode o P3}" "${IMGNGINX150:?rode o P3}"
prod "docker image inspect ruinarpg-legacy-api:1.5.0 ruinarpg-legacy-nginx:1.5.0 >/dev/null && docker rm $API150 $NGINX150 && docker tag ruinarpg-legacy-api:1.5.0 $IMGAPI150 && docker tag ruinarpg-legacy-nginx:1.5.0 $IMGNGINX150 && cd $REPO && docker compose -p ruinarpg up -d --no-build --no-deps api nginx && sleep 10 && curl -fsS http://127.0.0.1/api/health; echo"
```
O `--no-deps` deixa o `db` como está (é o contêiner que o compose da 2.0.0 criou, com outro healthcheck: sem
ele o compose da 1.5.0 o recriaria); o compose avisa de contêineres órfãos da 2.0.0 (`web`, `api-blue`):
é esperado — **nunca** `--remove-orphans`. Se os nomes novos diferirem de `$API150`/`$NGINX150` (compose
v1 antes), `guardar API150 <nome>` e `guardar NGINX150 <nome>`. Depois repita as contagens acima.

Com `OK` e `contagens-iguais`, **o usuário reabre a rota pública** (aval **sim**) e:
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
: "${URL:?guardar URL (0.1)}"
curl -fsS "$URL/api/health"; echo
```
Passou se: `OK`. Depois, o usuário repete V4 na 1.5.0. **Nunca** `make migrate`/`make deploy` da 1.5.0 aqui.
**Ensaio:** sem a rota pública.

Depois de uma volta: a `main` já tem a 2.0.0 e o `deploy.yml` de qualquer merge novo vai pedir aprovação —
**rejeitar** (como no J9) até uma nova virada ser combinada. O backup `$VOLTA_BKP` (X1b) guarda o que foi
escrito na 2.0.0 entre o J8 e a Volta: fica, como o dump final, até o critério de aceite (D1). Uma nova
tentativa recomeça na seção 5 (J5 remove o `deploy/state` que sobrou).

## 8. Depois (spec H.6)

**D1. Período de observação.** `$API150` e `$NGINX150` ficam parados e não removidos; as imagens
`ruinarpg-legacy-*:1.5.0`, o pacote (`$PACOTE`), o dump final (`$BANCO`) e, se houve Volta, o backup da
2.0.0 (`$VOLTA_BKP`), nos dois lugares quando copiados, ficam até o critério de aceite (2.0.0 sem perda
**e** 2.1.0 lançada). Durante o período, **nunca** `make clean` nem `make clean-data` na produção: os dois
fazem `docker compose down --remove-orphans`, que apagaria os contêineres parados da 1.5.0 (e o
`clean-data` também os volumes). Só depois do critério: `docker rm $API150 $NGINX150` (as tags legacy
guardam as imagens) e, com o aval do usuário, a remoção do resto.

**D2. Remover os Required reviewers** do Environment `production` (o usuário, no GitHub). O próximo deploy
(2.0.1/2.1.0) segue o caminho automático; antes dele, confira de novo
`git merge-base --is-ancestor 7fbbd16 origin/main && echo desfazer-ssh-ok` (notas, passo 12).

**D3. Backlog:** registrar o item "sistema de backup da produção" na 2.2.0 (memória e fila de planos).

**D4. Limpar o aquecimento** (aval **sim**):
```bash
. /home/pro/RuinaRPG/tools/virada/ambiente-producao.sh || exit 1
prod "docker rmi ruinarpg-prebuild-api:aquecimento ruinarpg-prebuild-client:aquecimento ruinarpg-prebuild-smoke:aquecimento"
```

**D5. Linhas perdidas notadas só depois do uso real.** Uma volta completa apagaria o que foi escrito na
2.0.0 desde a virada, então a recuperação é trazer as linhas que faltam do dump final para a 2.0.0
(descrito, não automatizado; cada escrita na produção com o aval do usuário; cada chamada com o ambiente
da produção e `: "${BANCO:?}"`):
1. Restaurar o dump final numa stack descartável nesta VM (**Longo**):
   `passo_longo D5 tools/virada/restaurar-pacote.sh "$LOCAL/$BANCO" --projeto ruinarpg-pacote-recuperacao` (o
   `fingerprint.jsonl` do pacote é o "antes").
2. Tirar um fingerprint novo da produção (`deploy/fingerprint.sh` como no J8, sem parar o site: aqui só as
   linhas perdidas interessam) e comparar: `python3 deploy/compare.py "$LOCAL/$BANCO/fingerprint.jsonl" <fingerprint novo> "$LOCAL/allowlist-vazia.yml" --full <relatório>`.
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
`rm -f ~/.ssh/ruinarpg_virada ~/.ssh/ruinarpg_virada.pub`. O estado (`~/.virada/`) e os logs dos passos
(`$LOGS`) não têm segredo nem dado pessoal: ficam com o pacote até o critério de aceite. Atualizar a
memória e a fila de planos.
