# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

This is **not a software codebase** — there is no source code, build system, package manifest, linter, or test suite. It is an **Obsidian vault** containing the design documentation for *Ruína RPG*, a tabletop RPG, plus early software requirements for a companion web app that will eventually manage character sheets, campaigns, and bestiaries for that game. All content is written in **Brazilian Portuguese**.

There are no build/lint/test commands to run. When asked to "work in this codebase," the task is almost always to read, write, or restructure Markdown documentation — not to write or run code.

## Repository structure

- `Docs/Sistema RPG/` — the actual RPG rulebook, split into topical files:
  - `Ruína RPG - Sistema Básico.md` — core mechanics: attributes, skills (Perícias), Adrenaline Points (PA) and the dice-scaling system, combat actions, defense vs. dodge, initiative, travel, and the four playable lineages (Humano, Phylauc, Nephrytes, Ecônos) with their sun/moon variants.
  - `Características.md` — the full list of positive/negative character traits and their point costs.
  - `Formulas.md` — derived-stat formulas (Vitality, Focus, Initiative, Movement, defenses, etc.) referenced by other docs.
  - `GRAUS & CÍRCULOS.md` — the magic/ability "Grade & Circle" power-scaling system (per-grade damage, range, cost) and per-grade effect lists.
  - `Tabela de *.md` files — reference tables (Classes, Vocações/Vocations, Níveis/Levels, Arquétipos, XP/Atributos/EAP, Grade-and-Circle by EAP) that other documents cross-reference for progression numbers.
  - `Relação de Vocacão e Classes.md` — maps each Vocação (base class) to its available sub-classes.
  - `Fichas/` — PDF character/NPC/creature sheet templates (source layouts, not meant to be edited as text).
- `Docs/Requisitos/` — software requirements for the character-sheet/campaign-management web app, all fleshed out:
  - `01 - Visão Geral.canvas` — an Obsidian canvas diagramming the app's page/panel flow (login, signup, GM panel, player panel). Predates several docs below (Banco de Magias, Gerenciador de Encontros, Compêndio de Regras, Técnico) — those have no corresponding canvas node yet.
  - `Requisitos - Ficha de Personagem.md` — the anchor document: specifies the character sheet field-by-field across 6 tabs (Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses, Diário). `Requisitos - Ficha de NPCs.md` and `Requisitos - Ficha de Criaturas.md` both define themselves as diffs against this doc rather than restating it — read it first.
  - `Requisitos - Catálogo de Itens e Equipamentos.md`, `Requisitos - Banco de Magias e Habilidades.md` — GM-curated, reusable libraries (items/equipment incl. Artefatos; spells/abilities) that character/NPC/creature sheets reference by picking an entry, which then becomes an independent copy on that sheet.
  - `Requisitos - Campanha.md` — the hub tying players, sheets, and library content together: membership, per-attachment public/private visibility, granting NPC/Criatura sheets to players as pets/summons, the GM-only diary, and player-targeted secret notes.
  - `Requisitos - Convite de Jogador.md`, `Requisitos - Gerenciador de Encontros.md`, `Requisitos - Compêndio de Regras.md` — invite-code flow, GM-only combat/initiative state tracker, and a read-only searchable index over `Docs/Sistema RPG/`.
  - `Requisitos - Login e Cadastro.md` — the three unauthenticated pages (landing, login, signup with GM/Player tabs).
  - `Requisitos - Técnico.md` — chosen stack (ASP.NET Core 8 + EF Core/Npgsql/PostgreSQL + SignalR + Blazor WebAssembly, self-hosted via Docker/nginx), solution layout (Domain/Infrastructure/Contracts/Api/Client/Tests), env vars, auth/authorization model, and deploy notes (host only needs git/docker/make; hosted on a Proxmox-homelab Ubuntu VM behind a Cloudflare Tunnel).
  - `Requisitos - Modelo de Dados.md` — the relational schema (Postgres via EF Core) implementing all of the above: entities, columns, FKs. Non-functional like Técnico; states 3 recurring linking patterns (independent copy / live catalog reference / live-link to another sheet) once in a legend rather than re-explaining them per table — read that legend first. No code exists yet — this and Técnico record decisions for whenever implementation starts.

## Conventions to follow when editing

- **Cross-references use Obsidian `[[wikilink]]` syntax** (e.g. `[[Tabela de Níveis]]`), not Markdown relative links. Keep new links in the same style, and update them if you rename a file — nothing will flag a broken wikilink automatically.
- **Requirement IDs**: requirements in `Requisitos - *.md` are numbered `**R0001**`, `**R0002**`, … as `#` headings. Continue the sequence within a file rather than renumbering existing ones.
- Each requirements doc opens with a blockquoted preamble describing conventions specific to that doc (e.g. field default/NULL/placeholder behavior, who is allowed to edit what). Follow the established preamble rather than restating rules inline on every field.
- The requirements describe a system with GM and Player roles, an `.env`-configured backend (e.g. `img_max_size`), and a relational data model (character sheets, invite codes, campaigns) — keep new requirements consistent with that implied architecture even though no backend exists yet.
- Numeric game data (level tables, XP curves, class Vida/Arcana columns) lives in the `Tabela de *.md` files and `Formulas.md` — don't duplicate numbers inline elsewhere; link to the source table instead, matching how `Requisitos - Ficha de Personagem.md` does it.
