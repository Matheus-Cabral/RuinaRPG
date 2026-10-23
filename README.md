# Ruína RPG

**Ruína RPG** é um sistema de RPG de mesa. Este repositório guarda duas coisas lado a lado:

1. **A documentação do jogo** (`Docs/`): um vault do [Obsidian](https://obsidian.md/) com o livro de regras e os requisitos do aplicativo web, tudo em português do Brasil.
2. **O aplicativo web** que implementa esses requisitos: gerencia fichas de personagem, NPCs e criaturas, campanhas, catálogos de itens e magias, encontros e o livro de regras. GMs curam os catálogos e conduzem campanhas; jogadores criam e evoluem suas fichas dentro delas.

## Stack

| Camada | Tecnologia |
|---|---|
| API | ASP.NET Core 8 (`src/RuinaRPG.Api`) |
| Banco de dados | PostgreSQL, via EF Core / Npgsql |
| Tempo real | SignalR |
| Cliente | Blazor WebAssembly 8 (`src/RuinaRPG.Client`), servido pelo nginx |
| Deploy | Docker Compose, blue-green atrás do nginx (`deploy/`) |
| Testes | xUnit: `tests/RuinaRPG.Tests.Unit`, `.Client` e `.Integration` (Postgres real via Testcontainers) |

## Como rodar localmente

Pré-requisitos: Docker, `make` e git.

```bash
cp .env.example .env
make deploy
```

`make deploy` builda as imagens, aplica migrations e seeders e sobe `db`, a API (cor `api-blue` ou `api-green`) e o nginx. O app fica em <http://localhost>, e <http://localhost/api/health> deve responder `OK`.

O deploy faz um backup antes de migrar, em `/var/backups/ruinarpg` por padrão, que só o root grava. Para rodar sem `sudo`, aponte para uma pasta sua: `RUINA_BACKUP_DIR=$HOME/ruina-backups make deploy`.

O `.env.example` vem com `ASPNETCORE_ENVIRONMENT=Development`: migrations pendentes são aplicadas na subida da API e o Swagger fica exposto. Em produção use `Production`, em que só o `deploy.sh` migra. Os segredos vêm do `.env`; a API não sobe sem as variáveis `Jwt__*` (chave de assinatura com 32 bytes ou mais).

| Alvo | Efeito |
|---|---|
| `make deploy` (ou `make up`) | Implanta o HEAD como está: build, migrations, troca de cor. Recusa alterações locais em arquivos versionados. |
| `make down` | Para os containers, mantendo imagens e volumes. |
| `make clean` | Remove containers, imagens locais e órfãos. **Mantém** os volumes `pgdata` e `images`. |
| `make clean-data` | Igual a `clean`, mas **apaga também os volumes de dados**. Destrutivo. |
| `make logs` | Segue os logs dos containers. |
| `make migrate` | Reaplica migrations e seeders com a imagem da cor ativa. |
| `make status` | Cor ativa, releases e backups mais recentes. |
| `make rollback` | Volta para a release anterior. |
| `make grant-rules-auditor` / `revoke-rules-auditor` / `reset-gm-password` | Com `EMAIL=...`: dá ou tira o papel de Auditor de Regras de um GM, ou redefine a senha dele. |

## Build e testes

Pré-requisitos: .NET 8 SDK e um daemon Docker em execução (os testes de integração sobem um Postgres).

```bash
dotnet build                                 # deve terminar com 0 warnings e 0 errors
dotnet test tests/RuinaRPG.Tests.Unit
dotnet test tests/RuinaRPG.Tests.Client      # ~3 min
dotnet test tests/RuinaRPG.Tests.Integration # ~5 min
```

Rode os três projetos um de cada vez, como o CI faz: juntos, eles carregam a CPU a ponto de quebrar os testes do cliente que medem janelas de tempo. Para rodar só uma parte: `dotnet test tests/<projeto> --filter FullyQualifiedName~NomeDoTeste`.

Outras verificações:

```bash
deploy/tests/run-bats.sh                          # testes do pipeline (deploy, gate, clone...)
python3 -m unittest deploy/tests/test_compare.py  # comparador de impressões digitais dos dados
python3 tools/docs/verificar-wikilinks.py         # [[wikilinks]] sem destino em Docs/
```

## Como uma release chega à produção

O deploy normal é automático (`Docs/Requisitos/Requisitos - Técnico.md`, R0013 a R0016):

1. **PR para `main`.** O `release.yml` roda o CI completo e depois monta um clone descartável com um dump real da produção, onde implanta o `main` e em seguida o PR. Confere as impressões digitais dos dados antes e depois, o smoke somente-leitura e se a versão anterior ainda funciona com o novo schema.
2. **Auto-merge.** Aprovado, o PR sai de rascunho e o auto-merge é habilitado.
3. **Deploy.** Na `main`, o `deploy.yml` implanta em produção (blue-green, sem downtime), roda o smoke e reverte sozinho se algo falhar. Um PR com o rótulo `deploy:janela` é implantado em janela de manutenção, com backup restaurável.

**Validação reprovada:** o PR volta a rascunho e recebe o comentário "Validação do release falhou", com o resultado do `ci` e do `clone` e o relatório do clone (etapa que falhou, dados perdidos ou alterados). Dados que mudam de propósito são declarados em `deploy/data-changes.yml`. Os logs completos ficam na execução e no artefato `clone-logs` (3 dias).

**Ensaio sem PR:** Actions > `release` > *Run workflow* roda a mesma validação, sem comentar nem fazer merge.

**Caminho manual de emergência**, na VM de produção, sempre com `sudo`: `sudo make deploy`, `sudo make migrate`, `sudo make status`, `sudo make rollback`; `sudo deploy/deploy.sh deploy <sha> --window` para uma janela; `sudo deploy/deploy.sh backup` e `sudo deploy/deploy.sh restore <diretório>`. Cada deploy, rollback e restore fica registrado em `deploy/state/deploy.log`. Em produção os backups ficam em `/var/backups/ruinarpg` (os 5 mais recentes, mais o da janela e o do último deploy), com cópias em `/var/backups/ruinarpg-offsite`.

O provisionamento único das máquinas, chaves e segredos está em [`deploy/provision/README.md`](deploy/provision/README.md).

## Onde está a documentação

Abra `Docs/` como vault no Obsidian para navegar pelos `[[wikilinks]]`.

- `Docs/Sistema RPG/` — o livro de regras. Comece por `Ruína RPG - Sistema Básico.md`.
- `Docs/Requisitos/` — os requisitos do app, um documento por área. `01 - Visão Geral.md` mapeia cada página do app ao documento que a especifica; `Requisitos - Técnico.md` e `Requisitos - Modelo de Dados.md` descrevem a stack, o deploy e o schema.
- `docs/superpowers/` (minúsculo, fora do vault) — specs e planos de implementação; os já entregues ficam em `docs/superpowers/archive/`.
- [`CLAUDE.md`](./CLAUDE.md) — orientações para o agente que edita o código e a documentação.
