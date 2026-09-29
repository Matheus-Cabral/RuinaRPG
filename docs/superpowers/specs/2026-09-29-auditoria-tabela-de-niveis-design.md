# Auditoria da Tabela de Níveis — Design

**Status:** approved by user 2026-09-29 (design approved in chat). Part 3 of 3 of version 1.4.1.

## Overview

The level table stops being free text parsed by regex out of the embedded `Tabela de Níveis.md` and becomes
a structured DB table the **Rules Auditor** edits on `/auditoria/tabela-de-niveis`: numeric columns per
level (budgets and caps), user-defined columns, a free-text "Outros bônus", and adding/removing levels.
Every calculator reads from it.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Scope | A + B + C + D: structured editable table, **caps per level**, add/remove levels, **configurable columns**. |
| Column types | **Acumulativa** (value = sum of levels 1..N — a budget) and **Por nível** (value = the row for level N — a cap or a per-level value). |
| Caps | Máx. de Atributo, Máx. de Perícia, and one max-count per Passiva category: **Livre, Vocacional, De Classe**. |
| Cap enforcement | **Server blocks** (400 with a message). A sheet already above a cap stays valid but cannot go higher. |
| Empty cell | Acumulativa → 0. Por nível → **inherits** the nearest lower level with a value; none → no cap. |
| Custom columns | Auditor picks name + type; shown on the sheet's "Progressão do nível" panel, informational only. |
| XP / EAP | Become system columns too (Por nível), so a new level 51 has XP/EAP. |
| Sheets covered | Personagem and NPC. Criatura keeps its Rank progression. |
| Livro de Regras | The Tabela de Níveis tab (and the XP/EAP-por-nível tables) is **generated** from the structured table. The `tabela-de-niveis` section is **removed from `/auditoria/livro-de-regras`** — its content is edited only on the new page. |

## Data

| Table | Columns |
|---|---|
| `NiveisProgressao` | `Nivel` (int, PK), `OutrosBonus` (text?) |
| `ColunasDeNivel` | `Id` (Guid), `Nome`, `Tipo` (`Acumulativa`/`PorNivel`), `ChaveDeSistema` (text?, unique when not null), `Ordem` (int), `IsDeleted` (bool) |
| `ValoresDeNivel` | `Nivel` (FK → NiveisProgressao, cascade), `ColunaId` (FK → ColunasDeNivel, cascade), `Valor` (int?); PK `(Nivel, ColunaId)` |

**System columns** (`ChaveDeSistema` set; renamable, not removable, type fixed):

| Chave | Nome inicial | Tipo | Feeds |
|---|---|---|---|
| `PontosDeAtributo` | Pontos de Atributo | Acumulativa | `AttributePointBudgetCalculator` (enforced), Criatura's non-level-1 part |
| `PontosDePericia` | Pontos de Perícia | Acumulativa | `SkillPointBudgetCalculator` |
| `EspacosDeCaracteristica` | Espaços de Característica | Acumulativa | `TraitPointBudgetCalculator` (enforced; `CriacaoBase = 5` unchanged) |
| `PontosDeIgnicao` | Pontos de Ignição | Acumulativa | `PontosDeIgnicaoCalculator` |
| `EspacosDeMaestria` | Espaços de Maestria | Acumulativa | shown |
| `PontosDeMaestria` | Pontos de Maestria | Acumulativa | shown |
| `MaxAtributo` | Máx. de Atributo | PorNivel | new cap |
| `MaxPericia` | Máx. de Perícia | PorNivel | new cap |
| `MaxPassivasLivres` | Máx. Passivas Livres | PorNivel | new cap |
| `MaxPassivasVocacionais` | Máx. Passivas Vocacionais | PorNivel | new cap |
| `MaxPassivasDeClasse` | Máx. Passivas De Classe | PorNivel | new cap |
| `XpNecessario` | XP necessário | PorNivel | level-up (replaces `XpPorNivel`) |
| `EapBase` | EAP base | PorNivel | `EapCalculator` (replaces `EapPorNivel`) |

**Seed** (startup seeder, only when `NiveisProgressao` is empty): one row per level from today's markdown,
extracting the six budget numbers with the **same regexes** the calculators use today; the rest of the cell
text (`<br>`-split lines not matched) goes to `OutrosBonus`. XP/EAP from today's `XpPorNivel`/`EapPorNivel`
parsers. Caps start empty (no cap) — nothing changes for existing sheets until the Auditor fills them.

## Domain

- `ITabelaDeNiveis` (Infrastructure service replacing `IRulesDataProvider.Niveis`, `XpPorNivel`, `EapPorNivel` for gameplay): `UltimoNivel`, `Acumulado(chave, nivel)`, `ValorNoNivel(chave, nivel)` (with inheritance), `Linhas()`.
- The resolution rules (sum for Acumulativa, nearest-lower inheritance for PorNivel) live in a pure Domain class `ProgressaoDeNivel` built from the rows, so they are unit-tested without a DB.
- Calculators take the resolved numbers instead of `IReadOnlyList<LevelBonus>`; their regexes are deleted (they survive only in the seeder).
- `LevelUpNoticeCalculator` builds its lines from the structured row (non-zero Acumulativa values as "+N Nome", then `OutrosBonus` lines).

