# CLAUDE.md

Guidance for Claude Code in this repository. What the project is, how to run it locally, the `make` targets, the build/test commands and the release pipeline are in [`README.md`](README.md) — read it first; this file only adds what an agent must not get wrong.

## Ground rules

- **`Docs/` is the source of truth.** `Docs/` (an Obsidian vault, all Brazilian Portuguese) holds the rulebook (`Docs/Sistema RPG/`) and the app's requirements (`Docs/Requisitos/`). Every code change must trace back to a requirement there. A task can be documentation work or code work — check which before assuming.
- **TDD is mandatory** (Técnico R0011): write the failing test first, then the minimum code to pass it — for every unit of behavior, not just the convenient ones.
- **`dotnet build` must end with 0 warnings, 0 errors**; a new warning is a failure.
- **Run the three test projects one at a time** (`dotnet test tests/<project>`), never a bare `dotnet test`: together they load the CPU enough to break the client tests that assert real-time windows. Integration needs a running Docker daemon.
- Specs and plans: live ones in `docs/superpowers/specs/` and `docs/superpowers/plans/` (lowercase `docs/`, not the vault); shipped ones move to `docs/superpowers/archive/{specs,plans}/`.

## Runtime-parsed rulebook files

Fourteen files of `Docs/Sistema RPG/` are embedded as build resources in `RuinaRPG.Infrastructure` (see its `.csproj`) and parsed at runtime by `IRulesDataProvider` and the startup seeders: `Tabela de Níveis.md`, `Tabela de Vocação.md`, `Tabela de Classes.md`, `Tabela de Arquetipos.md`, `Tabela de Circulo e Grau por EAP.md`, `Tabelas de XP, Atributos, Características e EAP.md`, `GRAUS & CÍRCULOS.md`, `Ruína RPG - Sistema Básico.md`, `Características.md`, `As estrelas alkerianas.md`, `Historico.md`, `Equipagem.md`, `Tabela de Durabilidade por Rank.md`, `Tabela de Afinidades.md`. **Editing their table structure can break the API**, not just the docs.

Exceptions — Markdown that only seeds once:
- `Tabela de Níveis.md` (with the XP/EAP table in `Tabelas de XP, Atributos, Características e EAP.md`) seeds `NiveisProgressao`/`ColunasDeNivel`/`ValoresDeNivel` only while they are empty, and feeds the Compêndio. After that, level budgets and caps come from the Auditoria table (Auditoria de Regras R0013); editing the Markdown no longer changes them.
- `Tabela de Afinidades.md` seeds `TabelaDeAfinidades` only while it is empty; afterwards the Auditoria table governs (Auditoria de Regras R0016).

## Migrations

- Add one with `dotnet ef migrations add <Name> --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`.
- Since 2.0.0 the chain starts at one baseline (`20261017120000_Baseline_2_0_0`); `--migrate` converts a 1.5.0 history to it (`HistoricoDeMigracoes150`, Técnico R0009). `tools/migrations/verificar-baseline.sh <1.5.0 ref>` proves the baseline equals the old chain.
- `Development` applies pending migrations at API startup. **`Production` never does** (Técnico R0009): `deploy.sh` runs them in a one-off container with the new image before switching colors, and `make migrate` re-runs them. A Production stack brought up without `deploy.sh` (e.g. a bare `docker compose up`) needs `make migrate` after moving to a release that changes the schema.
- Migrations must stay backward compatible with the previous release (the old color keeps serving during a blue-green deploy, and the clone checks it). If not, the PR needs the `deploy:janela` label.
- Keep the EF Core model and `Docs/Requisitos/Requisitos - Modelo de Dados.md` in sync.
- Every secret comes from environment variables (Técnico R0003); the API refuses to start without the `Jwt__*` variables or with a signing key under 32 bytes.

## Safety nets

Frozen snapshots and checkers. When one fails, the change is wrong until proven otherwise — never regenerate a snapshot to make it pass unless the change to the output is intended and explained.

- `tests/RuinaRPG.Tests.Integration/Controllers/RotasDaApiTests.cs` (+ `rotas-da-api.txt`) — the whole HTTP surface: verb, route, parameters, body, response, authorization of every action.
- `tests/RuinaRPG.Tests.Integration/Rules/LivroDeRegrasCongeladoTests.cs` and `RulebookEquipagemCongeladoTests.cs` (+ their `.json`) — what the API renders from `Docs/Sistema RPG/`.
- `tests/RuinaRPG.Tests.Client/Pages/Ficha/RedeDeSeguranca/` — the rendered sheet tabs for the three kinds.
- `python3 tools/docs/verificar-wikilinks.py` — broken `[[wikilinks]]` in `Docs/`.
- `deploy/tests/` (bats + python) — the deploy/gate/clone scripts.

