# Versão 1.5.0 — Spec C: Encontros — fases, vez, finalizados e log — Design

**Status:** design approved by user 2026-10-13 (in chat). Version 1.5.0, branch `release/1.5.0`.
Third of four specs (A, B, **C**, D); see Spec A for the table. Uses the sheet-change hook of Spec B.

## Overview

7. **Encontros** — an encounter gains three phases (Turno 0 / em andamento / finalizado), an explicit
   control of whose turn it is, a separate list of finished encounters, and a log recorded from
   Turno 1.

Vocabulary change on screen and in the docs: the counter today called **Rodada** becomes **Turno**,
and the current participant is **a vez**. Code identifiers (`CurrentRound`, `CurrentParticipantId`)
keep their names.

Schema migration exists, so `make migrate` is mandatory in the 1.5.0 deploy.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Turn control | Fixed indicator plus **Próxima vez**, **Vez anterior** and **Passar a vez para este** on each row. |
| Finished encounter | **Read-only, with Reabrir.** No delete. |
| Encounter log content | Encounter events **plus** Vitalidade/Foco/Adrenalina changes made on linked sheets while the encounter is in progress. |
| Turno 0 | Preparation only: add/remove participants and set Iniciativa. Nothing is logged. |
| Lists in the campaign | "Encontros" (preparation and in progress) and "Encontros finalizados". |
| Existing encounters | Never advanced → Turno 0. Otherwise in progress at their current Turno; their log starts at the update. |

## Data

`Encounters` gains:

| Column | Type | Notes |
|---|---|---|
| `Status` | enum `Preparacao`, `EmAndamento`, `Finalizado` | |
| `FinalizadoEm` | timestamptz, nullable | Set on finish, cleared on reopen. |

`CurrentRound` is `0` while `Preparacao`. New encounters are created with `Status = Preparacao`,
`CurrentRound = 0`, `CurrentParticipantId = null`.

Migration of existing rows: `CurrentParticipantId IS NULL` → `Preparacao`, `CurrentRound = 0`;
otherwise `EmAndamento`, `CurrentRound` unchanged.

New table `EncounterLogEntries`:

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | |
| `EncounterId` | uuid FK, cascade | |
| `OccurredAt` | timestamptz | UTC. |
| `Sequencia` | bigint | Monotonic per encounter, for stable ordering inside the same instant. |
| `Turno` | int | `CurrentRound` when the event happened. |
| `VezParticipantId` | uuid, nullable | Whose turn it was. No FK; the participant may be removed later. |
| `VezNome` | text, nullable | Snapshot. |
| `Tipo` | enum | See Events. |
| `ParticipanteNome` | text, nullable | Subject of the event, snapshot. |
| `Campo` | text, nullable | "PV", "Iniciativa", "Ações"... |
| `ValorAntes`, `ValorDepois` | text, nullable | |
| `Texto` | text, nullable | Free complement (e.g. a condition name). |

## Phases and rules

| Phase | Allowed |
|---|---|
| **Preparacao** (Turno 0) | Add participant, remove participant, edit Iniciativa and the rest of a participant, rename. No turn controls except **Iniciar encontro**. |
| **EmAndamento** | Everything above plus the turn controls and **Finalizar encontro**. |
| **Finalizado** | Read only. Only **Reabrir** and rename. Every other mutation returns 409 "Encontro finalizado." |

Turn rules, in `TurnAdvanceCalculator` (Domain), over the participants ordered by Iniciativa desc, Id:

- **Iniciar** — requires at least one participant. `CurrentRound = 1`, vez = first of the order, that
  participant's `AcoesRestantes = 3`.
- **Próxima vez** — next in order; after the last, `CurrentRound + 1` and the first. The participant
  receiving the turn gets `AcoesRestantes = 3` (R0005, unchanged). When the current participant no
  longer exists, the next is the first of the order, as today.
- **Vez anterior** — previous in order; from the first of Turno N to the last of Turno N−1. From the
  first of Turno 1 it is refused (400). `AcoesRestantes` is not touched.
- **Passar a vez para este** — vez = the chosen participant, `CurrentRound` unchanged, that
  participant's `AcoesRestantes = 3`.
- **Finalizar** — `Status = Finalizado`, `FinalizadoEm = now`. Turno and vez are kept.
- **Reabrir** — `Status = EmAndamento`, `FinalizadoEm = null`, same Turno and vez.

Removing the participant who has the turn passes the turn to the next in order (same Turno unless it
was the last, in which case the next Turno starts).

## API

All under the existing GM-only authorization of the encounter controllers.

