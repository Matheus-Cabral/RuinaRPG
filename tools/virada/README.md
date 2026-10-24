# tools/virada — pacote da 1.5.0 (sub-projeto H)

Scripts da virada da produção da 1.5.0 para a 2.0.0. São a rede de segurança dos dados da produção:
recusam gerar ou aceitar um pacote pela metade.

## `pacote.sh` — roda NA PRODUÇÃO

```
sudo ./pacote.sh /var/backups/ruinarpg-virada            # pacote completo, antes da janela
sudo ./pacote.sh --so-banco /var/backups/ruinarpg-virada # dump final, dentro da janela (api e nginx parados)
```

- Autossuficiente: a produção roda a 1.5.0 (`729dac9`), que não tem `deploy/`. Copie só `pacote.sh` e
  `contagens.sql`, juntos, para fora do checkout (ex.: `/root/virada/`). Precisa de root, docker com
  compose, coreutils, `tar` e `sha256sum`.
- Lê a stack do projeto compose `ruinarpg`: o contêiner em execução do serviço `db`, o volume
  `ruinarpg_images` (conferido contra o que a `api` monta em `/images`) e, no pacote completo, as imagens
  que os contêineres `api` e `nginx` estão rodando (o nome da imagem tem de apontar para o mesmo id do
  contêiner, senão recusa). Usuário e banco vêm do ambiente do contêiner do `db`.
- Gera `<destino>/<AAAAMMDDTHHMMSSZ>-pacote-150/` (ou `-banco-150/`), diretório 700, arquivos 600:
  `dump.tar` (`db.dump` + `images.tar` + `sha`, o formato do `deploy/clone.sh`), `contagens.tsv`,
  `SHA256SUMS` e, no completo, `imagens-docker.tar` (`docker save`) e `imagens.tsv`
  (`serviço<TAB>imagem<TAB>id` de db, api, nginx).
- O `pg_dump` e as contagens usam o mesmo snapshot (transação `REPEATABLE READ READ ONLY` com
  `pg_export_snapshot()`): as contagens descrevem exatamente o dump, mesmo com a aplicação no ar.
- Escreve em `.<nome>.partial` e renomeia no fim; qualquer falha sai com código ≠ 0 e apaga o parcial.
  Na saída padrão vai só o caminho do pacote.
- **Nunca** para, reinicia, marca, baixa ou apaga nada no docker, e nunca escreve no banco.

## `restaurar-pacote.sh` — roda NESTA VM

```
tools/virada/restaurar-pacote.sh <dir-pacote> [--projeto ruinarpg-pacote] [--porta 8091]
tools/virada/restaurar-pacote.sh --remover [--projeto ruinarpg-pacote]
```

- Confere o `SHA256SUMS`; sobe um projeto descartável (postgres:16-alpine e, no pacote completo, as
  imagens da 1.5.0 carregadas de `imagens-docker.tar`, api em Production, nginx em `127.0.0.1:<porta>`);
  restaura `db.dump` e `images.tar`; compara as contagens com `contagens.tsv` (diferença → código 1 com
  as tabelas); grava `<dir-pacote>/fingerprint.jsonl` (`deploy/fingerprint.sh`); confere que a api
  responde `/api/health` e que o nginx serve o primeiro arquivo do volume com os mesmos bytes; imprime
  `OK pacote <dir>`. Contagens e fingerprint são tirados antes de a api subir.
- Num pacote `--so-banco` só restaura e confere banco e volume:
  `OK pacote <dir> (só banco: ...)`.
- O projeto fica de pé para inspeção; `--remover` o derruba com os volumes.
- **Nunca** toca a stack de desenvolvimento (`ruinarpg`) nem a produção: só aceita projetos que começam
  com `ruinarpg-pacote` ou `ruinarpg-bats` e recusa um projeto que já exista. `docker load` pode
  re-marcar nesta VM o nome da imagem da 1.5.0 (ex.: `ruinarpg-api:latest`); a stack de desenvolvimento
  usa tags por sha e não é afetada.

## `ensaio-150.sh` — roda NESTA VM

```
tools/virada/ensaio-150.sh preparar <dir-pacote> <dir-checkout> [--remover-stack-atual]
tools/virada/ensaio-150.sh desfazer <dir-checkout> [--remover-stack-atual]
```

- Recria aqui uma stack da 1.5.0 igual à da produção, carregada do pacote completo, para ensaiar o
  runbook da virada ao pé da letra: `<dir-checkout>` vira um `git worktree` de `729dac9` com o `.env`
  deste repositório (forçado `ASPNETCORE_ENVIRONMENT=Production`, arquivo 600; nada do `.env` é
  impresso), as imagens do pacote são marcadas com os nomes que o compose da 1.5.0 deriva para os
  serviços com `build:` (`ruinarpg-api`, `ruinarpg-nginx`) e o projeto sobe pelo `docker-compose.yml` da
  1.5.0 (sem build). Restaura `db.dump` e `images.tar`, confere as contagens antes e depois de a api
  subir e o formato: só `db`, `api`, `nginx` (`ruinarpg-db-1`, `ruinarpg-api-1`, `ruinarpg-nginx-1`),
  nginx na porta 80, api em Production com as imagens do pacote, `/api/health` e o volume de imagens
  servido. Imprime `OK ensaio <projeto> em <dir> (porta N)`.
- O nome do diretório tem de ser o do projeto (ex.: `/home/pro/ensaio/RuinaRPG` para `ruinarpg`, como
  `/home/ruina/RuinaRPG` na produção): o Makefile da 1.5.0 roda `docker compose` sem `-p`.
- Se o projeto já existe (nesta VM, a stack 2.0.0 de desenvolvimento em `ruinarpg`), recusa; com
  `--remover-stack-atual` a remove **com os volumes**.
- `desfazer` remove o projeto (também uma 2.0.0 que o ensaio tenha instalado no checkout) e o worktree;
  recusa um projeto com contêineres de outro diretório sem `--remover-stack-atual`. As imagens ficam.
- `RUINA_ENSAIO_PROJECT` (padrão `ruinarpg`; ou `ruinarpg-bats-*`/`ruinarpg-ensaio-*`),
  `RUINA_ENSAIO_PORTA` (padrão 80; outra porta vai num `docker-compose.override.yml` em 127.0.0.1),
  `RUINA_ENSAIO_ENV` (padrão `<repo>/.env`). Os testes usam `ruinarpg-bats-ensaio-*` e porta alta.

`comum.sh` tem as funções que `restaurar-pacote.sh` e `ensaio-150.sh` dividem (checksum, restauração,
contagens, health, imagem do nginx, remoção do projeto).

## `contagens.sql`

Uma linha `tabela<TAB>linhas` por tabela do schema `public` (inclui Identity e
`__EFMigrationsHistory`), `count(*)` exato, ordenada por nome em ordem de bytes — comparável byte a byte
entre a produção e a restauração.

## Testes

`deploy/tests/run-bats.sh deploy/tests/virada.bats` — só projetos descartáveis `ruinarpg-bats-*`.

Os pacotes têm dados pessoais: ficam só na produção e nesta VM, diretórios 700 / arquivos 600, nunca no
git nem no GitHub.
