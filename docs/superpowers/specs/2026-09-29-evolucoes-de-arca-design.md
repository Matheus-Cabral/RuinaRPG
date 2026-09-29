# Evoluções de Arca — Design

**Status:** approved by user 2026-09-29 (design approved in chat). Part 1 of 3 of version 1.4.1.

## Overview

Each entry of the GM's **Tabela de Arcas** (the Humano/Sinir/Laonir racial ability, `Requisitos - Habilidades
Raciais.md` R0002) can have any number of **evoluções**, each unlocked at a minimum level. A Humano
Personagem/NPC sheet shows a button that opens a popup with every evolution its level has unlocked.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| What an evolution is | `Nível mínimo + Descrição`. Evolutions **add to** the Arca's base description; they never replace it. |
| How many | Any number per Arca, several allowed at the same level. |
| Who edits them | The GM, on `/habilidades-raciais`, per GM — same ownership as the Arcas table itself (not Auditoria). |
| What the sheet shows | A button **"Evoluções da Arca (N)"** (disabled when N = 0) opening a popup listing only the **unlocked** evolutions (`Nivel ≤ sheet.Nivel`), ordered by level. Locked ones are not shown. |
| Which sheets | Personagem and NPC (the two sheets that already resolve `ArcaRolada`). |

## Data

New entity `ArcaEvolucao` (Infrastructure/CharacterSheets):

| Column | Type | Notes |
|---|---|---|
| `Id` | Guid | PK |
| `ArcaEntryId` | Guid | FK → `ArcaEntries`, `ON DELETE CASCADE` |
| `Nivel` | int | 1–50 (validated in the API) |
| `Descricao` | text | required, non-blank |

Index on `(ArcaEntryId, Nivel)`. `ArcaEntry` gains the navigation `Evolucoes`.

`ArcaEntry` rows are still created lazily. Adding an evolution to a roll with no row yet creates the
`ArcaEntry` (empty `Nome`/`Descricao`) in the same save.

> The 1–50 upper bound is today's last level. When part 3 (Tabela de Níveis) lands, the validation reads
> the table's last level instead of the constant.

## API (`RacialAbilitiesController`, GM-only, scoped to the caller's GmId)

- `GET api/arcas` — each of the 18 `ArcaEntryResponse` rows gains `Evolucoes: List<ArcaEvolucaoResponse(Guid Id, int Nivel, string Descricao)>`, ordered by `Nivel`, then insertion.
- `POST api/arcas/{roll}/evolucoes` — body `ArcaEvolucaoRequest(int Nivel, string Descricao)` → 201 with the created row.
- `PUT api/arcas/{roll}/evolucoes/{id}` — same body → 200.
- `DELETE api/arcas/{roll}/evolucoes/{id}` → 204.
- Validation: roll outside 1–18, `Nivel` outside 1–50, or blank `Descricao` → 400. An evolution id that doesn't belong to the caller's Arca at that roll → 404.

## Sheet resolution

`RacialAbilityResponse` gains `ArcaEvolucoes: List<ArcaEvolucaoResponse>`. `CharacterSheetsController` and
`NpcSheetsController` already load the campaign GM's `ArcaEntry` for the rolled number; they now also
include its evolutions filtered to `Nivel ≤ sheet.Nivel`, ordered by level. Empty list when not Humano,
no roll, no Arca row, or nothing unlocked. The filter is a small pure Domain helper
(`ArcaEvolucoesDesbloqueadas`) so it is unit-testable.

## UI

- **`/habilidades-raciais`** — every row of the Tabela de Arcas gets an expandable "Evoluções" area: a list of `Nível` (numeric) + `Descrição` (multiline), edited inline (save on change, like the Arca row itself), a delete button per evolution and an "Adicionar evolução" button.
- **FichaDePersonagem / FichaDeNpc**, tab Magias & Habilidades, section "Habilidade Racial" — under the Arca's description, a button "Evoluções da Arca (N)". It opens a `MudDialog` listing "Nível X — descrição" for each unlocked evolution. Disabled when N = 0.

## Docs

- `Requisitos - Habilidades Raciais.md`: new **R0006** (evoluções por nível).
- `Requisitos - Ficha de Personagem.md` 4.a: the button + popup.
- `Requisitos - Modelo de Dados.md`: `ArcaEvolucoes` table.

## Testing (TDD)

- Unit: `ArcaEvolucoesDesbloqueadas` (≤ level, ordering, empty input).
- Integration (`RacialAbilitiesControllerTests`): create/update/delete; creating on an empty roll creates the Arca row; each validation 400; another GM's evolution → 404; `GET api/arcas` returns them ordered.
- Integration (Character and NPC sheet tests): response only includes evolutions with `Nivel ≤ sheet.Nivel`; raising the sheet's level reveals the next one; non-Humano sheet → empty.