## Caps (server-enforced, Personagem and NPC)

- `PUT .../attributes/{atributo}` — resulting attribute value > `MaxAtributo` at the sheet's level → 400 "Força não pode passar de X no nível N." Allowed if it doesn't increase an already-over-cap value.
- `PUT .../skills/{periciaId}` — same with `MaxPericia` on the perícia's value.
- Adding a Passiva (from bank or built) whose `CategoriaDePassiva` count on the sheet would exceed its cap → 400.
- The sheet's `Nivel` max becomes `UltimoNivel` (was 50).

## Levels and columns API (auditor-only, `RequireRulesAuditorAsync`)

- `GET api/tabela-de-niveis` — columns (ordered) + rows with values and `OutrosBonus`. Also readable by any authenticated user for the sheet panel and Livro (a separate non-auditor `GET` with the same payload).
- `PUT api/tabela-de-niveis/{nivel}/valores/{colunaId}` — `{ Valor: int? }`; negative → 400.
- `PUT api/tabela-de-niveis/{nivel}/outros-bonus` — text.
- `POST api/tabela-de-niveis/niveis` — appends `UltimoNivel + 1`, empty values.
- `DELETE api/tabela-de-niveis/niveis/ultimo` — 400 if level 1 is the only one or any Character/Npc sheet is at that level (message with the count).
- `POST api/tabela-de-niveis/colunas` — `{ Nome, Tipo }`; `PUT .../colunas/{id}` — rename (type fixed after creation); `DELETE .../colunas/{id}` — 400 for system columns; `PUT .../colunas/ordem` — ordered id list.

## UI

- **`/auditoria/tabela-de-niveis`** (link in `RulesAuditorNavLinks`): a grid, one row per level, one column per `ColunaDeNivel` + "Outros bônus"; cells save on change. Column header: rename, move left/right, remove (custom only), type shown as a MudBlazor icon (`Icons.Material.Filled.Functions` for Acumulativa, `Icons.Material.Filled.Straighten` for Por nível) with a tooltip. Toolbar: "+ Coluna" (name + type), "+ Nível", "Remover último nível" (with confirmation). Horizontally scrollable (`table-responsive`) on mobile.
- **`/auditoria/livro-de-regras`**: the Tabela de Níveis section is removed.
- **Fichas (Personagem/NPC)**: a "Progressão do nível" panel listing each column's resolved value at the sheet's level (budgets as "usado / total" where the sheet already tracks usage, caps as "máx. X", custom columns as plain values).
- **Livro de Regras**: Tabela de Níveis tab rendered from the structured table (one column per active column + Outros bônus).

## Ajuda (ⓘ)

Every new UI surface gets the existing `Shared/InfoPopup.razor` (ⓘ via `Section`'s `TitleInfo`), same pattern as
`AuditoriaDurabilidadePorRank.razor`. Draft texts (refine during implementation):

- **`/auditoria/tabela-de-niveis`, section Tabela de Níveis** — "Cada linha é um nível e cada coluna, um recurso. Colunas Acumulativas (ícone de somatório) somam do nível 1 até o nível do personagem — ex.: Pontos de Atributo. Colunas Por nível valem o número da linha do nível atual — ex.: Máx. de Perícia; uma célula vazia repete o valor do nível anterior mais próximo, e se nenhum nível tiver valor não há limite. Os limites (Máx. de Atributo, Máx. de Perícia e Máx. de Passivas por categoria) bloqueiam o salvamento da ficha; uma ficha que já passou do limite continua válida, mas não pode subir mais. Colunas com o ícone de cadeado são do sistema: podem ser renomeadas, não removidas. Colunas criadas por você aparecem na ficha só como informação. 'Remover último nível' é recusado se alguma ficha estiver nesse nível."
- **Same page, "+ Coluna" dialog** — short explanation of Acumulativa vs Por nível (the type can't be changed after creation).
- **Ficha (Personagem/NPC), panel Progressão do nível** — "Saldos e limites do nível atual, definidos na Tabela de Níveis. Totais somam todos os níveis até o atual; limites (máx.) valem para o nível atual e o app não deixa ultrapassá-los."

## Docs

- `Requisitos - Auditoria de Regras.md`: new **R0013**; R0002's list of editable Livro sections no longer includes Tabela de Níveis.
- `Requisitos - Livro de Regras.md`, `Requisitos - Ficha de Personagem.md` (panel + caps), `Requisitos - Modelo de Dados.md`.
- `Tabela de Níveis.md` stays as the seed source; CLAUDE.md's note on embedded resources updated.

## Testing (TDD)

- Unit: `ProgressaoDeNivel` (sum, inheritance, no-value → null, custom columns); `LevelUpNoticeCalculator` from structured rows.
- Seeder: resulting budgets for levels 1, 10, 25, 50 equal what today's regex calculators produce from the markdown (regression guard: nothing changes for existing sheets).
- Integration: auditor-only authorization on every write; value/outros-bônus edits; add/remove level incl. "in use" 400; custom column CRUD + reorder; system column removal 400; each cap blocks (attribute, perícia, each Passiva category) and allows lowering an over-cap value; sheet `Nivel` > `UltimoNivel` → 400; Livro renders the table; the auditoria/livro-de-regras list no longer offers `tabela-de-niveis`.
