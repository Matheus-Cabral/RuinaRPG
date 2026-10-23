# Habilidades Passivas — Design

**Status:** approved by user 2026-09-28 (two design sections approved in chat), ready for planning.

## Overview

A new kind of Magia/Habilidade, **Passiva**, with its own section **Habilidades Passivas** on the
Magias & Habilidades tab of all three sheets (Personagem, NPC, Criatura). A Passiva has only
**Nome**, **Descrição** and **Categoria** (Passiva Livre / Passiva Vocacional / Passiva de Classe) —
no Grau, Efeitos, Gasto em PI or Custo — plus a set of **Requisitos** that a sheet must meet for the
Passiva to be added to it.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Where Passivas are authored | **Only in the Banco de Magias e Habilidades** (GM-only page). Sheets never build a Passiva from scratch — they only pick one from the Banco, the GM included. |
| Where the Requisitos section lives | Only on Passivas (Banco form, when Tipo = Passiva). |
| What Requisitos do | **Block the addition**: the server refuses (400) adding a Passiva whose Requisitos the sheet doesn't meet — **for everyone, GM included**. |
| Sheets covered | Personagem, NPC **and** Criatura. The existing "Magia/Habilidade de Criatura" checkbox stays on the Passiva form. |
| Requisito on a field the sheet doesn't have (Criatura) | **Ignored** — only the requirements that exist on that sheet type are checked. |
| Sheet stops meeting Requisitos after the Passiva was added | **Kept, with a warning** ("⚠ Requisitos não cumpridos" + the list of what's missing). |
| What the player can do | Add from the Banco (only entries public in the campaign, per Ficha de Personagem R0003) if the sheet meets the Requisitos, and remove. Never create or edit. |
| Multiplicity | Scalar requirements are single-valued; Atributos, Sub-Atributos and Perícias are **lists** the GM builds incrementally (each item: which + minimum). |
| GM help | An ⓘ next to the Requisitos section opens a popup explaining how a Passiva is registered. |

## Requisitos

Every field is nullable; **NULL = not part of the requirements**. All non-null requirements must be
met simultaneously (AND).

| Requisito | Comparison | Personagem / NPC source | Criatura |
|---|---|---|---|
| Nível | sheet `Nivel ≥ N` | `Nivel` | `Nivel` |
| Vocação | equals | `Vocacao` | ignored |
| Classe | equals (depends on Vocação in the form, like the sheet's own Classe dropdown) | `SubVocacao` | ignored |
| Linhagem | equals | `Linhagem` | ignored |
| Variante | equals (depends on Linhagem in the form) | `Variante` | ignored |
| Grau/Círculo | `Graduacao ≥ N` (value from `GraduacaoCalculator`, the same number the sheet shows in 1.b) | computed | ignored |
| Coração de Mana | required when `true` (a `false`/NULL requisito means "not required") | `PossuiCoracaoDeMana` | ignored |
| Afinidade Elemental | equals the 1.a *Afinidade* dropdown | `Afinidade` | `Afinidade` |
| Estrela | equals | `Estrela` | ignored |
| Histórico | equals (FK to the Histórico catalog) | `HistoricoId` | ignored |
| Atributos (list) | each: attribute **Total** `≥ mínimo` (the same Total shown in 2.a, `AttributeTotalCalculator`, artefatos included) | 8 atributos | only Força, Vigor, Agilidade, Destreza, Astúcia are checked (mapped to `AtributoCriatura`); Instinto, Vontade, Influência ignored |
| Sub-Atributos (list) | each: calculated value `≥ mínimo` | the 6 values of `/sub-attributes`: Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Redução Física, Redução Mágica | same 6, from the Criatura's `/sub-attributes` |
| Perícias (list) | each: Perícia **Total** `≥ mínimo` (the 2.d Total) | `CharacterSkill`/`NpcSkill` | `CreatureSkill` |

Adrenalina is **not** offered as a Sub-Atributo requisito: it is a resource maximum (1.c), not one of
the 2.b sub-attributes the server calculates.

A requisito list item with the same attribute/sub-attribute/perícia twice is rejected (400) by the
Banco endpoints.

## Domain (`src/RuinaRPG.Domain/SpellsAndAbilities/`)

- `SpellAbilityTipo.Passiva` — appended **at the end** of the enum (stored as int).
- `enum CategoriaDePassiva { Livre, Vocacional, DeClasse }`.
- `enum SubAtributo { Iniciativa, Movimentacao, EsquivaNatural, DefesaNatural, ReducaoFisica, ReducaoMagica }`.
- `record RequisitosDePassiva` — `int? Nivel`, `Vocacao? Vocacao`, `string? Classe`,
  `Linhagem? Linhagem`, `Variante? Variante`, `int? Graduacao`, `bool? CoracaoDeMana`,
  `AfinidadeElemental? Afinidade`, `Estrela? Estrela`, `Guid? HistoricoId`,
  `List<RequisitoDeAtributo> Atributos`, `List<RequisitoDeSubAtributo> SubAtributos`,
  `List<RequisitoDePericia> Pericias` (each item = the enum + `int Minimo`).
- `record FichaParaRequisitos` — a snapshot of a sheet with **every field nullable** (and
  dictionaries for attribute/sub-attribute/perícia totals containing only the keys the sheet type
  has). A NULL scalar or a missing dictionary key means "this sheet type doesn't have it" → that
  requisito is ignored. (A Personagem with Vocação not yet chosen is *not* the same thing: the
  snapshot carries a sentinel/flag distinguishing "field absent on this sheet type" from "field
  present but empty" — an empty Vocação on a Personagem **fails** a Vocação requisito.)
- `static class PassivaRequisitosEvaluator` — `IReadOnlyList<string> Pendencias(RequisitosDePassiva, FichaParaRequisitos)`,
  one human-readable string per unmet requisito (e.g. `"Nível 5"`, `"Vocação: Feiticeiro"`,
  `"Força ≥ 4"`, `"Iniciativa ≥ 3"`, `"Histórico: Nobre"` — the Histórico name is passed in by
  the caller so the evaluator stays pure). Empty list = meets all.

## Persistence (`src/RuinaRPG.Infrastructure/`)

`SpellAbilityBankEntry`, `CharacterSpellAbility`, `NpcSpellAbility` and `CreatureSpellAbility` each gain:

- `CategoriaDePassiva? Categoria` — int, NULL for non-Passivas.
- `RequisitosDePassiva? Requisitos` — **jsonb**, mapped with a System.Text.Json value converter +
  value comparer (keeps the domain record free of EF owned-type constraints). NULL for non-Passivas. The whole object is always read/written as a unit and
  never queried into, so jsonb avoids ~11 columns + 3 child tables × 4 tables.

For a Passiva, `Grau = 0`, `GastoEmPI = 0`, `Custo = 0`, no Efeitos.

One migration (`AddHabilidadesPassivas`). `Docs/Requisitos/Requisitos - Modelo de Dados.md` updated
in the same change.

## API

### Banco (`SpellAbilityBankController`)

- Create/Update accept `Categoria` and `Requisitos`.
- `Tipo = Passiva`: `Categoria` required; `Requisitos` optional (NULL/empty = no requirements);
  `Grau` and `Efeitos` must be absent/empty (400 otherwise); stored with 0/0/0.
- `Tipo ≠ Passiva`: `Categoria`/`Requisitos` must be absent (400 otherwise).
- Classe without Vocação, or Variante without Linhagem (or not belonging to it) → 400. Classe is free
  text, like the sheet's own `SubVocacao` — its membership in the Vocação isn't validated.
  `HistoricoId` must exist in the catalog → 400 otherwise. Duplicate list items → 400.
- List filter by Tipo accepts `Passiva`.

### Sheets (`Character/Npc/CreatureSpellAbilitiesController`)

- **From scratch with `Tipo = Passiva`** → 400 `"Passivas só são cadastradas no Banco de Magias e Habilidades."`
- **From Banco, entry is Passiva**: after the existing access checks (GM ownership; player needs the
  entry public in the campaign), build the sheet's `FichaParaRequisitos` and evaluate. Any
  pendência → 400 with `"Requisitos não cumpridos: " + string.Join(", ", pendencias)`. Applies to the
  GM too. On success the sheet copy gets Nome, Descrição, Categoria and Requisitos (independent copy,
  `SourceBankEntryId` set — same pattern as today).
- **List responses** gain `Categoria`, `Requisitos` and `RequisitosPendentes` (computed on read
  against the sheet's current state — drives the ⚠ warning).
- **New** `GET .../spell-abilities/passivas-disponiveis` — the Passivas the caller can reach for
  this sheet (GM: all their Banco Passivas; player: the public ones in the campaign), each with its
  `Pendencias` list, so the add dialog can disable the unmet ones and show why.
- Delete: unchanged (player and GM can remove).

### Snapshot builders (the one refactor)

The sub-attribute calculation currently lives inline in each sheet controller's `/sub-attributes`
action. It is extracted into one builder per sheet type (`CharacterSheetStats`, `NpcSheetStats`,
`CreatureSheetStats` — Api layer, taking the `DbContext`) that produces the sub-attributes, attribute
totals and perícia totals. Both the existing `/sub-attributes` endpoint and the requisitos
snapshot use it, so the formulas are not duplicated. The `/sub-attributes` responses stay
byte-identical (existing tests guard that).

## Client (`src/RuinaRPG.Client/`)

- **`BancoDeMagiasForm`**: Tipo dropdown gains Passiva. When Passiva: hide Grau, Efeitos, Gasto em PI
  and Custo; show Categoria (select: Passiva Livre / Passiva Vocacional / Passiva de Classe) and a
  **Requisitos** section — the scalar fields (all clearable, placeholder when empty) plus three
  incremental lists (Atributos, Sub-Atributos, Perícias: select + mínimo + remove). An ⓘ opens a
  popup explaining: Passivas are only registered here; empty fields aren't requirements; all filled
  requirements must be met; a sheet that doesn't meet them can't receive the Passiva (not even by the
  GM); Criaturas ignore requirements on fields they don't have; a sheet that later stops meeting them
  keeps the Passiva with a warning. The "Magia/Habilidade de Criatura" checkbox stays.
- **`BancoDeMagias`** list: Tipo filter gains Passiva; Passiva rows show Categoria instead of
  Grau/Efeitos summary.
- **New shared `HabilidadesPassivasSection`** used by the three sheets, placed right after 4.b:
  table (Nome, Categoria, Descrição, ⚠ with the pendências as tooltip when non-empty), "Adicionar"
  opens a dialog listing `passivas-disponiveis` (unmet ones disabled, pendências shown), remove button.
- **4.b** no longer lists Passivas and its Tipo dropdown doesn't offer Passiva.

## Docs (`Docs/Requisitos/`)

- `Requisitos - Banco de Magias e Habilidades.md`: new R0009 (Passiva: fields, Categoria, Requisitos,
  authored only here, ⓘ popup); R0004 filter list and R0005 field list mention Passiva.
- `Requisitos - Ficha de Personagem.md`: new **4.f) Habilidades Passivas**; 4.b Tipo dropdown note;
  new R (addition blocked by Requisitos, for everyone; kept with warning afterwards).
- `Requisitos - Ficha de NPCs.md`, `Requisitos - Ficha de Criaturas.md`: the section exists; Criatura
  ignores requisitos on fields it lacks.
- `Requisitos - Modelo de Dados.md`: the new columns.

## Testing (TDD)

- **Unit** (`PassivaRequisitosEvaluator`): each scalar requisito met/unmet; NULL requisito ignored;
  each list; absent-on-sheet-type ignored vs present-but-empty fails; Criatura attribute mapping;
  pendência strings.
- **Integration**: Banco CRUD of a Passiva (+ validation 400s); from-scratch Passiva on a sheet → 400;
  from-Banco unmet → 400 for player **and** GM; met → 201 with the copy; `RequisitosPendentes`
  appears after the sheet changes (e.g. Nível lowered via XP); `passivas-disponiveis` for GM vs
  player; the three sheet types; `/sub-attributes` unchanged after the refactor.
- **Client (bUnit)**: form hides/shows fields by Tipo, list add/remove, ⓘ popup; section renders ⚠
  and disables unmet entries in the dialog.

## Out of scope

- Passivas having mechanical effects on the sheet (they are descriptive text).
- Ego as an attribute requisito (Criatura-only attribute).
- OR-combinations of requisitos.