## Code map

- `src/RuinaRPG.Domain` — pure C#, zero dependency on EF Core, ASP.NET or Identity.
- `src/RuinaRPG.Infrastructure` — `RuinaRpgDbContext`, Identity entities, migrations (`Persistence/Migrations`), JWT/refresh-token services, rules parsing and seeders.
- `src/RuinaRPG.Contracts` — request/response records shared by API and client; `Fichas/` has one record per shape shared by the three sheet kinds, not one per kind.
- `src/RuinaRPG.Api` — controllers; composition root and the `--migrate`/`--grant-rules-auditor`/`--reset-gm-password` commands in `Program.cs`. `Controllers/Fichas/` holds the generic sheet controllers (one abstract base per feature, e.g. `FichasController`, `PericiasDaFichaController`), closed per kind by an `ITipoDeFicha` adapter (`TipoPersonagem`, `TipoNpc`, `TipoCriatura`); kind-only endpoints stay in the per-kind subclasses. `Services/FichaStats.cs` is the shared derived-stats calculator (per-kind hooks such as `CargaAsync`).
- `src/RuinaRPG.Client` — Blazor WebAssembly. `Shared/Ficha/` holds the sheet tabs shared by the three kinds, `FichaEstado` (the page state they read and write) and `TipoDeFichaCliente` (per-kind addresses, capability flags and texts); `Pages/FichaDe*.razor` keep only what is exclusive to each kind.
- `tests/` — xUnit. Client tests use bUnit; Integration drives the real API over HTTP against a Testcontainers PostgreSQL.
- `deploy/` — `Dockerfile` (one multi-stage build, targets `api`, `client`, `smoke`), `deploy.sh` (blue-green, backup/restore, rollback), `gate.sh` (SSH forced command), `clone.sh` (release validation on a production clone), `fingerprint.sh`/`compare.py`/`data-changes.yml`, `nginx/`, `provision/`, `tests/`.
- `tools/` — `RuinaRPG.Smoke` (read-only smoke after deploys and in the clone), `docs/`, `migrations/`, `bench/`, `imagens/`.
- `.github/workflows/` — `release.yml` (PR validation, auto-merge), `deploy.yml` (production deploy).

## Requirements map (`Docs/Requisitos/`)

- `01 - Visão Geral.md` — maps every app page/panel (login, signup, GM panel tools, player panel) to the document that specifies it; other docs cite its block names (e.g. "PAINEL DO *GM*, ferramenta 3").
- `Requisitos - Ficha de Personagem.md` — the anchor: the character sheet field by field across 7 tabs. `Ficha de NPCs` and `Ficha de Criaturas` are written as diffs against it — read it first.
- `Requisitos - Modelo de Dados.md` — the relational schema `src/` implements. Its legend defines the 3 linking patterns (independent copy / live catalog reference / live link to another sheet) once; read it before any table.
- `Requisitos - Técnico.md` — stack, solution layout, env vars, auth model, deploy pipeline (R0013–R0016).
- The rest are one per feature: Catálogo de Itens e Equipamentos, Banco de Magias e Habilidades, Banco de Runas, Habilidades Raciais (GM libraries that sheets copy entries from); Campanha (membership, visibility, granted sheets, GM diary, secret notes); Convite de Jogador; Gerenciador de Encontros; Livro de Regras, Compêndio de Regras, Auditoria de Regras (reading and auditing the rulebook); Login e Cadastro.

## Conventions when editing `Docs/`

- **Cross-references use Obsidian `[[wikilink]]` syntax** (e.g. `[[Tabela de Níveis]]`), never Markdown relative links. Update them when renaming a file, and run `tools/docs/verificar-wikilinks.py`.
- **Requirement IDs** are `#` headings numbered `**R0001**`, `**R0002**`, … per file. Continue the sequence; never renumber. A removed requirement keeps its heading, marked removed with one line saying why or where it moved.
- Each requirements doc opens with a blockquoted preamble of conventions specific to that doc (defaults/NULL/placeholder behavior, who may edit what). Follow it rather than restating rules on every field.
- Keep new requirements consistent with the GM/Player roles, the `.env`-configured backend and the data model — and with what `src/` already implements.
- **Numbers live in the `Tabela de *.md` files and `Formulas.md`** (level tables, XP curves, class Vida/Arcana). Don't duplicate them elsewhere; link to the source table, as `Requisitos - Ficha de Personagem.md` does.
- `Docs/Sistema RPG/Fichas/` holds PDF sheet templates; not edited as text.
