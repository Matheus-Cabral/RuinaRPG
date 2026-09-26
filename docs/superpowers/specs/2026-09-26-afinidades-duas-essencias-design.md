# Afinidades com duas Essências Básicas — Design

**Status:** approved by user 2026-09-26 (three design sections approved in chat), ready for planning.

## Overview

The Afinidades table (Ficha de Personagem 2.c, inherited by the Ficha de NPC) changes from
"Elemento + Caminho + hand-picked Sub-Elemento" to "two Essências Básicas whose intersection on the
Matriz Elemental *is* the Sub-Elemento":

1. The Vocação/Escola-de-Magia restriction is removed — from the Afinidades rows **and** from the
   "Afinidade" dropdown in Informações Básicas (1.a), on both Personagem and NPC.
2. Each row gets a second Essência Básica (with its own numeric value).
3. The second Essência offers the 4 Elementos **plus** the Caminhos Alma, Vida and Mundano.
4. The Sub-Elemento is no longer chosen: it is the Matriz Elemental intersection of the two Essências
   (read-only name; its numeric value stays editable).

The separate Caminho column is removed (the 2nd Essência replaces it).

## The Matriz Elemental rule (`Docs/Sistema RPG/Matriz_Elemental.png`)

| Essência 1 (Elemento) | Essência 2 | Sub-Elemento |
|---|---|---|
| Ar | Água | Gelo |
| Ar | Fogo | Raio |
| Água | Terra | Flora |
| Fogo | Terra | Ferro |
| Ar | Alma | Prever |
| Água | Alma | Purificar |
| Fogo | Vida | Curar |
| Terra | Vida | Aprimorar |
| Ar | Mundano | Ecomancia |
| Água | Mundano | Hemomancia |
| Fogo | Mundano | Necromancia |
| Terra | Mundano | Invocação |

Element pairs are symmetric (Água + Ar = Gelo too). Every other combination (Ar+Terra, Água+Fogo,
the same Elemento twice, Fogo/Terra + Alma, Ar/Água + Vida) has no intersection.

Options offered for Essência 2, per Essência 1: Ar → Água, Fogo, Alma, Mundano · Água → Ar, Terra,
Alma, Mundano · Fogo → Ar, Terra, Vida, Mundano · Terra → Água, Fogo, Vida, Mundano.

## Rules

- Essência 1: Ar, Água, Fogo, Terra (unchanged; optional — blank placeholder rows remain allowed).
- Essência 2: optional; only allowed together with an Essência 1, and only a value that intersects
  with it. A row with only Essência 1 is valid (no Sub-Elemento).
- Sub-Elemento: always set by the server = intersection(Essência 1, Essência 2), or null when
  Essência 2 is empty. Recomputed **only when Essência 1 or Essência 2 changes** on an update;
  changing only numeric values/Experiência keeps the stored Sub-Elemento (this is what preserves an
  unconvertible legacy value "until edited").
- Uniqueness: two rows of the same sheet can't produce the same non-null Sub-Elemento (400).
  Essência 1 may repeat across rows. (The old "same Elemento" rule is dropped.)
- No Vocação restriction anywhere for Afinidades or the 1.a "Afinidade" field. The existing rule that
  Alma/Vida are not valid values for the 1.a "Afinidade" field stays.
- Changing Essência 1 in the UI clears an Essência 2 that no longer intersects before saving; the
  server rejects an incompatible pair anyway.

## Data model

`CharacterAffinities` and `NpcAffinities`:

| Column | Change |
|---|---|
| `SegundaEssencia` | **new**, enum `EssenciaBasica` (Ar, Agua, Fogo, Terra, Alma, Vida, Mundano), stored as integer like the other enums, NULL |
| `SegundaEssenciaValor` | **new**, int, NULL |
| `SubElemento` | kept (still stored); now server-derived |
| `CaminhoNome` | **dropped** after the data conversion |

`EssenciaBasica` integer values: Ar=0, Agua=1, Fogo=2, Terra=3, Alma=4, Vida=5, Mundano=6 (the first
four equal `Elemento`'s values). `SubElemento` keeps its legacy `Alma`/`Vida` members (integers must
not shift) for legacy rows.

### Conversion of existing rows (inside the migration, before dropping `CaminhoNome`)

1. If the stored Sub-Elemento is reproducible from the row's Elemento — i.e. there is an Essência 2
   with intersection(Elemento, Essência 2) = Sub-Elemento — set `SegundaEssencia` to it
   (Ar+Gelo → Água; Fogo+Curar → Vida; Ar+Ecomancia → Mundano; …).
2. Else, if the row has no Sub-Elemento but `CaminhoNome` is exactly `Alma`, `Vida` or `Mundano` and
   intersects with the Elemento: set `SegundaEssencia` to that Caminho and `SubElemento` to the
   intersection (Fogo + Caminho "Vida" → Curar).
