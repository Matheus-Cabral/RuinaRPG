# Passivas Coringa e aba Passivas no Livro de Regras — Design

**Status:** design approved by user 2026-10-03 (in chat, with two amendments already folded in). Version 1.4.2, branch `release/1.4.2`.

## Overview

Two independent changes shipped together as 1.4.2:

1. **Passivas Coringa** — a fourth Passiva column on the Auditoria's Tabela de Níveis. Each accumulated
   point is an extra slot that accepts a Passiva of **any** category (Livre, Vocacional, De Classe).
2. **Aba Habilidades Passivas** — a new tab on the Livro de Regras listing the Passivas the logged-in
   user can reach, placed right after Graus & Círculos.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| How a wildcard slot is consumed | **Computed, never stored.** No Passiva is tagged "coringa" on the sheet. Whatever exceeds a category's own limit consumes wildcard slots. |
| Empty Coringa column | **0 slots** (not "no limit"), so nothing changes for a table that never fills it. |
| Category with an entirely empty column | Still unlimited, and never consumes a wildcard slot. |
| Sheets covered | Personagem and NPC. Criatura keeps having no limits. |
| How the Passiva columns are named to players | **Amended 2026-10-03:** on every page that is not an Auditoria page, the wildcard reads just **"Habilidade Passiva"** and the three specific ones read **"Passiva Livre"**, **"Passiva Vocacional"**, **"Passiva de Classe"**. The column names ("Passivas Coringa", "Passivas Livres", …) are shown only on the Auditoria. |
| Which Passivas the Livro tab shows | **Per campaign, logged-in only.** GM: their whole bank. Jogador: the Passivas attached as public to the chosen campaign. Anonymous visitors do not see the tab. |
| Jogador in several campaigns | **Campaign selector** at the top of the tab, shown only when the Jogador has more than one campaign. |
| Tab position | Right after **Graus & Círculos**. |

## 1. Passivas Coringa

### Column

- New system key `ChavesDeNivel.MaxPassivasCoringa = "MaxPassivasCoringa"`, default name **"Passivas Coringa"**,
  `TipoDeColunaDeNivel.Acumulativa`, ordered right after "Passivas De Classe" (XP and EAP shift one position).
- Like every system column it can be renamed by the Auditor, not removed.
- No schema migration: `TabelaDeNiveisSeeder` already creates any missing system column on startup. It must
  additionally keep the system columns in `ChavesDeNivel.Sistema` order on databases seeded before 1.4.2, so
  the new column does not land after XP/EAP: when the Coringa column is created on an existing database, the
  system columns that come after it in `Sistema` (XP, EAP) get their `Ordem` shifted. Auditor-created columns
  keep their relative position after the system ones.
- The Auditor writes `1` on the levels that grant a wildcard slot, exactly as with the other three.

### Limit rule

Evaluated when a Passiva is added to a Personagem or NPC sheet (`CharacterSpellAbilitiesController`,
`NpcSpellAbilitiesController`), replacing the current per-category check:

- For each category `c` whose column has a limit (`LimiteAcumulado` not null):
  `excedente(c) = max(0, passivasNaFicha(c) − limite(c))`, counting the Passiva being added.
- `coringas = LimiteAcumulado(MaxPassivasCoringa, nivel) ?? 0`.
- The add is refused (400) when the new Passiva's category is limited, the add **raises** the total
  `Σ excedente(c)`, and the new total is greater than `coringas`.
- A sheet already above its limits stays valid (the table may have been edited afterwards); it just cannot
  add another Passiva that needs a slot it does not have. Removing is always allowed.
- A category with an entirely empty column is unlimited: adding to it is always allowed and it contributes
  0 to the total.

The rule lives in `LimitesDeNivel` (Domain, pure) as a function over the per-category counts, the
per-category limits and the wildcard total; the controllers only gather the numbers.

**Message**: with `coringas == 0` the current text is kept
("O nível N permite no máximo L Passiva(s) Livre(s)."). With `coringas > 0` it adds that the wildcard
slots are also used up, e.g. "O nível N permite no máximo L Passiva(s) Livre(s), e as K vaga(s) coringa já
estão em uso."

### Where it shows

The four Passiva columns have a **fixed player-facing label**, independent of the column name (so an
Auditor rename of one of these four columns only changes the Auditoria grid):

| System key | Auditoria (column name, default) | Everywhere else |
|---|---|---|
| `MaxPassivasCoringa` | Passivas Coringa | **Habilidade Passiva** |
| `MaxPassivasLivres` | Passivas Livres | **Passiva Livre** |
| `MaxPassivasVocacionais` | Passivas Vocacionais | **Passiva Vocacional** |
| `MaxPassivasDeClasse` | Passivas De Classe | **Passiva de Classe** |

The label is defined once in the Domain (next to `ChavesDeNivel`; the three category labels are the ones
`RequisitoLabels.Categoria` already produces) and used by:

