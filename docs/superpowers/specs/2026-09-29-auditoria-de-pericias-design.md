# Auditoria de Perícias — Design

**Status:** approved by user 2026-09-29 (design approved in chat). Part 2 of 3 of version 1.4.1.

## Overview

The 39 Perícias stop being a hardcoded C# `enum` and become a DB table the **Rules Auditor** manages on a new
page `/auditoria/pericias`: add, rename, describe, set a suggested attribute, mark as available for
Criaturas, remove (reversibly) and restore.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Fields | `Nome` (required), `Descrição` (nullable), `Atributo sugerido` (nullable), `Disponível para Criaturas` (bool). |
| Suggested attribute | Only **pre-selects** `AtributoEscolhido` on a new sheet row (and is the fallback when a row's `AtributoEscolhido` is null). It never restricts the choice — the GM may ask for e.g. Atletismo with Força or Agilidade. |
| Description on the sheet | Tooltip on hover (desktop); popup on tapping the name (mobile). |
| Removing a perícia in use | **Logical removal** (`IsDeleted`). Points spent on it (`Gasto`) are **zeroed on every sheet** so they return to the sheet's Pontos de Perícia balance for redistribution. While removed, it is hidden from sheets, dropdowns and the Livro; Histórico bonuses, Passiva requisites and Maestrias tied to it stop applying. **Restoring** brings it back with 0 points everywhere (otherwise the freed points would be counted twice). |
| Perícias used in formulas | **Prontidão, Reflexos, Fortitude** are **protected**: editable (name/description/attribute/criatura flag) but not removable. Formulas reference them by their fixed Id, so renaming is safe. |
| Criatura allow-list | The hardcoded `CreatureSkillAllowList` becomes the per-perícia flag **Disponível para Criaturas**, editable on the Auditoria page. New perícias default to `false`. |

## Data

New table `Pericias`:

| Column | Type | Notes |
|---|---|---|
| `Id` | int | PK. Seeded 0–38 = the old enum values; the identity sequence restarts at 39. |
| `Nome` | text | required; unique among rows with `IsDeleted = false` (partial unique index) |
| `Descricao` | text? | |
| `AtributoSugerido` | int? | `Atributo` enum |
| `DisponivelParaCriaturas` | bool | seeded from today's `CreatureSkillAllowList` |
| `IsDeleted` | bool | |

**Migration keeps every stored value as-is.** Because the seeded Ids equal the old enum values:

- Int columns `CharacterSkill/NpcSkill/CreatureSkill.Pericia`, `Character/Npc/CreatureMastery.Pericia`, `Historico.PericiaMaisSeis/PericiaMaisTres` become `PericiaId` FKs (`ON DELETE RESTRICT` — perícias are never hard-deleted) with no data change.
- `RequisitosDePassiva` jsonb already serializes `Pericia` as a number (default `System.Text.Json`), so `RequisitoDePericia(int PericiaId, int Minimo)` reads existing JSON unchanged — verify with a round-trip test on a pre-migration JSON literal.
- Artefato `Alvo` for `TipoDeAlvo = Pericia` is stored as the **enum member name** (e.g. `"ArmasBrancas"`); the migration rewrites those to the Id as a string (`"4"`), and `ArtifactBonusCalculator` / `ItemsController` switch to parsing the Id.
- Seed labels come from today's `PericiaLabels`.

Code: `enum Pericia`, `PericiaLabels`, `CreatureSkillAllowList` and the client's `PericiaDisplay` are removed.
Domain keeps a small `PericiasDeSistema` with the three protected Ids (`Prontidao = 32`, `Reflexos = 33`,
`Fortitude = 17`) used by `NpcSheetStats`/`CreatureSheetStats`/the character formulas and by the
removal guard. Everything that enumerated `Enum.GetValues<Pericia>()` (sheet creation in
`CharacterSheetsController`, `NpcSheetsController`, `CampaignGrantsController`, creature sheets) reads the
active rows instead. `HistoricoSeedParser`'s label→enum map becomes a label→Id lookup against the seeded rows.

## Sheet behaviour

- A sheet shows **every active perícia**. If a sheet has no row for an active perícia (added after the sheet was created), it is shown with `Gasto = 0` and the suggested attribute; the row is created on first save (`PUT .../skills/{periciaId}` upserts).
- Rows of removed perícias are kept in the DB (with `Gasto = 0`) but not returned.
- Criatura sheets show only active perícias with `DisponivelParaCriaturas = true`; the existing "only allowed perícias" server check uses the flag.
- `SkillPointBudgetCalculator` sums `Gasto` over active rows only (removed rows are 0 anyway).

## API

- `GET api/pericias` — any authenticated user; active perícias ordered by `Nome` → `PericiaResponse(int Id, string Nome, string? Descricao, string? AtributoSugerido, bool DisponivelParaCriaturas, bool Protegida)`.
- Auditor-only (`RequireRulesAuditorAsync`, same pattern as the other Auditoria controllers):
  - `GET api/pericias/auditoria` — all, including removed (adds `IsDeleted`).
  - `POST api/pericias` — create (400 on blank or duplicate active name).
  - `PUT api/pericias/{id}` — edit.
  - `DELETE api/pericias/{id}` — logical removal + zero `Gasto` on every Character/Npc/Creature skill row of that perícia, in one transaction. 400 for a protected perícia.
  - `POST api/pericias/{id}/restaurar` — 400 if an active perícia already has that name.
- Existing skill endpoints change their route key from the enum name to the int Id (`PUT .../skills/{periciaId:int}`); 404 for an unknown or removed Id.

## UI

- **`/auditoria/pericias`** (link in `RulesAuditorNavLinks`): table with Nome, Descrição, Atributo sugerido, Disponível para Criaturas, actions. Protected rows show a lock icon and no remove button. Removing asks for confirmation ("Os pontos gastos nesta perícia serão devolvidos em todas as fichas."). A "Removidas" section lists removed ones with "Restaurar".
- **Fichas (Personagem/NPC/Criatura)** — perícia list comes from `GET api/pericias`. The name shows the description as a `MudTooltip` on hover; on mobile, tapping the name opens a small popup with it. No icon when description is null.
- Históricos (Auditoria), Maestria forms, Passiva requisites editor, Catálogo Artefato Alvo dropdown — options come from `GET api/pericias`.

## Docs

- `Requisitos - Auditoria de Regras.md`: new **R0012**.
- `Requisitos - Ficha de Personagem.md` (Atributos & Perícias), `Requisitos - Ficha de Criaturas.md`, `Requisitos - Modelo de Dados.md`.

## Testing (TDD)

- Integration: CRUD + auditor-only authorization; duplicate name 400; removing a protected perícia 400; removal zeroes `Gasto` on all three sheet types and the budget reflects it; restored perícia comes back at 0; a sheet created before a new perícia existed lists it with 0 and can save it; Criatura list honours the flag; removed perícia disappears from `GET api/pericias`, Históricos bonus and Passiva evaluation.
- Migration: the 39 seeded Ids/names match the old enum/labels; existing skill rows, Históricos and a pre-migration Passiva JSON still resolve to the same perícia; an Artefato `Alvo` `"ArmasBrancas"` becomes `"4"`.
