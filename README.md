# Ruína RPG

**Ruína RPG** é um sistema de RPG de mesa, e este repositório reúne, lado a lado:

1. **A documentação de design do jogo** (`Docs/`) — um vault do [Obsidian](https://obsidian.md/) com o livro de regras completo e os requisitos de software do aplicativo web que o acompanha, tudo em **português do Brasil**.
2. **O aplicativo web** que implementa esses requisitos — uma solução .NET 8 (`RuinaRPG.sln`) com API em ASP.NET Core, cliente Blazor WebAssembly, PostgreSQL via EF Core e deploy via Docker/nginx.

O app gerencia fichas de personagem, campanhas e bestiários para as mesas de Ruína RPG: GMs curam catálogos de itens/magias/monstros e conduzem campanhas; jogadores criam e evoluem suas fichas dentro delas.

> `Docs/` é a fonte da verdade do produto — toda mudança de código deve rastrear até um requisito em `Docs/Requisitos/`.

## Stack

| Camada | Tecnologia |
|---|---|
| API | ASP.NET Core 8 |
| Banco de dados | PostgreSQL, via EF Core / Npgsql |
| Tempo real | SignalR |
| Cliente | Blazor WebAssembly 8 |
| Deploy | Docker Compose + nginx (reverse proxy + serve do cliente) |
| Testes | xUnit (`RuinaRPG.Tests.Unit`, `.Integration`, `.Client`) — a integração usa Testcontainers para subir um Postgres real |

## Estrutura do repositório

```
Docs/                     Vault Obsidian — regras do jogo e requisitos do app (PT-BR)
  Sistema RPG/             Livro de regras: atributos, perícias, combate, magia, linhagens...
  Requisitos/              Requisitos de software do app, um documento por área
docs/superpowers/plans/   Planos de implementação (pasta minúscula, não confundir com Docs/)
src/
  RuinaRPG.Domain          C# puro, sem dependência de EF Core/ASP.NET/Identity
  RuinaRPG.Infrastructure  DbContext, entidades do Identity, migrations, serviços de JWT
  RuinaRPG.Contracts       Records de request/response compartilhados entre API e cliente
  RuinaRPG.Api             Web API (controllers) — composition root em Program.cs
  RuinaRPG.Client          Blazor WebAssembly, servido pelo nginx
tools/
  RuinaRPG.Smoke           Smoke somente-leitura usado pelo deploy e pela validação do release
deploy/                    deploy.sh (blue-green), gate.sh, clone.sh, nginx/, provision/, tests/
.github/workflows/         release.yml (validação do PR) e deploy.yml (produção)
tests/
  RuinaRPG.Tests.Unit
  RuinaRPG.Tests.Integration
  RuinaRPG.Tests.Client
infra/client/              Build do cliente Blazor (publicado em releases versionados servidos pelo nginx de deploy/nginx/)
```

Sete arquivos de `Docs/Sistema RPG/` (`Ruína RPG - Sistema Básico.md`, `Tabela de Níveis.md`, `Tabela de Vocação.md`, `Tabela de Arquetipos.md`, `Tabela de Circulo e Grau por EAP.md`, `Tabelas de XP, Atributos, Características e EAP.md`, `GRAUS & CÍRCULOS.md`) são embarcados como build resources em `RuinaRPG.Infrastructure` e parseados em runtime por `IRulesDataProvider` — editar a estrutura de tabelas neles pode quebrar a API, não só a documentação.

## Como rodar localmente

Pré-requisitos: [Docker](https://docs.docker.com/get-docker/) e `make`.

```bash
cp .env.example .env
make deploy   # chama deploy/deploy.sh: builda as imagens e sobe db, api (blue/green) e nginx
```

A aplicação fica disponível em <http://localhost>, e `/api/health` deve responder `OK`.

### Outros alvos do Makefile

| Alvo | Efeito |
|---|---|
| `make deploy` (ou `make up`) | Chama `deploy/deploy.sh deploy <sha do HEAD>`: builda, migra e troca a cor ativa. Caminho manual. |
| `make down` | Para os containers, mantendo imagens e volumes. |
| `make clean` | Remove containers, imagens locais e órfãos. **Mantém** os volumes nomeados (`pgdata`/`images`). |
| `make clean-data` | Igual a `clean`, mas **também apaga os volumes de dados nomeados**. Destrutivo — os dados se vão. |
| `make logs` | Segue os logs dos containers. |
| `make migrate` | `deploy/deploy.sh migrate`: migrations e seeders com a imagem da cor ativa. |
| `make status` | Cor ativa, releases e backups mais recentes. |
| `make rollback` | Volta para a release anterior (num deploy em janela, restaura o backup da janela). |
| `make grant-rules-auditor` / `revoke-rules-auditor` / `reset-gm-password` | `EMAIL=...`; rodam na cor ativa via `deploy.sh exec-api`. |

### Desenvolvimento vs. Produção

`.env.example` vem com `ASPNETCORE_ENVIRONMENT=Development`, o modo certo para rodar local: migrations pendentes são aplicadas automaticamente na subida da API e o Swagger fica exposto. **Um deploy real deve usar `ASPNETCORE_ENVIRONMENT=Production`**, onde as migrations nunca são automáticas na subida da API: o `deploy.sh` as executa, com a imagem nova, antes de trocar a cor ativa (`make migrate` serve para reaplicá-las manualmente). Todo segredo vem de variáveis de ambiente; a API se recusa a iniciar se as variáveis `Jwt__*` estiverem ausentes ou a chave de assinatura tiver menos de 32 bytes.

## Build e testes

```bash
dotnet build   # builda a solução inteira — deve terminar com 0 warnings, 0 errors
dotnet test    # roda os testes unitários, de cliente e de integração
deploy/tests/run-bats.sh                     # testes bats do pipeline (deploy, gate, clone...)
python3 -m unittest deploy/tests/test_compare.py   # comparador de impressões digitais
```

`dotnet test` **requer um daemon Docker em execução**: os testes de integração sobem um PostgreSQL real via Testcontainers. Para restringir a execução:

```bash
dotnet test --filter FullyQualifiedName~SomeTests
```

**TDD é obrigatório** neste projeto (`Docs/Requisitos/Requisitos - Técnico.md`, R0011): o teste que falha vem antes do código mínimo que o faz passar, para cada unidade de comportamento.

### Migrations do EF Core

```bash
dotnet ef migrations add <Nome> \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

## Deploy e releases (2.0.0)

O deploy normal é automático (`Docs/Requisitos/Requisitos - Técnico.md`, R0013-R0016); o provisionamento único das máquinas, chaves e segredos está em [`deploy/provision/README.md`](deploy/provision/README.md).

**O que acontece num PR para `main`** (`release.yml`, só PRs do dono): CI completo, depois um clone descartável com um dump real da produção, onde o `main` e o PR são implantados em sequência; as impressões digitais dos dados antes/depois, o smoke somente-leitura e a compatibilidade da versão anterior com o novo schema são conferidos. Aprovado: o PR sai de rascunho e o auto-merge é habilitado. Ao entrar na `main`, `deploy.yml` implanta em produção (blue-green, sem downtime; com o rótulo `deploy:janela`, em janela de manutenção), faz smoke, e reverte sozinho se algo falhar.

**Lendo uma validação reprovada**: o PR vira rascunho e recebe um comentário "Validação do release falhou" com o resultado do `ci` e do `clone` e o relatório do clone (etapa que falhou, achados de perda/alteração de dados). Dados que mudam de propósito devem ser declarados em `deploy/data-changes.yml`. Os logs completos ficam no link da execução e no artefato `clone-logs` (3 dias).

**Ensaio sem PR**: Actions > `release` > *Run workflow* (`workflow_dispatch`, opcionalmente informando o sha) roda a mesma validação, sem comentar nem fazer merge.

**Caminho manual de emergência** (na VM de produção, no checkout, sempre com `sudo`: `ruina` não está no grupo `docker`): `sudo make deploy`, `sudo make migrate`, `sudo make status`, `sudo make rollback`; `sudo deploy/deploy.sh deploy <sha> --window` para uma janela, `sudo deploy/deploy.sh backup` e `sudo deploy/deploy.sh restore <diretório>` (sob manutenção). `make deploy` implanta o HEAD como está (sem fetch nem checkout) e recusa um checkout com alterações locais em arquivos versionados. O log de cada deploy, rollback e restore fica em `deploy/state/deploy.log`.

**Backups**: em produção, `/var/backups/ruinarpg` (`root`, `0700`; um por deploy, os 5 mais recentes, mais o da janela e o do último deploy); nesta VM, cópias de cada dump em `/var/backups/ruinarpg-offsite` (5 mais recentes).

## Documentação

A documentação completa do jogo e dos requisitos do app vive em `Docs/` (abra a pasta como vault no Obsidian para navegar pelos `[[wikilinks]]`). Pontos de entrada úteis:

- `Docs/Requisitos/Requisitos - Ficha de Personagem.md` — documento âncora da ficha de personagem; `Ficha de NPCs` e `Ficha de Criaturas` são diffs sobre ele.
- `Docs/Requisitos/Requisitos - Técnico.md` — stack, layout da solução, variáveis de ambiente e modelo de auth/deploy.
- `Docs/Requisitos/Requisitos - Modelo de Dados.md` — schema relacional implementado por `src/`.
- `Docs/Sistema RPG/Ruína RPG - Sistema Básico.md` — mecânicas centrais do sistema de RPG.

Diretrizes completas para quem for editar o código ou a documentação estão em [`CLAUDE.md`](./CLAUDE.md).
