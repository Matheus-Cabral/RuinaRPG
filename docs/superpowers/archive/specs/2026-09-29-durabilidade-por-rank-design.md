# Durabilidade por Rank — Design

**Status:** approved by user 2026-09-29 (design approved in chat, "aprovado, pode implementar").

## Overview

The free-typed **Durabilidade** field on Arma, Armadura and Escudo (Catálogo de Itens) is replaced by
the item's **Rank**: the maximum durability comes from a global Rank → Durabilidade table that the
Rules Auditor can edit.

| Rank | Durabilidade |
|---|---|
| F | 20 |
| E | 45 |
| D | 80 |
| C | 125 |
| B | 180 |
| A | 245 |
| S | Inquebrável |
| SS | Inquebrável |

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Which items get a Rank | Arma, Armadura **and** Escudo. The Arma's existing "Tier" (F–S) **becomes "Rank"** everywhere in the UI and gains **SS**; Armadura and Escudo gain a new Rank field. Criatura Rank (F–S) is unrelated and unchanged. |
| Item with no Rank (NULL) | No durability — same behaviour as today's NULL durability. |
| Existing hand-typed durability values | **Discarded.** Armas keep their Tier as Rank; Armaduras/Escudos start with no Rank. |
| Where the numbers live | New `Docs/Sistema RPG/Tabela de Durabilidade por Rank.md` (initial values), seeded into a DB table editable on a new **Auditoria** page. |
| Auditoria page | One fixed row per Rank (F–SS); the Auditor edits the number or marks "Inquebrável". No create/delete. |
| Livro de Regras | The table does **not** appear there. |
| Inquebrável on the sheets | Shows "Inquebrável", no Durabilidade atual field. |
| Max lowered (Rank change / Auditoria edit) | The sheet's Durabilidade atual is capped at the new max (on read and on the next save). Max raised: atual is not refilled. |

## Data

- Enum `RuinaRPG.Domain.Items.Tier` is renamed **`RankDeItem`** with values `F, E, D, C, B, A, S, SS` (SS appended — stored as int).
- `Arma.Tier` → `Arma.Rank` (column renamed, values preserved). `EquipmentKitChoiceSlot.Tier` → `Rank` likewise.
- `Armadura.Rank`, `Escudo.Rank` added (nullable).
- `DurabilidadeMaxima` removed from Arma, Armadura, Escudo (columns dropped).
- New global table `DurabilidadesPorRank`: `Rank` (PK, int), `Durabilidade` (int, nullable), `Inquebravel` (bool). Seeded insert-if-missing from the Tabela file (embedded resource), same pattern as `HistoricoSeeder`.
- Sheet rows keep only `DurabilidadeAtual` (unchanged); the max is resolved live.

## Durability resolution (domain)

- `record DurabilidadeDeRank(RankDeItem Rank, int? Durabilidade, bool Inquebravel)`.
- `DurabilidadeDeItem.Resolver(RankDeItem? rank, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela) → (int? Maxima, bool Inquebravel)`:
  NULL rank → `(null, false)`; rank marked Inquebrável → `(null, true)`; otherwise `(Durabilidade, false)`.
- `DurabilidadeDeItem.LimitarAtual(int atual, int? maxima) → int`: `Math.Clamp(atual, 0, maxima ?? 0)`.
- On add to a sheet (incl. Equipagem grants): atual = `Maxima ?? 0`.

## API

- New `DurabilidadesPorRankController` at `api/durabilidades-por-rank`: `GET` (any authenticated user — the catalog form shows the resulting durability) returns the 8 rows in Rank order; `PUT {rank}` (Rules Auditor only, checked against the DB like `HistoricosController`) with `{ Durabilidade: int?, Inquebravel: bool }`: Inquebravel=true → Durabilidade stored NULL; Inquebravel=false → Durabilidade required, ≥ 1.
- Items: `CreateItemRequest`/`UpdateItemRequest` drop `DurabilidadeMaxima`; `Tier` renamed `Rank` and accepted for Arma, Armadura and Escudo. `ItemResponse`: `Tier` → `Rank`; `DurabilidadeMaxima` stays (resolved) and a trailing `bool Inquebravel` is added.
- Sheet arsenal responses (Character/Npc/Creature weapon/armor/shield): `Tier` → `Rank` where present; max resolved from the Rank; trailing `bool Inquebravel` added; `DurabilidadeAtual` returned capped. Updating atual on an Inquebrável item stores 0 and is otherwise ignored.
- Equipment kits: choice-slot filter by Rank; grants use resolved max.
- One Infrastructure service (`DurabilidadePorRankProvider`, Scoped) loads the table once per request/operation and is used by all of the above.

## Client

- **Catálogo item form:** "Durabilidade" field removed; Arma/Armadura/Escudo show a **Rank** select (F–SS, clearable) and, read-only next to it, "Durabilidade: N" or "Inquebrável" (from `GET api/durabilidades-por-rank`); no Rank → "Sem durabilidade".
- **Catálogo list / Equipagem Auditoria / sheets:** "Tier" label → "Rank".
- **Sheets (3):** Inquebrável items show "Inquebrável" instead of the atual/max pair and have no atual input.
- **New page** `/auditoria/durabilidade-por-rank` ("Durabilidade por Rank"), linked in `RulesAuditorNavLinks`, Auditor only: table of the 8 ranks, each with a number field and an "Inquebrável" checkbox (disables the number), autosave per row like the other Auditoria pages, and an ⓘ explaining the rule.

## Docs

- New `Docs/Sistema RPG/Tabela de Durabilidade por Rank.md`.
- `Requisitos - Catálogo de Itens e Equipamentos.md`: Tier → Rank (F–SS) on Arma, new Rank on Armadura/Escudo, Durabilidade becomes derived from [[Tabela de Durabilidade por Rank]].
- `Requisitos - Auditoria de Regras.md`: new R for the Durabilidade por Rank page.
- `Requisitos - Modelo de Dados.md`: new table, renamed/added/dropped columns.
- `Requisitos - Ficha de Personagem.md` (3.a–3.c): Inquebrável display.

## Testing (TDD)

- Unit: resolver (null / numeric / inquebrável), clamp, Tabela parser.
- Integration: seeder + migration (Arma Tier preserved as Rank, columns dropped, 8 rows seeded); Auditoria GET/PUT incl. 403 for non-auditor and validation; items create/update with Rank on the 3 types and resolved durability in responses; arsenal add sets atual = max, Inquebrável behaviour, cap after an Auditoria lowering; Equipagem grant.
- Client (bUnit): catalog form shows resolved durability per Rank and no Durabilidade input; Auditoria page edits/marks Inquebrável; sheet shows "Inquebrável".

## Out of scope

- Durability for Artefato or Item Geral.
- Showing the table in the Livro de Regras.
