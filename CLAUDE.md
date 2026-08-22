# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

This repository holds **two things side by side**:

1. An **Obsidian vault** (`Docs/`) containing the design documentation for *Ruína RPG*, a tabletop RPG, plus the software requirements for a companion web app that manages character sheets, campaigns, and bestiaries for that game. All of this content is written in **Brazilian Portuguese**.
2. The **web app implementing those requirements** — a .NET 8 solution (`RuinaRPG.sln`, `src/`, `tests/`) with an ASP.NET Core API, a Blazor WebAssembly client, PostgreSQL via EF Core, and a Docker/nginx deployment (`docker-compose.yml`, `infra/`, `Makefile`).

The `Docs/` side is still the source of truth: code changes must trace back to a requirement in `Docs/Requisitos/`. A task here can be either documentation work or code work — check which before assuming.

Implementation plans live in `docs/superpowers/plans/` (lowercase `docs/`, distinct from the `Docs/` vault).

## Build, test, and run

- `dotnet build` — builds the whole solution. It is expected to finish with **0 warnings, 0 errors**; treat a new warning as a failure.
- `dotnet test` — runs `tests/RuinaRPG.Tests.Unit` and `tests/RuinaRPG.Tests.Integration`. **Requires a running Docker daemon**: the integration tests spin up a real PostgreSQL via Testcontainers. Narrow a run with `dotnet test --filter FullyQualifiedName~SomeTests`.
- `cp .env.example .env && make deploy` — brings the full stack (`db`, `api`, `nginx`) up locally on <http://localhost>. `/api/health` should answer `OK`.
- EF Core migrations: `dotnet ef migrations add <Name> --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`.

**TDD is mandatory** (`Docs/Requisitos/Requisitos - Técnico.md` R0011): write the failing test first, then the minimum code to pass it — for every unit of behavior, not just the convenient ones.

### `make` targets

| Target | Effect |
|---|---|
| `make deploy` (or `make up`) | Builds the images and starts all containers. |
| `make down` | Stops the containers, keeping images and volumes. |
| `make clean` | Removes containers, locally-built images and orphans. **Keeps** the named `pgdata`/`images` volumes. |
| `make clean-data` | Same as `clean`, but **also deletes the named data volumes**. Destructive — data is gone. |
| `make logs` | Follows the container logs. |
| `make migrate` | Applies pending EF Core migrations against the running stack. |

### Development vs. Production

`.env.example` defaults to `ASPNETCORE_ENVIRONMENT=Development`, which is right for local work: pending migrations are applied automatically at API startup and Swagger is exposed. A **real deploy must set `ASPNETCORE_ENVIRONMENT=Production`**, and there migrations are never automatic (Técnico R0009) — `make migrate` becomes a mandatory explicit step after `make deploy`. Every secret comes from environment variables (Técnico R0003); the API refuses to start if the `Jwt__*` variables are missing or the signing key is shorter than 32 bytes.

## Repository structure

- `Docs/Sistema RPG/` — the actual RPG rulebook, split into topical files:
  - `Ruína RPG - Sistema Básico.md` — core mechanics: attributes, skills (Perícias), Adrenaline Points (PA) and the dice-scaling system, combat actions, defense vs. dodge, initiative, travel, and the four playable lineages (Humano, Phylauc, Nephrytes, Ecônos) with their sun/moon variants.
  - `Características.md` — the full list of positive/negative character traits and their point costs.
  - `Formulas.md` — derived-stat formulas (Vitality, Focus, Initiative, Movement, defenses, etc.) referenced by other docs.
  - `GRAUS & CÍRCULOS.md` — the magic/ability "Grade & Circle" power-scaling system (per-grade damage, range, cost) and per-grade effect lists.
  - `Tabela de *.md` files — reference tables (Classes, Vocações/Vocations, Níveis/Levels, Arquétipos, XP/Atributos/EAP, Grade-and-Circle by EAP) that other documents cross-reference for progression numbers.
  - `Relação de Vocacão e Classes.md` — maps each Vocação (base class) to its available sub-classes.
  - `Fichas/` — PDF character/NPC/creature sheet templates (source layouts, not meant to be edited as text).
  - Seven of these files (`Tabela de Níveis.md`, `Tabela de Vocação.md`, `Tabela de Arquetipos.md`, `Tabela de Circulo e Grau por EAP.md`, `Tabelas de XP, Atributos, Características e EAP.md`, `GRAUS & CÍRCULOS.md`, `Ruína RPG - Sistema Básico.md`) are embedded as build resources in `RuinaRPG.Infrastructure` and parsed at runtime by `IRulesDataProvider` — editing their table structure can break the API, not just the docs.
