# Versão 1.5.0 — Spec D: Condições e Resumo da Ficha no Encontro — Design

**Status:** design approved by user 2026-10-13 (in chat). Version 1.5.0, branch `release/1.5.0`.
Last of four specs (A, B, C, **D**); see Spec A for the table. Depends on Spec C (phases, turn events,
encounter log) and on the `NotificationHub` introduced in 1.4.4.

## Overview

8. **Condições** — the free-text "Condições" of an encounter participant become entries of a global
   catalog kept by the Auditor (new page **Auditoria → Condições**). A condition carries modifiers and
   disables that change the sheet while the condition and the encounter are active.
9. **Resumo da ficha no encontro** — a read-only popup with the summary of an NPC or Criatura
   participant, with the active conditions already applied.

The existing page **Auditoria → Efeitos** (effects a Magia/Habilidade buys with PI) is a different
catalog and keeps its name.

Schema migrations exist, so `make migrate` is mandatory in the 1.5.0 deploy. This spec also carries
the 1.5.0 release bump.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Name | **Condições.** |
| What a condition can change | Atributos, Perícias, Sub-atributos (defenses included), total and current value of Vitalidade/Foco/Adrenalina/Estresse, Afinidade elemental (general or one element), dano elemental (general or one element), modificador de dano, Maestrias; disable equipment, Magias/Habilidades, Runas, Passivas. |
| Current-value modifiers | Per line: **ao aplicar** (once) or **a cada vez do participante**. Never given back. |
| Total modifiers | Temporary; gone when the condition leaves. |
| Duration | **Optional, in Turnos**, with a default in the catalog; expires by itself. Empty = until removed. |
| Value kind | **Fixed or percentage**, per line. The GM may adjust the value when applying. |
| Disable granularity | **Category, or specific items** chosen when applying to a participant. |
| When effects count | Only while the condition is on the participant **and** the encounter is em andamento. |
| Personagem | The sheet itself changes; the player sees it. |
| NPC/Criatura **granted to a player** | Same as Personagem: the sheet changes and the owner sees it. |
| NPC/Criatura **of the GM** (not granted) | Applied **only in the summary popup**; the original sheet never changes. |
| Same condition twice on one participant | Not allowed. Different conditions add up. |
| Existing free-text conditions | Stay as plain tags without effect, removable. New ones only from the catalog. |
| Catalog seed | Empty. |

## Data

### Catalog

`Condicoes`: `Id`, `Nome` (unique among non-deleted), `Descricao`, `Icone` (a MudBlazor icon key from a
fixed pick list), `DuracaoPadraoTurnos` (int, nullable), `IsDeleted`, `UpdatedByUserId`, `UpdatedAt`.

`CondicaoModificadores`: `Id`, `CondicaoId` (FK cascade), `Ordem`, and:

| Column | Values |
|---|---|
| `Alvo` (enum `AlvoDeCondicao`) | `Atributo`, `Pericia`, `SubAtributo`, `RecursoTotal`, `RecursoAtual`, `AfinidadeElemental`, `DanoElemental`, `Dano`, `Maestria` |
| `AlvoEspecifico` (text, nullable) | Atributo name; Perícia key or null for all; a `SubAtributoAlvo` constant; `Vitalidade`/`Foco`/`Adrenalina`/`Estresse`; `Elemento` or null for general; `TipoDeDano` or null for all; Perícia key of the Maestria or null for all |
| `TipoDeValor` | `Fixo`, `Percentual` |
| `Valor` | int, signed |
| `Momento` (nullable) | `AoAplicar`, `ACadaVez` — required when `Alvo = RecursoAtual`, null otherwise |

`CondicaoDesabilitacoes`: `Id`, `CondicaoId` (FK cascade), `Categoria` (enum `Arma`, `Armadura`,
`Escudo`, `Artefato`, `Magia`, `Habilidade`, `Runa`, `Passiva`).

### Applied to a participant

New table `EncounterParticipantCondicoes` — an **independent copy** (legend pattern 1 of the Modelo de
Dados): editing or deleting the catalog entry does not change a condition already applied.

| Column | Notes |
|---|---|
| `Id`, `EncounterParticipantId` (FK cascade) | |
| `CondicaoId` (uuid, nullable, no cascade) | Provenance only; used for the "same condition twice" check. |
| `Nome`, `Descricao`, `Icone` | Snapshot. |
| `AplicadaNoTurno` | `CurrentRound` when applied (0 if applied in preparation; counts from Turno 1 then). |
| `DuracaoTurnos` (nullable) | |
| `Modificadores` (jsonb) | The lines, with the values as adjusted by the GM. |
| `Desabilitacoes` (jsonb) | Per category: empty list = the whole category; otherwise the ids of the sheet rows. |

The legacy `EncounterParticipantCondition` (free text) table stays as is.

