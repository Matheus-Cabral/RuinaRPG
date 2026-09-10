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
tests/
  RuinaRPG.Tests.Unit
  RuinaRPG.Tests.Integration
  RuinaRPG.Tests.Client
infra/nginx/               Imagem nginx: builda o cliente Blazor, serve estático e faz proxy de /api/ e /hubs/
```

Sete arquivos de `Docs/Sistema RPG/` (`Ruína RPG - Sistema Básico.md`, `Tabela de Níveis.md`, `Tabela de Vocação.md`, `Tabela de Arquetipos.md`, `Tabela de Circulo e Grau por EAP.md`, `Tabelas de XP, Atributos, Características e EAP.md`, `GRAUS & CÍRCULOS.md`) são embarcados como build resources em `RuinaRPG.Infrastructure` e parseados em runtime por `IRulesDataProvider` — editar a estrutura de tabelas neles pode quebrar a API, não só a documentação.

## Como rodar localmente

Pré-requisitos: [Docker](https://docs.docker.com/get-docker/) e `make`.

```bash
cp .env.example .env
make deploy   # builda as imagens e sobe db, api e nginx
```

A aplicação fica disponível em <http://localhost>, e `/api/health` deve responder `OK`.

### Outros alvos do Makefile

| Alvo | Efeito |
|---|---|
| `make deploy` (ou `make up`) | Builda as imagens e sobe todos os containers. |
| `make down` | Para os containers, mantendo imagens e volumes. |
| `make clean` | Remove containers, imagens locais e órfãos. **Mantém** os volumes nomeados (`pgdata`/`images`). |
| `make clean-data` | Igual a `clean`, mas **também apaga os volumes de dados nomeados**. Destrutivo — os dados se vão. |
| `make logs` | Segue os logs dos containers. |
| `make migrate` | Aplica as migrations do EF Core pendentes contra a stack em execução. |

### Desenvolvimento vs. Produção

`.env.example` vem com `ASPNETCORE_ENVIRONMENT=Development`, o modo certo para rodar local: migrations pendentes são aplicadas automaticamente na subida da API e o Swagger fica exposto. **Um deploy real deve usar `ASPNETCORE_ENVIRONMENT=Production`**, onde as migrations nunca são automáticas — `make migrate` se torna um passo explícito obrigatório depois de `make deploy`. Todo segredo vem de variáveis de ambiente; a API se recusa a iniciar se as variáveis `Jwt__*` estiverem ausentes ou a chave de assinatura tiver menos de 32 bytes.

## Build e testes

```bash
dotnet build   # builda a solução inteira — deve terminar com 0 warnings, 0 errors
dotnet test    # roda os testes unitários e de integração
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

## Documentação

A documentação completa do jogo e dos requisitos do app vive em `Docs/` (abra a pasta como vault no Obsidian para navegar pelos `[[wikilinks]]`). Pontos de entrada úteis:

- `Docs/Requisitos/Requisitos - Ficha de Personagem.md` — documento âncora da ficha de personagem; `Ficha de NPCs` e `Ficha de Criaturas` são diffs sobre ele.
- `Docs/Requisitos/Requisitos - Técnico.md` — stack, layout da solução, variáveis de ambiente e modelo de auth/deploy.
- `Docs/Requisitos/Requisitos - Modelo de Dados.md` — schema relacional implementado por `src/`.
- `Docs/Sistema RPG/Ruína RPG - Sistema Básico.md` — mecânicas centrais do sistema de RPG.

Diretrizes completas para quem for editar o código ou a documentação estão em [`CLAUDE.md`](./CLAUDE.md).