- `Docs/Requisitos/` — software requirements for the character-sheet/campaign-management web app, all fleshed out:
  - `01 - Visão Geral.canvas` — an Obsidian canvas diagramming the app's page/panel flow (login, signup, GM panel, player panel). Predates several docs below (Banco de Magias, Gerenciador de Encontros, Compêndio de Regras, Técnico) — those have no corresponding canvas node yet.
  - `Requisitos - Ficha de Personagem.md` — the anchor document: specifies the character sheet field-by-field across 6 tabs (Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses, Diário). `Requisitos - Ficha de NPCs.md` and `Requisitos - Ficha de Criaturas.md` both define themselves as diffs against this doc rather than restating it — read it first.
  - `Requisitos - Catálogo de Itens e Equipamentos.md`, `Requisitos - Banco de Magias e Habilidades.md` — GM-curated, reusable libraries (items/equipment incl. Artefatos; spells/abilities) that character/NPC/creature sheets reference by picking an entry, which then becomes an independent copy on that sheet.
  - `Requisitos - Campanha.md` — the hub tying players, sheets, and library content together: membership, per-attachment public/private visibility, granting NPC/Criatura sheets to players as pets/summons, the GM-only diary, and player-targeted secret notes.
  - `Requisitos - Convite de Jogador.md`, `Requisitos - Gerenciador de Encontros.md`, `Requisitos - Compêndio de Regras.md` — invite-code flow, GM-only combat/initiative state tracker, and a read-only searchable index over `Docs/Sistema RPG/`.
  - `Requisitos - Login e Cadastro.md` — the three unauthenticated pages (landing, login, signup with GM/Player tabs).
  - `Requisitos - Técnico.md` — chosen stack (ASP.NET Core 8 + EF Core/Npgsql/PostgreSQL + SignalR + Blazor WebAssembly, self-hosted via Docker/nginx), solution layout (Domain/Infrastructure/Contracts/Api/Client/Tests), env vars, auth/authorization model, and deploy notes (host only needs git/docker/make; hosted on a Proxmox-homelab Ubuntu VM behind a Cloudflare Tunnel).
  - `Requisitos - Modelo de Dados.md` — the relational schema (Postgres via EF Core) implementing all of the above: entities, columns, FKs. Non-functional like Técnico; states 3 recurring linking patterns (independent copy / live catalog reference / live-link to another sheet) once in a legend rather than re-explaining them per table — read that legend first. This and Técnico are what `src/` implements — keep the schema here and the EF Core model in sync.

### Code

- `src/RuinaRPG.Domain` — pure C#, zero dependency on EF Core, ASP.NET, or Identity.
- `src/RuinaRPG.Infrastructure` — EF Core `RuinaRpgDbContext`, ASP.NET Core Identity entities, migrations, JWT/refresh-token services.
- `src/RuinaRPG.Contracts` — request/response records shared by the API and the client.
- `src/RuinaRPG.Api` — ASP.NET Core 8 Web API (controllers), the composition root in `Program.cs`.
- `src/RuinaRPG.Client` — Blazor WebAssembly 8 standalone app, served by nginx.
- `tests/RuinaRPG.Tests.Unit`, `tests/RuinaRPG.Tests.Integration` — xUnit; the integration project drives the real API over HTTP against a Testcontainers PostgreSQL.
- `infra/nginx/` — the nginx image that builds the Blazor client, serves it, and reverse-proxies `/api/` and `/hubs/` to the API.

## Conventions to follow when editing `Docs/`

- **Cross-references use Obsidian `[[wikilink]]` syntax** (e.g. `[[Tabela de Níveis]]`), not Markdown relative links. Keep new links in the same style, and update them if you rename a file — nothing will flag a broken wikilink automatically.
- **Requirement IDs**: requirements in `Requisitos - *.md` are numbered `**R0001**`, `**R0002**`, … as `#` headings. Continue the sequence within a file rather than renumbering existing ones.
- Each requirements doc opens with a blockquoted preamble describing conventions specific to that doc (e.g. field default/NULL/placeholder behavior, who is allowed to edit what). Follow the established preamble rather than restating rules inline on every field.
- The requirements describe a system with GM and Player roles, an `.env`-configured backend (e.g. `img_max_size`), and a relational data model (character sheets, invite codes, campaigns) — keep new requirements consistent with that architecture, and with what `src/` already implements.
- Numeric game data (level tables, XP curves, class Vida/Arcana columns) lives in the `Tabela de *.md` files and `Formulas.md` — don't duplicate numbers inline elsewhere; link to the source table instead, matching how `Requisitos - Ficha de Personagem.md` does it.