## Activation, duration and timing

- A condition is **active** when its participant's encounter has `Status == EmAndamento` and it has
  not expired. Preparation and finished encounters have no active conditions. Reopening reactivates
  what is still on the participants.
- **Expiry:** with `DuracaoTurnos = N`, applied at Turno T, the condition is removed when the encounter
  advances into Turno `max(T,1) + N`. Removal is logged (`CondicaoExpirada`).
- **`AoAplicar`** lines run once: when applied in an in-progress encounter, or at **Iniciar** if
  applied during preparation.
- **`ACadaVez`** lines run each time the participant receives the turn through **Próxima vez** or
  **Passar a vez para este**. **Vez anterior** runs nothing and undoes nothing, and does not bring back
  an expired condition.
- Where a current-value adjustment lands:

  | Participant | Target |
  |---|---|
  | Personagem, or granted NPC/Criatura | The sheet's Atual field, clamped to 0..max. |
  | NPC/Criatura of the GM | The instance's `PVAtual`/`PFAtual`/`PAAtual`, floor 0. Estresse has no instance field: the line is skipped and the log says so. |

  A percentage on a current value is a percentage of that resource's total.
- **Total reduced below Atual** (sheet-linked participants): when a condition becomes active, the
  stored Atual is clamped to the new total, as already happens when an Artefato is removed. It is not
  restored afterwards.

## Calculation

One Domain service, `EfeitosDeCondicao`, built from the active conditions of a sheet (or of a single
participant, for the summary):

```
Ajustar(alvo, alvoEspecifico, valorBase) → valor final
    soma os Fixos; depois aplica a soma dos Percentuais sobre (valorBase + fixos), arredondando para baixo
EstaDesabilitado(categoria, itemId) → bool
Explicar(alvo, alvoEspecifico) → linhas "Condição: +2" para exibição
```

It plugs into the existing modifier path (`CharacterSheetStats.ModificadoresAsync`, and the NPC and
Creature equivalents) that already sums Artefato bonuses and equipment penalties:

| Alvo | Where it applies |
|---|---|
| `Atributo`, `Pericia`, `SubAtributo`, `Dano` | Same points where `ArtifactBonusCalculator.Sum` is used today. |
| `RecursoTotal` | Vitalidade, Foco, Adrenalina and Estresse maximums (`ResourceMaximumCalculator` call sites). |
| `AfinidadeElemental` | The displayed value of each Afinidade row of that Elemento (or all). Does not change the Vocação Arcana budget. |
| `DanoElemental` | A new per-element damage modifier line in the Combate tab, shown only when non-zero. |
| `Maestria` | The Maestria total (`ComputeTotalAsync` in the masteries controllers), by Perícia or all; at apply time the GM may narrow it to specific Maestrias of the sheet. |

Disable semantics: a disabled Arma, Escudo, Armadura or Artefato counts as **not in use** in every
calculation (defenses, Artefato bonuses, equipment penalties). A disabled Magia, Habilidade, Runa or
Passiva is only marked. Budgets, limits and requirement checks ignore conditions.

Which sheets get this applied:

- **Personagem and granted NPC/Criatura:** every sheet endpoint computes with the sheet's active
  conditions (participants linked to it in in-progress encounters, all summed).
- **NPC/Criatura of the GM:** sheet endpoints never apply conditions. Only the summary endpoint does,
  with the conditions of that one participant.

## API

- `CondicoesController` (`api/condicoes`): `GET` for any authenticated user (the GM needs the list);
  `POST`/`PUT`/`DELETE` Auditor-only. Delete is a soft delete and is always allowed, since applied
  copies are independent. Validation: `Momento` iff `RecursoAtual`; `AlvoEspecifico` valid for the
  `Alvo`; at least one modifier or one disable.
- Encounter participants:
  - `POST api/encounters/{id}/participants/{pid}/condicoes` — `{ CondicaoId, DuracaoTurnos?,
    ValoresAjustados?, ItensDesabilitados? }`. 400 if already on the participant; 409 if finished.
  - `DELETE .../condicoes/{condicaoAplicadaId}`.
  - `GET .../itens-desabilitaveis` — the participant sheet's rows per category, for the picker.
  - `EncounterParticipantResponse.Condicoes` becomes structured (name, icon, remaining Turnos, legacy
    flag).
- Sheets: `GET api/{character|npc|creature}-sheets/{id}/condicoes-ativas` — for who can edit the
  sheet: the active conditions, what each changes, remaining Turnos. Sheet list responses for weapons,
  armor, shields, artifacts, spell abilities, runes and passives gain `DesabilitadoPor` (string?).
- Summary: `GET api/encounters/{id}/participants/{pid}/resumo` — GM only, NPC or Criatura participant
  (granted or not). 404 when the source sheet no longer exists.