3. Everything else (Sub-Elemento inconsistent with the Elemento, free-text Caminho, legacy Alma/Vida
   as Sub-Elemento, no Elemento): `SegundaEssencia` stays NULL and the stored Sub-Elemento stays.
   A free-text Caminho that fits none of the above is lost when the column is dropped (irreversible;
   `Down` re-adds an empty `CaminhoNome`).

The migration builds its SQL from the domain `MatrizElemental` table so the rule has a single
source. It is covered by an integration test that migrates an **isolated database** (created in the
Testcontainers Postgres just for that test) to the migration before this one, inserts legacy rows
with raw SQL, migrates forward, and checks all three cases.

## Domain

`RuinaRPG.Domain.CharacterSheets`:

- `EssenciaBasica` enum (above).
- `MatrizElemental` (static): `SubElemento? Intersecao(Elemento essencia1, EssenciaBasica essencia2)`;
  `IReadOnlyList<EssenciaBasica> OpcoesSegundaEssencia(Elemento essencia1)`;
  `EssenciaBasica? SegundaEssenciaQueProduz(Elemento essencia1, SubElemento subElemento)` (used by
  the conversion); `IReadOnlyList<(Elemento, EssenciaBasica, SubElemento)> Tabela` (the 12 rows;
  element pairs listed once, `Intersecao` handles symmetry).
- Removed once unused: `CaminhoSubElementoRules` (except `EhCaminho(AfinidadeElemental)`, still used by
  the 1.a validation — keep it in a trimmed class), `ElementoSubElementoValidator`, `VocacaoEscolaMap`,
  `EscolaDeMagiaCatalog`, `EscolaDeMagia`, `Caminho` enum, and their unit tests. Keep anything that
  still has a use.

## API

- `AddCharacterAffinityRequest` / `UpdateCharacterAffinityRequest` (and the Npc equivalents):
  `(string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor,
  int? SubElementoValor, int? Experiencia)`. `SubElemento` and `CaminhoNome` leave the requests.
- `CharacterAffinityResponse` / `NpcAffinityResponse`: `(string Id, string? Elemento,
  int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, string? SubElemento,
  int? SubElementoValor, int? Experiencia)`.
- 400s: unknown Elemento / Essência; Essência 2 without Essência 1 (`"Escolha a Essência Básica 1
  antes da 2."`); no intersection (`"Essas duas Essências não se cruzam na Matriz Elemental."`);
  duplicate Sub-Elemento (`"Já existe uma linha de Afinidade com esse Sub-Elemento."`).
- Vocação checks removed from both affinity controllers and from the 1.a Afinidade validation in
  `CharacterSheetsController.Update` / `NpcSheetsController.Update`.
- `CampaignGrantsController.DeepCopyNpcAsync` copies `SegundaEssencia`/`SegundaEssenciaValor`.

## Client (Personagem + NPC)

Afinidades table columns: **Essência Básica 1** (name + value) · **Essência Básica 2** (name + value;
options from `MatrizElemental.OpcoesSegundaEssencia`; disabled without Essência 1) · **Sub-Elemento**
(read-only name or "—", editable value) · **Experiência** · remove. Same for the "add" row. The
Caminho column and `CaminhoSelect` go away. The 1.a `AfinidadeSelect` shows all Afinidades (no
Vocação filter). `AfinidadeEssenciaField` gets the Essência 2 option list and a `Disabled` parameter;
`SubElementosDisponiveis` and the Vocação helpers are removed.

## Testing (TDD)

- Unit: `MatrizElemental` — all 12 intersections, symmetry of element pairs, every non-intersecting
  combination returns null, `OpcoesSegundaEssencia` per Elemento, `SegundaEssenciaQueProduz`.
- Integration (Personagem + NPC affinity controllers): add/update derive the Sub-Elemento; invalid
  pair 400; Essência 2 without 1 400; duplicate Sub-Elemento 400; repeated Essência 1 allowed;
  updating only values keeps a stored (legacy) Sub-Elemento; changing an Essência recomputes it;
  any Vocação (incl. none) may pick any Elemento; 1.a Afinidade no longer depends on Vocação; NPC
  deep copy carries the new columns. Old tests asserting the Caminho/Vocação rules are replaced.
- Migration: isolated-database test for the 3 conversion cases.
- bUnit: `AfinidadeEssenciaField` — Essência 2 options follow Essência 1, disabled without it.

## Docs

`Requisitos - Ficha de Personagem.md` 2.c rewritten (two Essências, the intersection table above
replacing the Caminho × Sub-Elemento table, no Escolas/Vocação restriction, new uniqueness rule); 1.a
Afinidade loses the Vocação filter mention. `Requisitos - Modelo de Dados.md`: new columns, dropped
`CaminhoNome`. Changelog untouched.

## Out of scope

Escolas de Magia elsewhere (the image stays in the Livro de Regras); Criatura sheets (no Afinidades
table); any formula using Afinidades.
