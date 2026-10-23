# Versão 1.5.0 — Spec A: Ficha de Criatura, Pontos de Ignição e Magias/Habilidades — Design

**Status:** design approved by user 2026-10-13 (in chat). Version 1.5.0, branch `release/1.5.0`.

Version 1.5.0 is split into four specs, implemented in this order:

| Spec | Items | Depends on |
|---|---|---|
| **A** (this file) | 1 to 5 | — |
| B — `2026-10-13-versao-1-5-0-b-log-de-ficha-design.md` | 6 | — |
| C — `2026-10-13-versao-1-5-0-c-encontros-design.md` | 7 | B (sheet-change hook) |
| D — `2026-10-13-versao-1-5-0-d-condicoes-e-resumo-design.md` | 8, 9 | C |

## Overview

Five changes to the sheets:

1. **Características de Criatura** — the Ficha de Criatura picker lists only the creature catalog, and
   the Auditor can register a new creature characteristic from a popup on the sheet.
2. **Pontos de Ignição por nível** — the "Total" leaves the Personagem and NPC sheets; a level change
   adds to (or subtracts from) Pontos de Ignição Atual.
3. **Largura do campo de bônus manual de PI** — the field is too narrow for its label.
4. **Saldo de PI nas Magias/Habilidades** — adding, editing and removing a Magia/Habilidade checks,
   debits and refunds the sheet's PI balance, on the three sheets.
5. **Edição de Magias/Habilidades montadas do zero** — an entry built on the sheet can be edited.

Implementation order: **1, 3, 2, 5, 4**. Item 4 is last because its edit rule needs the endpoint of item 5.

Schema migration exists in item 5, so `make migrate` is mandatory in the 1.5.0 deploy.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Creature sheets that already hold normal-catalog characteristics | **Kept**: still listed, still counted in the budget, still removable. Picker and API stop accepting new ones. No data migration. |
| After saving the Auditor popup | The new characteristic is left **selected in the picker**; the Auditor fills Especificação and clicks "Adicionar Característica". |
| "Pontos de Ignição Total" line | Removed from Personagem and NPC. The Progressão do nível section keeps showing the table sum only. |
| Manual PI bonus field (Personagem) | **Stays where it is**, same label. Applying a delta adds to the manual bonus **and** to Atual. |
| Level loss | **Symmetric**: the PI of the lost levels is subtracted from Atual, floor 0. |
| Sheets for the per-level sum | Personagem and NPC. Criatura keeps its single field. |
| PI balance on Magias/Habilidades | Check, debit, refund. Blocks **everyone, GM included**, on **the three sheets**. |
| Which entries are editable | **Only the ones built from scratch on the sheet.** Entries that already exist are not editable (their origin is unknown). |
| Refund on removing an entry that predates 1.5.0 | **Refunded** like any other, even though it was never debited. |

## 1. Características de Criatura

### API

`CreaturePossessionsController.AddTrait` resolves `TraitId` only against `CreatureExclusiveTrait`. An Id
that exists in `Traits` returns 400 "Esta característica não é do catálogo de criatura."; an unknown Id
keeps the current 400. `ListTraits`, `SumExistingCustoAsync` and `DeleteTrait` are unchanged, so a
`CreatureTrait` row with `TraitId` set keeps being listed, summed and removable.

### Client

- `FichaDeCriatura.razor` — `SearchTraitsAsync` queries only `creature-exclusive-traits`.
- New `Shared/CaracteristicaDeCriaturaDialog.razor` — the fields of the "Adicionar característica" form
  of `AuditoriaCaracteristicasDeCriatura` (Nome, Polaridade, Custo, Exige Especificação? with its
  `InfoPopup`, Descrição). It POSTs `creature-exclusive-traits` and closes with the created
  `CreatureExclusiveTraitResponse`. An API error shows inside the dialog.
- In the "Características" section of the sheet, a button "Nova característica de criatura"
  (`Icons.Material.Filled.Add`), rendered only when `auth/me` returns `IsRulesAuditor`. On success the
  page calls `EntityPicker.SelectExternallyAsync` with the new option.
- `/auditoria/caracteristicas-de-criatura` is unchanged.

### Docs

- `Requisitos - Ficha de Criaturas.md` 5.d — the picker lists only the creature catalog; normal ones
  already granted remain; the Auditor has the shortcut.
- `Requisitos - Auditoria de Regras.md` R0005 — replace "junto com as de R0003, no mesmo seletor" and
  mention the shortcut on the sheet.