- **Level-up dialog** and **Livro de Regras → Tabela de Níveis** — the bonus lines from
  `ProgressaoDeNivel.LinhasDeBonus`/`LinhasDeBonusAcumuladas`: "Habilidade Passiva: +1", "Passiva Livre: +1", …
- **Painel Progressão do nível** (sheet): "Habilidade Passiva: N" (accumulated; an empty Coringa column
  shows 0, not "sem limite" — unlike the three category columns), "Passiva Livre: N", …
- **Livro de Regras → "Limites e progressão"** table, should one of these columns ever appear there.

Unchanged:

- **Auditoria → Tabela de Níveis**: regular columns, header = column name.
- **Habilidades Passivas section** of the sheet: no per-Passiva "coringa" mark.
- The 400 limit messages keep their current wording for the categories.

## 2. Aba Habilidades Passivas (Livro de Regras)

### Access and content

- The tab is titled **"Habilidades Passivas"** and is rendered right after the Graus & Círculos tab.
- It exists only for authenticated users. An anonymous visitor sees the Livro exactly as today.
- **GM**: every Passiva (`Tipo = Passiva`) of their own bank; no selector.
- **Jogador**: the Passivas attached as **public** to the selected campaign. The selector lists the
  campaigns the Jogador is a member of and is shown only when there is more than one; with exactly one the
  list loads directly; with none the tab shows an empty-state message.
- An empty list (GM with no Passivas, campaign with none public) shows an empty-state message.

### Layout

Same pattern as the Características tab:

- A search field at the top filters by Passiva name (partial, case-insensitive).
- Passivas grouped by category in the order Livres, Vocacionais, De Classe; a group with no match is hidden.
- One card (`Section`) per Passiva: name as title, the description, and the Requisitos written out (the
  same labels the sheet uses for pendências), or "Sem requisitos".
- Icons are MudBlazor icons, never emojis.

### API

The tab is **not** part of `GET /api/rulebook` (anonymous, identical for everyone).

**Amended 2026-10-03 (planning):** one endpoint instead of two, returning ready-to-render rows.

- `GET /api/rulebook/passivas?campaignId={id}` (`RulebookPassivasController`, `[Authorize]`), returning
  `PassivaDoLivroResponse(Id, Nome, Categoria, Descricao, Requisitos)` with the Requisitos already written
  out by `PassivaRequisitosEvaluator.Descrever` (same labels as the sheet's pendências). The client would
  otherwise need the Histórico and Perícia catalogs just to print names.
- GM: their own bank's Passivas; `campaignId` ignored. Jogador: `campaignId` required (400), unknown
  campaign 404, non-member 403, otherwise the Passivas attached as public to it.
- The Jogador's campaign list comes from `GET /api/campaigns/mine`, which Minhas Campanhas already uses.

The tab is its own component (e.g. `Shared/PassivasDoLivro.razor`) so `LivroDeRegras.razor` only decides
where to place it and whether the user is authenticated.

## 3. Requisitos (Docs/)

- `Requisitos - Auditoria de Regras.md` R0013: the fourth Passiva column, the wildcard rule, the fixed
  player-facing labels of the four Passiva columns outside the Auditoria.
- `Requisitos - Ficha de Personagem.md`: 4.f limit paragraph and the Painel Progressão do nível note.
- `Requisitos - Livro de Regras.md`: new **R0010** for the tab (content per role, selector, layout,
  position) and a note in the access-model preamble that this one tab requires login.
- `Requisitos - Modelo de Dados.md`: only if it enumerates the system column keys.

## 4. Testing (TDD)

- **Unit**: the wildcard rule in `LimitesDeNivel` (within category limit; exceeds with a free wildcard;
  wildcards used up; wildcard column empty; unlimited category; sheet already above the limit);
  the fixed labels of the four Passiva columns in `LinhasDeBonus`/`LinhasDeBonusAcumuladas`; the seeder creating the
  column and keeping the order on an already-seeded table.
- **Integration**: add-Passiva on Personagem and NPC (accepted through a wildcard, refused when used up,
  unchanged behaviour with an empty Coringa column); the new campaign endpoint (member sees only public
  Passivas, non-member refused, non-Passiva entries excluded).
- **Client (bUnit)**: tab hidden for anonymous; GM list; Jogador with one campaign (no selector) and with
  several (selector switches the list); search filter; tab position after Graus & Círculos; Progressão
  panel showing the four Passiva columns with their fixed labels.

## 5. Release

`AppVersionInfo.Current` → `1.4.2` and the `ChangelogDialog` text updated in the same branch.

## Out of scope

- Tagging or choosing which Passiva occupies a wildcard slot.
- Limits for Criatura sheets.
- Showing Passivas to anonymous visitors or a global/Auditor-curated Passiva list.