- Real time: whenever the set of active conditions of a sheet-linked participant changes (apply,
  remove, expire, start, finish, reopen, per-turn adjustment), the API sends `CondicoesAlteradas`
  through `NotificationHub` to the sheet's owner, and notifies `EncounterHub` as usual.
- Encounter log (Spec C): `CondicaoAdicionada`, `CondicaoRemovida`, new `CondicaoExpirada` and
  `AjusteDeCondicao` (per-turn or on-apply adjustment, with before/after). Sheet log (Spec B): the
  Atual writes are captured by the interceptor; applying and removing a condition on a sheet-linked
  participant also writes one `SheetLogEntry` each, explicitly.

## Client

- **`AuditoriaCondicoes.razor`** (`/auditoria/condicoes`, nav link "Condições" with
  `Icons.Material.Filled.Coronavirus`): list and form with Nome, Descrição, icon picker, duração
  padrão, an editable table of modifier lines (Alvo → dependent "específico" select → Fixo/Percentual
  → Valor → Momento when applicable) and a multi-select of disabled categories.
- **`GerenciadorDeEncontros.razor`:** the "Condições" text field is replaced by chips (icon, name,
  remaining Turnos, remove) and an "Adicionar condição" button opening
  `AplicarCondicaoDialog`: searchable select of the catalog, duration, the lines with editable values,
  and per disabled category a choice "toda a categoria" or specific items of that participant's sheet.
  Legacy text tags render as plain chips with remove only.
- **`CondicoesAtivasAlert.razor`** at the top of `FichaDePersonagem`, and of `FichaDeNpc` /
  `FichaDeCriatura` when the sheet is granted: icon, name, description, the list of what it changes,
  remaining Turnos. Rows of disabled items show a chip "Desabilitado por: {Condição}". The page
  subscribes to `CondicoesAlteradas` and reloads the sheet and the alert.
- **`ResumoDaFichaDialog.razor`:** icon button (`Icons.Material.Filled.Visibility`, tooltip "Resumo da
  ficha") on each NPC/Criatura row of the encounter. Read-only: image, name, Nível or Rank, attributes,
  sub-attributes and defenses, resources (the instance values for GM sheets), skills with points,
  equipment in use, Magias & Habilidades, Características, active conditions. A value changed by a
  condition is highlighted with the original next to it. A link opens the full sheet.

## Release

At the end of this spec: `AppVersionInfo.Current = "1.5.0"` and the `ChangelogDialog` text covering
the nine items of Specs A to D, with the Auditor-only section for the creature-characteristic popup
and the Condições page. Branch `release/1.5.0` is based on `release/1.4.4` (PR #65, Nota Secreta),
because it needs `NotificationHub`; PR #65 merges first, or the 1.5.0 PR carries it.

## Docs

`Requisitos - Gerenciador de Encontros.md` (R0003 Condições rewritten, new requirements for applying
conditions and for the summary); `Requisitos - Auditoria de Regras.md` (new requirement: catálogo de
Condições); `Requisitos - Ficha de Personagem.md` (new requirement: Condições ativas);
`Requisitos - Ficha de NPCs.md` and `Requisitos - Ficha de Criaturas.md` (granted vs. GM-owned);
`Requisitos - Modelo de Dados.md` (four new tables).

## Testing

- **Unit:** `EfeitosDeCondicao` — fixed, percentage, both, several conditions, rounding, general vs.
  specific targets, disable by category and by item; expiry arithmetic; catalog validation.
- **Integration:**
  - Catalog CRUD and authorization; soft delete leaves applied copies intact.
  - Apply/remove; duplicate refused; finished refused; value and item overrides persisted.
  - A Personagem's attribute, skill, sub-attribute, resource total, affinity, damage and mastery
    change while active and return after removal, after expiry and after finish; nothing changes at
    Turno 0; reopen reapplies.
  - `AoAplicar` at apply and at start; `ACadaVez` on next and choose, not on previous; clamps.
  - Granted NPC/Criatura behave like Personagem; a GM-owned NPC sheet is unchanged while its summary
    shows the modified values; two instances of one sheet have independent conditions.
  - Disabled equipment stops counting; `DesabilitadoPor` present.
  - Encounter log and sheet log entries; `CondicoesAlteradas` sent to the owner.
  - Migration tests for the new tables.
- **Client (bUnit):** Auditoria form (dependent selects, Momento only for current value); apply dialog;
  chips with legacy tags; the alert and the disabled chips; the summary dialog highlights.

## Out of scope

- Conditions outside an encounter, or applied by a player.
- Stacking the same condition, or intensity levels.
- A summary popup for Personagem participants.
- Converting legacy free-text tags into catalog conditions.
- Conditions changing budgets, level limits or Passiva requirements.