## 2. Pontos de Ignição por nível

### Domain

New pure function next to `PontosDeIgnicaoCalculator`:

```
DeltaPorNivel(nivelAntigo, nivelNovo, tabela) =
    tabela.Acumulado(PontosDeIgnicao, nivelNovo) − tabela.Acumulado(PontosDeIgnicao, nivelAntigo)
AplicarAoAtual(atual, delta) = max(0, atual + delta)
```

### API

- `CharacterSheetsController.Update` — with `nivelAntigo = sheet.Nivel` (before the write) and the new
  `nivel` computed from XP: `PontosDeIgnicaoAtual = AplicarAoAtual(request.PontosDeIgnicaoAtual,
  DeltaPorNivel(...) + (request.PontosDeIgnicaoBonusManual − sheet.PontosDeIgnicaoBonusManual))`.
  The manual-bonus difference is what makes "Aplicar" on the bonus field also reach Atual.
- `NpcSheetsController.Update` — same with `request.Nivel` against `sheet.Nivel`; there is no manual
  bonus. `request.PontosDeIgnicaoTotal` becomes accepted-but-ignored (same treatment `EAPAtual`
  already has); the column and the contract field stay, no migration.
- `NpcSheetsController.UpdateNivel` and `UpdateExperienciaAtual` (the two dedicated endpoints the NPC
  page uses) apply the same delta directly to the stored `PontosDeIgnicaoAtual`.
- **New sheets (amended 2026-10-13 while planning):** a Ficha de Personagem and an NPC sheet created
  from blank start with `PontosDeIgnicaoAtual = Acumulado(PontosDeIgnicao, Nivel inicial)`. Without
  this a new sheet would never receive the level-1 grant and, with item 4, could not buy its first
  Magia. A sheet copied by a grant keeps the source values.
- `CharacterSheetResponse.PontosDeIgnicaoTotal` and `PontosDeIgnicaoBonusManual` stay in the contract.
- No backfill: existing sheets keep their Atual; the rule applies from the next level change.

- **Amended 2026-10-13 after the final review of Plan A:** the generic sheet PUT no longer takes the
  PI balance from the request. `request.PontosDeIgnicaoAtual` (Personagem, NPC) and
  `request.PontosDeIgnicao` (Criatura) are accepted but ignored; the level and manual-bonus deltas are
  applied to the **stored** value. Editing the field by hand goes through a dedicated endpoint,
  `PUT api/{character|npc|creature}-sheets/{id}/pontos-de-ignicao` (body: the new integer, >= 0), with
  the sheet's usual edit authorization. Reason: the pages autosave the whole form with no concurrency
  token, so a stale form (a second user on the same sheet, or a pending autosave) would write an old
  balance over a server-side debit, refund or level adjustment. This supersedes the
  `Aplicar(request.PontosDeIgnicaoAtual, …)` formulas above.

### Client

- `FichaDePersonagem.razor` — remove the "Pontos de Ignição Total" line. After any save, the form's
  `PontosDeIgnicaoAtual` is replaced by the server value (the save already reloads the sheet); this is
  required, otherwise the next autosave would send the pre-sum value back.
- `FichaDeNpc.razor` — remove the "Pontos de Ignição Total" field and its validation; the form keeps
  sending the loaded value. The Nível and Experiência save paths must reload `PontosDeIgnicaoAtual`
  from the response for the same reason.
- `FichaDeCriatura.razor` — unchanged.

### Docs

`Requisitos - Ficha de Personagem.md` 1.b (Pontos de Ignição) and `Requisitos - Ficha de NPCs.md`.

## 3. Largura do campo de bônus manual de PI

`FichaDePersonagem.razor`: the "Adicionar/subtrair PI (bônus manual)" field has `max-width:16rem` and
the label overflows. Widen it until the full label fits at the default font size, and give the
neighbouring "Adicionar/subtrair XP" field the same width so the two rows align.

## 4. Saldo de PI nas Magias/Habilidades

The balance is `PontosDeIgnicaoAtual` on `CharacterSheet` and `NpcSheet`, and `PontosDeIgnicao` on
`CreatureSheet`. The rule lives in one Domain function used by the three controllers:

```
Debitar(saldo, gasto): gasto > saldo → erro; senão saldo − gasto
```

