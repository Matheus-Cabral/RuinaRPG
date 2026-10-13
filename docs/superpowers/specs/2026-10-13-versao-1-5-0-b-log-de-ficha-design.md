# Versão 1.5.0 — Spec B: Log de Ficha — Design

**Status:** design approved by user 2026-10-13 (in chat). Version 1.5.0, branch `release/1.5.0`.
Second of four specs (A, **B**, C, D); see Spec A for the table. Independent of A; Spec C builds on it.

## Overview

6. **Log de ficha** — every change to a Ficha de Personagem, and to an NPC or Criatura sheet granted
   to a player, is recorded with who, when, and the value before and after. The GM opens the log in a
   popup from an icon next to the sheet in the campaign page.

One new table, so `make migrate` is mandatory in the 1.5.0 deploy.

## Decisions (from the brainstorming Q&A)

| Question | Decision |
|---|---|
| Detail of each line | **Field with value before and after.** Long texts appear only as "alterado". |
| Which sheets | **Personagem, and NPC/Criatura sheets granted to a player.** Non-granted NPC/Criatura sheets are not logged. |
| Retention | **Forever.** Removed together with the sheet. |
| Who sees it | **Only the GM.** A player has no access, not even to their own sheet's log. |
| Grouping and filter | Grouped by day, newest first; filter by date range. |
| Resource noise | A checkbox shows/hides **Vitalidade, Foco e Adrenalina** lines; a separate checkbox shows/hides **Estresse** lines. Both start checked. Entries are always recorded. |
| History before 1.5.0 | None. The log starts at the deploy. |

## Data

New table `SheetLogEntries`:

| Column | Type | Notes |
|---|---|---|
| `Id` | uuid PK | |
| `CharacterSheetId`, `NpcSheetId`, `CreatureSheetId` | uuid, nullable, FK with ON DELETE CASCADE | Exactly one is set (same shape as `EncounterParticipant.Source*SheetId`). **Amended 2026-10-13 while planning:** replaces the `SheetKind` + `SheetId` pair, so the database removes the log with the sheet. |
| `OccurredAt` | timestamptz | UTC. |
| `ActorUserId` | uuid | The authenticated caller. |
| `ActorNickname` | text | Snapshot, so the line survives a nickname change. |
| `ActorIsGm` | boolean | Caller is the GM of the sheet's campaign (Character) or the sheet's `GmId` (Npc/Creature). |
| `Categoria` | enum | `Geral`, `RecursoVFA` (Vitalidade/Foco/Adrenalina Atual), `Estresse` (Estresse Atual). Drives the two checkboxes. |
| `Acao` | enum | `Alterou`, `Adicionou`, `Removeu`, `Criou`. |
| `Secao` | text | Screen section label, e.g. "Atributos", "Magias & Habilidades". |
| `Campo` | text, nullable | Field label, e.g. "Vitalidade Atual". |
| `ItemNome` | text, nullable | Name of the list item (attribute, skill, weapon, spell...). |
| `ValorAntes`, `ValorDepois` | text, nullable | Rendered values. Null for long texts and for add/remove. |

One index per sheet column: `(CharacterSheetId, OccurredAt DESC)`, and the same for the other two.

## Recording

A single EF Core `SaveChangesInterceptor` (`SheetLogInterceptor`, Infrastructure), registered on
`RuinaRpgDbContext`. In `SavingChangesAsync` it walks the `ChangeTracker` and adds `SheetLogEntry` rows
to the same save, so log and change commit or fail together. No sheet controller is changed.

- **Tracked entities:** the sheet row itself and every child table of the three sheet kinds
  (attributes, skills, affinities, affections, traits, weapons, armor slots, shields, inventory,
  artifacts, spoils, spell abilities and their effects, runes, masteries, equipagem rows). The mapping
  entity → (kind, how to reach the sheet id, section label) is one static table (`SheetLogMap`).
- **Excluded:** the character diary tables and the História image links.
- **NPC/Criatura:** logged only when the sheet has `OwnerId` set (granted). The interceptor reads it
  from the tracked sheet or with one lookup per save.
- **Actor:** from `IHttpContextAccessor`. No authenticated user (seeders, startup) → nothing is logged.
- **Modified rows:** one entry per changed property that has a label. Unlabelled properties are logged
  with the technical name rather than dropped. Purely internal columns (ids, FKs to the sheet, audit
  stamps) are in an explicit ignore list.