| Endpoint | Effect |
|---|---|
| `POST api/encounters/{id}/start` | Iniciar. |
| `POST api/encounters/{id}/advance-turn` | Próxima vez (existing route, now 409 outside `EmAndamento`). |
| `POST api/encounters/{id}/previous-turn` | Vez anterior. |
| `POST api/encounters/{id}/turn/{participantId}` | Passar a vez. |
| `POST api/encounters/{id}/finish` | Finalizar. |
| `POST api/encounters/{id}/reopen` | Reabrir. |
| `DELETE api/encounters/{id}/participants/{participantId}` | Remove participant (new). |
| `GET api/encounters/{id}/log` | The whole log, ordered by `Sequencia`. |

`EncounterResponse` gains `Status` and `FinalizadoEm`. `GET campaigns/{id}/encounters` returns all;
the client splits by `Status`. Every mutation keeps notifying `EncounterHub` as today.

## Events

Recorded only while `Status == EmAndamento` (the events `Iniciado` and `Reaberto` are written right
after the status change; `Finalizado` right before).

| `Tipo` | When |
|---|---|
| `Iniciado`, `Finalizado`, `Reaberto` | Phase changes. |
| `NovoTurno` | `CurrentRound` increases. |
| `VezPassada`, `VezVoltou`, `VezEscolhida` | The three turn controls. |
| `ParticipanteAdicionado`, `ParticipanteRemovido` | |
| `CampoAlterado` | Iniciativa, Ações restantes, PV/PF/PA of an instance, with before/after. |
| `CondicaoAdicionada`, `CondicaoRemovida` | The free-text tags today; Spec D reuses these types. |
| `RecursoDaFicha` | Vitalidade/Foco/Adrenalina Atual changed on a linked sheet. |

The encounter events are written explicitly by `EncountersController` and
`EncounterParticipantsController` through one small service (`EncounterLog`), in the same save.

`RecursoDaFicha` comes from Spec B's interceptor: when it records a `RecursoVFA` change for a sheet,
it looks up participants linked to that sheet (`SourceCharacterSheetId`, or
`SourceNpcSheetId`/`SourceCreatureSheetId` of a granted sheet) in encounters with
`Status == EmAndamento`, and adds one `EncounterLogEntry` per encounter in the same save, then
notifies `EncounterHub`.

## Client

`GerenciadorDeEncontros.razor`:

- Header: "{Nome} — Turno {n}" and a status chip (Preparação / Em andamento / Finalizado).
- A highlighted bar "Turno {n}, vez de {Nome}" while in progress.
- Buttons by phase: **Iniciar encontro** (Turno 0); **Vez anterior**, **Próxima vez**, **Finalizar
  encontro** (in progress, finish behind a confirmation dialog); **Reabrir** (finished).
- The current participant's row gets a filled background and a leading icon
  (`Icons.Material.Filled.PlayArrow`), not just bold.
- Each row: "Passar a vez para este" icon button (in progress) and a remove icon button with
  confirmation (Turno 0 and in progress).
- Finished: the add form, the edit buttons and the turn controls are not rendered.
- New section "Log do encontro": entries grouped by Turno, and inside it by vez, each line with the
  time and the text. Reloads on the `EncounterHub` notification.

`CampanhaDetalhe.razor`, tab Encontros: section "Encontros" lists `Preparacao` and `EmAndamento` with
a chip ("Preparação" or "Turno {n}"); new section "Encontros finalizados" lists `Finalizado` with the
finish date, newest first.

## Docs

`Requisitos - Gerenciador de Encontros.md`: amend R0001 and R0006 (vocabulary, phases, turn
controls); new requirements for the phases, the finished list, participant removal and the encounter
log. `Requisitos - Campanha.md` where it mentions the encounter list. `Requisitos - Modelo de
Dados.md`: new columns and `EncounterLogEntries`.

## Testing

- **Unit:** `TurnAdvanceCalculator` — start, next with wrap, previous with wrap back, previous refused
  at the first of Turno 1, choose, removed current participant.
- **Integration:**
  - Create starts at Turno 0; start requires a participant; turn endpoints 409 in the wrong phase.
  - Next/previous/choose update Turno, vez and Ações as specified.
  - Finish blocks mutations with 409; reopen restores; rename still works when finished.
  - Remove participant, including the one holding the turn.
  - Log: nothing at Turno 0; each event type written; ordering by `Sequencia`; a sheet Vitalidade
    change during an in-progress encounter appears, and does not appear at Turno 0 or after finish.
  - Migration test: both branches of the existing-row conversion.
- **Client (bUnit):** buttons per phase; row marking; finished is read-only; log grouping; the two
  lists in the campaign page.

## Out of scope

- Deleting an encounter.
- Player view of the encounter or of its log.
- Undoing resource changes when going back a turn.
- Conditions with effects (Spec D).