| Action | Rule |
|---|---|
| **Add** (from scratch or from the bank), Tipo Magia or Habilidade | If `GastoEmPI > saldo` → 400 "Pontos de Ignição insuficientes: custa {gasto} e a ficha tem {saldo}." Otherwise `saldo −= GastoEmPI`. |
| **Delete** | `saldo += entry.GastoEmPI`. |
| **Update** (item 5) | `diff = novoGasto − gastoAntigo`. `diff > saldo` → same 400. Otherwise `saldo −= diff` (a negative diff refunds). |

- Passivas (no Gasto em PI) and the Habilidade Racial are outside the rule.
- The balance write and the entry write happen in the same `SaveChangesAsync`.
- `CampaignGrantsController`, when it copies a sheet for a grant, copies the entries and the balance as
  they are, with no debit.
- Entries that predate 1.5.0 were never debited and are still refunded on delete (user decision).

### Client

On the three sheet pages, after a successful add, edit or delete of a Magia/Habilidade, the PI field of
the sheet form is reloaded from the server before any further autosave. The 400 message is shown in the
existing error alert.

### Docs

`Requisitos - Ficha de Personagem.md` 4.b and 1.b, `Requisitos - Ficha de NPCs.md`, `Requisitos -
Ficha de Criaturas.md` (1.a Pontos de Ignição and R0007). NPC and Criatura documents must state that
this is a hard block for the GM too, unlike their attribute and Vocação Arcana counters.

## 5. Edição de Magias/Habilidades montadas do zero

### Schema

New column `CriadaNaFicha boolean NOT NULL DEFAULT false` on `CharacterSpellAbilities`,
`NpcSpellAbilities` and `CreatureSpellAbilities`. One migration. Set to `true` by the from-scratch
branch of `Add`; the from-bank branch leaves it `false`. `CampaignGrantsController` copies the flag.

### API

`PUT api/{character|npc|creature}-sheets/{sheetId}/spell-abilities/{id}` with
`UpdateSpellAbilityRequest(Nome, Tipo, Grau, Descricao, Efeitos)`:

- Same authorization as `Add` on each controller (who can edit the sheet).
- `CriadaNaFicha == false` → 400 "Só Magias e Habilidades montadas na ficha podem ser editadas."
- Tipo must be Magia or Habilidade. Same validations as the from-scratch `Add`
  (`EfeitoValidationHelper.ValidarAsync`, and whatever Grau limit each controller applies today).
- Replaces the Efeitos rows, recomputes `GastoEmPI` and `Custo`, applies the item-4 diff rule.
- The bank copy created at the original `Add` is **not** touched.

The three `*SpellAbilityResponse` records gain `bool CriadaNaFicha`.

### Client

New `Shared/EditarMagiaHabilidadeDialog.razor` with the from-scratch form fields (Nome, Tipo, Grau,
Descrição, `AddEfeitoForm`, `EfeitosTable`), taking the save URL as a parameter so the three sheets
share it. Each sheet's list shows an "Editar" button (`Icons.Material.Filled.Edit`) only on rows with
`CriadaNaFicha`.

### Docs

`Requisitos - Ficha de Personagem.md` 4.b (an entry built on the sheet is editable; one from the bank
is not), the NPC and Criatura diffs, `Requisitos - Modelo de Dados.md` (new column).

## Testing

TDD, test first for each behavior.

- **Unit:** `DeltaPorNivel`/`AplicarAoAtual` (up, down, several levels, floor 0, empty cells);
  `Debitar` (enough, exact, short).
- **Integration:**
  - `AddTrait` rejects a normal-catalog Id; existing creature trait tests move to the exclusive
    catalog; the mixed-sum test seeds the legacy normal row directly in the database.
  - Character and NPC `Update`: level up adds, level down subtracts with floor 0, manual bonus delta
    reaches Atual, NPC ignores `PontosDeIgnicaoTotal`.
  - Spell abilities on the three controllers: add blocked when short, debit, refund on delete, edit
    diff both ways, edit refused for a bank-sourced entry, Passiva untouched, flag persisted, migration
    test for the new column.
- **Client (bUnit):** dialog posts and returns the created characteristic; the button only renders for
  the Auditor; the picker no longer calls `traits`; the Total is gone from Personagem and NPC; the
  "Editar" button only on `CriadaNaFicha` rows.

## Out of scope

- Retroactive debit of existing Magias/Habilidades, or recalculation of any existing PI Atual.
- Marking existing entries as editable.
- Editing the bank copy from the sheet.
- The release bump and the changelog text: written once, at the end of Spec D.