- **Added / deleted child rows:** one entry `Adicionou`/`Removeu` with `ItemNome`. Effects of a
  Magia/Habilidade are folded into the parent's entry, not logged one by one.
- **Sheet creation:** a single `Criou` entry instead of one per column. A grant that copies a sheet
  logs one `Criou` for the copy and nothing for the copied children.
- **Sheet deletion:** nothing to do in code; the foreign key cascades.
- **Long texts** (História and other multi-line fields, listed explicitly): `Alterou` with null values.
- **Enum and reference values:** enums use their display labels; a foreign key (Histórico, catalog
  item) is rendered by the referenced name when the interceptor can resolve it from the context, and as
  "alterado" otherwise.
- **Derived writes** in the same request (Nível from XP, the PI sums of Spec A) are recorded like any
  other property change, under the same actor.

`ExecuteUpdate`/`ExecuteDelete` and raw SQL bypass interceptors. **Amended 2026-10-13 while planning:**
the only hits on sheet tables are in `PericiasController` (the Auditor removing a Perícia zeroes its
`Gasto` on every sheet). That is catalog maintenance, not a sheet edit, and stays unlogged.

Spec C extends the interceptor to feed the encounter log; nothing is exposed for it here.

## API

`GET api/sheet-logs/{kind}/{sheetId}?de=&ate=&recursos=&estresse=&antesDe=&limite=`

- `kind`: `character`, `npc`, `creature`.
- Authorization: Character → caller is the GM of the sheet's campaign; Npc/Creature → caller is the
  sheet's `GmId`. Anyone else, the owner included, gets 404.
- `de`/`ate`: UTC instants (the client converts the local day boundaries). `recursos=false` drops
  `RecursoVFA`; `estresse=false` drops `Estresse`. Both default to `true`.
- Keyset paging by `OccurredAt` then `Id` (`antesDe` cursor), `limite` default 100, max 500.
- Response: `SheetLogPageResponse(List<SheetLogEntryResponse> Itens, string? ProximoCursor)`.

All filters run in the query, so paging stays correct with lines hidden.

## Client

- New `Shared/LogDaFichaDialog.razor` (MudDialog, large): date range (two `MudDatePicker`), the two
  checkboxes ("Vitalidade, Foco e Adrenalina", "Estresse"), then the entries grouped by local day with
  a day header; each line shows time, actor (with a "GM" chip when `ActorIsGm`), section, and the
  change ("Vitalidade Atual: 30 → 25", "adicionou a Magia Bola de Fogo", "História: alterado").
  A "Carregar mais" button loads the next page. Changing a filter reloads from the first page.
- `CampanhaDetalhe.razor`: an icon button (`Icons.Material.Filled.History`, tooltip "Log da ficha")
  after "— Nível x, Vocação (Linhagem)" on each sheet of the Membros list, and on each row of
  "Concessões existentes".

## Docs

- `Requisitos - Campanha.md` — new requirement (next free number) for the GM-only sheet log.
- `Requisitos - Modelo de Dados.md` — `SheetLogEntries`.
- `Requisitos - Técnico.md` — a note on the interceptor as the single recording point.

## Testing

- **Unit:** value rendering (enum labels, long text, nulls); categoria classification; the map covers
  every sheet child entity registered in the `DbContext` (a reflection test that fails when a new
  sheet table is added without a map entry or an explicit exclusion).
- **Integration:**
  - Player edit and GM edit both produce entries with the right actor and `ActorIsGm`.
  - Field change has before/after; add and remove of a child row; spell effects folded.
  - Non-granted NPC/Criatura produce nothing; granted ones do.
  - Diary change produces nothing; sheet creation produces one entry; sheet deletion removes the log.
  - Endpoint: 404 for the owner and for another GM; date filter; both checkboxes; paging.
  - Migration test for the new table.
- **Client (bUnit):** grouping by day, the two checkboxes re-query, "Carregar mais", the icon renders
  on both lists.

## Out of scope

- Undo/restore from the log.
- Log for non-granted NPC/Criatura sheets, catalog pages, or campaign-level actions.
- Merging consecutive changes to the same field.
- Player access to the log.
